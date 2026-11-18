using System;
using MelonLoader;
using UnityEngine;

namespace YargAccess
{
    public static class ScanAnnouncer
    {
        const float SlowAfter = 1.5f;
        const float GiveUpAfter = 5f;

        static bool _requested, _sawLoading, _saidScanning;
        static float _startedAt;

        public static void Begin()
        {
            _requested = true;
            _sawLoading = _saidScanning = false;
            _startedAt = Time.unscaledTime;
        }

        public static void Tick()
        {
            if (!_requested) return;
            bool loading = false;
            try { loading = YARG.LoadingScreen.IsActive; } catch { }
            float elapsed = Time.unscaledTime - _startedAt;
            if (loading)
            {
                _sawLoading = true;
                if (!_saidScanning && elapsed >= SlowAfter) { _saidScanning = true; Captions.Say("Scanning songs"); }
                return;
            }
            if (!_sawLoading)
            {
                if (elapsed >= GiveUpAfter) _requested = false;
                return;
            }
            _requested = false;
            int count = 0;
            try { count = YARG.Song.SongContainer.Count; } catch (Exception ex) { ModLog.Warn("ScanAnnouncer", ex); }
            Captions.Say((_saidScanning ? "Song scan finished. " : "Scan finished. ") + count + (count == 1 ? " song" : " songs") + " in the library.", false);
        }
    }
}
