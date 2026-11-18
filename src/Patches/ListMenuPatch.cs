using System;
using HarmonyLib;
using MelonLoader;

namespace YargAccess.Patches
{
    [HarmonyPatch(typeof(YARG.Menu.MusicLibrary.MusicLibraryMenu), "OnSelectedIndexChanged")]
    static class MusicLibrarySelectionPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(YARG.Menu.MusicLibrary.MusicLibraryMenu), "OnSelectedIndexChanged");

        static void Postfix(YARG.Menu.MusicLibrary.MusicLibraryMenu __instance)
        {
            try { ListMenuAnnouncer.Announce(__instance); }
            catch (Exception ex) { ModLog.Warn("MusicLibrarySelectionPatch", ex); }
        }
    }

    [HarmonyPatch]
    static class HistorySelectionPatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            try
            {
                var baseType = typeof(YARG.Menu.History.HistoryMenu).BaseType;
                return AccessTools.Method(baseType, "OnSelectedIndexChanged");
            }
            catch { return null; }
        }

        static bool Prepare() { return TargetMethod() != null; }

        static void Postfix(object __instance)
        {
            try { ListMenuAnnouncer.Announce(__instance); }
            catch (Exception ex) { ModLog.Warn("HistorySelectionPatch", ex); }
        }
    }
}
