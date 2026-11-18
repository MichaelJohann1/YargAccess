using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace YargAccess.Patches
{

    [HarmonyPatch(typeof(YARG.Menu.MenuManager), "PushMenu")]
    static class PushMenuPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(YARG.Menu.MenuManager), "PushMenu");

        static void Prefix(object __0)
        {
            try
            {
                var name = CleanName(__0 != null ? __0.ToString() : "");
                if (!string.IsNullOrEmpty(name)) Menu.OnEnter(name);
                ResetMenuNavigators();
                MenuAnnouncer.BeginBatch();
            }
            catch (Exception ex) { ModLog.Warn("PushMenuPatch.Prefix", ex); }
        }

        static void Postfix()
        {
            try
            {
                if (DifficultySelectExtras.IsActive) MenuAnnouncer.Say(DifficultySelectExtras.OpenLine());
                MenuAnnouncer.EndBatchSettle(AnnounceCurrent);
            }
            catch (Exception ex) { ModLog.Warn("PushMenuPatch.Postfix", ex); }
        }

        internal static void ResetMenuNavigators()
        {
            try
            {
                DifficultySelectExtras.ResetZone();
                ProfilesNavigator.Reset();
                ProfileInfoNavigator.Reset();
                PresetsNavigator.ResetZone();
                SettingsExtras.ResetZone();
                HistoryNavigator.ResetZone();
                ListMenuAnnouncer.ResetLast();
            }
            catch (Exception ex) { ModLog.Warn("ResetMenuNavigators", ex); }
        }

        internal static string CleanName(string raw)
        {
            var pretty = Describer.PrettifyName(raw);
            if (pretty.EndsWith(" Menu", StringComparison.OrdinalIgnoreCase))
                pretty = pretty.Substring(0, pretty.Length - 5).Trim();
            return pretty;
        }

        internal static string MenuAtOffset(int offset)
        {
            try
            {
                var mm = UnityEngine.Object.FindObjectOfType<YARG.Menu.MenuManager>();
                if (mm == null) return null;
                var f = typeof(YARG.Menu.MenuManager).GetField("_openMenus", BindingFlags.NonPublic | BindingFlags.Instance);
                var stack = f != null ? f.GetValue(mm) : null;
                if (stack == null) return null;
                var arr = stack.GetType().GetMethod("ToArray").Invoke(stack, null) as Array;
                if (arr == null || arr.Length <= offset) return null;
                var v = arr.GetValue(offset);
                return CleanName(v != null ? v.ToString() : "");
            }
            catch { return null; }
        }

        internal static void AnnounceCurrent()
        {
            try
            {
                MenuAnnouncer.ForceMenuPrefixNext();
                if (DialogNavigator.IsActive) return;
                if (CreditsNavigator.IsActive) return;
                if (CalibratorAnnouncer.IsActive) { CalibratorAnnouncer.AnnounceCurrent(); return; }
                if (ProfileInfoNavigator.IsActive) { ProfileInfoNavigator.AnnounceCurrent(); return; }
                if (ProfilesNavigator.IsActive) { ProfilesNavigator.AnnounceCurrent(); return; }
                if (ListMenuAnnouncer.AnnounceActive()) return;
                var g = YARG.Menu.Navigation.NavigationGroup.CurrentNavigationGroup;
                var sel = g != null ? g.SelectedBehaviour : null;
                if (sel != null && sel.isActiveAndEnabled) { MenuAnnouncer.Announce(sel, true); return; }
                if (PresetsNavigator.IsActive) PresetsNavigator.AnnounceCurrent();
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(YARG.Menu.MenuManager), "PopMenu")]
    static class PopMenuPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(YARG.Menu.MenuManager), "PopMenu");

        static void Prefix()
        {
            try
            {
                var name = PushMenuPatch.MenuAtOffset(1);
                if (!string.IsNullOrEmpty(name)) Menu.OnEnter(name);
                PushMenuPatch.ResetMenuNavigators();
                MenuAnnouncer.BeginBatch();
            }
            catch (Exception ex) { ModLog.Warn("PopMenuPatch.Prefix", ex); }
        }

        static void Postfix()
        {
            try
            {
                var name = PushMenuPatch.MenuAtOffset(0);
                if (!string.IsNullOrEmpty(name)) Menu.OnEnter(name);
                MenuAnnouncer.ForceMenuPrefixNext();
                MenuAnnouncer.EndBatchSettle(PushMenuPatch.AnnounceCurrent);
            }
            catch (Exception ex) { ModLog.Warn("PopMenuPatch.Postfix", ex); }
        }
    }

    [HarmonyPatch(typeof(YARG.Menu.ScoreScreen.ScoreScreenMenu), "OnEnable")]
    static class ScoreScreenPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(YARG.Menu.ScoreScreen.ScoreScreenMenu), "OnEnable") != null;

        static void Postfix()
        {
            try
            {
                Menu.OnEnter("Score");
                MenuAnnouncer.ForceMenuPrefixNext();
                var line = ScoreScreenNavigator.OpenLine();
                MenuAnnouncer.SayItem(line);
            }
            catch (Exception ex) { ModLog.Warn("ScoreScreenPatch", ex); }
        }
    }

    [HarmonyPatch(typeof(YARG.Menu.Settings.SettingsMenu), "OnEnable")]
    static class SettingsOpenPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(YARG.Menu.Settings.SettingsMenu), "OnEnable");

        static void Prefix() { try { Menu.OnEnter("Settings"); PresetsNavigator.ResetZone(); SettingsExtras.ResetZone(); MenuAnnouncer.BeginBatch(); } catch { } }
        static void Postfix() { try { MenuAnnouncer.EndBatchSettle(PushMenuPatch.AnnounceCurrent); } catch { } }
    }

    [HarmonyPatch(typeof(YARG.Menu.Settings.SettingsMenu), "OnDisable")]
    static class SettingsClosePatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(YARG.Menu.Settings.SettingsMenu), "OnDisable");

        static void Prefix()
        {
            try
            {
                var name = PushMenuPatch.MenuAtOffset(0);
                if (!string.IsNullOrEmpty(name)) Menu.OnEnter(name);
                MenuAnnouncer.BeginBatch();
            }
            catch { }
        }

        static void Postfix()
        {
            try
            {
                var name = PushMenuPatch.MenuAtOffset(0);
                if (!string.IsNullOrEmpty(name)) Menu.OnEnter(name);
                MenuAnnouncer.ForceMenuPrefixNext();
                MenuAnnouncer.EndBatchSettle(PushMenuPatch.AnnounceCurrent);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(YARG.Menu.Persistent.DialogManager), "ClearDialog")]
    static class DialogClearPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(YARG.Menu.Persistent.DialogManager), "ClearDialog");

        static void Postfix()
        {
            try { MenuAnnouncer.BeginSettle(PushMenuPatch.AnnounceCurrent); }
            catch (Exception ex) { ModLog.Warn("DialogClearPatch", ex); }
        }
    }
}
