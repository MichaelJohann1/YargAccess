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
        class Item { public Kind Kind; public string Label, Id, Path; public YARG.Menu.ColoredButton Colored; public Button Btn; }

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
                    if (it.Id == "RefreshSongs") ScanAnnouncer.Begin();
                    bool adding = it.Id == "AddNewFolder";
                    int before = _items.Count;
                    it.Colored.OnClick.Invoke();
                    if (adding)
                    {
                        _idx = before + 1;
                        MenuAnnouncer.SayShown(Loc.F("Folder added, none chosen yet. Browse, {0} to choose the folder.", Keys.Ok));
                    }
                }
                else if (it.Kind == Kind.Remove && it.Btn != null)
                {
                    string path = _idx >= 2 && _items[_idx - 2].Kind == Kind.Folder ? _items[_idx - 2].Path : "";
                    bool empty = path == "" || IsNoFolder(path);
                    var btn = it.Btn;
                    int at = _idx;
                    Action remove = () =>
                    {
                        btn.onClick.Invoke();
                        _idx = Math.Max(0, at - 3);
                        MenuAnnouncer.SayShown(empty
                            ? Loc.T("Removed the empty folder.")
                            : Loc.F("Removed folder {0}. Scan Songs to update the library.", path));
                    };
                    // A folder with a path set asks first; the songs on disk are never deleted either way.
                    if (empty) remove();
                    else EntryActions.Confirm(Loc.T("Remove Folder"), Loc.F("Remove {0} from YARG's song folders? "
                        + "Its songs leave the library after the next scan. The files stay on disk, and Add New Folder brings it back.", path),
                        "Remove", remove);
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
                        _items.Add(new Item { Kind = Kind.Button, Colored = cb, Label = Label(cb), Id = Id(cb) });

            var dirs = new List<MonoBehaviour>();
            foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
                if (mb.GetType().Name == "SettingsDirectory" && mb.isActiveAndEnabled) dirs.Add(mb);
            dirs.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            foreach (var dir in dirs)
            {
                var nameTmp = FindTmpByName(dir, "Name");
                string path = nameTmp != null ? TextUtil.Clean(nameTmp.text) : Loc.T("folder");
                _items.Add(new Item { Kind = Kind.Folder, Label = Loc.F("Folder, {0}", path), Path = path });

                YARG.Menu.ColoredButton browse = null;
                foreach (var cb in dir.GetComponentsInChildren<YARG.Menu.ColoredButton>(false))
                    if (cb != null && cb.isActiveAndEnabled) { browse = cb; break; }
                if (browse != null) _items.Add(new Item { Kind = Kind.Button, Colored = browse, Label = Label(browse), Id = Id(browse) });

                foreach (var b in dir.GetComponentsInChildren<Button>(false))
                    if (b != null && b.isActiveAndEnabled && b.GetComponentInParent<YARG.Menu.ColoredButton>() == null)
                    { _items.Add(new Item { Kind = Kind.Remove, Btn = b, Label = Loc.T("Remove folder") }); break; }
            }
        }

        static string Label(YARG.Menu.ColoredButton cb)
        {
            return cb.Text != null ? TextUtil.Clean(cb.Text.text) : Describer.GetLabel(cb.gameObject);
        }

        // What a button does, whatever language YARG shows: the method its click calls (RefreshSongs,
        // AddNewFolder, Browse), else the English label (YARG's text before translation).
        static string Id(YARG.Menu.ColoredButton cb)
        {
            try
            {
                var e = cb.OnClick;
                for (int i = 0; e != null && i < e.GetPersistentEventCount(); i++)
                {
                    var m = e.GetPersistentMethodName(i);
                    if (!string.IsNullOrEmpty(m)) return m;
                }
            }
            catch { }
            var l = Label(cb);
            return l == "Scan Songs" ? "RefreshSongs" : l == "Add New Folder" ? "AddNewFolder" : l;
        }

        // YARG's "No Folder" (Menu.Settings.NoFolder) in English or YARG's language.
        static bool IsNoFolder(string path)
        {
            if (path == "No Folder") return true;
            try { return path == TextUtil.Clean(YARG.Localization.Localize.Key("Menu.Settings.NoFolder")); }
            catch { return false; }
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
            string hint = it.Kind == Kind.Remove ? Loc.F(", {0} to remove", Keys.Ok)
                : it.Id == "Browse" ? Loc.F(", {0} to choose a folder", Keys.Ok) : "";
            string role = it.Kind == Kind.Folder ? "" : Keys.Role("button");
            MenuAnnouncer.Focused(it.Label + role + hint + Keys.Pos(_idx, _items.Count));
        }
    }
}
