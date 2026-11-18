namespace YargAccess
{
    public static class Keys
    {
        public enum Device { Keyboard, Gamepad, Guitar }

        public static Device Current => InputDriver.LastDevice;

        static string Pick(string keyboard, string pad, string guitar)
        {
            switch (Current)
            {
                case Device.Gamepad: return pad;
                case Device.Guitar: return guitar;
                default: return keyboard;
            }
        }

        public static string Ok => Pick(Loc.T("Enter"), "A", Loc.T("Green"));
        public static string Back => Pick(Loc.T("Escape"), "B", Loc.T("Red"));
        public static string UpDown => Pick(Loc.T("Up and Down"), Loc.T("D-pad up and down"), Loc.T("Strum up and down"));
        public static string LeftRight => Pick(Loc.T("Left and Right"), Loc.T("D-pad left and right"), Loc.T("D-pad left and right"));
        public static string Tabs => Pick(Loc.T("Tab and Shift+Tab"), Loc.T("LB and RB"), Loc.T("Yellow and Orange"));
        public static string ReviewHint => Pick(Loc.T("left and right brackets to move through them, backslash for all"),
            Loc.T("right stick left and right to move through them, right stick down for all"),
            Loc.T("hold Select and press Yellow or Blue to move through them, Select and Green for all"));

        public static string Finish => Pick(Loc.T("Escape"), Loc.T("Start"), Loc.T("Start"));

        // A colour action's name (Green, Red, Yellow, Blue, Orange) in YARG's language; any other name as given.
        public static string Color(string english)
        {
            switch (english)
            {
                case "Green":  return Loc.T("Green");
                case "Red":    return Loc.T("Red");
                case "Yellow": return Loc.T("Yellow");
                case "Blue":   return Loc.T("Blue");
                case "Orange": return Loc.T("Orange");
                default:       return english;
            }
        }

        public static string Pos(int index, int count)
        {
            if (count <= 1 || index < 0 || !ModSettings.ReadPosition.Value) return "";
            return Loc.F(", {0} of {1}", index + 1, count);
        }

        public static string Role(string role)
        {
            if (string.IsNullOrEmpty(role) || !ModSettings.ReadRoles.Value) return "";
            return ", " + RoleWord(role);
        }

        public static string RoleThen(string role, string value)
        {
            if (!ModSettings.ReadRoles.Value) return value ?? "";
            return string.IsNullOrEmpty(value) ? RoleWord(role) : RoleWord(role) + ", " + value;
        }

        // The role words callers pass in English, translated here (each one a literal so it is listed
        // for translators). A word not listed is spoken as given.
        static string RoleWord(string role)
        {
            switch (role)
            {
                case "button":       return Loc.T("button");
                case "heading":      return Loc.T("heading");
                case "playlist":     return Loc.T("playlist");
                case "checkbox":     return Loc.T("checkbox");
                case "slider":       return Loc.T("slider");
                case "dropdown":     return Loc.T("dropdown");
                case "number":       return Loc.T("number");
                case "edit box":     return Loc.T("edit box");
                case "duration":     return Loc.T("duration");
                case "color":        return Loc.T("color");
                case "hit window":   return Loc.T("hit window");
                case "IP address":   return Loc.T("IP address");
                case "channel list": return Loc.T("channel list");
                case "image file":   return Loc.T("image file");
                default:             return role;
            }
        }
    }
}
