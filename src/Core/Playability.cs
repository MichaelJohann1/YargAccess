using System;
using System.Reflection;
using YARG.Core;
using YARG.Localization;

namespace YargAccess
{
    public static class Playability
    {
        public static string NotYet => Loc.T("not playable in YARG yet");

        public static bool IsPlayable(GameMode mode)
        {
            return mode != GameMode.SixFretGuitar && mode != GameMode.ProGuitar;
        }

        public static bool IsPlayable(Instrument instrument)
        {
            try { return IsPlayable(instrument.ToNativeGameMode()); }
            catch { return true; }
        }

        public static string InstrumentName(Instrument instrument)
        {
            string n;
            try { n = Localize.ToLocalizedName(instrument); } catch { n = instrument.ToString(); }
            try { if (instrument.ToNativeGameMode() == GameMode.SixFretGuitar) n = Loc.F("{0} (6-fret)", n); } catch { }
            return IsPlayable(instrument) ? n : n + ", " + NotYet;
        }

        public static string GameModeSuffix(GameMode mode)
        {
            return IsPlayable(mode) ? "" : ", " + NotYet;
        }

        static PropertyInfo _currentPlayer;
        public static string AnnotateDifficultyRow(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return text;
                var menu = UnityEngine.Object.FindObjectOfType<YARG.Menu.DifficultySelect.DifficultySelectMenu>();
                if (menu == null) return text;
                if (_currentPlayer == null)
                    _currentPlayer = typeof(YARG.Menu.DifficultySelect.DifficultySelectMenu)
                        .GetProperty("CurrentPlayer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var player = _currentPlayer != null ? _currentPlayer.GetValue(menu, null) as YARG.Player.YargPlayer : null;
                if (player == null || IsPlayable(player.Profile.GameMode)) return text;
                // The row headers are YARG's own (translated) text: compared with YARG's strings for them.
                if (!RowStarts(text, "Ready", "Ready")
                    && !RowStarts(text, "Instrument", "Instrument")
                    && !RowStarts(text, "SitOut", "Sit Out")
                    && !RowStarts(text, "Disconnect", "Disconnect")) return text;
                return text + ". " + Loc.F("{0}'s game mode is {1}, not playable in YARG yet. Change it in Profiles, Edit Profile, Binds.",
                    player.Profile.Name, Localize.ToLocalizedName(player.Profile.GameMode));
            }
            catch { return text; }
        }

        // Whether a difficulty screen row starts with YARG's header Menu.DifficultySelect.<key> (in YARG's
        // language) or its English text.
        static bool RowStarts(string text, string key, string english)
        {
            if (text.StartsWith(english, StringComparison.OrdinalIgnoreCase)) return true;
            try
            {
                var h = Localize.Key("Menu.DifficultySelect", key);
                return !string.IsNullOrEmpty(h) && text.StartsWith(h, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }
}
