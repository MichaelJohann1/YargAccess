namespace YargAccess
{
    public static class Menu
    {
        public static string CurrentName { get; private set; } = "";

        public static bool NeedsNamePrefix { get; set; }
        public static float PrefixArmedAt { get; private set; }

        static string _logged = "";

        public static void OnEnter(string name)
        {
            CurrentName = name ?? "";
            NeedsNamePrefix = !string.IsNullOrEmpty(CurrentName);
            try { PrefixArmedAt = UnityEngine.Time.unscaledTime; } catch { }
            if (CurrentName != _logged)
            {
                _logged = CurrentName;
                if (CurrentName.Length > 0) MelonLoader.MelonLogger.Msg("Menu: " + CurrentName);
            }
        }

        public static void SetContext(string name, bool armPrefix)
        {
            CurrentName = name ?? "";
            NeedsNamePrefix = armPrefix && !string.IsNullOrEmpty(CurrentName);
        }

        // CurrentName stays English (other code compares it); this is how it is spoken. A name with no
        // translation here (a pause menu label, a page title already translated) goes through the pause
        // menu's own names, else stays as it is.
        public static string Spoken => SpokenName(CurrentName);

        // "Music Library menu": the spoken heading of the current menu.
        public static string Heading => Loc.F("{0} menu", Spoken);

        public static string SpokenName(string name)
        {
            switch (name)
            {
                case "Main": return Loc.T("Main");
                case "Music Library": return Loc.T("Music Library");
                case "Difficulty Select": return Loc.T("Difficulty Select");
                case "Credits": return Loc.T("Credits");
                case "Profile List": return Loc.T("Profile List");
                case "Profile Info": return Loc.T("Profile Info");
                case "History": return Loc.T("History");
                case "Score": return Loc.T("Score");
                case "Settings": return Loc.T("Settings");
                case "Calibrator": return Loc.T("Calibrator");
                default: return PauseAnnouncer.Spoken(name);
            }
        }

        public static void Reset()
        {
            CurrentName = "";
            NeedsNamePrefix = false;
        }
    }
}
