using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using MelonLoader;

namespace YargAccess
{
    // Speaks through NVDA's controller client with nvdaController_speakText, the same path NVDA uses for its
    // own messages, so the lines show in NVDA's Speech History add-on (speakSsml bypasses the function the
    // add-on patches).
    //
    // The client can't say when NVDA stops talking, so the mod listens for it: a worker thread reads the
    // Windows audio peak meter of every audio session that belongs to an NVDA process (nvda*.exe) on every
    // active output device. NVDA is still talking while its sound is above silence; speech has ended once
    // it has been silent for SilenceMs. If nothing sounds within StartMs of a line (speech muted, NVDA's
    // speech mode off) the line counts as over. With no NVDA audio session found, Known is false and
    // Speech falls back to its estimate from the text length.
    public static class NvdaSpeech
    {
        public const string DllName = "nvdaControllerClient64.dll";

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        delegate int SpeakTextFn([MarshalAs(UnmanagedType.LPWStr)] string text);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int NoArgFn();

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryW(string path);
        [DllImport("kernel32", CharSet = CharSet.Ansi)]
        static extern IntPtr GetProcAddress(IntPtr module, string name);

        static bool _tried;
        static SpeakTextFn _speak;
        static NoArgFn _cancel, _running;
        static float _runningCheckedAt = -100f;
        static bool _isRunning, _failed;

        // A sentence pause is about 0.3 s; a line is over after this much silence.
        const long SilenceMs = 500;
        // NVDA starts a line's sound within about 0.2 s; a line with no sound by then is over.
        const long StartMs = 1500;

        static readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        static long _saidAt = -100000, _soundAt = -100000;
        static volatile bool _heard;

        // Off by default: when on, when each line's sound starts and ends goes to the MelonLoader log
        // (written from the main thread by FlushLog). Only for checking timings.
        public static bool LogTimes = false;
        static readonly System.Collections.Concurrent.ConcurrentQueue<string> _log = new System.Collections.Concurrent.ConcurrentQueue<string>();

        public static void FlushLog()
        {
            string s;
            while (_log.TryDequeue(out s)) MelonLogger.Msg(s);
        }

        public static bool Loaded => _speak != null;

        // True when the mod has found NVDA's audio and can hear when it stops.
        public static bool Known => AudioMeter.Sessions > 0;

        // True while NVDA is still speaking what the mod gave it (only meaningful when Known).
        public static bool Waiting
        {
            get
            {
                if (_speak == null) return false;
                long now = _clock.ElapsedMilliseconds;
                long said = Interlocked.Read(ref _saidAt);
                if (!_heard) return now - said < StartMs;
                return now - Interlocked.Read(ref _soundAt) < SilenceMs;
            }
        }

        static void Load()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath) ?? "", DllName);
                if (!File.Exists(path)) { MelonLogger.Msg("NvdaSpeech: " + DllName + " not found, using UniversalSpeech for NVDA."); return; }
                IntPtr lib = LoadLibraryW(path);
                if (lib == IntPtr.Zero) { MelonLogger.Warning("NvdaSpeech: LoadLibrary failed, error " + Marshal.GetLastWin32Error()); return; }
                var speak = Get<SpeakTextFn>(lib, "nvdaController_speakText");
                _cancel = Get<NoArgFn>(lib, "nvdaController_cancelSpeech");
                _running = Get<NoArgFn>(lib, "nvdaController_testIfRunning");
                if (speak == null || _cancel == null || _running == null)
                {
                    MelonLogger.Msg("NvdaSpeech: " + DllName + " is incomplete, using UniversalSpeech for NVDA.");
                    return;
                }
                _speak = speak;
                AudioMeter.Start(OnSound);
                MelonLogger.Msg("NvdaSpeech: NVDA client loaded; speech end is heard from NVDA's audio.");
            }
            catch (Exception ex) { ModLog.Warn("NvdaSpeech.Load", ex); _speak = null; }
        }

        static T Get<T>(IntPtr lib, string name) where T : class
        {
            IntPtr p = GetProcAddress(lib, name);
            return p == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }

        // Called by the meter thread each time NVDA's audio is above silence.
        static void OnSound(long ms)
        {
            if (ms < Interlocked.Read(ref _saidAt)) return;
            if (LogTimes && !_heard) _log.Enqueue("[speech] sound started " + (ms - Interlocked.Read(ref _saidAt)) + " ms after the line");
            Interlocked.Exchange(ref _soundAt, ms);
            _heard = true;
        }

        static bool NvdaRunning()
        {
            float now = UnityEngine.Time.unscaledTime;
            if (now - _runningCheckedAt < 3f && !_failed) return _isRunning;
            _runningCheckedAt = now;
            _failed = false;
            try { _isRunning = _running() == 0; } catch { _isRunning = false; }
            return _isRunning;
        }

        // Hands the line to NVDA. False when NVDA or the client isn't there: use UniversalSpeech.
        public static bool TrySay(string text, bool interrupt)
        {
            Load();
            if (_speak == null || !NvdaRunning()) return false;
            try
            {
                if (interrupt) _cancel();
                int rc = _speak(Clean(text));
                if (rc != 0) { _failed = true; return false; }
                long now = _clock.ElapsedMilliseconds;
                // A queued line (no interrupt) while NVDA is still talking keeps the current sound going.
                if (interrupt || !Waiting) _heard = false;
                Interlocked.Exchange(ref _saidAt, now);
                AudioMeter.Wake();
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Warn("NvdaSpeech.TrySay", ex);
                _speak = null;
                return false;
            }
        }

        public static void Stop()
        {
            if (_speak == null) return;
            try { _cancel(); } catch { }
            Interlocked.Exchange(ref _saidAt, _clock.ElapsedMilliseconds - StartMs);
            _heard = false;
        }

        static string Clean(string s)
        {
            var b = new StringBuilder(s.Length);
            foreach (char c in s) if (c >= ' ' || c == '\n' || c == '\t') b.Append(c);
            return b.ToString();
        }

        // Reads the peak level of NVDA's audio sessions through Core Audio (IAudioSessionManager2 and
        // IAudioMeterInformation), calling the COM methods through their vtables so no COM interop is needed.
        static class AudioMeter
        {
            [DllImport("ole32")] static extern int CoInitializeEx(IntPtr reserved, int coInit);
            [DllImport("ole32")] static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, int ctx, ref Guid iid, out IntPtr obj);
            [DllImport("kernel32")] static extern IntPtr OpenProcess(int access, bool inherit, uint pid);
            [DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
            [DllImport("kernel32", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageNameW(IntPtr h, int flags, StringBuilder name, ref int size);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int QueryFn(IntPtr self, ref Guid iid, out IntPtr obj);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint ReleaseFn(IntPtr self);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumEndpointsFn(IntPtr self, int flow, int mask, out IntPtr coll);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CountFn(IntPtr self, out int n);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ItemFn(IntPtr self, int i, out IntPtr item);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ActivateFn(IntPtr self, ref Guid iid, int ctx, IntPtr p, out IntPtr obj);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int OutPtrFn(IntPtr self, out IntPtr obj);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int PidFn(IntPtr self, out uint pid);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int PeakFn(IntPtr self, out float peak);

            static Guid CLSID_MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
            static Guid IID_IMMDeviceEnumerator = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
            static Guid IID_IAudioSessionManager2 = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
            static Guid IID_IAudioSessionControl2 = new Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D");
            static Guid IID_IAudioMeterInformation = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");

            const int ClsctxAll = 23, ERender = 0, DeviceStateActive = 1;
            // Silence NVDA plays to keep the device awake is exact zeros; speech peaks far above this.
            const float Threshold = 0.002f;

            struct Meter { public IntPtr Ptr; public PeakFn Peak; }

            static Action<long> _onSound;
            static readonly List<Meter> _meters = new List<Meter>();
            static readonly Dictionary<uint, bool> _isNvda = new Dictionary<uint, bool>();
            static readonly AutoResetEvent _wake = new AutoResetEvent(false);
            static volatile int _sessions;
            static volatile bool _rescan;
            static bool _loggedFound;

            public static int Sessions => _sessions;

            public static void Start(Action<long> onSound)
            {
                _onSound = onSound;
                new Thread(Loop) { IsBackground = true, Name = "YargAccess NVDA audio" }.Start();
            }

            public static void Wake()
            {
                if (_sessions == 0) _rescan = true;
                _wake.Set();
            }

            static T Slot<T>(IntPtr obj, int slot) where T : class
            {
                IntPtr vtbl = Marshal.ReadIntPtr(obj);
                return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(vtbl, slot * IntPtr.Size), typeof(T)) as T;
            }

            static void Release(IntPtr obj) { if (obj != IntPtr.Zero) Slot<ReleaseFn>(obj, 2)(obj); }

            static void Loop()
            {
                try { CoInitializeEx(IntPtr.Zero, 0); } catch { }
                long nextScan = 0, lastBusy = 0;
                while (true)
                {
                    long now = _clock.ElapsedMilliseconds;
                    try
                    {
                        if (_rescan || now >= nextScan)
                        {
                            _rescan = false;
                            Scan();
                            // NVDA can open a new stream (a device change); look again now and then.
                            nextScan = now + (_meters.Count == 0 ? 2000 : 5000);
                        }
                        bool loud = false;
                        foreach (var m in _meters)
                        {
                            float peak;
                            if (m.Peak(m.Ptr, out peak) < 0) { _rescan = true; continue; }
                            if (peak > Threshold) loud = true;
                        }
                        if (loud) { lastBusy = now; _onSound(now); }
                    }
                    catch (Exception) { _rescan = true; }
                    // Read often while speech may be going, rarely when everything has been quiet a while.
                    bool recent = now - lastBusy < 3000 || now - Interlocked.Read(ref _saidAt) < 3000;
                    _wake.WaitOne(recent ? 20 : 250);
                }
            }

            static void Scan()
            {
                foreach (var m in _meters) Release(m.Ptr);
                _meters.Clear();
                IntPtr enumerator = IntPtr.Zero, coll = IntPtr.Zero;
                try
                {
                    if (CoCreateInstance(ref CLSID_MMDeviceEnumerator, IntPtr.Zero, ClsctxAll, ref IID_IMMDeviceEnumerator, out enumerator) < 0) return;
                    if (Slot<EnumEndpointsFn>(enumerator, 3)(enumerator, ERender, DeviceStateActive, out coll) < 0) return;
                    int devices;
                    Slot<CountFn>(coll, 3)(coll, out devices);
                    for (int d = 0; d < devices; d++)
                    {
                        IntPtr device = IntPtr.Zero, manager = IntPtr.Zero, sessions = IntPtr.Zero;
                        try
                        {
                            if (Slot<ItemFn>(coll, 4)(coll, d, out device) < 0) continue;
                            if (Slot<ActivateFn>(device, 3)(device, ref IID_IAudioSessionManager2, ClsctxAll, IntPtr.Zero, out manager) < 0) continue;
                            if (Slot<OutPtrFn>(manager, 5)(manager, out sessions) < 0) continue;
                            int count;
                            Slot<CountFn>(sessions, 3)(sessions, out count);
                            for (int i = 0; i < count; i++) AddIfNvda(sessions, i);
                        }
                        finally { Release(sessions); Release(manager); Release(device); }
                    }
                }
                finally
                {
                    Release(coll);
                    Release(enumerator);
                    _sessions = _meters.Count;
                    if (_meters.Count > 0 && !_loggedFound)
                    {
                        _loggedFound = true;
                        _log.Enqueue("NvdaSpeech: listening to " + _meters.Count + " NVDA audio session(s) for the end of speech.");
                    }
                }
            }

            static void AddIfNvda(IntPtr sessions, int i)
            {
                IntPtr control = IntPtr.Zero, control2 = IntPtr.Zero, meter = IntPtr.Zero;
                try
                {
                    if (Slot<ItemFn>(sessions, 4)(sessions, i, out control) < 0) return;
                    if (Slot<QueryFn>(control, 0)(control, ref IID_IAudioSessionControl2, out control2) < 0) return;
                    uint pid;
                    if (Slot<PidFn>(control2, 14)(control2, out pid) < 0 || pid == 0 || !IsNvda(pid)) return;
                    if (Slot<QueryFn>(control, 0)(control, ref IID_IAudioMeterInformation, out meter) < 0) return;
                    _meters.Add(new Meter { Ptr = meter, Peak = Slot<PeakFn>(meter, 3) });
                    meter = IntPtr.Zero;
                }
                finally { Release(meter); Release(control2); Release(control); }
            }

            static bool IsNvda(uint pid)
            {
                bool known;
                if (_isNvda.TryGetValue(pid, out known)) return known;
                bool yes = false;
                IntPtr h = OpenProcess(0x1000, false, pid);
                if (h != IntPtr.Zero)
                {
                    var name = new StringBuilder(520);
                    int size = name.Capacity;
                    if (QueryFullProcessImageNameW(h, 0, name, ref size))
                        yes = Path.GetFileName(name.ToString()).StartsWith("nvda", StringComparison.OrdinalIgnoreCase);
                    CloseHandle(h);
                }
                _isNvda[pid] = yes;
                return yes;
            }
        }
    }
}
