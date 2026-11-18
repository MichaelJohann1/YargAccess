using UnityEngine;

namespace YargAccess
{
    public static class StarPowerSpeech
    {
        static bool? _last;
        static float _lastAt = -10f;

        static float _readyAt = -10f;

        public static void OnReady()
        {
            float now = Time.unscaledTime;
            if (now - _readyAt < 1f) return;
            _readyAt = now;
            Audio.CueBank.Play("spready", 0.8f);
        }

        public static void OnStatus(bool active)
        {
            if (ModSettings.InSongSpeech != null && ModSettings.InSongSpeech.Value == "off") return;
            float now = Time.unscaledTime;
            if (_last == active && now - _lastAt < 1f) return;
            _last = active;
            _lastAt = now;
            Captions.Say(active ? "Star power on" : "Star power off", false);
        }
    }
}
