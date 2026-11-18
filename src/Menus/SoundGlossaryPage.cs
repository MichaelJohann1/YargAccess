using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Settings.Metadata;

namespace YargAccess
{
    public static class SoundGlossaryPage
    {
        static NavigationScheme _scheme;
        static ModSettings.Page _page;
        static MethodInfo _selectTab;
        static Action _onClose;

        public static bool IsOpen => _scheme != null;
        public static string Title => IsOpen && _page != null ? _page.Title : null;

        static SettingsMenu Menu()
        {
            var m = SettingsMenu.Instance;
            return m != null && m.gameObject.activeInHierarchy ? m : null;
        }

        static bool Select(SettingsMenu menu, Tab tab)
        {
            if (_selectTab == null) _selectTab = AccessTools.Method(typeof(SettingsMenu), "SelectTab", new[] { typeof(Tab) });
            if (_selectTab == null || tab == null) return false;
            _selectTab.Invoke(menu, new object[] { tab });
            return true;
        }

        public static void Open(ModSettings.Page page, Action onClose = null)
        {
            var menu = Menu();
            if (menu == null || IsOpen || page == null) return;
            try
            {
                _page = page;
                _onClose = onClose;
                MenuAnnouncer.BeginBatch();
                bool ok = Select(menu, page.Tab);
                MenuAnnouncer.DiscardBatch();
                if (!ok) { MelonLogger.Warning("SoundGlossaryPage: SettingsMenu.SelectTab not found"); return; }
                _scheme = new NavigationScheme(new List<NavigationScheme.Entry>
                {
                    NavigationScheme.Entry.NavigateSelect,
                    new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", (Action) Close),
                    NavigationScheme.Entry.NavigateUp,
                    NavigationScheme.Entry.NavigateDown,
                }, true);
                UnityEngine.Object.FindObjectOfType<Navigator>().PushScheme(_scheme);
                MenuAnnouncer.CancelSuppress();
                YargAccess.Menu.SetContext(page.Title, false);
                MenuAnnouncer.MenuAlreadySpoken();
                MenuAnnouncer.Focused(page.Title + ". " + FirstRow());
            }
            catch (Exception ex) { ModLog.Warn("SoundGlossaryPage.Open", ex); }
        }

        public static void Close()
        {
            var menu = Menu();
            var after = _onClose;
            _onClose = null;
            if (!PopScheme() || menu == null) return;
            if (after != null) { after(); return; }
            try
            {
                MenuAnnouncer.BeginBatch();
                menu.SelectTabByName(ModSettings.TabName);
                int row = _page != null ? ModSettings.RowIndex(_page.Row) : -1;
                if (row >= 0) menu.SelectSettingByIndex(row);
                MenuAnnouncer.DiscardBatch();
                MenuAnnouncer.Focused("YargAccess. " + FirstRow());
            }
            catch (Exception ex) { ModLog.Warn("SoundGlossaryPage.Close", ex); }
        }

        public static void Leave() { _onClose = null; PopScheme(); }

        static bool PopScheme()
        {
            if (_scheme == null) return false;
            var s = _scheme;
            _scheme = null;
            try
            {
                var nav = UnityEngine.Object.FindObjectOfType<Navigator>();
                var stack = AccessTools.Field(typeof(Navigator), "_schemeStack")?.GetValue(nav) as Stack<NavigationScheme>;
                if (stack != null && stack.Count > 0 && stack.Peek() == s) nav.PopScheme();
            }
            catch (Exception ex) { ModLog.Warn("SoundGlossaryPage.PopScheme", ex); }
            return true;
        }

        static string FirstRow()
        {
            var g = NavigationGroup.CurrentNavigationGroup;
            return g != null && g.SelectedBehaviour != null ? Describer.Describe(g.SelectedBehaviour) : "";
        }
    }

    [HarmonyPatch(typeof(SettingsMenu), "OnDisable")]
    static class SettingsMenuDisableGlossaryPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(SettingsMenu), "OnDisable") != null;
        static void Prefix() { SoundGlossaryPage.Leave(); }
    }
}
