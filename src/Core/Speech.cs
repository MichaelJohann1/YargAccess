using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MelonLoader;

namespace YargAccess
{
    public static class Speech
    {
        const string Dll = "UniversalSpeech.dll";

        [DllImport(Dll, CharSet = CharSet.Unicode, EntryPoint = "speechSay")]
        static extern int speechSay([MarshalAs(UnmanagedType.LPWStr)] string text, int interrupt);

        [DllImport(Dll, EntryPoint = "speechStop")]
        static extern int speechStop();

        [DllImport(Dll, EntryPoint = "speechSetValue")]
        static extern int speechSetValue(int what, int value);

        const int SP_ENABLE_NATIVE_SPEECH = 0xFFFF;

        static bool _initialized;

        const int HistoryCapacity = 64;
        static readonly List<string> _history = new List<string>(HistoryCapacity);

        public static string LastSpoken => _history.Count > 0 ? _history[_history.Count - 1] : "";

        public static IReadOnlyList<string> History => _history;

        // Test mode (YARGACCESS_QUIET=1 in the environment): lines are recorded in History but not spoken,
        // and Mod mutes all game audio.
        public static readonly bool Quiet = Environment.GetEnvironmentVariable("YARGACCESS_QUIET") == "1";

        static float _quietAt, _nvdaCapAt;
        static bool _viaNvda;

        // With NVDA, the end of each line is heard from NVDA's own audio (NvdaSpeech). Other screen readers,
        // or NVDA before its audio session is found, use an estimate from the text length. The NVDA wait is
        // capped at twice the estimate plus 2 s, in case other NVDA sound (typing echo) keeps it going.
        public static bool Busy
        {
            get
            {
                float now = UnityEngine.Time.unscaledTime;
                if (_viaNvda && NvdaSpeech.Known) return NvdaSpeech.Waiting && now < _nvdaCapAt;
                return now < _quietAt;
            }
        }

        public static void ClearHistory() => _history.Clear();

        public static void Initialize(bool enableSapiFallback = true)
        {
            if (_initialized) return;
            _initialized = true;
            if (enableSapiFallback)
            {
                try { speechSetValue(SP_ENABLE_NATIVE_SPEECH, 1); }
                catch (Exception ex) { ModLog.Warn("Speech.Initialize SAPI toggle failed", ex); }
            }
        }

        // Lines to say, each as its own queued call, right after the next line that contains key
        // (a long dialog: its first sentence goes out with the title, the rest follow). Dropped if that
        // line has not been said within 2 seconds.
        static string _thenKey;
        static List<string> _then;
        static float _thenUntil;

        public static void Then(string key, List<string> rest)
        {
            _thenKey = key;
            _then = rest;
            _thenUntil = UnityEngine.Time.unscaledTime + 2f;
        }

        public static void Say(string text, bool interrupt = true)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            SayOne(text, interrupt);
            if (_then == null) return;
            if (UnityEngine.Time.unscaledTime > _thenUntil) { _then = null; return; }
            if (_thenKey == null || !text.Contains(_thenKey)) return;
            var rest = _then;
            _then = null;
            foreach (var line in rest) SayOne(line, false, false);
            TextReview.Spoken(text + " " + string.Join(" ", rest.ToArray()));
        }

        static void SayOne(string text, bool interrupt, bool review = true)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            Record(text);
            MelonLogger.Msg("Said: " + text);
            if (review) TextReview.Spoken(text);
            float now = UnityEngine.Time.unscaledTime;
            float estimate = 0.3f + text.Length / 18f;
            _quietAt = (interrupt ? now : Math.Max(now, _quietAt)) + estimate;
            if (Quiet) { _viaNvda = false; return; }

            try
            {
                if (NvdaSpeech.TrySay(text, interrupt))
                {
                    _nvdaCapAt = (interrupt || !_viaNvda ? now : Math.Max(now, _nvdaCapAt)) + estimate * 2f + 2f;
                    _viaNvda = true;
                    return;
                }
                _viaNvda = false;
                speechSay(text, interrupt ? 1 : 0);
            }
            catch (DllNotFoundException)
            {
                MelonLogger.Error(
                    "UniversalSpeech.dll not found. Copy it (plus nvdaControllerClient.dll and ZDSRAPI.dll) "
                    + "to the GAME ROOT (not Mods\\).");
            }
            catch (Exception ex)
            {
                ModLog.Warn("Speech.Say failed", ex);
            }
        }

        public static void Stop()
        {
            NvdaSpeech.Stop();
            try { speechStop(); } catch { }
        }

        static void Record(string text)
        {
            if (_history.Count >= HistoryCapacity) _history.RemoveAt(0);
            _history.Add(text);
        }
    }
}
