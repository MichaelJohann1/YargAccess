using System;
using HarmonyLib;
using MelonLoader;
using YARG.Menu.Dialogs;

namespace YargAccess.Patches
{
    [HarmonyPatch(typeof(Dialog), "OnEnable")]
    static class DialogOpenPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(Dialog), "OnEnable");

        static void Postfix(Dialog __instance)
        {
            try { if (__instance != null) DialogAnnouncer.Schedule(__instance); }
            catch (Exception ex) { ModLog.Warn("DialogOpenPatch", ex); }
        }
    }
}
