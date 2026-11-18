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

        public static void Reset()
        {
            CurrentName = "";
            NeedsNamePrefix = false;
        }
    }
}
