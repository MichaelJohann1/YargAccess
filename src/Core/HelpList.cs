using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace YargAccess
{
    public static class HelpList
    {
        static readonly List<string> _lines = new List<string>();
        static int _idx;
        static int _openedFrame = -1;
        static string _title;

        public static bool IsOpen { get; private set; }

        // firstIsTitle: the first line is the help's title (ContextHelp says so, since a translated title may
        // hold ": " too); null keeps the old rule, a first line with no ": " is the title.
        public static void Open(List<string> lines, string device, bool? firstIsTitle = null)
        {
            _lines.Clear();
            string title = null;
            foreach (var l in lines)
            {
                var line = (l ?? "").Trim().TrimEnd('.');
                if (line.Length == 0) continue;
                if (title == null && _lines.Count == 0 && (firstIsTitle ?? line.IndexOf(": ", StringComparison.Ordinal) < 0)) { title = line; continue; }
                // The same line twice (a key YARG names and the mod names too) is read once.
                if (_lines.Exists(x => string.Equals(x, line, StringComparison.OrdinalIgnoreCase))) continue;
                _lines.Add(line);
            }
            bool inSong = GameplayContext.InGameplay;
            if (device == "keyboard") _lines.Add(inSong ? Loc.T("Backspace or F1: close this help") : Loc.T("Escape, Backspace or F1: close this help"));
            else if (device == "controller") _lines.Add(Loc.T("B or View: close this help"));
            else _lines.Add(Loc.T("Red fret or Select: close this help"));
            _idx = 0;
            _title = (title != null ? Loc.F("{0} help", title) : Loc.T("Help"));
            IsOpen = true;
            _openedFrame = Time.frameCount;
            string read = device == "keyboard" ? Loc.T("Up and Down") : device == "controller" ? Loc.T("D-pad up and down") : Loc.T("Strum up and down");
            int n = _lines.Count;
            string head;
            if (device == "keyboard")
                head = title != null ? Loc.F("{0}, help for the keyboard, {1} lines.", title, n) : Loc.F("Help for the keyboard, {0} lines.", n);
            else if (device == "controller")
                head = title != null ? Loc.F("{0}, help for the controller, {1} lines.", title, n) : Loc.F("Help for the controller, {0} lines.", n);
            else
                head = title != null ? Loc.F("{0}, help for the guitar, {1} lines.", title, n) : Loc.F("Help for the guitar, {0} lines.", n);
            Captions.SayIn(_title ?? Loc.T("Help"), () => IsOpen, head + " " + Loc.F("{0} to read.", read) + " " + Line(0), true);
        }

        static string Line(int i) { return _lines[i] + Keys.Pos(i, _lines.Count); }

        static void Move(int dir)
        {
            int n = Mathf.Clamp(_idx + dir, 0, _lines.Count - 1);
            _idx = n;
            Captions.SayIn(_title ?? Loc.T("Help"), () => IsOpen, Line(_idx), true);
        }

        static void Close()
        {
            IsOpen = false;
            _lines.Clear();
            var g = YARG.Menu.Navigation.NavigationGroup.CurrentNavigationGroup;
            var sel = g != null ? g.SelectedBehaviour : null;
            if (ShowPickerNavigator.IsActive) { ShowPickerNavigator.Move(0); return; }
            if (ScoreScreenNavigator.IsActive) { ScoreScreenNavigator.Move(0); return; }
            if (sel != null && sel.isActiveAndEnabled && !DialogNavigator.IsActive && !ShowPickerNavigator.IsActive
                && !ProfilesNavigator.IsActive && !ProfileInfoNavigator.IsActive)
                MenuAnnouncer.Announce(sel, true);
            else Patches.PushMenuPatch.AnnounceCurrent();
        }

        public static bool Tick()
        {
            if (!IsOpen) return false;
            if (Time.frameCount == _openedFrame) return true;
            try
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.upArrowKey.wasPressedThisFrame) Move(-1);
                    if (kb.downArrowKey.wasPressedThisFrame) Move(+1);
                    if (kb.homeKey.wasPressedThisFrame) { _idx = 0; Move(0); }
                    if (kb.endKey.wasPressedThisFrame) { _idx = _lines.Count - 1; Move(0); }
                    if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Move(0);
                    if (kb.backspaceKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame
                        || (kb.escapeKey.wasPressedThisFrame && !GameplayContext.InGameplay)) { Close(); return true; }
                }

                var gp = Gamepad.current;
                if (gp != null)
                {
                    if (gp.dpad.up.wasPressedThisFrame) Move(-1);
                    if (gp.dpad.down.wasPressedThisFrame) Move(+1);
                    if (gp.buttonSouth.wasPressedThisFrame) Move(0);
                    if (gp.buttonEast.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame) { Close(); return true; }
                }

                foreach (var dev in InputSystem.devices)
                {
                    if (dev == null || dev is Keyboard || dev is Mouse || dev is Gamepad) continue;
                    if (dev.GetType().Name.IndexOf("Guitar", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var dpad = dev.TryGetChildControl<DpadControl>("dpad");
                    if (dpad != null && dpad.up.wasPressedThisFrame) Move(-1);
                    if (dpad != null && dpad.down.wasPressedThisFrame) Move(+1);
                    var green = dev.TryGetChildControl<ButtonControl>("greenFret");
                    if (green != null && green.wasPressedThisFrame) Move(0);
                    var red = dev.TryGetChildControl<ButtonControl>("redFret");
                    var sel = dev.TryGetChildControl<ButtonControl>("selectButton");
                    if ((red != null && red.wasPressedThisFrame) || (sel != null && sel.wasPressedThisFrame)) { Close(); return true; }
                }
            }
            catch (Exception ex) { ModLog.Warn("HelpList.Tick", ex); IsOpen = false; }
            return true;
        }

        public static void Reset() { IsOpen = false; _lines.Clear(); }
    }
}
