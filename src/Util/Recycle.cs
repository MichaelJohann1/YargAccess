using System;
using System.IO;
using System.Runtime.InteropServices;

namespace YargAccess
{
    // Moves files and folders to the Windows Recycle Bin (so a deleted song or replay can be restored).
    public static class Recycle
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

        const uint FO_DELETE = 3;
        const ushort FOF_SILENT = 0x4, FOF_NOCONFIRMATION = 0x10, FOF_ALLOWUNDO = 0x40, FOF_NOERRORUI = 0x400;

        // True when the path is gone afterwards (or was already gone).
        public static bool Path(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!File.Exists(path) && !Directory.Exists(path)) return true;
            try
            {
                var op = new SHFILEOPSTRUCT
                {
                    wFunc = FO_DELETE,
                    pFrom = System.IO.Path.GetFullPath(path) + "\0",
                    fFlags = (ushort) (FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI),
                };
                int r = SHFileOperation(ref op);
                if (r != 0) MelonLoader.MelonLogger.Msg("Recycle: SHFileOperation returned " + r + " for " + path);
            }
            catch (Exception ex) { ModLog.Warn("Recycle.Path", ex); }
            return !File.Exists(path) && !Directory.Exists(path);
        }
    }
}
