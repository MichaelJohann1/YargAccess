using System;
using HarmonyLib;
using MelonLoader;
using YARG.Settings.Types;

namespace YargAccess.Patches
{

    [HarmonyPatch(typeof(YARG.Menu.Settings.SettingsMenu), "Start")]
    static class SettingsMenuStartPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(YARG.Menu.Settings.SettingsMenu), "Start") != null;

        static void Prefix() { ModSettings.InstallTab(); }
    }

    [HarmonyPatch(typeof(YARG.Settings.SettingsManager), "GetSettingByName")]
    static class GetSettingByNamePatch
    {
        static bool Prepare() => AccessTools.Method(typeof(YARG.Settings.SettingsManager), "GetSettingByName") != null;

        static bool Prefix(string name, ref ISettingType __result)
        {
            try
            {
                ISettingType s;
                if (ModSettings.TryGetSetting(name, out s)) { __result = s; return false; }
            }
            catch (Exception ex) { ModLog.Warn("GetSettingByNamePatch", ex); }
            return true;
        }
    }

    [HarmonyPatch(typeof(YARG.Localization.LocalizationManager), "TryGetLocalizedKey")]
    static class LocalizedKeyPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(YARG.Localization.LocalizationManager), "TryGetLocalizedKey") != null;

        static void Postfix(string key, ref string value, ref bool __result)
        {
            if (__result) return;
            string v;
            if (ModSettings.TryLocalize(key, out v)) { value = v; __result = true; }
        }
    }
}
