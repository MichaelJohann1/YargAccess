using System;
using HarmonyLib;
using MelonLoader;
using YARG.Menu.Settings;

namespace YargAccess.Patches
{
    [HarmonyPatch(typeof(BaseSettingNavigatable), nameof(BaseSettingNavigatable.Confirm))]
    static class SettingConfirmPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(BaseSettingNavigatable), "Confirm");

        static bool Prefix(BaseSettingNavigatable __instance)
        {
            try
            {
                var vis = __instance.BaseSettingVisual;
                if (vis == null) return true;
                if (ModSettings.IsGlossaryDropdown(vis.UnlocalizedName))
                {
                    ModSettings.PlayGlossary(vis.UnlocalizedName);
                    return false;
                }
                var page = ModSettings.PageForRow(vis.UnlocalizedName);
                if (page != null)
                {
                    SoundGlossaryPage.Open(page);
                    return false;
                }
                if (SettingsAccess.IsToggle(vis))
                {
                    if (SettingsAccess.ToggleValue(vis)) MenuAnnouncer.Say(SettingsAccess.ValueOnly(vis));
                    return false;
                }
                if (SettingsAccess.IsSlider(vis) || SettingsAccess.IsDropdown(vis))
                {
                    MenuAnnouncer.Say(InputDriver.LeftRightHint());
                    return false;
                }
            }
            catch (Exception ex) { ModLog.Warn("SettingConfirmPatch", ex); }
            return true;
        }
    }
}
