using System;
using System.Runtime.InteropServices;

namespace YargAccess.Util
{
    internal static class Win32
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        public static extern void NotifyWinEvent(uint eventId, IntPtr hwnd, int idObject, int idChild);

        public const int GWLP_WNDPROC = -4;

        public const uint WM_GETOBJECT  = 0x003D;
        public const uint WM_ACTIVATE   = 0x0006;
        public const uint WM_SETFOCUS   = 0x0007;
        public const uint WM_KILLFOCUS  = 0x0008;
        public const uint WM_CLOSE      = 0x0010;
        public const uint WM_NCDESTROY  = 0x0082;

        public const int WA_INACTIVE = 0;

        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint EVENT_OBJECT_FOCUS      = 0x8005;

        public const int OBJID_WINDOW = 0;
        public const int OBJID_CLIENT = unchecked((int)0xFFFFFFFC);
        public const int CHILDID_SELF = 0;
    }
}
