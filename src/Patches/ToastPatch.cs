using System;
using HarmonyLib;
using MelonLoader;

namespace YargAccess.Patches
{
    [HarmonyPatch(typeof(YARG.Menu.Persistent.ToastManager), "AddToast")]
    static class ToastPatch
    {
        public static int Count;

        static bool Prepare() => AccessTools.Method(typeof(YARG.Menu.Persistent.ToastManager), "AddToast") != null;

        static void Postfix(object type, string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                if (Captions.Raising) return;
                var kind = type != null ? type.ToString() : "";
                var msg = YargEnglish(TextUtil.Clean(text));
                if (kind == "Error") msg = Loc.F("Error: {0}", msg);
                else if (kind == "Warning") msg = Loc.F("Warning: {0}", msg);
                Count++;
                Speech.Say(msg, false);
            }
            catch (Exception ex) { ModLog.Warn("ToastPatch", ex); }
        }

        // Toasts YARG writes in English in its code, not through its language files (InputManager,
        // GameManager, MusicLibraryMenu, PopupMenu, SacnHardware): spoken in the mod's language.
        // The toast on screen stays as YARG shows it.
        static string YargEnglish(string m)
        {
            if (Loc.English || string.IsNullOrEmpty(m)) return m;
            switch (m)
            {
                case "Song not present in library": return Loc.T("Song not present in library");
                case "Failed to load replay!": return Loc.T("Failed to load replay!");
                case "Chart requires a rescan!": return Loc.T("Chart requires a rescan!");
                case "No network found! sACN ouput disabled!": return Loc.T("No network found! sACN output disabled!");
                case "Cannot rename Favorites playlist": return Loc.T("Cannot rename Favorites playlist");
                case "Sorted A-Z": return Loc.T("Sorted A-Z");
                case "Sorted Z-A": return Loc.T("Sorted Z-A");
                case "Sorted by Artist A-Z": return Loc.T("Sorted by Artist A-Z");
                case "Sorted by Artist Z-A": return Loc.T("Sorted by Artist Z-A");
                case "Cannot delete this playlist": return Loc.T("Cannot delete this playlist");
                case "Sort not supported in playlists": return Loc.T("Sort not supported in playlists");
                case "Removed from playlist": return Loc.T("Removed from playlist");
                case "You can't create a playlist with that name": return Loc.T("You can't create a playlist with that name");
                case "You can't add that to a playlist": return Loc.T("You can't add that to a playlist");
                case "Playlist Created": return Loc.T("Playlist Created");
            }
            string rest;
            if (Rest(m, "Devices found: ", out rest)) return Loc.F("Devices found: {0}", rest);
            if (Rest(m, "Device added: ", out rest)) return Loc.F("Device added: {0}", rest);
            if (Rest(m, "Device removed: ", out rest)) return Loc.F("Device removed: {0}", rest);
            if (Quoted(m, "Created '", out rest)) return Loc.F("Created {0}", rest);
            if (Quoted(m, "Renamed to '", out rest)) return Loc.F("Renamed to {0}", rest);
            if (Quoted(m, "Deleted '", out rest)) return Loc.F("Deleted {0}", rest);
            return m;
        }

        static bool Rest(string m, string prefix, out string rest)
        {
            rest = m.StartsWith(prefix, StringComparison.Ordinal) ? m.Substring(prefix.Length) : null;
            return rest != null;
        }

        static bool Quoted(string m, string prefix, out string rest)
        {
            rest = m.StartsWith(prefix, StringComparison.Ordinal) && m.EndsWith("'") && m.Length > prefix.Length
                ? m.Substring(prefix.Length, m.Length - prefix.Length - 1) : null;
            return rest != null;
        }
    }
}
