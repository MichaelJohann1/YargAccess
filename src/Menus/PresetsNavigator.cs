using System;
using System.Collections.Generic;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;

namespace YargAccess
{
    public static class PresetsNavigator
    {
        enum Kind { Type, Preset, Action, Text }
        class Item { public Kind Kind; public string Label; public TMP_Dropdown Dropdown; public Component Owner; public Button Button; }

        static readonly List<Item> _items = new List<Item>();
        static int _idx = -1;

        public static bool IsActive
        {
            get
            {
                var t = UnityEngine.Object.FindObjectOfType<PresetTypeDropdown>();
                return t != null && t.isActiveAndEnabled;
            }
        }

        public static void ResetZone() { _idx = -1; }

        internal static bool GroupEmpty
        {
            get { var g = NavigationGroup.CurrentNavigationGroup; return g == null || g.Count == 0 || !g.isActiveAndEnabled; }
        }

        public static bool InZone { get { return _idx >= 0 || GroupEmpty; } }

        public static string EntryText()
        {
            Rebuild();
            if (_items.Count == 0) return null;
            _idx = 0;
            return ItemText(0);
        }

        public static void AnnounceCurrent()
        {
            Rebuild();
            if (_items.Count == 0) return;
            if (_idx < 0 || _idx >= _items.Count) _idx = 0;
            MenuAnnouncer.Focused(ItemText(_idx), true);
        }

        public static bool MoveUp()
        {
            Rebuild();
            if (_items.Count == 0) { _idx = -1; return false; }
            if (_idx >= _items.Count) _idx = _items.Count - 1;
            if (_idx < 0 && GroupEmpty) _idx = 0;
            if (_idx >= 0)
            {
                if (_idx > 0) _idx--;
                Announce();
                return true;
            }
            var g = NavigationGroup.CurrentNavigationGroup;
            if (g != null && g.Count > 0 && g.SelectedIndex.GetValueOrDefault(0) == 0)
            {
                _idx = _items.Count - 1;
                Announce();
                return true;
            }
            return false;
        }

        public static bool MoveDown()
        {
            Rebuild();
            if (_items.Count == 0) { _idx = -1; return false; }
            if (_idx < 0 && GroupEmpty) { _idx = 0; Announce(); return true; }
            if (_idx < 0) return false;
            if (_idx < _items.Count - 1) { _idx++; Announce(); return true; }
            var g = NavigationGroup.CurrentNavigationGroup;
            if (GroupEmpty) { Announce(); return true; }
            _idx = -1;
            g.SelectAt(0, SelectionOrigin.Programmatically);
            var nb = g.SelectedBehaviour;
            if (nb != null) MenuAnnouncer.Announce(nb, true);
            return true;
        }

        public static bool Adjust(int dir)
        {
            if (!InZone) return false;
            Rebuild();
            if (_idx < 0) _idx = 0;
            if (_idx >= _items.Count) return false;
            var it = _items[_idx];
            if (it.Dropdown == null) return true;
            int count = it.Dropdown.options.Count;
            int nv = Mathf.Clamp(it.Dropdown.value + dir, 0, count - 1);
            if (nv == it.Dropdown.value) { Announce(); return true; }
            var kind = it.Kind;
            MenuAnnouncer.BeginBatch();
            try
            {
                it.Dropdown.SetValueWithoutNotify(nv);
                if (kind == Kind.Type) ((PresetTypeDropdown)it.Owner).OnDropdownChange();
                else ((PresetDropdown)it.Owner).OnDropdownChange();
            }
            catch (Exception ex) { ModLog.Warn("PresetsNavigator.Adjust", ex); }
            MenuAnnouncer.DiscardBatch();
            Rebuild();
            _idx = Mathf.Clamp(_idx, 0, _items.Count - 1);
            var line = ValueOf(_items[_idx]);
            if (kind == Kind.Type) { var p = PresetItem(); if (p != null) line += ", preset " + ValueOf(p); }
            if (BuiltIn()) line += ", built-in";
            string tip = BuiltInTip();
            var pi = PresetItem();
            Tooltips.Show(tip, pi != null ? ValueOf(pi) : null);
            MenuAnnouncer.Focused(tip != null ? line + ". " + tip : line);
            return true;
        }

        public static bool Confirm()
        {
            if (!InZone) return false;
            Rebuild();
            if (_idx < 0) _idx = 0;
            if (_idx >= _items.Count) return false;
            var it = _items[_idx];
            switch (it.Kind)
            {
                case Kind.Type:
                case Kind.Preset:
                    MenuAnnouncer.Say(Keys.LeftRight + " to change. " + ValueOf(it));
                    break;
                case Kind.Text:
                    MenuAnnouncer.Say(it.Label);
                    break;
                case Kind.Action:
                    var label = it.Label;
                    MenuAnnouncer.BeginBatch();
                    try { it.Button.onClick.Invoke(); }
                    catch (Exception ex) { ModLog.Warn("PresetsNavigator.Confirm", ex); }
                    MenuAnnouncer.DiscardBatch();
                    AfterAction(label);
                    break;
            }
            return true;
        }

        static void AfterAction(string label)
        {
            Rebuild();
            var p = PresetItem();
            switch (label)
            {
                case "Copy":
                case "Delete":
                    _idx = 0;
                    for (int i = 0; i < _items.Count; i++) if (_items[i].Kind == Kind.Preset) { _idx = i; break; }
                    MenuAnnouncer.SayShown((label == "Copy" ? "Copied. " : "Deleted. ") + "Preset, " + (p != null ? ValueOf(p) : "") +
                        (label == "Copy" ? ". Its settings are below the actions." : ""));
                    break;
                case "Import":
                case "Export":
                case "Open Folder":
                    MenuAnnouncer.SayShown(label == "Open Folder" ? "Opening the preset folder in File Explorer." : "Opening the Windows file dialog.");
                    break;
            }
        }

        static void Announce()
        {
            if (_idx < 0 || _idx >= _items.Count) return;
            MenuAnnouncer.Focused(ItemText(_idx));
        }

        static string ItemText(int i)
        {
            var it = _items[i];
            string s;
            switch (it.Kind)
            {
                case Kind.Type: s = "Preset type, " + Keys.RoleThen("dropdown", ValueOf(it)); break;
                case Kind.Preset: s = "Preset, " + Keys.RoleThen("dropdown", ValueOf(it)) + (BuiltIn() ? ", built-in" : ""); break;
                case Kind.Action: s = it.Label + Keys.Role("button"); break;
                default: s = it.Label; break;
            }
            s += Keys.Pos(i, _items.Count);
            string tip = it.Kind == Kind.Preset ? BuiltInTip() : null;
            Tooltips.Show(tip, ValueOf(it));
            return tip != null ? s + ". " + tip : s;
        }

        static string ValueOf(Item it)
        {
            var d = it.Dropdown;
            if (d == null) return it.Label;
            return (d.value >= 0 && d.value < d.options.Count) ? TextUtil.Clean(d.options[d.value].text) : "none";
        }

        static Item PresetItem()
        {
            foreach (var it in _items) if (it.Kind == Kind.Preset) return it;
            return null;
        }

        static string BuiltInTip()
        {
            if (!BuiltIn()) return null;
            Item type = null, preset = null;
            foreach (var it in _items) { if (it.Kind == Kind.Type) type = it; else if (it.Kind == Kind.Preset) preset = it; }
            return type != null && preset != null ? Tooltips.ForBuiltInPreset(ValueOf(type), ValueOf(preset)) : null;
        }

        static bool BuiltIn()
        {
            try
            {
                var tab = SettingsMenu.Instance != null ? SettingsMenu.Instance.CurrentTab as YARG.Settings.Metadata.PresetsTab : null;
                return tab != null && tab.SelectedPreset != null && tab.SelectedPreset.DefaultPreset;
            }
            catch { return false; }
        }

        static void Rebuild()
        {
            _items.Clear();
            var type = UnityEngine.Object.FindObjectOfType<PresetTypeDropdown>();
            if (type == null) return;
            var typeDd = type.GetComponentInChildren<TMP_Dropdown>(false);
            if (typeDd != null) _items.Add(new Item { Kind = Kind.Type, Dropdown = typeDd, Owner = type });

            var preset = UnityEngine.Object.FindObjectOfType<PresetDropdown>();
            var presetDd = preset != null ? preset.GetComponentInChildren<TMP_Dropdown>(false) : null;
            if (presetDd != null) _items.Add(new Item { Kind = Kind.Preset, Dropdown = presetDd, Owner = preset });

            bool builtIn = BuiltIn();
            var actions = UnityEngine.Object.FindObjectOfType<PresetActions>();
            if (actions != null)
            {
                foreach (var cb in actions.GetComponentsInChildren<YARG.Menu.ColoredButton>(false))
                {
                    var label = cb.Text != null ? TextUtil.Clean(cb.Text.text) : Describer.PrettifyName(cb.name);
                    if (builtIn && (label == "Rename" || label == "Delete" || label == "Export")) continue;
                    var b = cb.GetComponentInChildren<Button>(false);
                    if (b != null) _items.Add(new Item { Kind = Kind.Action, Label = label, Button = b });
                }
            }

            foreach (var b in type.GetComponentsInChildren<Button>(false))
                if (b.GetComponentInParent<TMP_Dropdown>() == null) { _items.Add(new Item { Kind = Kind.Action, Label = "Open Folder", Button = b }); break; }

            var def = GameObject.Find("PresetDefaultText(Clone)");
            if (def != null && def.activeInHierarchy)
            {
                var t = def.GetComponentInChildren<TMP_Text>(false);
                var s = t != null ? TextUtil.Clean(t.text) : "";
                if (!string.IsNullOrEmpty(s)) _items.Add(new Item { Kind = Kind.Text, Label = s });
            }
        }
    }
}
