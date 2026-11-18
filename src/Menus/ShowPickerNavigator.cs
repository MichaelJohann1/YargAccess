using System;
using System.Reflection;
using MelonLoader;
using TMPro;
using YARG.Menu.Dialogs;

namespace YargAccess
{
    public static class ShowPickerNavigator
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string[] Colors = { "Green", "Red", "Yellow", "Blue", "Orange" };
        static readonly string[] ColorNames = { Loc.T("Green"), Loc.T("Red"), Loc.T("Yellow"), Loc.T("Blue"), Loc.T("Orange") };
        static readonly string[] Pickers = { "OnGreenButton", "OnRedButton", "OnYellowButton", "OnBlueButton", "OnOrangeButton" };

        static SongPickerListDialog _dialog;
        static int _index;
        static int _lastCount = -1;
        static string _lastChoices;

        static SongPickerListDialog Dialog
        {
            get
            {
                if (_dialog == null || !_dialog.gameObject.activeInHierarchy)
                {
                    _dialog = null;
                    try { _dialog = UnityEngine.Object.FindObjectOfType<SongPickerListDialog>(); } catch { }
                }
                return _dialog;
            }
        }

        public static bool IsActive => Dialog != null;

        public static void Opened()
        {
            var d = Dialog;
            if (d == null) return;
            _index = 0;
            _lastCount = ShowCount(d);
            _lastChoices = ChoicesKey(d);
            _lastSecond = -1;
            MenuAnnouncer.Focused(Choice(d, _index) + Keys.Pos(_index, Colors.Length));
        }

        static int _lastSecond = -1;

        public static void Tick()
        {
            var d = Dialog;
            if (d == null) { _lastCount = -1; _held = false; return; }
            try
            {
                int count = ShowCount(d);
                var key = ChoicesKey(d);
                HoldTimer(d, HelpList.IsOpen);
                if (_lastCount < 0) { _lastCount = count; _lastChoices = key; _lastSecond = -1; return; }
                if (key != _lastChoices)
                {
                    bool added = count != _lastCount && _lastCount >= 0;
                    _lastCount = count;
                    _lastChoices = key;
                    _lastSecond = -1;
                    MenuAnnouncer.SayShown(added ? Loc.F("Added. {0} New choices. {1}", CountLine(d), Choice(d, _index))
                        : Loc.F("Time's up. New choices. {0}", Choice(d, _index)));
                    return;
                }
                int left = (int) Math.Ceiling(SecondsLeft(d));
                if (left != _lastSecond)
                {
                    if (left >= 1 && left <= 5 && _lastSecond > left) PlayTick(left);
                    _lastSecond = left;
                }
            }
            catch (Exception ex) { ModLog.Warn("ShowPickerNavigator.Tick", ex); }
        }

        static int _tickLow, _tickHigh;

        static void PlayTick(int secondsLeft)
        {
            if (!Audio.CuePlayer.Ready) return;
            if (_tickLow == 0) _tickLow = Audio.CuePlayer.CreateTone(660, 0.07, 0.4);
            if (_tickHigh == 0) _tickHigh = Audio.CuePlayer.CreateTone(990, 0.14, 0.45);
            int s = secondsLeft == 1 ? _tickHigh : _tickLow;
            if (s != 0) Audio.CuePlayer.Play(s);
        }

        public static void SayTime()
        {
            var d = Dialog;
            if (d == null) return;
            int left = (int) Math.Ceiling(SecondsLeft(d));
            MenuAnnouncer.SayShown(left == 1 ? Loc.F("{0} second before the choices change.", left) : Loc.F("{0} seconds before the choices change.", left));
        }

        static bool _held;

        static void HoldTimer(SongPickerListDialog d, bool hold)
        {
            if (hold == _held) return;
            try
            {
                var seq = typeof(SongPickerListDialog).GetField("_timerSequence", BF).GetValue(d) as DG.Tweening.Tween;
                if (seq == null) return;
                if (hold && DG.Tweening.TweenExtensions.IsPlaying(seq)) { DG.Tweening.TweenExtensions.Pause(seq); _held = true; }
                else if (!hold) { DG.Tweening.TweenExtensions.Play(seq); _held = false; }
            }
            catch { }
        }

        static float SecondsLeft(SongPickerListDialog d)
        {
            try
            {
                var slider = typeof(SongPickerListDialog).GetField("_countdownSlider", BF).GetValue(d) as UnityEngine.UI.Slider;
                float total = YARG.Settings.SettingsManager.Settings.PlayAShowTimeout.Value;
                return slider != null ? slider.value * total : 0f;
            }
            catch { return 0f; }
        }

        public static void Move(int dir)
        {
            var d = Dialog;
            if (d == null) return;
            _index = Math.Max(0, Math.Min(Colors.Length - 1, _index + dir));
            MenuAnnouncer.Focused(Choice(d, _index) + Keys.Pos(_index, Colors.Length));
        }

        public static void Confirm() { Pick(_index); }

        public static bool PickColor(string color)
        {
            int i = Array.IndexOf(Colors, color);
            if (i < 0 || !IsActive) return false;
            Pick(i);
            return true;
        }

        static void Pick(int i)
        {
            var d = Dialog;
            if (d == null) return;
            try
            {
                var name = ChoiceText(d, i);
                var song = SongName(d, i);
                if (song != null) name = Loc.F("{0}: {1}", name, song);
                typeof(SongPickerListDialog).GetMethod(Pickers[i]).Invoke(d, null);
                _lastCount = ShowCount(d);
                _lastChoices = ChoicesKey(d);
                _lastSecond = -1;
                MenuAnnouncer.SayShown(Loc.F("Added {0}. {1} New choices. {2}", name, CountLine(d), Choice(d, _index)));
            }
            catch (Exception ex) { ModLog.Warn("ShowPickerNavigator.Pick", ex); }
        }

        public static bool Back()
        {
            var d = Dialog;
            if (d == null) return false;
            int n = ShowCount(d);
            try { d.OnSelectButton(); }
            catch (Exception ex) { ModLog.Warn("ShowPickerNavigator.Back", ex); }
            MenuAnnouncer.SayShown(n == 0
                ? Loc.T("Done. The show has no songs. Back leaves show mode.")
                : n == 1 ? Loc.F("Done. {0} song in the show. Blue, or 4 on the keyboard, starts the show. Back leaves show mode and clears it.", n)
                : Loc.F("Done. {0} songs in the show. Blue, or 4 on the keyboard, starts the show. Back leaves show mode and clears it.", n));
            return true;
        }

        static string CountLine(SongPickerListDialog d)
        {
            int n = ShowCount(d);
            return n == 1 ? Loc.F("{0} song in the show.", n) : Loc.F("{0} songs in the show.", n);
        }

        static int ShowCount(SongPickerListDialog d)
        {
            try { return d.MusicLibrary != null ? d.MusicLibrary.ShowPlaylist.Count : 0; } catch { return 0; }
        }

        static string Choice(SongPickerListDialog d, int i) { return ColorNames[i] + ", " + ChoiceText(d, i); }

        static string ChoicesKey(SongPickerListDialog d)
        {
            var parts = new string[Colors.Length];
            for (int i = 0; i < Colors.Length; i++) parts[i] = ChoiceText(d, i);
            return string.Join("|", parts);
        }

        static string ChoiceText(SongPickerListDialog d, int i)
        {
            try
            {
                var views = typeof(SongPickerListDialog).GetField("_showCategoryViews", BF).GetValue(d) as Array;
                var view = views != null && i < views.Length ? views.GetValue(i) as YARG.Menu.MusicLibrary.ShowCategoryView : null;
                var t = view != null ? view.CategoryText : null;
                var s = t != null ? TextUtil.Clean(t.text) : "";
                return string.IsNullOrEmpty(s) ? Loc.T("empty") : s;
            }
            catch { return Loc.T("unknown"); }
        }

        static string SongName(SongPickerListDialog d, int i)
        {
            try
            {
                var cats = typeof(SongPickerListDialog).GetField("_categories", BF).GetValue(d) as Array;
                var cat = cats != null && i < cats.Length ? cats.GetValue(i) : null;
                if (cat == null) return null;
                var t = cat.GetType();
                object song = t.GetField("Song")?.GetValue(cat) ?? t.GetProperty("Song")?.GetValue(cat, null);
                var entry = song as YARG.Core.Song.SongEntry;
                if (entry == null) return null;
                string n = entry.Name, a = entry.Artist;
                return string.IsNullOrWhiteSpace(a) ? TextUtil.Clean(n) : Loc.F("{0} by {1}", TextUtil.Clean(n), TextUtil.Clean(a));
            }
            catch { return null; }
        }
    }
}
