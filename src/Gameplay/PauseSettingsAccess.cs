using System;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using YARG.Menu.Navigation;

namespace YargAccess
{
    public static class PauseSettingsAccess
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static object SettingOf(Component nb)
        {
            if (nb == null) return null;
            for (var t = nb.GetType(); t != null; t = t.BaseType)
            {
                if (t.Name != "BasePauseSetting`1") continue;
                var f = t.GetField("Setting", BF);
                if (f != null) return f.GetValue(nb);
            }
            return null;
        }

        public static bool IsSetting(Component nb) => SettingOf(nb) != null;

        public static bool IsToggle(Component nb)
        {
            var s = SettingOf(nb);
            return s != null && s.GetType().Name == "ToggleSetting";
        }

        public static bool Adjust(Component nb, int dir)
        {
            try
            {
                var s = SettingOf(nb);
                if (s == null) return false;
                var vp = s.GetType().GetProperty("Value");
                if (vp == null) return false;

                if (vp.PropertyType == typeof(float))
                {
                    vp.SetValue(s, (float) vp.GetValue(s, null) + 0.05f * dir, null);
                }
                else if (vp.PropertyType == typeof(int))
                {
                    vp.SetValue(s, (int) vp.GetValue(s, null) + dir, null);
                }
                else if (vp.PropertyType == typeof(bool))
                {
                    vp.SetValue(s, dir > 0, null);
                }
                else return false;

                Refresh(nb);
                return true;
            }
            catch (Exception ex) { ModLog.Warn("PauseSettingsAccess.Adjust", ex); return false; }
        }

        public static bool Toggle(Component nb)
        {
            try
            {
                var s = SettingOf(nb);
                var vp = s?.GetType().GetProperty("Value");
                if (vp == null || vp.PropertyType != typeof(bool)) return false;
                vp.SetValue(s, !(bool) vp.GetValue(s, null), null);
                Refresh(nb);
                return true;
            }
            catch (Exception ex) { ModLog.Warn("PauseSettingsAccess.Toggle", ex); return false; }
        }

        public static string ValueText(Component nb)
        {
            try
            {
                var s = SettingOf(nb);
                var v = s?.GetType().GetProperty("Value")?.GetValue(s, null);
                if (v == null) return "";
                if (v is bool b) return b ? Loc.T("checked") : Loc.T("not checked");
                if (v is float f) return Loc.F("{0}%", Mathf.RoundToInt(f * 100f));
                return v.ToString();
            }
            catch { return ""; }
        }

        static void Refresh(Component nb)
        {
            try
            {
                var m = nb.GetType().GetMethod("RefreshVisual", BindingFlags.Instance | BindingFlags.NonPublic);
                if (m != null) m.Invoke(nb, null);
            }
            catch { }
        }

        public static string Name(Component nb)
        {
            if (nb == null) return "";
            try
            {
                for (var t = nb.GetType(); t != null; t = t.BaseType)
                {
                    if (t.Name != "BasePauseSetting`1") continue;
                    var f = t.GetField("_text", BindingFlags.Instance | BindingFlags.NonPublic);
                    var tmp = f != null ? f.GetValue(nb) as TMPro.TMP_Text : null;
                    return tmp != null ? TextUtil.Clean(tmp.text) : "";
                }
            }
            catch { }
            return "";
        }

        public static string Role(Component nb)
        {
            var s = SettingOf(nb);
            if (s == null) return "";
            switch (s.GetType().Name)
            {
                case "ToggleSetting": return "checkbox";
                case "VolumeSetting": return "slider";
                default:              return "number";
            }
        }

        public static NavigatableBehaviour Selected()
        {
            var pg = GameplayContext.ActivePauseGroup();
            return pg != null ? pg.SelectedBehaviour : null;
        }
    }
}
