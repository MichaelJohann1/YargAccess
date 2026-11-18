using System;
using System.Reflection;
using MelonLoader;

namespace YargAccess
{
    public static class ListMenuAnnouncer
    {
        const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        static string _last;

        public static void ResetLast() { _last = null; }

        public static bool AnnounceActive()
        {
            object menu = UnityEngine.Object.FindObjectOfType<YARG.Menu.MusicLibrary.MusicLibraryMenu>();
            if (menu == null) menu = UnityEngine.Object.FindObjectOfType<YARG.Menu.History.HistoryMenu>();
            if (menu == null) return false;
            _last = null;
            _pending = null;
            AnnounceNow(menu);
            return true;
        }

        static object _pending;
        static string EmptyText => Loc.T("Empty list");

        public static void Announce(object listMenu)
        {
            if (listMenu == null) return;
            _pending = listMenu;
        }

        public static void CheckSoon()
        {
            object menu = UnityEngine.Object.FindObjectOfType<YARG.Menu.MusicLibrary.MusicLibraryMenu>();
            if (menu == null) menu = UnityEngine.Object.FindObjectOfType<YARG.Menu.History.HistoryMenu>();
            if (menu != null && _pending == null) _pending = menu;
        }

        public static void Flush()
        {
            var m = _pending;
            if (m == null) return;
            _pending = null;
            if (m is UnityEngine.Object uo && uo == null) return;
            AnnounceNow(m);
        }

        static bool CoveredByOverlay()
        {
            try
            {
                if (FiltersNavigator.IsActive) return true;
                var popup = UnityEngine.Object.FindObjectOfType<YARG.Menu.MusicLibrary.PopupMenu>();
                return popup != null && popup.gameObject.activeInHierarchy;
            }
            catch { return false; }
        }

        static void AnnounceNow(object listMenu)
        {
            if (ShowPickerNavigator.IsActive) { _last = null; return; }
            if (CoveredByOverlay()) { _last = null; return; }
            var text = Line(listMenu);
            if (string.IsNullOrEmpty(text) || text == _last) return;
            _last = text;
            MenuAnnouncer.Focused(text + DetailsHint(), true);
        }

        public static string TakeActiveLine()
        {
            object menu = UnityEngine.Object.FindObjectOfType<YARG.Menu.MusicLibrary.MusicLibraryMenu>();
            if (menu == null) menu = UnityEngine.Object.FindObjectOfType<YARG.Menu.History.HistoryMenu>();
            if (menu == null) return null;
            var text = Line(menu);
            if (!string.IsNullOrEmpty(text)) _last = text;
            _pending = null;
            return string.IsNullOrEmpty(text) ? text : text + DetailsHint();
        }

        static bool _rowIsSong;
        static float _hintVisit = -1f;
        static Keys.Device _hintDevice;

        static string DetailsHint()
        {
            if (!_rowIsSong) return "";
            if (_hintVisit == Menu.PrefixArmedAt && _hintDevice == Keys.Current) return "";
            _hintVisit = Menu.PrefixArmedAt;
            _hintDevice = Keys.Current;
            return Loc.F(". Song details: {0}", Keys.ReviewHint);
        }

        static string Line(object listMenu)
        {
            _rowIsSong = false;
            try
            {
                var t = listMenu.GetType();
                var sel = GetProp(listMenu, t, "CurrentSelection");
                if (sel == null)
                {
                    var rows = GetProp(listMenu, t, "ViewList") as System.Collections.ICollection;
                    return rows != null && rows.Count == 0 ? EmptyText : null;
                }
                var st = sel.GetType();

                var primary = CallText(sel, st, "GetPrimaryText");
                var secondary = CallText(sel, st, "GetSecondaryText");

                var text = Calm(TextUtil.Clean(primary));
                var sec = Calm(TextUtil.Clean(secondary));
                if (!string.IsNullOrEmpty(sec) && !string.Equals(sec, text, StringComparison.OrdinalIgnoreCase))
                    text = string.IsNullOrEmpty(text) ? sec : (text + ", " + sec);

                var role = RoleOf(st.Name);
                if (role == "button" && IsInertButton(sel)) role = "heading";
                text += Keys.Role(role);
                if (role == "button" && ModSettings.ReadDescriptions.Value)
                {
                    var help = Calm(TextUtil.Clean(CallText(sel, st, "GetSideText")));
                    if (!string.IsNullOrEmpty(help) && help != text) text += ", " + help;
                }

                var song = sel as YARG.Menu.MusicLibrary.SongViewType;
                _rowIsSong = song != null;
                string full = null;
                if (song != null)
                {
                    var details = SongDetails.For(song);
                    if (details.Length > 0) full = text + ", " + details;
                    var head = SongDetails.Head(song);
                    if (head.Length > 0) text += ", " + head;
                }
                string hs, hf;
                if (song == null && HistoryNavigator.Describe(sel, out hs, out hf))
                {
                    // A history row says all of it; no "Song details" hint, nothing extra in the review.
                    if (!string.IsNullOrEmpty(hs)) text += ", " + hs;
                }
                DrawElsewhere(listMenu, song);

                var idxObj = GetProp(listMenu, t, "SelectedIndex");
                var viewList = GetProp(listMenu, t, "ViewList") as System.Collections.ICollection;
                string pos = idxObj is int idx && viewList != null ? Keys.Pos(idx, viewList.Count) : "";
                text += pos;
                if (full != null) TextReview.SetExtended(text, full + pos);
                return text;
            }
            catch (Exception ex) { ModLog.Warn("ListMenuAnnouncer", ex); return null; }
        }

        static void DrawElsewhere(object listMenu, YARG.Menu.MusicLibrary.SongViewType song)
        {
            string title, body;
            if (!SongDetails.Elsewhere(song, out title, out body)) { Captions.Panel(null, null, null); return; }
            var menu = listMenu as UnityEngine.Behaviour;
            var t = listMenu.GetType();
            Captions.Panel(body, title, () => menu != null && menu.isActiveAndEnabled && !CoveredByOverlay()
                && ReferenceEquals(GetProp(listMenu, t, "CurrentSelection"), song));
        }

        public static string LibraryHeader()
        {
            try
            {
                var menu = UnityEngine.Object.FindObjectOfType<YARG.Menu.MusicLibrary.MusicLibraryMenu>();
                if (menu == null) return "";
                var parts = new System.Collections.Generic.List<string>();
                var primary = HeaderCase(TextUtil.Clean(HeaderText(menu, "_sortInfoHeaderPrimaryText")));
                if (!string.IsNullOrEmpty(primary)) parts.Add(primary);
                var count = TextUtil.Clean(HeaderText(menu, "_sortInfoHeaderSongCountText"));
                if (!string.IsNullOrEmpty(count)) parts.Add(count.ToLowerInvariant());
                var stars = TextUtil.Clean(HeaderText(menu, "_sortInfoHeaderStarCountText"));
                if (!string.IsNullOrEmpty(stars)) parts.Add(Loc.F("{0} stars", stars.Replace(" / ", Loc.T(" of ")).Replace("/", Loc.T(" of "))));
                return string.Join(", ", parts.ToArray());
            }
            catch (Exception ex) { ModLog.Warn("ListMenuAnnouncer.LibraryHeader", ex); return ""; }
        }

        static string HeaderCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var words = s.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                var w = words[i];
                if (w.Length > 1 && w.ToUpperInvariant() == w && w.ToLowerInvariant() != w) words[i] = w.ToLowerInvariant();
            }
            var r = string.Join(" ", words);
            return char.ToUpperInvariant(r[0]) + r.Substring(1);
        }

        static string HeaderText(object menu, string field)
        {
            var f = menu.GetType().GetField(field, BF);
            var t = f != null ? f.GetValue(menu) as TMPro.TMP_Text : null;
            return t != null && t.gameObject.activeInHierarchy ? t.text : "";
        }

        static string RoleOf(string viewTypeName)
        {
            switch (viewTypeName)
            {
                case "SongViewType":       return "";
                case "SortHeaderViewType": return "heading";
                case "CategoryViewType":   return "heading";
                case "ButtonViewType":     return "button";
                case "PlaylistViewType":   return "playlist";
                case "HistoryViewType":    return "";
                default:                   return "";
            }
        }

        static bool IsInertButton(object view)
        {
            try
            {
                var f = view.GetType().GetField("_buttonAction", BF);
                var d = f != null ? f.GetValue(view) as Delegate : null;
                if (d == null) return true;
                var body = d.Method.GetMethodBody();
                var il = body != null ? body.GetILAsByteArray() : null;
                if (il == null) return false;
                foreach (var b in il) if (b != 0x00 && b != 0x2A) return false;
                return true;
            }
            catch { return false; }
        }

        static string Calm(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.IndexOf(' ') < 0 && s.Length < 5 && s != "BACK") return s;
            bool letters = false;
            foreach (var ch in s)
            {
                if (char.IsLower(ch)) return s;
                if (char.IsLetter(ch)) letters = true;
            }
            if (!letters) return s;
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
        }

        static object GetProp(object o, Type t, string name)
        {
            var p = t.GetProperty(name, BF);
            return p != null ? p.GetValue(o, null) : null;
        }

        static string CallText(object sel, Type t, string method)
        {
            var m = t.GetMethod(method, new[] { typeof(bool) });
            if (m == null) return "";
            try { return (string)m.Invoke(sel, new object[] { false }); }
            catch { return ""; }
        }

        static string RowText(object view, out string role)
        {
            var st = view.GetType();
            var text = TextUtil.Clean(CallText(view, st, "GetPrimaryText"));
            var sec = TextUtil.Clean(CallText(view, st, "GetSecondaryText"));
            if (!string.IsNullOrEmpty(sec) && !string.Equals(sec, text, StringComparison.OrdinalIgnoreCase))
                text = string.IsNullOrEmpty(text) ? sec : (text + ", " + sec);
            role = RoleOf(st.Name);
            return text;
        }
    }
}
