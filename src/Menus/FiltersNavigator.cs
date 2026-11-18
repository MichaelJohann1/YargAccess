using System;
using MelonLoader;
using UnityEngine;
using YARG.Menu.Filters;
using YARG.Menu.Navigation;

namespace YargAccess
{
    public static class FiltersNavigator
    {
        public static bool IsActive
        {
            get
            {
                try
                {
                    var m = UnityEngine.Object.FindObjectOfType<FiltersMenu>();
                    return m != null && m.gameObject.activeInHierarchy;
                }
                catch { return false; }
            }
        }

        public static bool Confirm()
        {
            if (!IsActive) return false;
            try
            {
                var g = NavigationGroup.CurrentNavigationGroup;
                var sel = g != null ? g.SelectedBehaviour : null;
                if (sel == null) return false;

                if (sel is FilterCategoryRow)
                {
                    YargInput.Invoke("Green");
                    return true;
                }

                var entry = sel.GetComponent<FilterEntryRow>();
                if (entry != null && entry.Toggle != null)
                {
                    YargInput.Invoke("Green");
                    MenuAnnouncer.Say(entry.Toggle.isOn ? "checked" : "not checked");
                    return true;
                }
            }
            catch (Exception ex) { ModLog.Warn("FiltersNavigator.Confirm", ex); }
            return false;
        }
    }
}
