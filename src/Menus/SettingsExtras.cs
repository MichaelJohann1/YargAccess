using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.Navigation;

namespace YargAccess
{
    public static class SettingsExtras
    {
        enum Kind { Button, Folder, Remove }
        class Item { public Kind Kind; public string Label; public YARG.Menu.ColoredButton Colored; public Button Btn; }

        static readonly List<Item> _items = new();
        static int _idx = -1;

        public static bool IsActive
        {
            get
            {
                var smh = UnityEngine.Object.FindObjectOfType<YARG.Menu.Settings.SongManagerHeader>();
                return smh != null && smh.isActiveAndEnabled;
            }
        }

        public static void ResetZone() { _idx = -1; }

        public static bool MoveUp()
        {
            Rebuild();
            if (_items.Count == 0) { _idx = -1; return false; }
            if (_idx >= _items.Count) _idx = -1;

            if (_idx >= 0)
            {
                if (_idx > 0) { _idx--; Announce(); }
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
            if (_idx >= _items.Count) _idx = -1;

            if (_idx >= 0)
            {
                if (_idx < _items.Count - 1) { _idx++; Announce(); }
                else
                {
                    _idx = -1;
                    var g = NavigationGroup.CurrentNavigationGroup;
                    if (g != null && g.Count > 0)
                    {
                        g.SelectAt(0, SelectionOrigin.Programmatically);
                        var nb = g.SelectedBehaviour;
                        if (nb != null) MenuAnnouncer.Announce(nb, true);
                    }
                }
                return true;
            }
            return false;
        }

        public static bool Confirm()
        {
            Rebuild();
            if (_idx < 0 || _idx >= _items.Count) return false;
            var it = _items[_idx];
            try
            {
                if (it.Kind == Kind.Button && it.Colored != null && it.Colored.OnClick != null)
                {
                    if (it.Label == "Scan Songs") ScanAnnouncer.Begin();
                    bool adding = it.Label == "Add New Folder";
                    int before = _items.Count;
                    it.Colored.OnClick.Invoke();
                    if (adding)
                    {
                        _idx = before + 1;
                        MenuAnnouncer.SayShown("Folder added, none chosen yet. Browse, " + Keys.Ok + " to choose the folder.");
                    }
                }
                else if (it.Kind == Kind.Remove && it.Btn != null)
                {
                    string path = _idx >= 2 && _items[_idx - 2].Kind == Kind.Folder ? _items[_idx - 2].Label.Replace("Folder, ", "") : "";
                    it.Btn.onClick.Invoke();
                    _idx = Math.Max(0, _idx - 3);
                    MenuAnnouncer.SayShown(path == "No Folder" || path == ""
                        ? "Removed the empty folder."
                        : "Removed folder " + path + ". Scan Songs to update the library.");
                }
                else if (it.Kind == Kind.Folder) MenuAnnouncer.Say(it.Label);
            }
            catch (Exception ex) { ModLog.Warn("SettingsExtras.Confirm", ex); }
            return true;
        }

        public static bool InHeader => _idx >= 0;

        static void Rebuild()
        {
            _items.Clear();

            var smh = UnityEngine.Object.FindObjectOfType<YARG.Menu.Settings.SongManagerHeader>();
            if (smh != null)
                foreach (var cb in smh.GetComponentsInChildren<YARG.Menu.ColoredButton>(false))
                    if (cb != null && cb.isActiveAndEnabled)
                        _items.Add(new Item { Kind = Kind.Button, Colored = cb, Label = Label(cb) });

            var dirs = new List<MonoBehaviour>();
            foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
                if (mb.GetType().Name == "SettingsDirectory" && mb.isActiveAndEnabled) dirs.Add(mb);
            dirs.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            foreach (var dir in dirs)
            {
                var nameTmp = FindTmpByName(dir, "Name");
                string path = nameTmp != null ? TextUtil.Clean(nameTmp.text) : "folder";
                _items.Add(new Item { Kind = Kind.Folder, Label = "Folder, " + path });

                YARG.Menu.ColoredButton browse = null;
                foreach (var cb in dir.GetComponentsInChildren<YARG.Menu.ColoredButton>(false))
                    if (cb != null && cb.isActiveAndEnabled) { browse = cb; break; }
                if (browse != null) _items.Add(new Item { Kind = Kind.Button, Colored = browse, Label = Label(browse) });

                foreach (var b in dir.GetComponentsInChildren<Button>(false))
                    if (b != null && b.isActiveAndEnabled && b.GetComponentInParent<YARG.Menu.ColoredButton>() == null)
                    { _items.Add(new Item { Kind = Kind.Remove, Btn = b, Label = "Remove folder" }); break; }
            }
        }

        static string Label(YARG.Menu.ColoredButton cb)
        {
            return cb.Text != null ? TextUtil.Clean(cb.Text.text) : Describer.GetLabel(cb.gameObject);
        }

        static TMPro.TMP_Text FindTmpByName(MonoBehaviour root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
                if (t.gameObject.name == name) return t;
            return null;
        }

        static void Announce()
        {
            if (_idx < 0 || _idx >= _items.Count) return;
            var it = _items[_idx];
            string hint = it.Kind == Kind.Remove ? ", " + Keys.Ok + " to remove"
                : it.Label == "Browse" ? ", " + Keys.Ok + " to choose a folder" : "";
            string role = it.Kind == Kind.Folder ? "" : Keys.Role("button");
            MenuAnnouncer.Focused(it.Label + role + hint + Keys.Pos(_idx, _items.Count));
        }
    }
}
