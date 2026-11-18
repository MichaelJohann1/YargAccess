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

        public static string Ok => Pick("Enter", "A", "Green");
        public static string Back => Pick("Escape", "B", "Red");
        public static string UpDown => Pick("Up and Down", "D-pad up and down", "Strum up and down");
        public static string LeftRight => Pick("Left and Right", "D-pad left and right", "D-pad left and right");
        public static string Tabs => Pick("Tab and Shift+Tab", "LB and RB", "Yellow and Orange");
        public static string ReviewHint => Pick("left and right brackets to move through them, backslash for all",
            "right stick left and right to move through them, right stick down for all",
            "hold Select and press Yellow or Blue to move through them, Select and Green for all");

        public static string Finish => Pick("Escape", "Start", "Start");

        public static string Pos(int index, int count)
        {
            if (count <= 1 || index < 0 || !ModSettings.ReadPosition.Value) return "";
            return ", " + (index + 1) + " of " + count;
        }

        public static string Role(string role)
        {
            if (string.IsNullOrEmpty(role) || !ModSettings.ReadRoles.Value) return "";
            return ", " + role;
        }

        public static string RoleThen(string role, string value)
        {
            if (!ModSettings.ReadRoles.Value) return value ?? "";
            return string.IsNullOrEmpty(value) ? role : role + ", " + value;
        }
    }
}
