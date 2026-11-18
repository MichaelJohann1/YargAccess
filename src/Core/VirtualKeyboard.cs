using System;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace YargAccess
{
    public static class VirtualKeyboard
    {
        static readonly string[] RowNames = { Loc.T("Lowercase"), Loc.T("Capitals"), Loc.T("Numbers"), Loc.T("Symbols") };
        static readonly string[] Rows =
        {
            "abcdefghijklmnopqrstuvwxyz",
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            "0123456789.-",
            "!?'\",:;_()&@#/+*=%$<>[]~^",
        };

        const float InitialDelay = 0.4f;
        const float RepeatInterval = 0.08f;

        static bool _open;
        static int _row, _col;
        static float _heldAt, _lastFire;
        static int _heldDir;

        public static bool IsOpen => _open;

        public static string Help => Loc.T("On-screen keyboard. Left and Right choose a character, Up and Down change rows. "
            + "A types it, X deletes, Y adds a space, left bumper reads what you typed. Start saves, B cancels.");

        public static string Open(TMP_InputField field)
        {
            _open = true;
            _col = 0;
            _heldDir = 0;
            var ct = field != null ? field.contentType : TMP_InputField.ContentType.Standard;
            _row = (ct == TMP_InputField.ContentType.IntegerNumber || ct == TMP_InputField.ContentType.DecimalNumber) ? 2 : 0;
            return Help + " " + RowNames[_row] + ", " + CharName(Current);
        }

        public static void Close() { _open = false; }

        static char Current => Rows[_row][_col];

        public static int Tick(TMP_InputField field, Action readAll)
        {
            if (!_open) return 0;
            var gp = Gamepad.current;
            if (gp == null || field == null) return 0;
            try
            {
                if (gp.startButton.wasPressedThisFrame) return +1;
                // A Switch Pro controller has A / B and X / Y the other way round, as in the menus.
                bool sw = gp is UnityEngine.InputSystem.Switch.SwitchProControllerHID;
                if ((sw ? gp.buttonSouth : gp.buttonEast).wasPressedThisFrame) return -1;
                if (gp.selectButton.wasPressedThisFrame) { MenuAnnouncer.Say(Help); return 0; }
                if (gp.leftShoulder.wasPressedThisFrame) { readAll(); return 0; }

                int h = Repeat(gp.dpad.left, gp.dpad.right);
                if (h != 0)
                {
                    _col = (_col + h + Rows[_row].Length) % Rows[_row].Length;
                    Speech.Say(CharName(Current), true);
                }
                if (gp.dpad.up.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame)
                {
                    int v = gp.dpad.up.wasPressedThisFrame ? -1 : +1;
                    _row = (_row + v + Rows.Length) % Rows.Length;
                    _col = Mathf.Min(_col, Rows[_row].Length - 1);
                    Speech.Say(RowNames[_row] + ", " + CharName(Current), true);
                }

                if ((sw ? gp.buttonEast : gp.buttonSouth).wasPressedThisFrame) Type(field, Current);
                if ((sw ? gp.buttonWest : gp.buttonNorth).wasPressedThisFrame) Type(field, ' ');
                if ((sw ? gp.buttonNorth : gp.buttonWest).wasPressedThisFrame) Delete(field);
            }
            catch (Exception ex) { ModLog.Warn("VirtualKeyboard.Tick", ex); }
            return 0;
        }

        static int Repeat(ButtonControl left, ButtonControl right)
        {
            float now = Time.unscaledTime;
            int dir = left.isPressed ? -1 : right.isPressed ? +1 : 0;
            if (dir == 0) { _heldDir = 0; return 0; }
            if (dir != _heldDir || left.wasPressedThisFrame || right.wasPressedThisFrame)
            {
                _heldDir = dir; _heldAt = now; _lastFire = now;
                return dir;
            }
            if (now - _heldAt >= InitialDelay && now - _lastFire >= RepeatInterval) { _lastFire = now; return dir; }
            return 0;
        }

        static void Type(TMP_InputField field, char c)
        {
            var t = field.text ?? "";
            if (field.characterLimit > 0 && t.Length >= field.characterLimit) { Captions.Say(Loc.T("Full"), true); return; }
            field.text = t + c;
            field.caretPosition = field.text.Length;
        }

        static void Delete(TMP_InputField field)
        {
            var t = field.text ?? "";
            if (t.Length == 0) { Speech.Say(Loc.T("blank"), true); return; }
            field.text = t.Substring(0, t.Length - 1);
            field.caretPosition = field.text.Length;
        }

        public static string CharName(char c)
        {
            if (char.IsUpper(c)) return Loc.F("capital {0}", c);
            switch (c)
            {
                case ' ': return Loc.T("space");
                case '.': return Loc.T("dot");
                case ',': return Loc.T("comma");
                case '-': return Loc.T("dash");
                case '!': return Loc.T("exclamation");
                case '?': return Loc.T("question");
                case '\'': return Loc.T("apostrophe");
                case '"': return Loc.T("quote");
                case ':': return Loc.T("colon");
                case ';': return Loc.T("semicolon");
                case '_': return Loc.T("underscore");
                case '(': return Loc.T("left paren");
                case ')': return Loc.T("right paren");
                case '&': return Loc.T("and");
                case '@': return Loc.T("at");
                case '#': return Loc.T("hash");
                case '/': return Loc.T("slash");
                case '+': return Loc.T("plus");
                case '*': return Loc.T("star");
                case '=': return Loc.T("equals");
                case '%': return Loc.T("percent");
                case '$': return Loc.T("dollar");
                case '<': return Loc.T("less than");
                case '>': return Loc.T("greater than");
                case '[': return Loc.T("left bracket");
                case ']': return Loc.T("right bracket");
                case '~': return Loc.T("tilde");
                case '^': return Loc.T("caret");
                default: return c.ToString();
            }
        }

        static string _drawn;
        public static void OnGUI()
        {
            if (!_open) { _drawn = null; return; }
            try
            {
                var row = Rows[_row];
                var shown = "<noparse>" + row.Substring(0, _col) + "</noparse> <color=#FFFFFF><b>[<noparse>" + row[_col]
                    + "</noparse>]</b></color> <noparse>" + row.Substring(_col + 1) + "</noparse>\n" + Loc.T("A type   X delete   Y space   LB read   Start save   B cancel");
                string key = _row + "|" + _col;
                if (key == _drawn) return;
                _drawn = key;
                Tooltips.Show(shown, Loc.F("On-screen keyboard: {0}", RowNames[_row]), () => _open, true, false);
            }
            catch { }
        }
    }
}
