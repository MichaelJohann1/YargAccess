using System;
using System.Reflection;
using YARG.Core;
using YARG.Localization;

namespace YargAccess
{
    public static class Playability
    {
        public const string NotYet = "not playable in YARG yet";

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
            try { if (instrument.ToNativeGameMode() == GameMode.SixFretGuitar) n += " (6-fret)"; } catch { }
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
                if (!text.StartsWith("Ready", StringComparison.OrdinalIgnoreCase)
                    && !text.StartsWith("Instrument", StringComparison.OrdinalIgnoreCase)
                    && !text.StartsWith("Sit Out", StringComparison.OrdinalIgnoreCase)
                    && !text.StartsWith("Disconnect", StringComparison.OrdinalIgnoreCase)) return text;
                return text + ". " + player.Profile.Name + "'s game mode is "
                    + Localize.ToLocalizedName(player.Profile.GameMode) + ", " + NotYet
                    + ". Change it in Profiles, Edit Profile, Binds.";
            }
            catch { return text; }
        }
    }
}
