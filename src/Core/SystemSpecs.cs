using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace YargAccess
{
    // Writes the player's setup to the log once, when the first menu opens, so a log sent for help
    // says what it ran on: YARG build, Windows, CPU, memory, graphics, screen, audio output, the
    // screen reader and the controllers plugged in. No user or computer names are written.
    public static class SystemSpecs
    {
        static bool _logged;

        public static void LogOnce()
        {
            if (_logged) return;
            _logged = true;
            var lines = new List<string>();
            Add(lines, "YargAccess", () => Mod.Version + (Speech.Quiet ? " (quiet test mode)" : ""));
            Add(lines, "YARG", YargVersion);
            Add(lines, "Unity", () => Application.unityVersion);
            Add(lines, "Windows", () => SystemInfo.operatingSystem);
            Add(lines, "CPU", () => SystemInfo.processorType.Trim() + ", " + SystemInfo.processorCount + " threads");
            Add(lines, "Memory", () => Gb(SystemInfo.systemMemorySize));
            Add(lines, "Graphics", () => SystemInfo.graphicsDeviceName + ", " + Gb(SystemInfo.graphicsMemorySize)
                + ", " + SystemInfo.graphicsDeviceVersion);
            Add(lines, "Screen", () =>
            {
                var r = Screen.currentResolution;
                return r.width + " x " + r.height + " at " + Math.Round(r.refreshRateRatio.value) + " Hz, game window "
                    + Screen.width + " x " + Screen.height + (Screen.fullScreen ? ", full screen" : ", windowed");
            });
            Add(lines, "Audio", AudioDevice);
            Add(lines, "Screen reader", ScreenReaders);
            Add(lines, "Input devices", InputDevices);

            MelonLogger.Msg("System specs:\n  " + string.Join("\n  ", lines));
        }

        static void Add(List<string> lines, string name, Func<string> value)
        {
            string v;
            try { v = value(); }
            catch (Exception ex) { v = "unknown (" + ex.GetType().Name + ")"; }
            lines.Add(name + ": " + (string.IsNullOrWhiteSpace(v) ? "unknown" : v));
        }

        static string Gb(int mb) => mb <= 0 ? "unknown" : Math.Round(mb / 1024.0, 1) + " GB";

        // YARG's own build string (Application.version is always 0.1.0).
        static string YargVersion()
        {
            var t = typeof(YARG.Settings.SettingsManager).Assembly.GetType("YARG.GlobalVariables");
            object v = null;
            if (t != null)
            {
                var inst = t.GetProperty("Instance", System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy)?.GetValue(null);
                if (inst != null) v = t.GetProperty("CurrentVersion")?.GetValue(inst);
            }
            return v != null ? v.ToString() : Application.version;
        }

        static string AudioDevice()
        {
            int dev = ManagedBass.Bass.CurrentDevice;
            if (dev < 0) return "BASS not started";
            string name = ManagedBass.Bass.GetDeviceInfo(dev, out var info) ? info.Name : "device " + dev;
            // BASS's "Default" device follows Windows; name the output it is following.
            if (name == "Default")
                for (int i = 1; ManagedBass.Bass.GetDeviceInfo(i, out var d); i++)
                    if (d.IsDefault && d.Name != "Default") { name = "Default (" + d.Name + ")"; break; }
            var bi = ManagedBass.Bass.Info;
            return name + ", " + bi.SampleRate + " Hz, " + bi.SpeakerCount + " speakers, latency " + bi.Latency + " ms";
        }

        static readonly (string Process, string Name)[] Readers =
        {
            ("nvda", "NVDA"), ("jfw", "JAWS"), ("narrator", "Narrator"), ("zdsrmain", "ZDSR"),
            ("snova", "SuperNova"), ("dolwinhk", "Dolphin"), ("sa", "System Access"), ("cobra", "COBRA"),
        };

        // Mono's Process.GetProcesses leaves out NVDA (it runs with UIAccess), so the process list comes
        // from a Toolhelp snapshot.
        static string ScreenReaders()
        {
            var found = new List<string>();
            foreach (var (pid, exe) in ProcessList())
            {
                string pn = System.IO.Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();
                foreach (var r in Readers)
                {
                    if (pn != r.Process || found.Any(f => f.StartsWith(r.Name))) continue;
                    found.Add(r.Name + ReaderVersion(pid));
                }
            }
            return found.Count > 0 ? string.Join(", ", found) : "none found";
        }

        static string ReaderVersion(int pid)
        {
            IntPtr h = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
            if (h == IntPtr.Zero) return "";
            try
            {
                var sb = new System.Text.StringBuilder(1024);
                int size = sb.Capacity;
                if (!QueryFullProcessImageName(h, 0, sb, ref size)) return "";
                var v = FileVersionInfo.GetVersionInfo(sb.ToString()).ProductVersion;
                return string.IsNullOrWhiteSpace(v) ? "" : " " + v.Trim();
            }
            catch { return ""; }
            finally { CloseHandle(h); }
        }

        static List<(int, string)> ProcessList()
        {
            var list = new List<(int, string)>();
            IntPtr snap = CreateToolhelp32Snapshot(2 /* TH32CS_SNAPPROCESS */, 0);
            if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return list;
            try
            {
                var e = new PROCESSENTRY32W { dwSize = (uint) System.Runtime.InteropServices.Marshal.SizeOf(typeof(PROCESSENTRY32W)) };
                for (bool ok = Process32FirstW(snap, ref e); ok; ok = Process32NextW(snap, ref e))
                    list.Add(((int) e.th32ProcessID, e.szExeFile ?? ""));
            }
            finally { CloseHandle(snap); }
            return list;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        struct PROCESSENTRY32W
        {
            public uint dwSize, cntUsage, th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID, cntThreads, th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern bool Process32FirstW(IntPtr snap, ref PROCESSENTRY32W e);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern bool Process32NextW(IntPtr snap, ref PROCESSENTRY32W e);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern bool QueryFullProcessImageName(IntPtr h, int flags, System.Text.StringBuilder name, ref int size);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);

        static string InputDevices()
        {
            var names = UnityEngine.InputSystem.InputSystem.devices
                .Where(d => !(d is UnityEngine.InputSystem.Mouse))
                .Select(d => string.IsNullOrEmpty(d.description.product) ? d.displayName : d.description.product)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();
            return names.Count > 0 ? string.Join(", ", names) : "none";
        }
    }
}
