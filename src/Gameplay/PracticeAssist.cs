using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using MelonLoader;
using TMPro;
using YARG.Gameplay;
using YARG.Gameplay.HUD;

namespace YargAccess
{
    public static class PracticeAssist
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        static PracticeHud _hud;
        static GameManager _gm;
        static string _lastSection = "";

        static PracticeHud Hud
        {
            get
            {
                if (_hud == null) { try { _hud = UnityEngine.Object.FindObjectOfType<PracticeHud>(); } catch { } }
                return _hud;
            }
        }

        static GameManager Game
        {
            get
            {
                if (_gm == null) { try { _gm = UnityEngine.Object.FindObjectOfType<GameManager>(); } catch { } }
                return _gm;
            }
        }

        public static bool InPractice
        {
            get { try { var g = Game; return g != null && g.IsPractice; } catch { return false; } }
        }

        public static void TickInSong()
        {
            if (!InPractice) return;
            try
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.f1Key.wasPressedThisFrame) ContextHelp.Speak();
                    if (kb.f2Key.wasPressedThisFrame) AnnounceStatus();
                    if (kb.f3Key.wasPressedThisFrame) AdjustSpeed(-0.05f);
                    if (kb.f4Key.wasPressedThisFrame) AdjustSpeed(+0.05f);
                    if (kb.f8Key.wasPressedThisFrame) RestartSection();
                    if (kb.f9Key.wasPressedThisFrame)
                    {
                        var on = ModSettings.AnnounceSections;
                        on.SetValueWithoutNotify(!on.Value); on.ForceInvokeCallback();
                        Captions.Say(on.Value ? Loc.T("Section announcements on") : Loc.T("Section announcements off"), true);
                    }
                }

                WatchSpeed();
                if (ModSettings.AnnounceSections.Value) WatchSection();
            }
            catch (Exception ex) { ModLog.Warn("PracticeAssist.TickInSong", ex); }
        }

        static void WatchSection()
        {
            var s = Text("_sectionText");
            if (string.IsNullOrEmpty(s) || s == _lastSection) return;
            bool first = string.IsNullOrEmpty(_lastSection);
            _lastSection = s;
            if (!first) Speech.Say(Pretty(s), false);
        }

        static string Pretty(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            try { return YARG.Assets.Script.Helpers.PracticeSectionHelper.ParseSectionName(raw); }
            catch { return Describer.PrettifyName(raw); }
        }

        public static void AnnounceStatus()
        {
            if (!InPractice) return;
            var parts = new System.Collections.Generic.List<string>(5);
            var section = Text("_sectionText");
            if (!string.IsNullOrEmpty(section)) parts.Add(Pretty(section));
            parts.Add(SpeedPhrase());
            var pct = Text("_percentHitText");
            if (!string.IsNullOrEmpty(pct)) parts.Add(Loc.F("{0} hit", pct));
            var best = Text("_bestPercentText");
            if (!string.IsNullOrEmpty(best)) parts.Add(Loc.F("best {0}", best));
            var notes = Text("_notesHitTotalText");
            if (!string.IsNullOrEmpty(notes)) parts.Add(NotesLine(notes));
            Speech.Say(string.Join(", ", parts.ToArray()), true);
        }

        static float _lastSpeed = -1f;

        static void WatchSpeed()
        {
            var g = Game;
            if (g == null || !g.IsSongStarted) return;
            float sp = g.SongSpeed;
            if (_lastSpeed < 0f) { _lastSpeed = sp; return; }
            if (Mathf.Abs(sp - _lastSpeed) < 0.001f) return;
            _lastSpeed = sp;
            Speech.Say(SpeedPhrase(), true);
        }

        public static void AdjustSpeed(float delta)
        {
            var g = Game;
            if (g == null || !g.IsPractice) return;
            try
            {
                g.AdjustSongSpeed(delta);
                var hud = Hud;
                if (hud != null) hud.ResetStats();
                _lastSpeed = g.SongSpeed;
                Speech.Say(SpeedPhrase(), true);
            }
            catch (Exception ex) { ModLog.Warn("PracticeAssist.AdjustSpeed", ex); }
        }

        public static void RestartSection()
        {
            var g = Game;
            if (g == null || !g.IsPractice) return;
            try
            {
                var pm = g.PracticeManager;
                if (pm == null) return;
                _lastSection = "";
                pm.ResetPractice();
                Captions.Say(Loc.T("Restarting section"), true);
            }
            catch (Exception ex) { ModLog.Warn("PracticeAssist.RestartSection", ex); }
        }

        public static string SpeedPhrase()
        {
            try
            {
                var g = Game;
                if (g == null) return "";
                return Loc.F("speed {0} percent", Mathf.RoundToInt(g.SongSpeed * 100f));
            }
            catch { return ""; }
        }

        public static bool TryAdjustAb(int dir)
        {
            var g = Game;
            if (g == null || !g.IsPractice) return false;
            var pm = g.PracticeManager;
            if (pm == null) return false;
            try
            {
                var pause = UnityEngine.Object.FindObjectOfType<PracticePause>();
                if (pause == null || !pause.gameObject.activeInHierarchy) return false;

                var m = typeof(PracticePause).GetMethod("AdjustSelectedPosition", BF);
                if (m == null) return false;

                double a0 = pm.TimeStart, b0 = pm.TimeEnd;
                m.Invoke(pause, new object[] { (double) dir });

                if (Math.Abs(pm.TimeStart - a0) > 1e-6) Speech.Say(AbPhrase(true), true);
                else if (Math.Abs(pm.TimeEnd - b0) > 1e-6) Speech.Say(AbPhrase(false), true);
                else return false;
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Warn("PracticeAssist.TryAdjustAb", ex);
                return false;
            }
        }

        public static string AbPhrase(bool a)
        {
            try
            {
                var g = Game;
                if (g == null || g.PracticeManager == null) return "";
                double t = a ? g.PracticeManager.TimeStart : g.PracticeManager.TimeEnd;
                return a ? Loc.F("A position {0}", Clock(t)) : Loc.F("B position {0}", Clock(t));
            }
            catch { return ""; }
        }

        public static string Clock(double seconds)
        {
            if (seconds < 0) seconds = 0;
            var ts = TimeSpan.FromSeconds(seconds);
            int m = (int) ts.TotalMinutes;
            return m == 1 ? Loc.F("{0} minute {1} seconds", m, ts.Seconds) : Loc.F("{0} minutes {1} seconds", m, ts.Seconds);
        }

        // YARG's "hit/total" notes text as "hit of total notes"; anything else keeps its old wording.
        static string NotesLine(string notes)
        {
            var bits = notes.Split('/');
            if (bits.Length == 2) return Loc.F("{0} of {1} notes", bits[0].Trim(), bits[1].Trim());
            return Loc.F("{0} notes", notes);
        }

        public static void Reset()
        {
            _hud = null;
            _gm = null;
            _lastSection = "";
            _lastSpeed = -1f;
        }

        static string Text(string field)
        {
            try
            {
                var hud = Hud;
                if (hud == null) return "";
                var f = typeof(PracticeHud).GetField(field, BF);
                var tmp = f != null ? f.GetValue(hud) as TMP_Text : null;
                return tmp != null ? TextUtil.Clean(tmp.text) : "";
            }
            catch { return ""; }
        }
    }
}
