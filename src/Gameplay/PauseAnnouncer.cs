using System;
using UnityEngine;
using MelonLoader;
using YARG.Menu.Navigation;

namespace YargAccess
{
    public static class PauseAnnouncer
    {
        static bool _open;
        static bool _skipNextResume;

        public static void SuppressNextResume() { _skipNextResume = true; }

        public static void OnPushMenu(string menuName, Component result)
        {
            try
            {
                _open = true;
                Menu.OnEnter(Label(menuName));
                var grp = result != null ? result.GetComponentInChildren<NavigationGroup>(true) : null;
                if (grp != null && grp.SelectedBehaviour == null) grp.SelectFirst();

                if (menuName == "SelectSections")
                {
                    PracticeSectionNavigator.Reset();
                    MenuAnnouncer.SayItem(PracticeSectionNavigator.OpenLine());
                }

                if (!MenuAnnouncer.EndBatch())
                {
                    MenuAnnouncer.ForceMenuPrefixNext();
                    var sel = grp != null ? grp.SelectedBehaviour : null;
                    if (sel != null) MenuAnnouncer.Announce(sel, true);
                    else MenuAnnouncer.Say(Label(menuName) + " menu");
                }
            }
            catch (Exception ex) { ModLog.Warn("PauseAnnouncer.OnPushMenu", ex); MenuAnnouncer.DiscardBatch(); }
        }

        public static void OnPopMenu()
        {
            try
            {
                Menu.OnEnter("Pause");
                if (!MenuAnnouncer.EndBatch())
                {
                    var grp = GameplayContext.ActivePauseGroup();
                    var sel = grp != null ? grp.SelectedBehaviour : null;
                    if (grp != null && sel == null) { grp.SelectFirst(); sel = grp.SelectedBehaviour; }
                    MenuAnnouncer.ForceMenuPrefixNext();
                    if (sel != null) MenuAnnouncer.Announce(sel, true);
                }
            }
            catch (Exception ex) { ModLog.Warn("PauseAnnouncer.OnPopMenu", ex); MenuAnnouncer.DiscardBatch(); }
        }

        public static void OnResume()
        {
            if (!_open) return;
            _open = false;
            if (_skipNextResume) { _skipNextResume = false; return; }
            MenuAnnouncer.SayShown("Resumed");
        }

        public static string Label(string e)
        {
            switch (e)
            {
                case "QuickPlayPause":
                case "SetlistPause": return "Pause";
                case "QuickSettings": return "Quick Settings";
                case "SettingsMenu": return "Settings";
                case "PracticePause": return "Practice";
                case "SelectSections": return "Select Sections";
                case "ReplayPause": return "Replay Pause";
                case "FailPause": return "Song Failed";
                default: return Describer.PrettifyName(string.IsNullOrEmpty(e) ? "Pause" : e);
            }
        }
    }
}
