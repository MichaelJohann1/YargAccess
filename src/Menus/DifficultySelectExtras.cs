using System;
using System.Globalization;
using System.Reflection;
using MelonLoader;
using TMPro;
using UnityEngine;
using YARG.Menu.DifficultySelect;
using YARG.Menu.Navigation;

namespace YargAccess
{
    public static class DifficultySelectExtras
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        static bool _inZone;

        static DifficultySelectMenu Menu
        {
            get
            {
                try
                {
                    var m = UnityEngine.Object.FindObjectOfType<DifficultySelectMenu>();
                    return m != null && m.gameObject.activeInHierarchy ? m : null;
                }
                catch { return null; }
            }
        }

        public static bool IsActive => Menu != null && Field() != null;

        public static void ResetZone() { _inZone = false; }

        public static bool InZone => _inZone;

        public static bool MoveDown()
        {
            if (!IsActive) { _inZone = false; return false; }
            if (_inZone) return true;

            var g = NavigationGroup.CurrentNavigationGroup;
            if (g == null || g.Count == 0) return false;
            if (g.SelectedIndex.GetValueOrDefault(0) != g.Count - 1) return false;

            _inZone = true;
            Announce();
            return true;
        }

        public static bool MoveUp()
        {
            if (!IsActive || !_inZone) return false;
            _inZone = false;
            var g = NavigationGroup.CurrentNavigationGroup;
            if (g != null && g.Count > 0)
            {
                g.SelectAt(g.Count - 1, SelectionOrigin.Programmatically);
                var nb = g.SelectedBehaviour;
                if (nb != null) MenuAnnouncer.Announce(nb, true);
            }
            return true;
        }

        public static bool Adjust(int dir)
        {
            if (!IsActive || !_inZone) return false;
            var f = Field();
            if (f == null) return false;
            try
            {
                int pct = Parse(f.text) + 5 * dir;
                pct = Mathf.Clamp(pct, 10, 5000);
                f.text = pct + "%";
                MenuAnnouncer.Focused(Loc.F("{0} percent", pct));
            }
            catch (Exception ex) { ModLog.Warn("DifficultySelectExtras.Adjust", ex); }
            return true;
        }

        public static bool Confirm()
        {
            if (!IsActive || !_inZone) return false;
            Announce();
            return true;
        }

        static void Announce()
        {
            var f = Field();
            if (f == null) return;
            MenuAnnouncer.Focused(Loc.F("Song speed, {0} percent. {1} to change.", Parse(f.text), Keys.LeftRight));
        }

        public static string Warning()
        {
            var m = Menu;
            if (m == null) return "";
            try
            {
                var cf = typeof(DifficultySelectMenu).GetField("_warningMessageContainer", BF);
                var container = cf != null ? cf.GetValue(m) as GameObject : null;
                if (container == null || !container.activeInHierarchy) return "";
                var tf = typeof(DifficultySelectMenu).GetField("_warningMessage", BF);
                var tmp = tf != null ? tf.GetValue(m) as TMP_Text : null;
                return tmp != null ? TextUtil.Clean(tmp.text) : "";
            }
            catch { return ""; }
        }

        public static string OpenLine()
        {
            try
            {
                var state = YARG.GlobalVariables.State;
                var song = state.CurrentSong;
                if (song == null) return "";
                var mode = state.IsPractice ? Loc.T("Practice") : Loc.T("Quickplay");
                var line = Loc.F("{0}, {1}, by {2}", mode, song.Name, song.Artist);
                var phrase = TextUtil.Clean(song.LoadingPhrase);
                if (!string.IsNullOrEmpty(phrase)) line += ". " + phrase;
                var warn = Warning();
                if (!string.IsNullOrEmpty(warn)) line += ". " + warn;
                return line;
            }
            catch { return ""; }
        }

        static TMP_InputField Field()
        {
            var m = Menu;
            if (m == null) return null;
            try
            {
                var f = typeof(DifficultySelectMenu).GetField("_speedInput", BF);
                return f != null ? f.GetValue(m) as TMP_InputField : null;
            }
            catch { return null; }
        }

        static int Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return 100;
            float v;
            if (!float.TryParse(text.TrimEnd('%', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out v)) return 100;
            return Mathf.RoundToInt(v);
        }
    }
}
