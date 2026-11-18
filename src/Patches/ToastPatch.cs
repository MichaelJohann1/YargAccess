using System;
using HarmonyLib;
using MelonLoader;

namespace YargAccess.Patches
{
    [HarmonyPatch(typeof(YARG.Menu.Persistent.ToastManager), "AddToast")]
    static class ToastPatch
    {
        public static int Count;

        static bool Prepare() => AccessTools.Method(typeof(YARG.Menu.Persistent.ToastManager), "AddToast") != null;

        static void Postfix(object type, string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                if (Captions.Raising) return;
                var kind = type != null ? type.ToString() : "";
                var msg = TextUtil.Clean(text);
                if (kind == "Error") msg = "Error: " + msg;
                else if (kind == "Warning") msg = "Warning: " + msg;
                Count++;
                Speech.Say(msg, false);
            }
            catch (Exception ex) { ModLog.Warn("ToastPatch", ex); }
        }
    }
}
