using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Menu.Settings.Visuals;

namespace YargAccess
{
    public static class SettingsAccess
    {
        const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        public static BaseSettingVisual GetVisual(NavigatableBehaviour nav)
        {
            var bsn = nav as BaseSettingNavigatable;
            return bsn != null ? bsn.BaseSettingVisual : null;
        }

        public static bool IsToggle(BaseSettingVisual vis) { return vis is ToggleSettingVisual; }
        public static bool IsSlider(BaseSettingVisual vis) { return vis is SliderSettingVisual; }
        public static bool IsDropdown(BaseSettingVisual vis) { return vis is DropdownSettingVisual; }
        public static bool IsInt(BaseSettingVisual vis) { return vis is IntSettingVisual; }
        public static bool IsDuration(BaseSettingVisual vis) { return vis is DurationSettingVisual; }
        public static bool IsAdjustable(BaseSettingVisual vis)
        {
            return IsSlider(vis) || IsDropdown(vis) || IsInt(vis) || IsDuration(vis);
        }

        public static bool IsEditable(BaseSettingVisual vis)
        {
            return vis is ColorSettingVisual || vis is IPv4SettingVisual || vis is DMXChannelsSettingVisual
                || vis is HitWindowSettingVisual || vis is DurationSettingVisual || vis is IntSettingVisual
                || vis is FileInfoSettingVisual
                || ModSettings.IsGlossaryDropdown(vis.UnlocalizedName)
                || ModSettings.PageForRow(vis.UnlocalizedName) != null;
        }

        public static string GetName(BaseSettingVisual vis)
        {
            if (vis == null) return "";
            var tmp = FieldVal(vis, "_settingLabel") as TMP_Text;
            var name = tmp != null ? TextUtil.Clean(tmp.text) : "";
            if (string.IsNullOrEmpty(name) || name == ValueOnly(vis))
            {
                var loc = LocalizedSettingText(vis, "Name");
                if (!string.IsNullOrEmpty(loc)) return loc;
                return Describer.PrettifyName(vis.UnlocalizedName ?? "");
            }
            return name;
        }

        static MethodInfo _localizeKey3;

        public static string GetDescription(BaseSettingVisual vis)
        {
            if (vis == null) return "";
            try
            {
                if (!vis.HasDescription) return "";
                return LocalizedSettingText(vis, "Description");
            }
            catch { return ""; }
        }

        static string LocalizedSettingText(BaseSettingVisual vis, string part)
        {
            try
            {
                var name = vis.UnlocalizedName;
                if (string.IsNullOrEmpty(name)) return "";
                if (_localizeKey3 == null)
                {
                    var lt = Type.GetType("YARG.Localization.Localize, Assembly-CSharp");
                    if (lt != null)
                        foreach (var m in lt.GetMethods(BindingFlags.Public | BindingFlags.Static))
                            if (m.Name == "Key" && m.IsGenericMethodDefinition && m.GetParameters().Length == 3) { _localizeKey3 = m; break; }
                }
                if (_localizeKey3 == null) return "";
                var gm = _localizeKey3.MakeGenericMethod(typeof(string), typeof(string), typeof(string));
                string table = vis.IsPresetSetting ? "Settings.PresetSetting" : "Settings.Setting";
                var s = gm.Invoke(null, new object[] { table, name, part }) as string;
                if (string.IsNullOrEmpty(s)) return "";
                if (s.IndexOf(table, StringComparison.Ordinal) >= 0) return "";
                return TextUtil.Clean(s);
            }
            catch { return ""; }
        }

        public static string RoleAndValue(BaseSettingVisual vis)
        {
            if (vis is ToggleSettingVisual)
            {
                var t = FieldVal(vis, "_toggle") as Toggle;
                return Keys.RoleThen("checkbox", t != null && t.isOn ? "checked" : "not checked");
            }
            if (vis is SliderSettingVisual) return Keys.RoleThen("slider", SliderText(vis));
            if (vis is DropdownSettingVisual)
            {
                return Keys.RoleThen("dropdown", DropdownValue(vis));
            }
            if (vis is IntSettingVisual)
            {
                var f = FieldVal(vis, "_inputField") as TMP_InputField;
                var t = f != null ? TextUtil.Clean(f.text) : "";
                return Keys.RoleThen("number", string.IsNullOrEmpty(t) ? "blank" : t);
            }
            if (vis is ColorSettingVisual) return Keys.RoleThen("color", ValueOnly(vis)) + ", " + Keys.Ok + " to type a hex code";
            if (vis is DurationSettingVisual) return Keys.RoleThen("duration", ValueOnly(vis));
            if (vis is HitWindowSettingVisual) return Keys.RoleThen("hit window", ValueOnly(vis)) + ", " + Keys.Ok + " to type";
            if (vis is IPv4SettingVisual) return Keys.RoleThen("IP address", ValueOnly(vis)) + ", " + Keys.Ok + " to type";
            if (vis is DMXChannelsSettingVisual) return Keys.RoleThen("channel list", ValueOnly(vis)) + ", " + Keys.Ok + " to type";
            if (vis is FileInfoSettingVisual)
                return Keys.RoleThen("image file", ValueOnly(vis)) + ", " + Keys.Ok + " to browse" + (Keys.Current == Keys.Device.Keyboard ? ", Delete to remove" : "");
            return Describer.PrettifyName(vis.GetType().Name.Replace("SettingVisual", ""));
        }

        static string DropdownValue(BaseSettingVisual vis)
        {
            YARG.Settings.Types.ISettingType own;
            var nd = ModSettings.TryGetSetting(vis.UnlocalizedName, out own) ? own as ModSettings.NamedDropdown : null;
            if (nd != null) return nd.ValueToString(nd.Value);
            return DropdownText(FieldVal(vis, "_dropdown") as TMP_Dropdown);
        }

        public static string ValueOnly(BaseSettingVisual vis)
        {
            if (vis is ToggleSettingVisual)
            {
                var t = FieldVal(vis, "_toggle") as Toggle;
                return t != null && t.isOn ? "checked" : "not checked";
            }
            if (vis is SliderSettingVisual) return SliderText(vis);
            if (vis is DropdownSettingVisual) return DropdownValue(vis);
            if (vis is IntSettingVisual)
            {
                var f = FieldVal(vis, "_inputField") as TMP_InputField;
                return f != null ? TextUtil.Clean(f.text) : "";
            }
            if (vis is ColorSettingVisual) return ColorText(vis);
            if (vis is DurationSettingVisual) return SpokenDuration(FieldText(FieldVal(vis, "_inputField") as Component));
            if (vis is HitWindowSettingVisual)
            {
                var dyn = FieldVal(vis, "_dynamicContainer") as GameObject;
                if (dyn != null && dyn.activeSelf)
                    return "dynamic, from " + SpokenDuration(FieldText(FieldVal(vis, "_minField") as Component)) +
                           " to " + SpokenDuration(FieldText(FieldVal(vis, "_maxField") as Component));
                return SpokenDuration(FieldText(FieldVal(vis, "_constantField") as Component));
            }
            if (vis is IPv4SettingVisual) return FieldText(FieldVal(vis, "_inputField") as Component);
            if (vis is DMXChannelsSettingVisual)
            {
                var arr = FieldVal(vis, "_inputField") as TMP_InputField[];
                if (arr == null) return "";
                var parts = new System.Collections.Generic.List<string>();
                foreach (var f in arr) if (f != null) parts.Add(TextUtil.Clean(f.text));
                return string.Join(", ", parts.ToArray());
            }
            if (vis is FileInfoSettingVisual)
            {
                var t = FieldVal(vis, "_statusText") as TMP_Text;
                return t != null ? TextUtil.Clean(t.text) : "";
            }
            return "";
        }

        static string ColorText(BaseSettingVisual vis)
        {
            var hex = FieldText(FieldVal(vis, "_inputField") as Component);
            var s = "#" + hex;
            Color c;
            if (ColorUtility.TryParseHtmlString("#" + hex, out c))
                s += ", red " + Mathf.RoundToInt(c.r * 255) + ", green " + Mathf.RoundToInt(c.g * 255) + ", blue " + Mathf.RoundToInt(c.b * 255);
            var op = FieldVal(vis, "_opacityField") as TMP_InputField;
            if (op != null && op.gameObject.activeSelf) s += ", opacity " + TextUtil.Clean(op.text) + " percent";
            return s;
        }

        static string SpokenDuration(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            if (t.EndsWith(" ms")) return t.Substring(0, t.Length - 3) + " milliseconds";
            if (t.EndsWith(" s")) return t.Substring(0, t.Length - 2) + " seconds";
            if (t.EndsWith(" m")) return t.Substring(0, t.Length - 2) + " minutes";
            if (t.EndsWith(" h")) return t.Substring(0, t.Length - 2) + " hours";
            return t;
        }

        static string FieldText(Component c)
        {
            if (c == null) return "";
            var f = c as TMP_InputField ?? c.GetComponent<TMP_InputField>();
            return f != null ? TextUtil.Clean(f.text) : "";
        }

        static TMP_InputField InputOf(object o)
        {
            var c = o as Component;
            if (c == null) return null;
            return c as TMP_InputField ?? c.GetComponent<TMP_InputField>();
        }

        public static bool Activate(BaseSettingVisual vis)
        {
            if (vis == null) return false;
            if (ModSettings.IsGlossaryDropdown(vis.UnlocalizedName)) { ModSettings.PlayGlossary(vis.UnlocalizedName); return true; }
            var page = ModSettings.PageForRow(vis.UnlocalizedName);
            if (page != null) { SoundGlossaryPage.Open(page); return true; }
            var name = GetName(vis);
            var fields = new System.Collections.Generic.List<KeyValuePair<TMP_InputField, string>>();
            Action after = null;
            if (vis is ColorSettingVisual)
            {
                fields.Add(Pair(InputOf(FieldVal(vis, "_inputField")), name + " hex code"));
                var op = FieldVal(vis, "_opacityField") as TMP_InputField;
                if (op != null && op.gameObject.activeSelf) fields.Add(Pair(op, name + " opacity percent"));
                after = () => { Call(vis, "OnTextFieldChange"); if (op != null && op.gameObject.activeSelf) Call(vis, "OnOpacityFieldChange"); };
            }
            else if (vis is IPv4SettingVisual || vis is IntSettingVisual || vis is DurationSettingVisual)
            {
                fields.Add(Pair(InputOf(FieldVal(vis, "_inputField")), name));
                if (!(vis is DurationSettingVisual)) after = () => Call(vis, "OnTextFieldChange");
            }
            else if (vis is HitWindowSettingVisual)
            {
                var dyn = FieldVal(vis, "_dynamicContainer") as GameObject;
                if (dyn != null && dyn.activeSelf)
                {
                    fields.Add(Pair(InputOf(FieldVal(vis, "_minField")), name + " minimum"));
                    fields.Add(Pair(InputOf(FieldVal(vis, "_maxField")), name + " maximum"));
                }
                else fields.Add(Pair(InputOf(FieldVal(vis, "_constantField")), name));
            }
            else if (vis is DMXChannelsSettingVisual)
            {
                var arr = FieldVal(vis, "_inputField") as TMP_InputField[];
                if (arr != null)
                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (arr[i] == null) continue;
                        fields.Add(Pair(arr[i], name + " channel " + (i + 1) + " of " + arr.Length));
                    }
                after = () =>
                {
                    var a = FieldVal(vis, "_inputField") as TMP_InputField[];
                    var m = vis.GetType().GetMethod("OnTextFieldChange", BF);
                    if (a != null && m != null) for (int i = 0; i < a.Length; i++) { try { m.Invoke(vis, new object[] { i }); } catch { } }
                };
            }
            else if (vis is FileInfoSettingVisual)
            {
                Call(vis, "Browse");
                return true;
            }
            else return false;

            fields.RemoveAll(kv => kv.Key == null);
            if (fields.Count == 0) return false;
            EditSequence(vis, fields, 0, after);
            return true;
        }

        static void EditSequence(BaseSettingVisual vis, System.Collections.Generic.List<KeyValuePair<TMP_InputField, string>> fields, int i, Action after)
        {
            var kv = fields[i];
            TextEdit.Begin(kv.Key, kv.Value, text =>
            {
                if (after != null) { try { after(); } catch { } }
                if (i + 1 < fields.Count) EditSequence(vis, fields, i + 1, after);
                else MenuAnnouncer.Focused(GetName(vis) + ", " + ValueOnly(vis));
            });
        }

        static KeyValuePair<TMP_InputField, string> Pair(TMP_InputField f, string label) { return new KeyValuePair<TMP_InputField, string>(f, label); }

        static void Call(object o, string method)
        {
            try { var m = o.GetType().GetMethod(method, BF, null, Type.EmptyTypes, null); if (m != null) m.Invoke(o, null); }
            catch (Exception ex) { ModLog.Warn("SettingsAccess." + method, ex); }
        }

        public static bool Remove(BaseSettingVisual vis)
        {
            if (!(vis is FileInfoSettingVisual)) return false;
            Call(vis, "Remove");
            return true;
        }

        public static bool ToggleValue(BaseSettingVisual vis)
        {
            var t = FieldVal(vis, "_toggle") as Toggle;
            if (t == null) return false;
            t.isOn = !t.isOn;
            return true;
        }

        public static bool Adjust(BaseSettingVisual vis, int dir)
        {
            if (vis is SliderSettingVisual)
            {
                var s = FieldVal(vis, "_slider");
                if (s == null) return false;
                var st = s.GetType();
                float v = (float)st.GetProperty("Value").GetValue(s, null);
                float min = (float)st.GetProperty("MinimumValue").GetValue(s, null);
                float max = (float)st.GetProperty("MaximumValue").GetValue(s, null);
                float range = max - min;
                if (range <= 0f) range = 1f;
                float step = range / 20f;
                bool integral = Mathf.Approximately(min, Mathf.Round(min)) && Mathf.Approximately(max, Mathf.Round(max));
                if (integral && range >= 20f) step = Mathf.Max(1f, Mathf.Round(step));
                float nv = Mathf.Clamp(v + dir * step, min, max);
                if (Mathf.Approximately(nv, v)) return true;
                var setM = st.GetMethod("SetValue", new[] { typeof(float) });
                if (setM != null) setM.Invoke(s, new object[] { nv });
                else st.GetProperty("Value").SetValue(s, nv, null);
                return true;
            }
            if (vis is DropdownSettingVisual)
            {
                var d = FieldVal(vis, "_dropdown") as TMP_Dropdown;
                if (d == null || d.options.Count == 0) return false;
                int nv = Mathf.Clamp(d.value + dir, 0, d.options.Count - 1);
                if (nv != d.value) d.value = nv;
                return true;
            }
            if (vis is DurationSettingVisual)
            {
                var setting = vis.GetType().GetProperty("Setting", BF)?.GetValue(vis, null);
                if (setting == null) return false;
                var st = setting.GetType();
                var unit = st.GetProperty("PreferredUnit").GetValue(setting, null);
                double mult = (double)typeof(YARG.Menu.DurationInputField).GetMethod("GetMultiplierForUnit").Invoke(null, new[] { unit });
                var vp = st.GetProperty("Value");
                double cur = (double)vp.GetValue(setting, null);
                double max = (double)st.GetProperty("Max").GetValue(setting, null);
                double nv = Math.Max(0.0, Math.Min(max, cur + dir * mult));
                vp.SetValue(setting, nv, null);
                Call(vis, "RefreshVisual");
                return true;
            }
            if (vis is IntSettingVisual)
            {
                var f = FieldVal(vis, "_inputField") as TMP_InputField;
                if (f == null) return false;
                int cur; int.TryParse(f.text, out cur);
                f.text = (cur + dir).ToString();
                var m = vis.GetType().GetMethod("OnTextFieldChange", BF);
                if (m != null) { try { m.Invoke(vis, null); } catch { } }
                return true;
            }
            return false;
        }

        static int _rowId;
        static int _rowFocus;

        public static bool IsButtonRow(NavigatableBehaviour nav) { return nav is SettingsButton; }

        public static List<Button> RowButtons(NavigatableBehaviour nav)
        {
            var res = new List<Button>();
            if (nav == null) return res;
            foreach (var b in nav.GetComponentsInChildren<Button>(false))
                if (b != null && b.isActiveAndEnabled) res.Add(b);
            return res;
        }

        static int RowFocus(NavigatableBehaviour nav, int count)
        {
            if (nav.GetInstanceID() != _rowId) { _rowId = nav.GetInstanceID(); _rowFocus = 0; }
            _rowFocus = Mathf.Clamp(_rowFocus, 0, Math.Max(0, count - 1));
            return _rowFocus;
        }

        static string ButtonLabel(Button b)
        {
            var cb = b.GetComponentInParent<YARG.Menu.ColoredButton>();
            if (cb != null && cb.Text != null) return TextUtil.Clean(cb.Text.text);
            return Describer.GetLabel(b.gameObject);
        }

        static string RowHeader(NavigatableBehaviour nav)
        {
            var t = nav.transform;
            var parent = t.parent;
            if (parent == null) return null;
            for (int i = t.GetSiblingIndex() - 1; i >= 0; i--)
            {
                var sib = parent.GetChild(i);
                if (!sib.gameObject.activeSelf) continue;
                if (sib.name.StartsWith("SettingsHeader"))
                {
                    var tmp = sib.GetComponentInChildren<TMP_Text>(false);
                    return tmp != null ? TextUtil.Clean(tmp.text) : null;
                }
                if (sib.GetComponent<NavigatableBehaviour>() != null && !(sib.GetComponent<NavigatableBehaviour>() is SettingsButton)) return null;
            }
            return null;
        }

        public static string DescribeButtonRow(NavigatableBehaviour nav)
        {
            var bs = RowButtons(nav);
            if (bs.Count == 0) return Describer.GetLabel(nav.gameObject);
            if (bs.Count == 1) return ButtonLabel(bs[0]) + Keys.Role("button");
            int f = RowFocus(nav, bs.Count);
            var header = RowHeader(nav);
            return (string.IsNullOrEmpty(header) ? "" : header + ", ") + ButtonLabel(bs[f]) + Keys.Role("button") + Keys.Pos(f, bs.Count);
        }

        public static bool AdjustButtonRow(NavigatableBehaviour nav, int dir)
        {
            var bs = RowButtons(nav);
            if (bs.Count <= 1) return false;
            int f = RowFocus(nav, bs.Count);
            int nf = Mathf.Clamp(f + dir, 0, bs.Count - 1);
            _rowFocus = nf;
            MenuAnnouncer.Focused(ButtonLabel(bs[nf]) + Keys.Role("button") + Keys.Pos(nf, bs.Count));
            return true;
        }

        public static bool ActivateButtonRow(NavigatableBehaviour nav)
        {
            var bs = RowButtons(nav);
            if (bs.Count == 0) return false;
            int f = RowFocus(nav, bs.Count);
            try { bs[f].onClick.Invoke(); }
            catch (Exception ex) { ModLog.Warn("SettingsAccess.ActivateButtonRow", ex); }
            return true;
        }

        static string SliderText(BaseSettingVisual vis)
        {
            var s = FieldVal(vis, "_slider");
            if (s == null) return "";
            var v = (float)s.GetType().GetProperty("Value").GetValue(s, null);
            return FormatNum(v);
        }

        static string DropdownText(TMP_Dropdown d)
        {
            return (d != null && d.value >= 0 && d.value < d.options.Count) ? TextUtil.Clean(d.options[d.value].text) : "";
        }

        static string FormatNum(float v)
        {
            if (Mathf.Approximately(v, Mathf.Round(v))) return Mathf.RoundToInt(v).ToString();
            return v.ToString("0.##");
        }

        static object FieldVal(object o, string name)
        {
            if (o == null) return null;
            var t = o.GetType();
            while (t != null)
            {
                var f = t.GetField(name, BF);
                if (f != null) return f.GetValue(o);
                t = t.BaseType;
            }
            return null;
        }
    }
}
