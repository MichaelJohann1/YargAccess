using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using YARG.Menu.Dialogs;
using YARG.Menu.Navigation;

namespace YargAccess
{
    public static class DialogNavigator
    {
        static readonly List<object> _items = new List<object>();
        static object _focused;

        // A long dialog message (a tutorial page) is split into sentences, each its own item, so Up and
        // Down step through it and each is its own speech call.
        public sealed class Piece
        {
            public readonly int Index;
            public readonly string Text;
            public Piece(int index, string text) { Index = index; Text = text; }
            public override bool Equals(object o) { var p = o as Piece; return p != null && p.Index == Index && p.Text == Text; }
            public override int GetHashCode() { return Index * 31 + (Text ?? "").GetHashCode(); }
        }

        const int SplitOver = 200;
        static readonly System.Text.RegularExpressions.Regex SentenceEnd =
            new System.Text.RegularExpressions.Regex(@"(?<=[.!?])\s+(?=[A-Z0-9""(])");

        // The sentences of a dialog message, or null when it is short or one sentence.
        public static List<string> Sentences(string message)
        {
            if (string.IsNullOrEmpty(message) || message.Length <= SplitOver) return null;
            var list = new List<string>();
            foreach (var part in SentenceEnd.Split(message))
            {
                var t = part.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list.Count > 1 ? list : null;
        }

        public static void FocusPiece(int index)
        {
            Rebuild();
            foreach (var c in _items) { var p = c as Piece; if (p != null && p.Index == index) { _focused = p; return; } }
        }
        static object _dm;
        static PropertyInfo _showingProp;

        public static bool IsActive
        {
            get
            {
                if (ShowPickerNavigator.IsActive) return false;
                try
                {
                    if (_dm == null)
                    {
                        var t = Type.GetType("YARG.Menu.Persistent.DialogManager, Assembly-CSharp");
                        if (t != null) _dm = UnityEngine.Object.FindObjectOfType(t);
                    }
                    if (_dm != null)
                    {
                        if (_showingProp == null) _showingProp = _dm.GetType().GetProperty("IsDialogShowing");
                        if (_showingProp != null) return (bool)_showingProp.GetValue(_dm, null);
                    }
                }
                catch { }
                return UnityEngine.Object.FindObjectOfType<Dialog>() != null;
            }
        }

        public static void Move(int dir)
        {
            Rebuild();
            if (_items.Count == 0) { _focused = null; return; }
            int idx = _focused != null ? _items.IndexOf(_focused) : -1;
            if (idx < 0) idx = dir > 0 ? 0 : _items.Count - 1;
            else idx = Mathf.Clamp(idx + dir, 0, _items.Count - 1);
            _focused = _items[idx];
            SyncSelection(_focused);
            Announce(idx);
        }

        public static void Confirm()
        {
            if (_focused == null) { Move(1); return; }
            var tog = _focused as Toggle;
            if (tog != null) { tog.isOn = !tog.isOn; MenuAnnouncer.Focused(tog.isOn ? Loc.T("checked") : Loc.T("not checked")); return; }
            var txt = _focused as TMP_Text;
            if (txt != null) { MenuAnnouncer.Focused(TextUtil.Clean(txt.text)); return; }
            var piece = _focused as Piece;
            if (piece != null) { if (!Tutorials.PressMain()) MenuAnnouncer.Focused(piece.Text); return; }
            var field = _focused as TMP_InputField;
            if (field != null)
            {
                var lbl = FieldLabel(field);
                TextEdit.Begin(field, lbl, text => MenuAnnouncer.Say(Loc.F("{0}, {1}. Move to the confirm button and press {2}.", lbl, string.IsNullOrEmpty(text) ? Loc.T("blank") : TextUtil.Clean(text), Keys.Ok)));
                return;
            }
            var cb = _focused as YARG.Menu.ColoredButton;
            if (cb != null)
            {
                try { if (cb.OnClick != null) cb.OnClick.Invoke(); }
                catch (Exception ex) { ModLog.Warn("DialogNavigator.Confirm", ex); }
                return;
            }
            var nb = _focused as NavigatableBehaviour;
            if (nb != null) Press(nb);
        }

        static void Press(NavigatableBehaviour nb)
        {
            SyncSelection(nb);
            if (ContextHelp.ActionName("Green") != null) { YargInput.Invoke("Green"); return; }
            try { nb.Confirm(); }
            catch (Exception ex) { ModLog.Warn("DialogNavigator.Press", ex); }
        }

        public static void Reset() { _items.Clear(); _focused = null; }

        // label is the English label; the button shows it in YARG's language (the mod's translation, or
        // YARG's own Menu.Common word for Close, Cancel, Back and the like).
        public static void FocusLabel(string label)
        {
            Rebuild();
            foreach (var c in _items)
            {
                var nb = c as NavigatableBehaviour;
                if (nb != null && LabelIs(Describer.GetLabel(nb.gameObject), label)) { _focused = nb; SyncSelection(nb); return; }
                var cb = c as YARG.Menu.ColoredButton;
                if (cb != null && cb.Text != null && LabelIs(TextUtil.Clean(cb.Text.text), label)) { _focused = cb; return; }
            }
        }

        static readonly HashSet<string> CommonWords = new HashSet<string>
            { "Apply", "Back", "Cancel", "Close", "Confirm", "Continue", "Delete", "Next", "Previous", "Reset", "Save" };

        // A lookup for comparing, not a new key: the labels are keys where their callers write them.
        static readonly Func<string, string> Translated = Loc.T;

        // Whether text shown on a button is this English label, in English or in YARG's language.
        public static bool LabelIs(string text, string english)
        {
            if (text == null || english == null) return false;
            if (text == english) return true;
            if (Loc.English) return false;
            if (text == Translated(english)) return true;
            string mod;
            if (ModSettings.TryLocalize(ModSettings.TextKey + english, out mod) && text == mod) return true;
            if (!CommonWords.Contains(english)) return false;
            try { return text == TextUtil.Clean(YARG.Localization.Localize.Key("Menu.Common." + english)); }
            catch { return false; }
        }

        public static bool Back()
        {
            if (!IsActive) return false;
            if (ContextHelp.ActionName("Red") != null) { YargInput.Invoke("Red"); return true; }
            Rebuild();
            foreach (var c in _items)
            {
                var nb = c as NavigatableBehaviour;
                if (nb == null) continue;
                var lbl = Describer.GetLabel(nb.gameObject);
                if (LabelIs(lbl, "Close") || LabelIs(lbl, "Cancel") || LabelIs(lbl, "Back"))
                {
                    Press(nb);
                    return true;
                }
            }
            try { YARG.Menu.Persistent.DialogManager.Instance.ClearDialog(); }
            catch (Exception ex) { ModLog.Warn("DialogNavigator.Back", ex); }
            return true;
        }

        static void Rebuild()
        {
            _items.Clear();
            var d = UnityEngine.Object.FindObjectOfType<Dialog>();
            if (d == null) return;

            var title = d.Title;
            if (title != null && title.isActiveAndEnabled && !string.IsNullOrWhiteSpace(title.text)) _items.Add(title);
            foreach (var t in d.GetComponentsInChildren<TMP_Text>(false))
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                if (t.gameObject.name == "Message" && !string.IsNullOrWhiteSpace(t.text))
                {
                    var parts = Sentences(TextUtil.Clean(t.text));
                    if (parts == null) _items.Add(t);
                    else for (int i = 0; i < parts.Count; i++) _items.Add(new Piece(i, parts[i]));
                    break;
                }
            }

            foreach (var cb in d.GetComponentsInChildren<YARG.Menu.ColoredButton>(false))
            {
                if (cb == null || !cb.isActiveAndEnabled) continue;
                if (cb.GetComponentInChildren<NavigatableBehaviour>(false) != null) continue;
                _items.Add(cb);
            }

            var chrome = new List<Component>();
            foreach (var f in d.GetComponentsInChildren<TMP_InputField>(false))
                if (f.isActiveAndEnabled && f.interactable) chrome.Add(f);
            foreach (var t in d.GetComponentsInChildren<Toggle>(false))
                if (t.isActiveAndEnabled && t.interactable) chrome.Add(t);
            foreach (var nb in d.GetComponentsInChildren<NavigatableBehaviour>(false))
                if (nb != null && nb.isActiveAndEnabled) chrome.Add(nb);
            chrome.Sort((a, c) =>
            {
                float dy = c.transform.position.y - a.transform.position.y;
                return Mathf.Abs(dy) > 1f ? (dy > 0 ? 1 : -1) : a.transform.position.x.CompareTo(c.transform.position.x);
            });
            _items.AddRange(chrome);
        }

        static void SyncSelection(object c)
        {
            var nb = c as NavigatableBehaviour;
            if (nb == null) return;
            try
            {
                var grp = nb.NavigationGroup;
                if (grp == null) return;
                var lf = typeof(NavigationGroup).GetField("_navigatables", BindingFlags.NonPublic | BindingFlags.Instance);
                var list = lf != null ? lf.GetValue(grp) as System.Collections.IList : null;
                int i = list != null ? list.IndexOf(nb) : -1;
                if (i >= 0) grp.SelectAt(i, SelectionOrigin.Programmatically);
            }
            catch (Exception ex) { ModLog.Warn("DialogNavigator.SyncSelection", ex); }
        }

        static void Announce(int idx)
        {
            string suffix = Keys.Pos(idx, _items.Count);
            var tog = _focused as Toggle;
            if (tog != null)
            {
                MenuAnnouncer.Say(ToggleLabel(tog) + ", " + Keys.RoleThen("checkbox", tog.isOn ? Loc.T("checked") : Loc.T("not checked")) + suffix);
                return;
            }
            var txt = _focused as TMP_Text;
            if (txt != null)
            {
                var s = TextUtil.Clean(txt.text);
                MenuAnnouncer.Say(s + suffix);
                return;
            }
            var piece = _focused as Piece;
            if (piece != null) { MenuAnnouncer.Say(piece.Text + suffix); return; }
            var field = _focused as TMP_InputField;
            if (field != null)
            {
                var v = TextUtil.Clean(field.text);
                MenuAnnouncer.Say(FieldLabel(field) + ", " + Keys.RoleThen("edit box", string.IsNullOrEmpty(v) ? Loc.T("blank") : v) + Loc.F(", {0} to type", Keys.Ok) + suffix);
                return;
            }
            var cb = _focused as YARG.Menu.ColoredButton;
            if (cb != null)
            {
                var lbl = cb.Text != null ? TextUtil.Clean(cb.Text.text) : Describer.GetLabel(cb.gameObject);
                MenuAnnouncer.Say(lbl + Keys.Role("button") + suffix);
                return;
            }
            var comp = _focused as Component;
            if (comp != null) MenuAnnouncer.Say(Describer.GetLabel(comp.gameObject) + Keys.Role("button") + suffix);
        }

        static string FieldLabel(TMP_InputField f)
        {
            var ph = f.placeholder as TMP_Text;
            var s = ph != null ? TextUtil.Clean(ph.text) : "";
            return string.IsNullOrEmpty(s) ? Loc.T("Text") : s;
        }

        static string ToggleLabel(Toggle t)
        {
            for (var tr = t.transform; tr != null; tr = tr.parent)
            {
                if (tr.GetComponent<Dialog>() != null || tr.name == "Base") break;
                foreach (var tmp in tr.GetComponentsInChildren<TMP_Text>(false))
                {
                    var n = tmp.gameObject.name;
                    if ((n == "Label" || n == "Text (TMP)") && tmp.isActiveAndEnabled)
                    {
                        var s = TextUtil.Clean(tmp.text);
                        if (!string.IsNullOrEmpty(s)) return s;
                    }
                }
            }
            return Loc.T("checkbox");
        }
    }
}
