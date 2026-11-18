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

        class Item { public Component comp; public string kind; public string label; public ReviewBinds.Bind bind; public int review; public bool addNew; }

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
            var line = Loc.F("{0} tab", list[nv].DisplayName) + Keys.Pos(nv, list.Count);
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
            if (_items.Count == 0) { MenuAnnouncer.SayShown(Loc.T("Nothing here")); return; }
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
                    if (it.addNew) { _pendingAddNew = it.comp; _pendingSince = Time.unscaledTime; OpenAddNew(); }
                    else InvokeColored(it.comp);
                    break;
                case "gamemode": SelectGameMode(it.comp as NavigatableBehaviour); break;
                case "bind":
                    if (_pendingDelete == it.comp)
                    {
                        _pendingDelete = null;
                        try { it.comp.GetType().GetMethod("DeleteBinding").Invoke(it.comp, null); MenuAnnouncer.SayShown(Loc.F("Removed {0}", it.label)); }
                        catch (Exception ex) { ModLog.Warn("ProfileInfoNavigator delete", ex); }
                    }
                    else { _pendingDelete = it.comp; MenuAnnouncer.SayShown(Loc.F("Remove {0}? Press {1} again to confirm.", it.label, Keys.Ok)); }
                    break;
                case "rvadd":
                    ReviewBinds.BeginCapture(CurrentProfile(), (ReviewBinds.Kind) it.review);
                    break;
                case "rvbind":
                    if (_pendingReview == it.bind)
                    {
                        _pendingReview = null;
                        ReviewBinds.Remove(it.bind);
                        MenuAnnouncer.SayShown(Loc.F("Removed {0}", it.label));
                    }
                    else { _pendingReview = it.bind; MenuAnnouncer.SayShown(Loc.F("Remove {0}? Press {1} again to confirm.", it.label, Keys.Ok)); }
                    break;
                case "name":     MenuAnnouncer.SayShown(Loc.F("{0}. To rename the profile, go back to the profile list and use Rename on the Tab toolbar.", it.label)); break;
                default:         MenuAnnouncer.Say(it.label); break;
            }
        }

        public static bool Back()
        {
            var cd = ActiveControlDialog();
            if (cd != null) { _awaitingBind = false; try { cd.CancelAndClose(); } catch { } MenuAnnouncer.SayShown(Loc.T("Canceled")); return true; }
            if (_pendingAddNew != null) { _pendingAddNew = null; MenuAnnouncer.SayShown(Loc.T("Canceled")); return true; }
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
                case "rvbind":   return it.label + Loc.F(", {0} to remove", Keys.Ok) + suffix;
                case "rvadd":    return it.label + Keys.Role("button") + Loc.F(", {0} to bind a control", Keys.Ok) + suffix;
                case "bind":     return it.label + Loc.F(", {0} to remove", Keys.Ok) + suffix;
                case "button":   return it.label + Keys.Role("button") + (it.addNew ? Loc.F(", {0} to bind a control", Keys.Ok) : "") + suffix;
                case "gamemode": return it.label + (IsShownGameMode(it.comp) ? Loc.T(", selected") : "") + suffix;
                default:         return it.label + suffix;
            }
        }

        public static void AnnounceCurrent()
        {
            var menu = UnityEngine.Object.FindObjectOfType<ProfileInfoMenu>();
            string who = "";
            try { if (menu != null && menu.CurrentProfile != null) who = Loc.F("Editing {0}.", menu.CurrentProfile.Name) + " "; } catch { }
            var ht = Tabs();
            string tab = "";
            if (ht != null && ht.Tabs != null)
            {
                bool onBinds = false;
                var bindsRoot = menu != null ? GetTabRoot(menu, "_editBindsTab") : null;
                if (bindsRoot != null) onBinds = bindsRoot.activeInHierarchy;
                tab = (onBinds ? Loc.T("Binds tab.") : Loc.T("Overview tab.")) + " ";
            }
            Rebuild();
            _idx = Mathf.Clamp(_idx, 0, Mathf.Max(0, _items.Count - 1));
            MenuAnnouncer.Focused(who + tab + (_items.Count > 0 ? ItemText(_idx) : Loc.T("Nothing here")), true);
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

            var qb = FindColored(menu.transform, "Quick Bind", "ShowQuickBind");
            if (qb != null) _items.Add(new Item { comp = qb, kind = "button", label = ColoredText(qb, "Quick Bind") });
        }

        static void RebuildOverview(ProfileInfoMenu menu)
        {
            var root = GetTabRoot(menu, "_overviewTab");
            if (root == null) return;

            var nameTmp = FindTmpByName(root.transform, "Profile Name");
            if (nameTmp != null) _items.Add(new Item { comp = nameTmp, kind = "name", label = Loc.F("Profile, {0}", TextUtil.Clean(nameTmp.text)) });

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
                    ? Loc.T("Menu binds, the buttons that move through menus")
                    : Loc.F("Game mode binds, {0}, the buttons used while playing a song", nm);
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
                    string summary = bindTexts.Count > 0 ? string.Join(", ", bindTexts) : Loc.T("unbound");
                    _items.Add(new Item { comp = header, kind = "header", label = nm + ", " + summary });
                }
                for (int i = 0; i < bindViews.Count; i++)
                    _items.Add(new Item { comp = bindViews[i], kind = "bind", label = bindTexts[i] });

                var add = FindColored(grp.transform, "Add New", "AddNewBind");
                if (add != null) _items.Add(new Item { comp = add, kind = "button", label = ColoredText(add, "Add New"), addNew = true });
            }

            if (!tab.SelectingMenuBinds) return;
            var profile = CurrentProfile();
            if (profile == null) return;
            for (int k = 0; k < 3; k++)
            {
                var kind = (ReviewBinds.Kind) k;
                var own = ReviewBinds.For(profile, kind);
                string label = Loc.F("{0}, default {1}", ReviewBinds.Names[k], ReviewBinds.Defaults(profile, kind));
                foreach (var b in own) label += "; " + b.Name;
                _items.Add(new Item { kind = "rvheader", label = label, review = k });
                foreach (var b in own) _items.Add(new Item { kind = "rvbind", label = b.Name, bind = b, review = k });
                _items.Add(new Item { kind = "rvadd", label = Loc.T("Add New"), review = k });
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
                string what = control != null ? control.displayName : Loc.T("a control");
                string device = control != null && control.device != null ? ", " + control.device.displayName : "";
                MenuAnnouncer.SayShown(Loc.F("{0} set to {1}", name, what + device));
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
            MenuAnnouncer.SayShown(Loc.F("Several controls responded. Choose the one to bind, {0} to move, {1} to bind, {2} to cancel.", Keys.UpDown, Keys.Ok, Keys.Back)
                + " " + FirstTmp(entries[0].transform) + Keys.Pos(0, entries.Count));
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
            MenuAnnouncer.SayShown(Loc.F("Waiting for input. Press the control you want to bind, or {0} to cancel.", Keys.Back) + ListeningTo());
        }

        static string ListeningTo()
        {
            var names = new List<string>();
            foreach (var d in ReviewBinds.Devices(CurrentProfile())) if (!names.Contains(d.displayName)) names.Add(d.displayName);
            if (names.Count == 0) return " " + Loc.T("This profile has no devices to bind with: add one from the profile's Devices first.");
            return " " + Loc.F("Only {0} can be bound here; to use another device, add it from the profile's Devices first.", string.Join(", ", names.ToArray()));
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
            if (entries.Count == 0) { MenuAnnouncer.SayShown(Loc.F("Waiting for input. Press the control you want to bind, or {0} to cancel.", Keys.Back)); return; }
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
                MenuAnnouncer.SayShown(Loc.F("Selected. {0} to read the bindings.", Keys.UpDown));
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

        // A YARG button by the method its click calls (any language), else by its English text.
        static YARG.Menu.ColoredButton FindColored(Transform root, string label, string method)
        {
            foreach (var c in root.GetComponentsInChildren<YARG.Menu.ColoredButton>(false))
            {
                try
                {
                    var e = c.OnClick;
                    for (int i = 0; e != null && i < e.GetPersistentEventCount(); i++)
                        if (e.GetPersistentMethodName(i) == method) return c;
                }
                catch { }
                var t = c.Text;
                if (t != null && TextUtil.Clean(t.text) == label) return c;
            }
            return null;
        }

        // The text YARG shows on the button (in its language), else the English label.
        static string ColoredText(YARG.Menu.ColoredButton c, string english)
        {
            var s = c != null && c.Text != null ? TextUtil.Clean(c.Text.text) : "";
            return string.IsNullOrEmpty(s) ? english : s;
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
