using System;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using YARG.Gameplay.HUD;

namespace YargAccess.Patches
{
    [HarmonyPatch(typeof(TextNotificationQueue), nameof(TextNotificationQueue.Enqueue))]
    static class NotificationPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(TextNotificationQueue), "Enqueue");

        static string _last;
        static float _lastAt = -10f;

        static void Prefix(TextNotification notification)
        {
            try
            {
                if (!GameplayContext.InGameplay) return;
                var text = TextUtil.Clean(notification.Text);
                if (string.IsNullOrEmpty(text)) return;
                if (text.ToUpperInvariant() == text && text.ToLowerInvariant() != text)
                    text = SentenceCase(text);
                if (!ShouldSpeak(text)) return;
                float now = Time.unscaledTime;
                if (text == _last && now - _lastAt < 1f) return;
                _last = text;
                _lastAt = now;
                Speech.Say(text, false);
            }
            catch (Exception ex) { ModLog.Warn("NotificationPatch", ex); }
        }

        static bool ShouldSpeak(string text)
        {
            var t = text.ToLowerInvariant();
            var mode = ModSettings.InSongSpeech != null ? ModSettings.InSongSpeech.Value : "important";
            if (mode == "off") return false;
            if (mode == "all" || VocalSound.Active) return true;
            return t.Contains("star power ready") || t.Contains("full combo") || t.Contains("high score");
        }

        static string SentenceCase(string text)
        {
            var lower = text.ToLowerInvariant();
            return char.IsLetter(lower[0]) ? char.ToUpperInvariant(lower[0]) + lower.Substring(1) : lower;
        }
    }
}
