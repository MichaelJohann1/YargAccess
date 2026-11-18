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
        static readonly string[] RowNames = { "Lowercase", "Capitals", "Numbers", "Symbols" };
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

        public const string Help = "On-screen keyboard. Left and Right choose a character, Up and Down change rows. "
            + "A types it, X deletes, Y adds a space, left bumper reads what you typed. Start saves, B cancels.";

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
            if (field.characterLimit > 0 && t.Length >= field.characterLimit) { Captions.Say("Full", true); return; }
            field.text = t + c;
            field.caretPosition = field.text.Length;
        }

        static void Delete(TMP_InputField field)
        {
            var t = field.text ?? "";
            if (t.Length == 0) { Speech.Say("blank", true); return; }
            field.text = t.Substring(0, t.Length - 1);
            field.caretPosition = field.text.Length;
        }

        public static string CharName(char c)
        {
            if (char.IsUpper(c)) return "capital " + c;
            switch (c)
            {
                case ' ': return "space";
                case '.': return "dot";
                case ',': return "comma";
                case '-': return "dash";
                case '!': return "exclamation";
                case '?': return "question";
                case '\'': return "apostrophe";
                case '"': return "quote";
                case ':': return "colon";
                case ';': return "semicolon";
                case '_': return "underscore";
                case '(': return "left paren";
                case ')': return "right paren";
                case '&': return "and";
                case '@': return "at";
                case '#': return "hash";
                case '/': return "slash";
                case '+': return "plus";
                case '*': return "star";
                case '=': return "equals";
                case '%': return "percent";
                case '$': return "dollar";
                case '<': return "less than";
                case '>': return "greater than";
                case '[': return "left bracket";
                case ']': return "right bracket";
                case '~': return "tilde";
                case '^': return "caret";
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
                    + "</noparse>]</b></color> <noparse>" + row.Substring(_col + 1) + "</noparse>\nA type   X delete   Y space   LB read   Start save   B cancel";
                string key = _row + "|" + _col;
                if (key == _drawn) return;
                _drawn = key;
                Tooltips.Show(shown, "On-screen keyboard: " + RowNames[_row], () => _open, true, false);
            }
            catch { }
        }
    }
}
