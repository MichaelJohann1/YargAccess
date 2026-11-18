using System;
using MelonLoader;
using TMPro;
using UnityEngine.InputSystem;

namespace YargAccess
{
    public static class TextEdit
    {
        static TMP_InputField _field;
        static string _label;
        static string _original;
        static string _lastText;
        static bool _fresh;
        static Action<string> _commit;
        static Action _cancel;

        static bool _keysTold;

        public static bool IsActive
        {
            get
            {
                if (_field == null) return false;
                if (!_field.gameObject.activeInHierarchy) { Clear(); return false; }
                return true;
            }
        }

        public static void Begin(TMP_InputField field, string label, Action<string> commit, Action cancel = null)
        {
            if (field == null) return;
            _field = field;
            _label = label;
            _original = field.text ?? "";
            _lastText = _original;
            _fresh = true;
            _commit = commit;
            _cancel = cancel;
            try
            {
                field.Select();
                field.ActivateInputField();
            }
            catch (Exception ex) { ModLog.Warn("TextEdit.Begin", ex); }
            var cur = TextUtil.Clean(_original);
            var head = Loc.F("Editing {0}, {1}.", label, string.IsNullOrEmpty(cur) ? Loc.T("blank") : cur) + " ";
            if (InputDriver.GamepadLastUsed && Gamepad.current != null) MenuAnnouncer.Focused(head + VirtualKeyboard.Open(field));
            else
            {
                MenuAnnouncer.Focused(_keysTold ? head.TrimEnd() : head + Loc.T("Type a new value to replace it, or Backspace to edit it. Enter to save, Escape to cancel."));
                _keysTold = true;
            }
        }

        public static bool Tick()
        {
            if (!IsActive) return false;
            var kb = Keyboard.current;
            try
            {
                if (kb != null)
                {
                    if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { Commit(); return true; }
                    if (kb.escapeKey.wasPressedThisFrame) { Cancel(); return true; }
                    if (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) { ReadAll(); return true; }
                    if (kb.f1Key.wasPressedThisFrame)
                    {
                        MenuAnnouncer.Say(Loc.F("Editing {0}. Type a new value. Up or Down reads it. Enter saves, Escape cancels.", _label));
                        return true;
                    }
                }
                int vk = VirtualKeyboard.Tick(_field, ReadAll);
                if (vk > 0) { Commit(); return true; }
                if (vk < 0) { Cancel(); return true; }

                if (!_field.isFocused) _field.ActivateInputField();
                EchoChanges();
            }
            catch (Exception ex) { ModLog.Warn("TextEdit.Tick", ex); }
            return true;
        }

        public static void Commit()
        {
            if (_field == null) return;
            var f = _field;
            var commit = _commit;
            var label = _label;
            Clear();
            try { if (f.isFocused) f.DeactivateInputField(); } catch { }
            try { f.onEndEdit.Invoke(f.text); } catch (Exception ex) { ModLog.Warn("TextEdit.Commit end edit", ex); }
            if (commit != null)
            {
                try { commit(f.text); }
                catch (Exception ex) { ModLog.Warn("TextEdit.Commit", ex); }
            }
            else
            {
                var v = TextUtil.Clean(f.text);
                MenuAnnouncer.Say(label + ", " + (string.IsNullOrEmpty(v) ? Loc.T("blank") : v));
            }
        }

        public static void Cancel()
        {
            if (_field == null) return;
            var f = _field;
            var original = _original;
            var cancel = _cancel;
            Clear();
            try
            {
                f.text = original;
                if (f.isFocused) f.DeactivateInputField();
                f.onEndEdit.Invoke(f.text);
            }
            catch (Exception ex) { ModLog.Warn("TextEdit.Cancel", ex); }
            try { cancel?.Invoke(); } catch { }
            MenuAnnouncer.SayShown(Loc.T("Canceled"));
        }

        static void ReadAll()
        {
            var v = TextUtil.Clean(_field.text);
            MenuAnnouncer.Say(string.IsNullOrEmpty(v) ? Loc.T("blank") : v);
        }

        static void EchoChanges()
        {
            var t = _field.text ?? "";
            if (t == _lastText) return;
            if (_fresh)
            {
                _fresh = false;
                var orig = _original ?? "";
                if (orig.Length > 0 && t.Length > orig.Length && t.StartsWith(orig))
                {
                    _lastText = "";
                    _field.text = t.Substring(orig.Length);
                    _field.caretPosition = _field.text.Length;
                    t = _field.text;
                }
            }
            var before = _lastText ?? "";
            _lastText = t;
            if (t.Length == before.Length + 1 && t.StartsWith(before)) { Speech.Say(CharName(t[t.Length - 1]), true); return; }
            if (t.Length + 1 == before.Length && before.StartsWith(t)) { Speech.Say(Loc.F("deleted {0}", CharName(before[before.Length - 1])), true); return; }
            Speech.Say(string.IsNullOrEmpty(t) ? Loc.T("blank") : t, true);
        }

        static string CharName(char c)
        {
            switch (c)
            {
                case ' ': return Loc.T("space");
                case '.': return Loc.T("dot");
                case ',': return Loc.T("comma");
                case '-': return Loc.T("minus");
                case '#': return Loc.T("hash");
                default: return c.ToString();
            }
        }

        static void Clear()
        {
            VirtualKeyboard.Close();
            _field = null;
            _commit = null;
            _cancel = null;
            _label = null;
        }
    }
}
