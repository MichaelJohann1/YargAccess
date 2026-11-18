using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YARG.Menu.Navigation;

namespace YargAccess
{
    public static class Describer
    {
        public static string Describe(NavigatableBehaviour nav)
        {
            _modTip = null;
            var s = Playability.AnnotateDifficultyRow(DescribeCore(nav));
            if (_modTip == null && OnDifficultyScreen())
            {
                _modTip = Tooltips.ForDifficultyRow(s);
                if (_modTip != null) s += ". " + _modTip;
            }
            int comma = s.IndexOf(',');
            Tooltips.Show(_modTip, comma > 0 ? s.Substring(0, comma) : s, OnDifficultyScreen);
            return s;
        }

        static string _modTip;

        static bool OnDifficultyScreen()
        {
            try
            {
                var m = UnityEngine.Object.FindObjectOfType<YARG.Menu.DifficultySelect.DifficultySelectMenu>();
                return m != null && m.gameObject.activeInHierarchy;
            }
            catch { return false; }
        }

        static string DescribeCore(NavigatableBehaviour nav)
        {
            if (nav == null) return "";

            var vis = SettingsAccess.GetVisual(nav);
            if (vis != null)
            {
                var sb = new StringBuilder(96);
                var name = SettingsAccess.GetName(vis);
                sb.Append(string.IsNullOrEmpty(name) ? PrettifyName(nav.gameObject.name) : name);
                var rv = SettingsAccess.RoleAndValue(vis);
                if (!string.IsNullOrEmpty(rv)) { sb.Append(", "); sb.Append(rv); }
                AppendIndex(sb, nav);
                var desc = ModSettings.ReadDescriptions.Value ? SettingsAccess.GetDescription(vis) : null;
                if (string.IsNullOrEmpty(desc) && vis.IsPresetSetting) desc = _modTip = Tooltips.ForPresetSetting(vis.UnlocalizedName);
                if (!string.IsNullOrEmpty(desc)) { sb.Append(". "); sb.Append(desc); }
                return TextUtil.Clean(sb.ToString());
            }

            if (PauseSettingsAccess.IsSetting(nav))
            {
                var ps = new StringBuilder(64);
                ps.Append(PauseSettingsAccess.Name(nav));
                var psRole = PauseSettingsAccess.Role(nav);
                ps.Append(Keys.Role(psRole));
                var val = PauseSettingsAccess.ValueText(nav);
                if (!string.IsNullOrEmpty(val)) { ps.Append(", "); ps.Append(val); }
                AppendIndex(ps, nav);
                return TextUtil.Clean(ps.ToString());
            }

            if (SettingsAccess.IsButtonRow(nav))
            {
                var bsb = new StringBuilder(96);
                bsb.Append(SettingsAccess.DescribeButtonRow(nav));
                AppendIndex(bsb, nav);
                return TextUtil.Clean(bsb.ToString());
            }

            var go = nav.gameObject;
            var sb2 = new StringBuilder(96);
            sb2.Append(GetLabel(go));
            var role = GetRoleState(go);
            if (!string.IsNullOrEmpty(role)) { sb2.Append(", "); sb2.Append(role); }
            AppendIndex(sb2, nav);
            return TextUtil.Clean(sb2.ToString());
        }

        static void AppendIndex(StringBuilder sb, NavigatableBehaviour nav)
        {
            var grp = nav.NavigationGroup;
            if (grp == null) return;
            sb.Append(Keys.Pos(grp.SelectedIndex.HasValue ? grp.SelectedIndex.Value : -1, grp.Count));
        }

        public static string GetLabel(GameObject go)
        {
            if (go == null) return "";
            var parts = CollectTexts(go.transform);
            if (parts.Count == 0 && go.transform.parent != null)
                parts = CollectTexts(go.transform.parent);
            if (parts.Count == 0) return PrettifyName(go.name);
            for (int i = 0; i < parts.Count; i++) parts[i] = SpeakableTime(parts[i]);
            return string.Join(", ", parts.ToArray());
        }

        static string SpeakableTime(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 5 || s.Length > 8) return s;
            var bits = s.Split(':');
            if (bits.Length != 3) return s;
            int h, m, sec;
            if (!int.TryParse(bits[0], out h) || !int.TryParse(bits[1], out m) || !int.TryParse(bits[2], out sec)) return s;
            m += h * 60;
            return m + (m == 1 ? " minute " : " minutes ") + sec + (sec == 1 ? " second" : " seconds");
        }

        static List<string> CollectTexts(Transform root)
        {
            var parts = new List<string>();
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(false))
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                var s = TextUtil.Clean(t.text);
                if (!string.IsNullOrEmpty(s) && !parts.Contains(s)) parts.Add(s);
            }
            if (parts.Count == 0)
            {
                foreach (var t in root.GetComponentsInChildren<Text>(false))
                {
                    if (t == null || !t.isActiveAndEnabled) continue;
                    var s = TextUtil.Clean(t.text);
                    if (!string.IsNullOrEmpty(s) && !parts.Contains(s)) parts.Add(s);
                }
            }
            return parts;
        }

        static string GetRoleState(GameObject go)
        {
            var custom = CustomToggleState(go);
            if (custom != null) return Keys.RoleThen("checkbox", custom.Value ? "checked" : "not checked");

            var toggle = go.GetComponentInChildren<Toggle>();
            if (toggle != null) return Keys.RoleThen("checkbox", toggle.isOn ? "checked" : "not checked");

            var slider = go.GetComponentInChildren<Slider>();
            if (slider != null) return Keys.RoleThen("slider", FormatSlider(slider));

            var dd = go.GetComponentInChildren<TMP_Dropdown>();
            if (dd != null)
            {
                var v = (dd.value >= 0 && dd.value < dd.options.Count) ? TextUtil.Clean(dd.options[dd.value].text) : "";
                return Keys.RoleThen("dropdown", v);
            }

            var input = go.GetComponentInChildren<TMP_InputField>();
            if (input != null)
            {
                var t = TextUtil.Clean(input.text);
                return Keys.RoleThen("edit box", string.IsNullOrEmpty(t) ? "blank" : t);
            }

            return ModSettings.ReadRoles.Value ? "button" : "";
        }

        static readonly string[] CustomToggleTypes = { "ModifierItem" };

        static bool? CustomToggleState(GameObject go)
        {
            if (go == null) return null;
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                var tn = mb.GetType().Name;
                bool match = false;
                for (int i = 0; i < CustomToggleTypes.Length; i++)
                    if (tn == CustomToggleTypes[i]) { match = true; break; }
                if (!match) continue;

                foreach (var name in new[] { "Active", "Value", "IsOn" })
                {
                    var p = mb.GetType().GetProperty(name);
                    if (p != null && p.PropertyType == typeof(bool))
                    {
                        try { return (bool) p.GetValue(mb, null); } catch { }
                    }
                }
            }
            return null;
        }

        public static bool IsInPlaceToggle(GameObject go)
        {
            if (go == null) return false;
            if (CustomToggleState(go).HasValue) return true;
            return go.GetComponentInChildren<Toggle>() != null;
        }

        public static string FormatSlider(Slider s)
        {
            if (s.wholeNumbers) return Mathf.RoundToInt(s.value).ToString();
            var range = s.maxValue - s.minValue;
            if (range <= 0.0001f) return s.value.ToString("0.##");
            return Mathf.RoundToInt((s.value - s.minValue) / range * 100f) + "%";
        }

        public static string PrettifyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var sb = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (c == '_' || c == '-') { sb.Append(' '); continue; }
                if (i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return TextUtil.Clean(sb.ToString());
        }
    }
}
