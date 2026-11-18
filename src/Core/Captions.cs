using System;
using MelonLoader;

namespace YargAccess
{
    public static class Captions
    {
        public static bool Raising { get; private set; }

        static string _last;
        static float _lastAt;

        public static void Say(string text, bool interrupt = true)
        {
            Speech.Say(text, interrupt);
            Toast(text);
        }

        public static void Toast(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            string clean = TextUtil.Clean(text);
            float now = UnityEngine.Time.unscaledTime;
            if (clean == _last && now - _lastAt < 1f) return;
            _last = clean;
            _lastAt = now;
            try
            {
                Raising = true;
                YARG.Menu.Persistent.ToastManager.ToastMessage(clean);
            }
            catch (Exception ex) { ModLog.Warn("Captions.Toast", ex); }
            finally { Raising = false; }
        }

        public static void SayIn(string title, Func<bool> open, string text, bool interrupt = true)
        {
            Speech.Say(text, interrupt);
            Panel(TextUtil.Clean(text), title, open);
        }

        public static void Panel(string text, string title, Func<bool> stillHere)
        {
            Tooltips.Show(text, title, stillHere);
        }
    }
}
