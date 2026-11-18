using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TMPro;
using MelonLoader;
using YARG.Menu;
using YARG.Menu.Navigation;
using YARG.Menu.ProfileInfo;
using YARG.Input;
using UnityEngine.InputSystem;

namespace YargAccess
{
    public static class ProfileInfoNavigator
    {
        const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        class Item { public Component comp; public string kind; public string label; public ReviewBinds.Bind bind; public int review; }

        static readonly List<Item> _items = new List<Item>();
        static int _idx;
        static Component _pendingDelete;
        static ReviewBinds.Bind _pendingReview;
        static int _cdIdx;
        static bool _awaitingBind;
        static bool _subscribed;
        static bool _choiceSpoken;
        static Component _pendingAddNew;
        static float _pendingSince;

        public static bool IsActive
        {
            get { var m = UnityEngine.Object.FindObjectOfType<ProfileInfoMenu>(); return m != null && m.isActiveAndEnabled; }
        }

        public static bool Listening
        {
            get
            {
                if (_pendingAddNew != null) return true;
                var cd = ActiveControlDialog();
                return cd != null && ControlEntries().Count == 0;
            }
        }

        public static bool ChoosingControl => ActiveControlDialog() != null && ControlEntries().Count > 0;

        public static void Reset() { _items.Clear(); _idx = 0; _pendingDelete = null; _awaitingBind = false; _pendingAddNew = null; }

        public static void Tab(int dir)
        {
            if (ActiveControlDialog() != null) return;
            var ht = Tabs();
            if (ht == null) return;
            var list = ht.Tabs;
            if (list == null || list.Count == 0) return;
            int cur = list.FindIndex(t => t.Id == ht.SelectedTabId);
            if (cur < 0) cur = 0;
            int nv = ((cur + dir) % list.Count + list.Count) % list.Count;
            MenuAnnouncer.BeginBatch();
            ht.SelectTabById(list[nv].Id);
            MenuAnnouncer.DiscardBatch();
            _idx = 0; _pendingDelete = null;
            var line = list[nv].DisplayName + " tab" + Keys.Pos(nv, list.Count);
            Rebuild();
            if (_items.Count > 0) line += ". " + ItemText(0);
            MenuAnnouncer.Focused(line);
        }

        public static void Move(int dir)
        {
            var cd = ActiveControlDialog();
            if (cd != null) { MoveControlDialog(dir); return; }
            _awaitingBind = false;
            _pendingAddNew = null;
            Rebuild();
            _pendingDelete = null; _pendingReview = null;
            if (_items.Count == 0) { MenuAnnouncer.SayShown("Nothing here"); return; }
            _idx = Mathf.Clamp(_idx + dir, 0, _items.Count - 1);
            Announce();
        }

        public static void Confirm()
        {
            var cd = ActiveControlDialog();
            if (cd != null) { ConfirmControlDialog(); return; }
            Rebuild();
            if (_idx < 0 || _idx >= _items.Count) return;
            var it = _items[_idx];
            switch (it.kind)
            {
                case "button":
                    if (it.label == "Add New") { _pendingAddNew = it.comp; _pendingSince = Time.unscaledTime; OpenAddNew(); }
                    else InvokeColored(it.comp);
                    break;
                case "gamemode": SelectGameMode(it.comp as NavigatableBehaviour); break;
                case "bind":
                    if (_pendingDelete == it.comp)
                    {
                        _pendingDelete = null;
                        try { it.comp.GetType().GetMethod("DeleteBinding").Invoke(it.comp, null); MenuAnnouncer.SayShown("Removed " + it.label); }
                        catch (Exception ex) { ModLog.Warn("ProfileInfoNavigator delete", ex); }
                    }
                    else { _pendingDelete = it.comp; MenuAnnouncer.SayShown("Remove " + it.label + "? Press " + Keys.Ok + " again to confirm."); }
                    break;
                case "rvadd":
                    ReviewBinds.BeginCapture(CurrentProfile(), (ReviewBinds.Kind) it.review);
                    break;
                case "rvbind":
                    if (_pendingReview == it.bind)
                    {
                        _pendingReview = null;
                        ReviewBinds.Remove(it.bind);
                        MenuAnnouncer.SayShown("Removed " + it.label);
                    }
                    else { _pendingReview = it.bind; MenuAnnouncer.SayShown("Remove " + it.label + "? Press " + Keys.Ok + " again to confirm."); }
                    break;
                case "name":     MenuAnnouncer.SayShown(it.label + ". To rename the profile, go back to the profile list and use Rename on the Tab toolbar."); break;
                default:         MenuAnnouncer.Say(it.label); break;
            }
        }

        public static bool Back()
        {
            var cd = ActiveControlDialog();
            if (cd != null) { _awaitingBind = false; try { cd.CancelAndClose(); } catch { } MenuAnnouncer.SayShown("Canceled"); return true; }
            if (_pendingAddNew != null) { _pendingAddNew = null; MenuAnnouncer.SayShown("Canceled"); return true; }
            try
            {
                var mm = UnityEngine.Object.FindObjectOfType<MenuManager>();
                if (mm != null) { mm.PopMenu(); Reset(); return true; }
            }
            catch (Exception ex) { ModLog.Warn("ProfileInfoNavigator.Back", ex); }
            return false;
        }

        static void Announce() { MenuAnnouncer.Focused(ItemText(_idx)); }

        static string ItemText(int i)
        {
            var it = _items[i];
            string suffix = Keys.Pos(i, _items.Count);
            switch (it.kind)
            {
                case "header":
                case "rvheader": return it.label + Keys.Role("heading") + suffix;
                case "rvbind":   return it.label + ", " + Keys.Ok + " to remove" + suffix;
                case "rvadd":    return it.label + Keys.Role("button") + ", " + Keys.Ok + " to bind a control" + suffix;
                case "bind":     return it.label + ", " + Keys.Ok + " to remove" + suffix;
                case "button":   return it.label + Keys.Role("button") + (it.label == "Add New" ? ", " + Keys.Ok + " to bind a control" : "") + suffix;
                case "gamemode": return it.label + (IsShownGameMode(it.comp) ? ", selected" : "") + suffix;
                default:         return it.label + suffix;
            }
        }

        public static void AnnounceCurrent()
        {
            var menu = UnityEngine.Object.FindObjectOfType<ProfileInfoMenu>();
            string who = "";
            try { if (menu != null && menu.CurrentProfile != null) who = "Editing " + menu.CurrentProfile.Name + ". "; } catch { }
            var ht = Tabs();
            string tab = "";
            if (ht != null && ht.Tabs != null)
            {
                bool onBinds = false;
                var bindsRoot = menu != null ? GetTabRoot(menu, "_editBindsTab") : null;
                if (bindsRoot != null) onBinds = bindsRoot.activeInHierarchy;
                tab = (onBinds ? "Binds" : "Overview") + " tab. ";
            }
            Rebuild();
            _idx = Mathf.Clamp(_idx, 0, Mathf.Max(0, _items.Count - 1));
            MenuAnnouncer.Focused(who + tab + (_items.Count > 0 ? ItemText(_idx) : "Nothing here"), true);
        }

        static void Rebuild()
        {
            _items.Clear();
            var menu = UnityEngine.Object.FindObjectOfType<ProfileInfoMenu>();
            if (menu == null) return;

            var bindsRoot = GetTabRoot(menu, "_editBindsTab");
            bool onBinds = bindsRoot != null && bindsRoot.activeInHierarchy;

            if (onBinds) RebuildBinds();
            else RebuildOverview(menu);

            var qb = FindColored(menu.transform, "Quick Bind");
            if (qb != null) _items.Add(new Item { comp = qb, kind = "button", label = "Quick Bind" });
        }

        static void RebuildOverview(ProfileInfoMenu menu)
        {
            var root = GetTabRoot(menu, "_overviewTab");
            if (root == null) return;

            var nameTmp = FindTmpByName(root.transform, "Profile Name");
            if (nameTmp != null) _items.Add(new Item { comp = nameTmp, kind = "name", label = "Profile, " + TextUtil.Clean(nameTmp.text) });

            var extras = FindTmpByName(root.transform, "Profile Extras");
            if (extras != null && !string.IsNullOrWhiteSpace(extras.text)) _items.Add(new Item { comp = extras, kind = "text", label = TextUtil.Clean(extras.text) });

            foreach (var nameLbl in FindAllTmpByName(root.transform, "Stat Name"))
            {
                var val = nameLbl.transform.parent != null ? FindTmpByName(nameLbl.transform.parent, "Stat Text") : null;
                string s = TextUtil.Clean(nameLbl.text) + (val != null ? (": " + TextUtil.Clean(val.text)) : "");
                _items.Add(new Item { comp = nameLbl, kind = "text", label = s });
            }
        }

        static void RebuildBinds()
        {
            var tab = UnityEngine.Object.FindObjectOfType<EditBindsTab>();
            if (tab == null) return;

            foreach (var mb in tab.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb.GetType().Name != "GameModeView") continue;
                string nm = TmpFieldText(mb, "_gameModeName");
                if (string.IsNullOrEmpty(nm)) nm = FirstTmp(mb.transform);
                bool menuBinds = false;
                try
                {
                    var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var im = mb.GetType().GetField("_isMenuBindings", bf);
                    menuBinds = im != null && (bool) im.GetValue(mb);
                    var gm = mb.GetType().GetField("_gameMode", bf);
                    if (gm != null && !menuBinds) nm += Playability.GameModeSuffix((YARG.Core.GameMode) gm.GetValue(mb));
                }
                catch { }
                var label = menuBinds
                    ? "Menu binds, the buttons that move through menus"
                    : "Game mode binds, " + nm + ", the buttons used while playing a song";
                _items.Add(new Item { comp = mb, kind = "gamemode", label = label });
            }

            foreach (var grp in BindGroups(tab))
            {
                var bindViews = new List<MonoBehaviour>();
                var bindTexts = new List<string>();
                foreach (var mb in grp.GetComponentsInChildren<MonoBehaviour>(false))
                {
                    if (!mb.GetType().Name.Contains("BindView")) continue;
                    string bt = TmpFieldText(mb, "_bindText");
                    if (string.IsNullOrEmpty(bt)) bt = FirstTmp(mb.transform);
                    if (string.IsNullOrWhiteSpace(bt)) continue;
                    bindViews.Add(mb);
                    bindTexts.Add(TextUtil.Clean(bt));
                }

                var header = grp.GetComponentInChildren<BindHeader>(false);
                if (header != null)
                {
                    string nm = TmpFieldText(header, "_bindingNameText");
                    if (string.IsNullOrEmpty(nm)) nm = FirstTmp(header.transform);
                    string summary = bindTexts.Count > 0 ? string.Join(", ", bindTexts) : "unbound";
                    _items.Add(new Item { comp = header, kind = "header", label = nm + ", " + summary });
                }
                for (int i = 0; i < bindViews.Count; i++)
                    _items.Add(new Item { comp = bindViews[i], kind = "bind", label = bindTexts[i] });

                var add = FindColored(grp.transform, "Add New");
                if (add != null) _items.Add(new Item { comp = add, kind = "button", label = "Add New" });
            }

            if (!tab.SelectingMenuBinds) return;
            var profile = CurrentProfile();
            if (profile == null) return;
            for (int k = 0; k < 3; k++)
            {
                var kind = (ReviewBinds.Kind) k;
                var own = ReviewBinds.For(profile, kind);
                string label = ReviewBinds.Names[k] + ", default " + ReviewBinds.Defaults(profile, kind);
                foreach (var b in own) label += "; " + b.Name;
                _items.Add(new Item { kind = "rvheader", label = label, review = k });
                foreach (var b in own) _items.Add(new Item { kind = "rvbind", label = b.Name, bind = b, review = k });
                _items.Add(new Item { kind = "rvadd", label = "Add New", review = k });
            }
        }

        static YARG.Core.Game.YargProfile CurrentProfile()
        {
            try { var m = UnityEngine.Object.FindObjectOfType<ProfileInfoMenu>(); return m != null ? m.CurrentProfile : null; }
            catch { return null; }
        }

        static InputControlDialogMenu ActiveControlDialog()
        {
            var cd = UnityEngine.Object.FindObjectOfType<InputControlDialogMenu>();
            return (cd != null && cd.isActiveAndEnabled) ? cd : null;
        }

        static void WatchBindings()
        {
            if (_subscribed) return;
            ControlBinding.BindingAdded += OnBindingAdded;
            _subscribed = true;
        }

        static void OnBindingAdded(ControlBinding binding, InputControl control)
        {
            if (!_awaitingBind || QuickBindAnnouncer.IsWaiting) return;
            _awaitingBind = false;
            try
            {
                string name = "";
                try { name = TextUtil.Clean(YARG.Localization.Localize.Key("Bindings", binding.Name)); } catch { }
                if (string.IsNullOrEmpty(name)) name = binding.Name;
                string what = control != null ? control.displayName : "a control";
                string device = control != null && control.device != null ? ", " + control.device.displayName : "";
                MenuAnnouncer.SayShown(name + " set to " + what + device);
            }
            catch (Exception ex) { ModLog.Warn("ProfileInfoNavigator.OnBindingAdded", ex); }
        }

        public static void Tick()
        {
            if (_pendingAddNew != null) OpenAddNew();
            if (ActiveControlDialog() == null) { _choiceSpoken = false; return; }
            if (_choiceSpoken) return;
            var entries = ControlEntries();
            if (entries.Count == 0) return;
            _choiceSpoken = true;
            _cdIdx = 0;
            MenuAnnouncer.SayShown("Several controls responded. Choose the one to bind, " + Keys.UpDown + " to move, "
                + Keys.Ok + " to bind, " + Keys.Back + " to cancel. " + FirstTmp(entries[0].transform) + Keys.Pos(0, entries.Count));
        }

        static void OpenAddNew()
        {
            if (_pendingAddNew == null) return;
            bool waited = Time.unscaledTime - _pendingSince > 3f;
            if (!waited && ReviewBinds.AnyHeld(CurrentProfile())) return;
            var comp = _pendingAddNew;
            _pendingAddNew = null;
            InvokeColored(comp);
            _cdIdx = 0;
            WatchBindings();
            _awaitingBind = true;
            MenuAnnouncer.SayShown("Waiting for input. Press the control you want to bind, or " + Keys.Back + " to cancel." + ListeningTo());
        }

        static string ListeningTo()
        {
            var names = new List<string>();
            foreach (var d in ReviewBinds.Devices(CurrentProfile())) if (!names.Contains(d.displayName)) names.Add(d.displayName);
            if (names.Count == 0) return " This profile has no devices to bind with: add one from the profile's Devices first.";
            return " Only " + string.Join(", ", names.ToArray()) + " can be bound here; to use another device, add it from the profile's Devices first.";
        }

        static List<ControlEntry> ControlEntries()
        {
            var res = new List<ControlEntry>();
            foreach (var e in UnityEngine.Object.FindObjectsOfType<ControlEntry>())
                if (e != null && e.isActiveAndEnabled) res.Add(e);
            res.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            return res;
        }

        static void MoveControlDialog(int dir)
        {
            var entries = ControlEntries();
            if (entries.Count == 0) { MenuAnnouncer.SayShown("Waiting for input. Press the control you want to bind, or " + Keys.Back + " to cancel."); return; }
            _cdIdx = Mathf.Clamp(_cdIdx + dir, 0, entries.Count - 1);
            MenuAnnouncer.Focused(FirstTmp(entries[_cdIdx].transform) + Keys.Pos(_cdIdx, entries.Count));
        }

        static void ConfirmControlDialog()
        {
            var entries = ControlEntries();
            if (entries.Count == 0) return;
            if (_cdIdx < 0 || _cdIdx >= entries.Count) _cdIdx = 0;
            try { entries[_cdIdx].SelectControl(); }
            catch (Exception ex) { ModLog.Warn("ProfileInfoNavigator.ConfirmControlDialog", ex); }
        }

        static HeaderTabs Tabs()
        {
            var menu = UnityEngine.Object.FindObjectOfType<ProfileInfoMenu>();
            return menu != null ? menu.GetComponentInChildren<HeaderTabs>(true) : null;
        }

        static GameObject GetTabRoot(ProfileInfoMenu menu, string field)
        {
            try { var f = typeof(ProfileInfoMenu).GetField(field, BF); return f != null ? f.GetValue(menu) as GameObject : null; }
            catch { return null; }
        }

        static List<Component> BindGroups(Component tab)
        {
            var res = new List<Component>();
            foreach (var mb in tab.GetComponentsInChildren<MonoBehaviour>(false))
            {
                var n = mb.GetType().Name;
                if (n == "ButtonBindGroup" || n == "AxisBindGroup" || n == "IntegerBindGroup") res.Add(mb);
            }
            return res;
        }

        static bool IsShownGameMode(Component view)
        {
            try
            {
                var tab = UnityEngine.Object.FindObjectOfType<EditBindsTab>();
                if (tab == null || view == null) return false;
                var bf = BindingFlags.Instance | BindingFlags.NonPublic;
                var im = view.GetType().GetField("_isMenuBindings", bf);
                bool menu = im != null && (bool) im.GetValue(view);
                if (menu || tab.SelectingMenuBinds) return menu && tab.SelectingMenuBinds;
                var gm = view.GetType().GetField("_gameMode", bf);
                return gm != null && (YARG.Core.GameMode) gm.GetValue(view) == tab.SelectedGameMode;
            }
            catch { return false; }
        }

        static void SelectGameMode(NavigatableBehaviour nb)
        {
            if (nb == null) return;
            try
            {
                var grp = nb.NavigationGroup;
                if (grp != null)
                {
                    var lf = typeof(NavigationGroup).GetField("_navigatables", BindingFlags.NonPublic | BindingFlags.Instance);
                    var list = lf != null ? lf.GetValue(grp) as System.Collections.IList : null;
                    int i = list != null ? list.IndexOf(nb) : -1;
                    if (i >= 0) grp.SelectAt(i, SelectionOrigin.Programmatically);
                }
                _idx = 0;
                MenuAnnouncer.SayShown("Selected. " + Keys.UpDown + " to read the bindings.");
            }
            catch (Exception ex) { ModLog.Warn("ProfileInfoNavigator.SelectGameMode", ex); }
        }

        static void InvokeColored(Component cb)
        {
            var c = cb as YARG.Menu.ColoredButton;
            if (c == null) return;
            try { if (c.OnClick != null) c.OnClick.Invoke(); }
            catch (Exception ex) { ModLog.Warn("ProfileInfoNavigator.InvokeColored", ex); }
        }

        static YARG.Menu.ColoredButton FindColored(Transform root, string label)
        {
            foreach (var c in root.GetComponentsInChildren<YARG.Menu.ColoredButton>(false))
            {
                var t = c.Text;
                if (t != null && TextUtil.Clean(t.text) == label) return c;
            }
            return null;
        }

        static TMP_Text FindTmpByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(false))
                if (t.gameObject.name == name) return t;
            return null;
        }

        static List<TMP_Text> FindAllTmpByName(Transform root, string name)
        {
            var res = new List<TMP_Text>();
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(false))
                if (t.gameObject.name == name) res.Add(t);
            return res;
        }

        static string FirstTmp(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(false))
                if (!string.IsNullOrWhiteSpace(t.text)) return TextUtil.Clean(t.text);
            return "";
        }

        static string TmpFieldText(Component c, string field)
        {
            try
            {
                for (var t = c.GetType(); t != null && t != typeof(object); t = t.BaseType)
                {
                    var f = t.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (f != null) { var v = f.GetValue(c) as TMP_Text; return v != null ? TextUtil.Clean(v.text) : null; }
                }
            }
            catch { }
            return null;
        }
    }
}
