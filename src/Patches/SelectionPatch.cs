using System;
using HarmonyLib;
using MelonLoader;
using YARG.Menu.Navigation;

namespace YargAccess.Patches
{
    [HarmonyPatch(typeof(NavigatableBehaviour), "SetSelected")]
    static class SetSelectedPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(NavigatableBehaviour), "SetSelected");

        static void Postfix(NavigatableBehaviour __instance)
        {
            try
            {
                if (__instance != null && __instance.Selected)
                    MenuAnnouncer.Announce(__instance);
            }
            catch (Exception ex)
            {
                ModLog.Warn("SetSelectedPatch", ex);
            }
        }
    }
}
