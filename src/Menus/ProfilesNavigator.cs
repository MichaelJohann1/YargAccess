using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using YARG.Menu.ProfileList;
using YARG.Core.Game;
using YARG.Localization;
using YARG.Player;

namespace YargAccess
{
    public static class ProfilesNavigator
    {
        const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        static int _profileIdx;
        static int _action = -1;
        internal static bool _inDevices;
        static int _deviceIdx;
        internal static bool _inSettings;
        static int _settingIdx;
        static YargProfile _pendingDelete;
        static YargProfile _pendingConfigure;

        public static bool IsActive { get { return UnityEngine.Object.FindObjectOfType<ProfileListMenu>() != null; } }

        public static void Move(int dir)
        {
            if (_inDevices) { MoveDevice(dir); return; }
            if (_inSettings) { MoveSetting(dir); return; }
            _action = -1;
            _pendingDelete = null;
            var ps = ProfileViews();
            if (ps.Count == 0) { MenuAnnouncer.SayShown("No profiles. Press Tab for Add Profile or Add Bot."); return; }
            _profileIdx = Mathf.Clamp(_profileIdx + dir, 0, ps.Count - 1);
            AnnounceProfile(ps[_profileIdx], false);
        }

        public static void Tab(int dir)
        {
            _inDevices = false;
            _inSettings = false;
            _pendingDelete = null;
            var actions = Actions();
            int n = actions.Count;
            if (_action < 0 || _action >= n) _action = (dir > 0 ? 0 : n - 1);
            else _action = (_action + dir + n) % n;
            AnnounceAction();
        }

        public static void Adjust(int dir)
        {
            if (_inSettings) { AdjustSetting(dir); return; }
            if (CurrentAction() == "Instrument") CycleInstrument(dir);
        }

        public static void Confirm()
        {
            if (_inDevices) return;
            if (_inSettings) { ConfirmSetting(); return; }
            if (_action < 0) { var pv = Selected(); if (pv != null) EditProfile(pv); return; }
            var name = CurrentAction();
            var view = Selected();
            switch (name)
            {
                case "Add Profile":   InvokeMenu("AddProfile"); AnnounceAdded(); break;
                case "Add Bot":       InvokeMenu("AddBotProfile"); AnnounceAdded(); break;
                case "Connect":       if (view != null) Connect(view); break;
                case "Disconnect":    if (view != null) Disconnect(view); break;
                case "Edit Binds":    if (view != null) EditProfile(view); break;
                case "Rename":        if (view != null) Rename(view); break;
                case "Profile Settings": if (view != null) EnterSettings(view); break;
                case "Add Device":    if (view != null) SidebarCall(view, "AddDevice"); break;
                case "Remove Device": if (view != null) SidebarCall(view, "RemoveDevice"); break;
                case "Move Player Up":   if (view != null) MovePlayer(view, -1); break;
                case "Move Player Down": if (view != null) MovePlayer(view, +1); break;
                case "Delete Profile":
                {
                    if (view == null || view.Profile == null) break;
                    if (_pendingDelete == view.Profile)
                    {
                        var nm = view.Profile.Name;
                        bool ok = DeleteProfile(view.Profile);
                        _pendingDelete = null;
                        _action = -1;
                        MenuAnnouncer.SayShown(ok ? ("Deleted " + nm) : ("Could not delete " + nm));
                    }
                    else
                    {
                        _pendingDelete = view.Profile;
                        MenuAnnouncer.SayShown("Delete " + view.Profile.Name + "? Press " + Keys.Ok + " again to confirm.");
                    }
                    break;
                }
                case "Instrument":    AnnounceAction(); break;
                case "Devices":       EnterDevices(); break;
            }
        }

        public static bool Back()
        {
            if (_inDevices) { _inDevices = false; MenuAnnouncer.Focused("Devices closed. " + ActionText()); return true; }
            if (_inSettings) { _inSettings = false; _pendingConfigure = null; MenuAnnouncer.Focused("Profile settings closed. " + ActionText()); return true; }
            return false;
        }

        public static void Reset()
        {
            _profileIdx = 0; _action = -1; _inDevices = false; _deviceIdx = 0;
            _inSettings = false; _settingIdx = 0; _pendingDelete = null; _pendingConfigure = null;
        }

        public static void AnnounceCurrent()
        {
            var ps = ProfileViews();
            if (ps.Count == 0)
            {
                MenuAnnouncer.SayItem("No profiles. Press Tab for Add Profile or Add Bot.");
                return;
            }
            _profileIdx = Mathf.Clamp(_profileIdx, 0, ps.Count - 1);
            AnnounceProfile(ps[_profileIdx], true);
        }

        static List<ProfileView> ProfileViews()
        {
            // YARG's RefreshList destroys the old rows at the end of the frame and adds new ones in the
            // same frame, so for one frame each profile can have two rows: keep the newest (later in the
            // hierarchy). Rows are ordered by the hierarchy, as the layout shows them (new rows have no
            // position yet).
            var newest = new Dictionary<YargProfile, ProfileView>();
            var list = new List<ProfileView>();
            foreach (var pv in UnityEngine.Object.FindObjectsOfType<ProfileView>())
            {
                if (pv == null || !pv.isActiveAndEnabled) continue;
                if (pv.Profile == null) { list.Add(pv); continue; }
                ProfileView old;
                if (!newest.TryGetValue(pv.Profile, out old) || ComparePath(pv, old) > 0) newest[pv.Profile] = pv;
            }
            list.AddRange(newest.Values);
            list.Sort(ComparePath);
            if (_profileIdx >= list.Count) _profileIdx = Mathf.Max(0, list.Count - 1);
            return list;
        }

        // Hierarchy order: compare the sibling indices from the root down.
        static int ComparePath(ProfileView a, ProfileView b)
        {
            var pa = SiblingPath(a.transform);
            var pb = SiblingPath(b.transform);
            for (int i = 0; i < pa.Count && i < pb.Count; i++)
                if (pa[i] != pb[i]) return pa[i].CompareTo(pb[i]);
            return pa.Count.CompareTo(pb.Count);
        }

        static List<int> SiblingPath(Transform t)
        {
            var path = new List<int>();
            for (; t != null; t = t.parent) path.Insert(0, t.GetSiblingIndex());
            return path;
        }

        static ProfileView Selected()
        {
            var ps = ProfileViews();
            return (ps.Count > 0 && _profileIdx < ps.Count) ? ps[_profileIdx] : null;
        }

        static YargProfile SelectedProfile() { var pv = Selected(); return pv != null ? pv.Profile : null; }

        internal static string ProfileNameOr(string fallback) { var p = SelectedProfile(); return p != null ? p.Name : fallback; }

        static void Follow(YargProfile p)
        {
            if (p == null) return;
            var ps = ProfileViews();
            for (int i = 0; i < ps.Count; i++) if (ps[i].Profile == p) { _profileIdx = i; return; }
        }

        static void AnnounceProfile(ProfileView pv, bool asItem)
        {
            var text = ProfileText(pv);
            MenuAnnouncer.Focused(text, asItem);
        }

        static string ProfileText(ProfileView pv)
        {
            var p = pv.Profile;
            var sb = new System.Text.StringBuilder();
            sb.Append(p != null ? p.Name : Describer.GetLabel(pv.gameObject));
            if (p != null && p.IsBot) sb.Append(", bot");
            if (p != null) { sb.Append(", "); sb.Append(InstrumentName(p)); }
            sb.Append(", "); sb.Append(ConnectionInfo(p));
            return sb + Keys.Pos(_profileIdx, ProfileViews().Count);
        }

        static string ProfileLabel(ProfileView pv)
        {
            var p = pv.Profile;
            if (p == null) return Describer.GetLabel(pv.gameObject);
            return p.Name + (p.IsBot ? ", bot" : "") + ", " + InstrumentName(p) + ", " + ConnectionInfo(p);
        }

        static string InstrumentName(YargProfile p)
        {
            return Playability.InstrumentName(p.PreferredInstrument);
        }

        static string ConnectionInfo(YargProfile p)
        {
            var player = GetPlayer(p);
            if (player == null) return "not connected";
            if (p != null && p.IsBot) return "connected";
            int n = DeviceCount(player);
            return "connected, " + n + (n == 1 ? " device" : " devices");
        }

        internal static List<string> Actions()
        {
            var list = new List<string> { "Add Profile", "Add Bot" };
            var p = SelectedProfile();
            if (p == null) return list;
            var player = GetPlayer(p);
            if (player != null)
            {
                list.Add("Disconnect");
                if (!p.IsBot) list.Add("Edit Binds");
                list.Add("Rename");
                list.Add("Profile Settings");
                if (!p.IsBot) { list.Add("Add Device"); list.Add("Remove Device"); }
                int idx = PlayerIndex(player);
                if (idx > 0) list.Add("Move Player Up");
                if (idx >= 0 && idx < PlayerContainer.Players.Count - 1) list.Add("Move Player Down");
            }
            else if (CanConnect()) list.Add("Connect");
            list.Add("Delete Profile");
            list.Add("Instrument");
            if (player != null && !p.IsBot) list.Add("Devices");
            return list;
        }

        static string CurrentAction()
        {
            var a = Actions();
            return (_action >= 0 && _action < a.Count) ? a[_action] : null;
        }

        static void AnnounceAction() { MenuAnnouncer.Focused(ActionText()); }

        static string ActionText()
        {
            var actions = Actions();
            if (_action < 0 || _action >= actions.Count) return "";
            var name = actions[_action];
            var p = SelectedProfile();
            string pos = Keys.Pos(_action, actions.Count);
            switch (name)
            {
                case "Instrument": return "Instrument, " + (p != null ? InstrumentName(p) : "no profile") + ", " + Keys.LeftRight + " to change" + pos;
                case "Devices": return "Devices" + Keys.Role("button") + ", " + (p != null ? ConnectionInfo(p) : "no profile") + ", " + Keys.Ok + " to list them" + pos;
                case "Profile Settings": return "Profile Settings for " + ProfileNameOr("profile") + Keys.Role("button") + pos;
                default: return name + Keys.Role("button") + pos;
            }
        }

        static void AnnounceAdded()
        {
            var ps = ProfileViews();
            if (ps.Count == 0) return;
            _profileIdx = ps.Count - 1;
            YargProfile newest = null;
            try { var all = PlayerContainer.Profiles; if (all != null && all.Count > 0) newest = all[all.Count - 1]; } catch { }
            Follow(newest);
            _action = -1;
            var pv = Selected();
            if (pv != null) MenuAnnouncer.SayShown("Added. " + ProfileText(pv));
        }

        static bool CanConnect()
        {
            var menu = UnityEngine.Object.FindObjectOfType<ProfileListMenu>();
            try { return menu == null || menu.CanConnectProfile; } catch { return true; }
        }

        static int PlayerIndex(YargPlayer player)
        {
            try { return PlayerContainer.GetPlayerIndex(player); } catch { return -1; }
        }

        static void EditProfile(ProfileView pv)
        {
            var p = pv.Profile;
            var player = p != null ? GetPlayer(p) : null;
            if (player != null)
            {
                if (p.IsBot) { MenuAnnouncer.SayShown("Bots have no binds. Use Profile Settings to change this bot's options."); return; }
                var sidebar = Sidebar();
                if (sidebar != null)
                {
                    try
                    {
                        sidebar.UpdateSidebar(p, pv); sidebar.EditProfile();
                        ProfileInfoNavigator.Reset();
                    }
                    catch (Exception ex) { ModLog.Warn("ProfilesNavigator.EditProfile", ex); }
                }
            }
            else Connect(pv);
        }

        static void Connect(ProfileView pv)
        {
            if (!CanConnect()) { MenuAnnouncer.SayShown("Can't connect another profile. Disconnect one first."); return; }
            var p = pv.Profile;
            MenuAnnouncer.BeginBatch();
            InvokeView(pv, "ConnectButtonAction");
            MenuAnnouncer.DiscardBatch();
            Follow(p);
            _action = -1;
            if (p != null && GetPlayer(p) != null && !DialogNavigator.IsActive)
                { int n = DeviceCount(GetPlayer(p)); MenuAnnouncer.SayShown("Connected " + p.Name + (p.IsBot ? "" : ", " + n + (n == 1 ? " device" : " devices"))); }
        }

        static void Disconnect(ProfileView pv)
        {
            var p = pv.Profile;
            MenuAnnouncer.BeginBatch();
            try { pv.Disconnect(); } catch (Exception ex) { ModLog.Warn("ProfilesNavigator.Disconnect", ex); }
            MenuAnnouncer.DiscardBatch();
            Follow(p);
            _action = -1;
            MenuAnnouncer.SayShown("Disconnected " + (p != null ? p.Name : "profile"));
        }

        static void MovePlayer(ProfileView pv, int dir)
        {
            var p = pv.Profile;
            InvokeMenu(dir < 0 ? "MoveProfileUp" : "MoveProfileDown", p);
            Follow(p);
            var player = GetPlayer(p);
            int idx = PlayerIndex(player);
            _action = -1;
            MenuAnnouncer.SayShown(p.Name + " is now player " + (idx + 1) + " of " + PlayerContainer.Players.Count);
        }

        static void CycleInstrument(int dir)
        {
            var p = SelectedProfile();
            if (p == null) return;
            var vals = (YARG.Core.Instrument[])Enum.GetValues(typeof(YARG.Core.Instrument));
            int i = Array.IndexOf(vals, p.PreferredInstrument);
            if (i < 0) i = 0;
            i = (i + dir + vals.Length) % vals.Length;
            p.PreferredInstrument = vals[i];
            try { p.EnsureValidInstrument(); } catch { }
            MenuAnnouncer.Focused(InstrumentName(p));
        }

        static ProfileSidebar Sidebar() { return UnityEngine.Object.FindObjectOfType<ProfileSidebar>(true); }

        static ProfileSidebar BindSidebar(ProfileView pv)
        {
            var sb = Sidebar();
            if (sb == null || pv == null || pv.Profile == null) return null;
            if (GetPlayer(pv.Profile) == null) { MenuAnnouncer.SayShown("Connect " + pv.Profile.Name + " first."); return null; }
            try { sb.UpdateSidebar(pv.Profile, pv); }
            catch (Exception ex) { ModLog.Warn("ProfilesNavigator.BindSidebar", ex); return null; }
            return sb;
        }

        static void SidebarCall(ProfileView pv, string method)
        {
            var sb = BindSidebar(pv);
            if (sb == null) return;
            try { typeof(ProfileSidebar).GetMethod(method, BF).Invoke(sb, null); }
            catch (Exception ex) { ModLog.Warn("ProfilesNavigator." + method, ex); }
        }

        static void Rename(ProfileView pv)
        {
            var sb = BindSidebar(pv);
            if (sb == null) return;
            var p = pv.Profile;
            var field = Field<TMP_InputField>(sb, "_nameInput");
            if (field == null) return;
            try { sb.SetNameEditMode(true); }
            catch (Exception ex) { ModLog.Warn("ProfilesNavigator.Rename", ex); return; }
            TextEdit.Begin(field, "profile name", text =>
            {
                MenuAnnouncer.SayShown("Renamed to " + p.Name);
            });
        }

        class Row { public string Label; public string Key; public TMP_Dropdown Dropdown; public TMP_InputField Input; public Toggle Toggle; public Button Button; }

        static void EnterSettings(ProfileView pv)
        {
            if (BindSidebar(pv) == null) return;
            var rows = Rows();
            if (rows.Count == 0) { MenuAnnouncer.SayShown("No settings available."); return; }
            _inSettings = true;
            _settingIdx = 0;
            _pendingConfigure = null;
            MenuAnnouncer.Focused("Profile settings for " + pv.Profile.Name + ". " + RowText(rows, 0));
        }

        static void MoveSetting(int dir)
        {
            var rows = Rows();
            if (rows.Count == 0) { _inSettings = false; return; }
            _pendingConfigure = null;
            _settingIdx = Mathf.Clamp(_settingIdx + dir, 0, rows.Count - 1);
            MenuAnnouncer.Focused(RowText(rows, _settingIdx));
        }

        static Row CurrentRow()
        {
            var rows = Rows();
            if (rows.Count == 0) return null;
            _settingIdx = Mathf.Clamp(_settingIdx, 0, rows.Count - 1);
            return rows[_settingIdx];
        }

        static void AdjustSetting(int dir)
        {
            var r = CurrentRow();
            if (r == null) return;
            if (r.Dropdown != null)
            {
                int count = r.Dropdown.options.Count;
                if (count == 0) return;
                int nv = Mathf.Clamp(r.Dropdown.value + dir, 0, count - 1);
                if (nv != r.Dropdown.value) r.Dropdown.value = nv;
                var again = CurrentRow() ?? r;
                string preset = PresetTip(again);
                Tooltips.Show(RowTip(again), again.Label, StillInSettings);
                MenuAnnouncer.Focused(ValueText(again) + (preset != null ? ". " + preset : ""));
                return;
            }
            if (r.Input != null)
            {
                float step = StepFor(r.Key);
                if (step <= 0f) return;
                float cur;
                if (!float.TryParse(r.Input.text, NumberStyles.Float, CultureInfo.CurrentCulture, out cur)) cur = 0f;
                float nv = cur + dir * step;
                r.Input.text = step >= 1f
                    ? Mathf.RoundToInt(nv).ToString(CultureInfo.CurrentCulture)
                    : (Mathf.Round(nv / step) * step).ToString("0.0###", CultureInfo.CurrentCulture);
                try { r.Input.onEndEdit.Invoke(r.Input.text); } catch (Exception ex) { ModLog.Warn("ProfilesNavigator.AdjustSetting", ex); }
                MenuAnnouncer.Focused(ValueText(r));
                return;
            }
            if (r.Toggle != null) MenuAnnouncer.Say("Press " + Keys.Ok + " to change. " + ValueText(r));
        }

        static void ConfirmSetting()
        {
            var r = CurrentRow();
            if (r == null) return;
            if (r.Toggle != null)
            {
                r.Toggle.isOn = !r.Toggle.isOn;
                MenuAnnouncer.Focused(ValueText(r));
                return;
            }
            if (r.Input != null)
            {
                var row = r;
                TextEdit.Begin(r.Input, r.Label, text => MenuAnnouncer.Say(row.Label + ", " + ValueText(row)));
                return;
            }
            if (r.Button != null)
            {
                var p = SelectedProfile();
                if (_pendingConfigure != p)
                {
                    _pendingConfigure = p;
                    MenuAnnouncer.SayShown("This opens the drums lane-order editor, which can only be used with a mouse. Press " + Keys.Ok + " again to open it anyway.");
                    return;
                }
                _pendingConfigure = null;
                try { r.Button.onClick.Invoke(); } catch (Exception ex) { ModLog.Warn("ProfilesNavigator.Configure", ex); }
                return;
            }
            if (r.Dropdown != null) MenuAnnouncer.Say(Keys.LeftRight + " to change. " + ValueText(r));
        }

        static string RowText(List<Row> rows, int i)
        {
            var r = rows[i];
            string role = r.Dropdown != null ? "dropdown" : r.Toggle != null ? "checkbox" : r.Input != null ? "number" : "";
            string val = ValueText(r);
            var s = r.Label + Keys.Role(role);
            if (!string.IsNullOrEmpty(val)) s += ", " + val;
            s += Keys.Pos(i, rows.Count);
            string tip = RowTip(r);
            Tooltips.Show(tip, r.Label, StillInSettings);
            return tip != null ? s + ". " + tip : s;
        }

        static string RowTip(Row r)
        {
            string tip = Tooltips.ForProfileSetting(r.Key, r.Label);
            string preset = PresetTip(r);
            if (preset != null) tip = tip != null ? tip + " " + preset : preset;
            return tip;
        }

        static bool StillInSettings()
        {
            var sb = Sidebar();
            return _inSettings && sb != null && sb.isActiveAndEnabled;
        }

        static string ValueText(Row r)
        {
            if (r.Dropdown != null)
            {
                var d = r.Dropdown;
                return (d.value >= 0 && d.value < d.options.Count) ? TextUtil.Clean(d.options[d.value].text) : "none";
            }
            if (r.Toggle != null) return r.Toggle.isOn ? "checked" : "not checked";
            if (r.Input != null)
            {
                var t = TextUtil.Clean(r.Input.text);
                if (string.IsNullOrEmpty(t)) return "blank";
                return r.Key == "Input Calibration" ? t + " milliseconds" : t;
            }
            return "opens the lane-order editor";
        }

        static string PresetTip(Row r)
        {
            if (r == null || r.Dropdown == null) return null;
            switch (r.Key)
            {
                case "Engine Preset": case "Rock Meter Preset": case "Camera Preset": case "Highway Preset":
                case "Theme Select": case "Color Profile Select":
                    return Tooltips.ForBuiltInPreset(r.Key, ValueText(r));
                default: return null;
            }
        }

        static float StepFor(string key)
        {
            switch (key)
            {
                case "Note Speed": return 0.5f;
                case "Highway Length": return 0.1f;
                case "Input Calibration": return 1f;
                default: return 0f;
            }
        }

        static List<Row> Rows()
        {
            var res = new List<Row>();
            var sb = Sidebar();
            if (sb == null) return res;
            var content = Field<GameObject>(sb, "_sidebarContent");
            if (content == null || !content.activeInHierarchy) return res;
            for (int i = 0; i < content.transform.childCount; i++)
            {
                var child = content.transform.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                if (child.Find("Option Name") != null) { AddRow(res, child); continue; }
                for (int j = 0; j < child.childCount; j++)
                {
                    var sub = child.GetChild(j);
                    if (sub.gameObject.activeSelf && sub.Find("Option Name") != null) AddRow(res, sub);
                }
            }
            return res;
        }

        static void AddRow(List<Row> res, Transform row)
        {
            var nameTmp = row.Find("Option Name").GetComponent<TMP_Text>();
            var r = new Row { Key = row.name, Label = TitleCase(nameTmp != null ? TextUtil.Clean(nameTmp.text) : Describer.PrettifyName(row.name)) };
            r.Dropdown = row.GetComponentInChildren<TMP_Dropdown>(false);
            if (r.Dropdown == null)
            {
                r.Input = row.GetComponentInChildren<TMP_InputField>(false);
                if (r.Input == null)
                {
                    r.Toggle = row.GetComponentInChildren<Toggle>(false);
                    if (r.Toggle == null) r.Button = row.GetComponentInChildren<Button>(false);
                }
            }
            if (r.Dropdown == null && r.Input == null && r.Toggle == null && r.Button == null) return;
            res.Add(r);
        }

        static string TitleCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("\n", " ");
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
        }

        static void EnterDevices()
        {
            var names = DeviceNames();
            if (names.Count == 0) { MenuAnnouncer.SayShown("No devices. Use Add Device on the toolbar to connect one."); return; }
            _inDevices = true; _deviceIdx = 0;
            AnnounceDevice();
        }

        static void MoveDevice(int dir)
        {
            var names = DeviceNames();
            if (names.Count == 0) { _inDevices = false; return; }
            _deviceIdx = Mathf.Clamp(_deviceIdx + dir, 0, names.Count - 1);
            AnnounceDevice();
        }

        static void AnnounceDevice()
        {
            var names = DeviceNames();
            if (_deviceIdx < names.Count)
                MenuAnnouncer.Focused(names[_deviceIdx] + Keys.Pos(_deviceIdx, names.Count));
        }

        static List<string> DeviceNames()
        {
            var res = new List<string>();
            var player = GetPlayer(SelectedProfile());
            if (player == null) return res;
            try
            {
                var b = player.Bindings;
                if (b != null)
                {
                    if (b.InputDevices != null) foreach (var d in b.InputDevices) if (d != null) res.Add(d.displayName);
                    if (b.Microphone != null) res.Add("Microphone");
                }
            }
            catch { }
            return res;
        }

        static YargPlayer GetPlayer(YargProfile p)
        {
            if (p == null) return null;
            try { return PlayerContainer.GetPlayerFromProfile(p); } catch { return null; }
        }

        static int DeviceCount(YargPlayer player)
        {
            if (player == null) return 0;
            try
            {
                var b = player.Bindings;
                if (b == null) return 0;
                int n = (b.InputDevices != null) ? b.InputDevices.Count : 0;
                if (b.Microphone != null) n++;
                return n;
            }
            catch { return 0; }
        }

        static T Field<T>(object o, string name) where T : class
        {
            try { var f = o.GetType().GetField(name, BF); return f != null ? f.GetValue(o) as T : null; }
            catch { return null; }
        }

        static void InvokeView(ProfileView pv, string method)
        {
            try { var m = typeof(ProfileView).GetMethod(method, BF); if (m != null) m.Invoke(pv, null); }
            catch (Exception ex) { ModLog.Warn("ProfilesNavigator." + method, ex); }
        }

        static bool DeleteProfile(YargProfile p)
        {
            try
            {
                var player = GetPlayer(p);
                if (player != null) { try { PlayerContainer.DisposePlayer(player); } catch { } }
                bool ok = PlayerContainer.RemoveProfile(p);
                if (ok)
                {
                    var menu = UnityEngine.Object.FindObjectOfType<ProfileListMenu>();
                    if (menu != null) { var m = typeof(ProfileListMenu).GetMethod("RefreshList", BF); if (m != null) m.Invoke(menu, new object[] { null }); }
                    _profileIdx = 0;
                }
                return ok;
            }
            catch (Exception ex) { ModLog.Warn("DeleteProfile", ex); return false; }
        }

        static void InvokeMenu(string method, params object[] args)
        {
            try
            {
                var menu = UnityEngine.Object.FindObjectOfType<ProfileListMenu>();
                if (menu != null) { var m = typeof(ProfileListMenu).GetMethod(method, BF); if (m != null) m.Invoke(menu, args.Length == 0 ? null : args); }
            }
            catch (Exception ex) { ModLog.Warn("ProfilesNavigator." + method, ex); }
        }
    }
}
