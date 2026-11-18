using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Menu.Navigation;

namespace YargAccess
{
    public static class MenuAnnouncer
    {
        static int _lastId;
        static float _lastAt;
        static float _suppressUntil;
        static string _lastMenu = "";

        static bool _batching;
        static int _batchOpenedFrame = -1;
        static readonly List<string> _batch = new List<string>();
        static string _batchItem;
        static string _batchTab;

        public static void SuppressFor(float seconds)
        {
            _suppressUntil = Time.unscaledTime + seconds;
            _graceArmed = true;
            _menuReadyAt = 0f;
        }

        static bool _graceArmed;
        static float _menuReadyAt;

        public static void TickGrace()
        {
            if (!_graceArmed || Time.unscaledTime < _suppressUntil) return;
            if (DialogNavigator.IsActive || GameplayContext.InGameplay)
            {
                _graceArmed = false;
                return;
            }
            bool loading = false;
            try { loading = YARG.LoadingScreen.IsActive; } catch { }
            var g = NavigationGroup.CurrentNavigationGroup;
            if (loading || g == null || g.SelectedBehaviour == null) return;
            if (_menuReadyAt == 0f) { _menuReadyAt = Time.unscaledTime; return; }
            if (Time.unscaledTime - _menuReadyAt < 1.5f) return;
            _graceArmed = false;
            _menuReadyAt = 0f;
            Patches.PushMenuPatch.AnnounceCurrent();
        }

        public static void CancelSuppress() { _suppressUntil = 0f; _graceArmed = false; DropSettle(); }

        public static void Announce(NavigatableBehaviour nav, bool force = false)
        {
            if (nav == null) return;
            if (!force && (DialogNavigator.IsActive || Time.unscaledTime < _suppressUntil)) return;
            if (!force && OwnedByCustomNavigator(nav)) return;
            int id = nav.GetInstanceID();
            float now = Time.unscaledTime;
            if (!force && id == _lastId && now - _lastAt < 0.04f) return;
            _lastId = id;
            _lastAt = now;

            var text = Describer.Describe(nav);
            if (string.IsNullOrEmpty(text)) return;

            if (_settling && IsTabHeader(nav)) { _settleTab = Describer.GetLabel(nav.gameObject); _settleActivityFrame = Time.frameCount; return; }
            if (_batching && IsTabHeader(nav)) { _batchTab = Describer.GetLabel(nav.gameObject); return; }

            SayItem(text);
        }

        static bool OwnedByCustomNavigator(NavigatableBehaviour nav)
        {
            if (ProfileInfoNavigator.IsActive) return true;
            return ProfilesNavigator.IsActive && nav.GetComponentInParent<YARG.Menu.ProfileList.ProfileListMenu>() != null;
        }

        static bool IsTabHeader(NavigatableBehaviour nav)
        {
            var g = nav.NavigationGroup;
            return g != null && g.gameObject.name == "Tabs";
        }

        public static void Say(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Emit(text);
        }

        public static void SayShown(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Say(text);
            Captions.Toast(text);
        }

        public static void Focused(string text, bool asItem = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (asItem) SayItem(text); else Emit(text);
        }

        public static void SayItem(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _graceArmed = false;
            if (_settling) { _settleItem = text; _settleActivityFrame = Time.frameCount; return; }
            if (_flushCapture != null) { _flushCapture.Add(text); return; }
            if (_batching) { _batchItem = text; return; }
            if (!string.IsNullOrEmpty(Menu.CurrentName) && Menu.CurrentName != _lastMenu)
                text = MenuPrefix() + text;
            _lastMenu = Menu.CurrentName;
            Emit(text);
        }

        static void Emit(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_settling)
            {
                if (!_settleSays.Contains(text)) _settleSays.Add(text);
                _settleActivityFrame = Time.frameCount;
                return;
            }
            if (_flushCapture != null) { _flushCapture.Add(text); return; }
            if (_batching)
            {
                if (_batch.Count == 0 || _batch[_batch.Count - 1] != text) _batch.Add(text);
                return;
            }
            Speech.Say(text, true);
        }

        public static void BeginBatch()
        {
            _batching = true;
            _batchOpenedFrame = Time.frameCount;
            _batch.Clear();
            _batchItem = null;
            _batchTab = null;
        }

        public static bool EndBatch()
        {
            _batching = false;
            var parts = new List<string>(_batch);
            _batch.Clear();
            var item = _batchItem;
            if (!string.IsNullOrEmpty(item))
            {
                if (!string.IsNullOrEmpty(_batchTab)) item = _batchTab + " tab, " + item;
                if (!string.IsNullOrEmpty(Menu.CurrentName) && Menu.CurrentName != _lastMenu) item = MenuPrefix() + item;
                _lastMenu = Menu.CurrentName;
                parts.Add(item);
            }
            _batchItem = null;
            _batchTab = null;
            if (parts.Count == 0) return false;
            var combined = JoinSentences(parts);
            if (string.IsNullOrEmpty(combined)) return false;
            Speech.Say(combined, true);
            return true;
        }

        public static void DiscardBatch() { _batching = false; _batch.Clear(); _batchItem = null; _batchTab = null; }

        public static void SafetyFlush()
        {
            if (_batching && Time.frameCount != _batchOpenedFrame) DiscardBatch();
            SettleTick();
        }

        const int SettleQuietFrames = 4;
        const float SettleMaxSeconds = 0.6f;
        static bool _settling;
        static int _settleActivityFrame;
        static float _settleStartedAt;
        static string _settleItem;
        static string _settleTab;
        static readonly List<string> _settleSays = new List<string>();
        static Action _settleFallback;

        static List<string> _flushCapture;

        public static bool Settling => _settling;

        public static void EndBatchSettle(Action fallback)
        {
            var buffered = new List<string>(_batch);
            var item = _batchItem;
            var tab = _batchTab;
            DiscardBatch();
            BeginSettle(fallback);
            foreach (var b in buffered) if (!_settleSays.Contains(b)) _settleSays.Add(b);
            if (item != null) _settleItem = item;
            if (tab != null) _settleTab = tab;
        }

        public static void BeginSettle(Action fallback)
        {
            if (!_settling) { _settleSays.Clear(); _settleItem = null; _settleTab = null; }
            _settling = true;
            _settleFallback = fallback;
            _settleStartedAt = Time.unscaledTime;
            _settleActivityFrame = Time.frameCount;
        }

        public static void DropSettle()
        {
            _settling = false;
            _settleSays.Clear();
            _settleItem = null;
            _settleTab = null;
            _settleFallback = null;
        }

        static void SettleTick()
        {
            if (!_settling) return;
            bool quiet = Time.frameCount - _settleActivityFrame >= SettleQuietFrames;
            bool timedOut = Time.unscaledTime - _settleStartedAt >= SettleMaxSeconds;
            if (quiet || timedOut) FlushSettle();
        }

        public static void FlushSettle()
        {
            if (!_settling) return;
            var says = new List<string>(_settleSays);
            var item = _settleItem;
            var tab = _settleTab;
            var fallback = _settleFallback;
            DropSettle();

            if (item == null && fallback != null)
            {
                _flushCapture = new List<string>();
                try { fallback(); } catch (Exception ex) { ModLog.Warn("MenuAnnouncer.FlushSettle", ex); }
                var cap = _flushCapture;
                _flushCapture = null;
                if (cap.Count > 0)
                {
                    item = cap[cap.Count - 1];
                    cap.RemoveAt(cap.Count - 1);
                    foreach (var c in cap) if (!says.Contains(c)) says.Add(c);
                }
            }

            var parts = new List<string>();
            foreach (var line in says) if (line != item) parts.Add(line);
            string last = item;
            if (!string.IsNullOrEmpty(tab) && !string.IsNullOrEmpty(last)) last = tab + " tab, " + last;
            if (!string.IsNullOrEmpty(last)) parts.Add(last);
            if (parts.Count == 0) return;

            var text = JoinSentences(parts);
            if (!string.IsNullOrEmpty(Menu.CurrentName) && Menu.CurrentName != _lastMenu && !text.StartsWith(Menu.CurrentName + " menu"))
                text = MenuPrefix() + text;
            _lastMenu = Menu.CurrentName;
            Speech.Say(text, true);
        }

        static string MenuPrefix()
        {
            var intro = Menu.CurrentName == "Music Library" ? ListMenuAnnouncer.LibraryHeader() : "";
            return Menu.CurrentName + " menu, " + (string.IsNullOrEmpty(intro) ? "" : intro + ". ");
        }

        public static void ForceMenuPrefixNext() { _lastMenu = "\0"; }

        public static void MenuAlreadySpoken() { _lastMenu = Menu.CurrentName; }

        public static void ResetDedupe()
        {
            _lastId = 0;
            _lastAt = 0;
        }

        static string JoinSentences(List<string> parts)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in parts)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (sb.Length > 0)
                {
                    int k = sb.Length - 1;
                    while (k > 0 && (sb[k] == '"' || sb[k] == '\'' || sb[k] == ')' || sb[k] == '”')) k--;
                    char end = sb[k];
                    sb.Append(end == '.' || end == '!' || end == '?' ? " " : ". ");
                }
                sb.Append(p);
            }
            return sb.ToString();
        }
    }
}
