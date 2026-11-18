using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine.InputSystem;
using YARG.Core.Input;
using YARG.Menu.Navigation;

namespace YargAccess
{
    public static class ContextHelp
    {
        static FieldInfo _schemeStackF;

        public enum Device { Keyboard, Gamepad, Guitar }

        static Device _dev;
        static InputDevice _input;

        public static void Speak()
        {
            _dev = Device.Keyboard;
            _input = null;
            try { MenuAnnouncer.SayShown(string.Join(". ", Build().ToArray()) + "."); }
            catch (Exception ex)
            {
                ModLog.Warn("ContextHelp", ex);
                MenuAnnouncer.SayShown("Up and Down to move. Enter to activate. Escape to go back.");
            }
        }

        public static void Open(Device dev, InputDevice input = null)
        {
            _dev = dev;
            _input = input;
            List<string> lines;
            try { lines = Build(); }
            catch (Exception ex)
            {
                ModLog.Warn("ContextHelp", ex);
                lines = new List<string> { Move() + ": move", Ok() + ": activate", BackKey() + ": go back" };
            }
            HelpList.Open(lines, DeviceName());
        }

        static string DeviceName()
        {
            switch (_dev)
            {
                case Device.Gamepad: return "controller";
                case Device.Guitar:  return "guitar";
                default:             return "keyboard";
            }
        }

        static string Move()    { return _dev == Device.Keyboard ? "Up and Down arrows" : _dev == Device.Gamepad ? "D-pad up and down" : "Strum up and down"; }
        static string Side()    { return _dev == Device.Keyboard ? "Left and Right arrows" : "D-pad left and right"; }
        static string Ok()      { return _dev == Device.Keyboard ? "Enter" : _dev == Device.Gamepad ? "A" : "Green fret"; }
        static string BackKey() { return _dev == Device.Keyboard ? "Escape or Backspace" : _dev == Device.Gamepad ? "B" : "Red fret"; }
        static string Tabs()    { return _dev == Device.Keyboard ? "Tab and Shift+Tab" : _dev == Device.Gamepad ? "LB and RB" : "Yellow and Orange frets"; }
        static string HelpKey() { return _dev == Device.Keyboard ? "F1" : _dev == Device.Gamepad ? "View" : "Select"; }

        static string ColorKey(MenuAction a)
        {
            if (_dev == Device.Keyboard)
            {
                switch (a)
                {
                    case MenuAction.Green:  return "Enter or 1";
                    case MenuAction.Red:    return "Escape, Backspace or 2";
                    case MenuAction.Yellow: return "3";
                    case MenuAction.Blue:   return "4";
                    case MenuAction.Orange: return "5";
                    case MenuAction.Start:  return "Enter";
                    case MenuAction.Select: return "Backspace";
                    default:                return a.ToString();
                }
            }
            var bound = BoundName(a);
            if (bound != null) return bound;
            if (_dev == Device.Gamepad)
            {
                switch (a)
                {
                    case MenuAction.Green:  return "A";
                    case MenuAction.Red:    return "B";
                    case MenuAction.Yellow: return "Y";
                    case MenuAction.Blue:   return "X";
                    case MenuAction.Orange: return "LB";
                    case MenuAction.Start:  return "Start";
                    case MenuAction.Select: return "View";
                    default:                return a.ToString();
                }
            }
            switch (a)
            {
                case MenuAction.Start:  return "Start";
                case MenuAction.Select: return "Select";
                default:                return a + " fret";
            }
        }

        static string HoldKey(MenuAction a)
        {
            var k = ColorKey(a);
            if (_dev == Device.Keyboard && a == MenuAction.Green) k = "1";
            return "Hold " + k;
        }

        static string BoundName(MenuAction a)
        {
            if (_input == null) return null;
            try
            {
                foreach (var player in YARG.Player.PlayerContainer.Players)
                {
                    bool has = false;
                    foreach (var d in player.Bindings.InputDevices) if (d == _input) { has = true; break; }
                    if (!has) continue;
                    var names = new List<string>();
                    foreach (var cb in player.Bindings.MenuBindings)
                    {
                        if (cb.Action != (int) a) continue;
                        var list = cb.GetType().GetProperty("Bindings")?.GetValue(cb, null) as System.Collections.IEnumerable;
                        if (list == null) continue;
                        foreach (var single in list)
                        {
                            var ctl = single.GetType().GetProperty("Control")?.GetValue(single, null) as InputControl;
                            if (ctl == null || ctl.device != _input) continue;
                            var n = Friendly(string.IsNullOrEmpty(ctl.displayName) ? ctl.name : ctl.displayName);
                            if (!names.Contains(n)) names.Add(n);
                        }
                    }
                    if (names.Count == 0) return "unbound";
                    return string.Join(" or ", names.ToArray());
                }
            }
            catch (Exception ex) { ModLog.Warn("ContextHelp.BoundName", ex); }
            return null;
        }

        static string Friendly(string n)
        {
            switch (n)
            {
                case "Button South":   return "A";
                case "Button East":    return "B";
                case "Button West":    return "X";
                case "Button North":   return "Y";
                case "Left Shoulder":  return "LB";
                case "Right Shoulder": return "RB";
                case "Left Trigger":   return "LT";
                case "Right Trigger":  return "RT";
                case "Select":         return _dev == Device.Gamepad ? "View" : n;
                default:               return n;
            }
        }

        static bool ModDriven()
        {
            if (_dev == Device.Keyboard) return true;
            try { if (YARG.Player.PlayerContainer.Players.Count == 0) return true; } catch { }
            return InputDriver.PadOwnedByMod();
        }

        static List<string> Build()
        {
            var lines = new List<string>();
            Action<string, string> L = (keys, what) => lines.Add(keys + ": " + what);
            bool kb = _dev == Device.Keyboard;

            if (ShowPickerNavigator.IsActive)
            {
                lines.Add("Play A Show song picker. Five choices, one per colour");
                lines.Add("When the timer runs out, the most voted choice joins the show and new choices appear. With no votes, the choices just change");
                L(Move(), "read the choices");
                L(Side(), "seconds left before the choices change");
                if (kb)
                {
                    L("Enter", "add the choice you are on");
                    L("1, 2, 3, 4 and 5", "add the Green, Red, Yellow, Blue or Orange choice");
                    L("Escape or Backspace", "done picking");
                }
                else
                {
                    foreach (var c in new[] { MenuAction.Green, MenuAction.Red, MenuAction.Yellow, MenuAction.Blue, MenuAction.Orange })
                        L(ColorKey(c), "vote for the " + c + " choice");
                    L(HelpKey(), "done picking");
                }
                return lines;
            }

            if (DialogNavigator.IsActive)
            {
                lines.Add("Dialog");
                L(Move(), "move through the title, message, checkboxes and buttons");
                L(Ok(), "activate a button or flip a checkbox");
                L(BackKey(), "close the dialog");
                AddReviewKeys(L);
                L(HelpKey(), HelpWhat());
                return lines;
            }

            int cal = CalibratorAnnouncer.IsActive ? CalibratorAnnouncer.Phase() : -1;
            if (cal >= 0)
            {
                lines.Add("Audio calibrator");
                var tap = kb ? "Space or Enter" : Ok();
                if (cal == 0) { L(Ok(), "start audio calibration: you will hear clicks"); L(BackKey(), "return to Settings"); }
                else if (cal == 1) { L(tap, "press when you are ready, then on every click you hear"); L(BackKey(), "stop"); }
                else if (cal == 2) { L(tap, "press on every click"); L(BackKey(), "stop"); }
                else L(BackKey(), "return to the start screen");
                AddReviewKeys(L);
                L(HelpKey(), HelpWhat());
                return lines;
            }

            if (FileBrowserNavigator.IsActive)
            {
                lines.Add("Folder and file picker");
                L(Move(), "move through the folder: choose this folder, the folder above, its folders and files, drives and places, cancel");
                if (_dev == Device.Keyboard) L("Home and End", "first and last row");
                L(Ok(), "open a folder, choose a file, or choose this folder");
                L(BackKey(), "the folder above; at the top of a drive, the drives and places list; there, cancel");
                AddReviewKeys(L);
                L(HelpKey(), HelpWhat());
                return lines;
            }

            if (CreditsNavigator.IsActive)
            {
                lines.Add("Credits");
                L(Move(), "read the entries one at a time");
                if (_dev == Device.Keyboard) L("Home and End", "first and last entry");
                L(Ok(), "read the entry again");
                L(BackKey(), "go back");
                AddMusicKeys(L);
                return lines;
            }

            if (ScoreScreenNavigator.IsActive)
            {
                lines.Add("Score screen");
                L(Move(), "read the stats");
                L(Side(), "switch between players in a band");
                AddSchemeColors(L, false);
                AddReviewKeys(L);
                L(HelpKey(), HelpWhat());
                return lines;
            }

            if (PracticeSectionNavigator.IsActive)
            {
                lines.Add("Choose practice sections");
                L(Move(), "move through the song's sections");
                if (!kb) L(Tabs(), "also move through the sections");
                L(Ok(), "the first press sets the first section, a second press on the last section starts");
                L(kb ? "Backspace" : BackKey(), "clear the first section, or leave practice if none is set");
                AddReviewKeys(L);
                L(HelpKey(), HelpWhat());
                return lines;
            }

            if (GameplayContext.InGameplay)
            {
                bool practice = PracticeAssist.InPractice;
                if (!GameplayContext.Paused)
                {
                    lines.Add(practice ? "Practice song" : "Song");
                    L("Escape", "pause");
                    if (practice) AddPracticeKeys(L);
                    return lines;
                }
                lines.Add(Menu.CurrentName + " menu");
                L(Move(), "move");
                L(Ok(), "activate, or flip a checkbox");
                if (practice && Menu.CurrentName == "Practice") L(Side(), "move the selected A or B loop point");
                else L(Side(), "change a value");
                L(kb ? "Backspace" : BackKey(), "go back, or resume from the first pause menu");
                L(kb ? "Escape" : "Start", "resume the song");
                AddReviewKeys(L);
                L(HelpKey(), HelpWhat());
                return lines;
            }

            if (ProfileInfoNavigator.IsActive)
            {
                if (ProfileInfoNavigator.ChoosingControl)
                {
                    lines.Add("Choose the control to bind: several responded at once, such as a stick's direction and its axis");
                    L(Move(), "read them");
                    L(Ok(), "bind the one you are on");
                    L(BackKey(), "cancel");
                    return lines;
                }
                lines.Add("Profile binding editor");
                L(Tabs(), "switch between the Overview and Binds tabs");
                L(Move(), "move");
                L(Ok(), "choose a game mode, start Add New or Quick Bind, or remove a binding (press twice)");
                lines.Add("The Binds tab lists the game mode's binds, then the menu binds. Each action reads what it is bound to, then its binds and Add New");
                lines.Add("After Add New, let go of every button, then press the control to bind. Until it is set, every button is the bind: help, music and text review keys do nothing. Escape or Backspace on the keyboard cancels");
                lines.Add("Quick Bind on the Overview tab asks for each bind in turn and says what was set");
                lines.Add("The menu binds end with the text review actions: Add New there binds your own button, or a pair, hold one and press another");
                L(BackKey(), "go back");
                AddReviewKeys(L);
                L(HelpKey(), HelpWhat());
                return lines;
            }

            if (ProfilesNavigator.IsActive && !TextEdit.IsActive)
            {
                var who = ProfilesNavigator.ProfileNameOr("this profile");
                if (ProfilesNavigator._inSettings)
                {
                    lines.Add("Profile settings for " + who);
                    L(Move(), "move");
                    L(Side(), "change a choice or a number");
                    L(Ok(), "flip a checkbox, or type a number");
                    L(BackKey(), "return to the action toolbar");
                }
                else if (ProfilesNavigator._inDevices)
                {
                    lines.Add("Devices of " + who);
                    L(Move(), "read them");
                    L(BackKey(), "return to the action toolbar");
                    lines.Add("Add Device and Remove Device on the toolbar change them");
                }
                else
                {
                    lines.Add("Profiles");
                    L(Move(), "choose a profile");
                    L(Ok(), "on a connected profile, edit its binds; on a disconnected one, connect it; on the toolbar, do the action");
                    L(Tabs(), "move through the actions for the selected profile");
                    lines.Add("The actions are " + string.Join(", ", ProfilesNavigator.Actions().ToArray()));
                    L(Side(), "on Instrument, change the instrument");
                    L(BackKey(), "go back");
                }
                AddReviewKeys(L);
                L(HelpKey(), HelpWhat());
                return lines;
            }

            if (PresetsNavigator.IsActive && PresetsNavigator.InZone)
            {
                lines.Add("Presets");
                L(Move(), "move through preset type, preset, and the actions" + (PresetsNavigator.GroupEmpty ? "" : ", then the preset's own settings"));
                L(Side(), "change the preset type or the preset");
                L(Ok(), "do the action");
                lines.Add("Built-in presets can't be changed: Copy one to make an editable preset");
                L(Tabs(), "switch tabs");
                L(BackKey(), "go back");
                AddReviewKeys(L);
                L(HelpKey(), HelpWhat());
                return lines;
            }

            var page = SoundGlossaryPage.Title;
            if (page != null)
            {
                lines.Add(page + " page");
                L(Move(), "move");
                if (page == "Sound Glossary")
                {
                    L(Side(), "play each sound of this list and say what it means");
                    L(Ok(), "play it again");
                }
                else L(Side(), "change the volume; the sound plays at the new level");
                L(BackKey(), "return to the YargAccess tab");
                AddMusicKeys(L);
                return lines;
            }

            var scheme = ActiveScheme();
            lines.Add(OverlayName() ?? (string.IsNullOrEmpty(Menu.CurrentName) ? "Menu" : Menu.CurrentName + " menu"));
            bool mod = ModDriven();
            L(Move(), "move");

            var cur = CurrentSetting();
            if (cur != null && SettingsAccess.IsAdjustable(cur)) L(Side(), "change the value");
            else if (CurrentIsButtonRow()) L(Side(), "choose a button in this row");
            else
            {
                var left = SchemeName(scheme, MenuAction.Left);
                if (left != null && !InputDriver.SettingsOpen()) L(Side(), left);
            }
            if (HasTabs()) L(Tabs(), "switch tabs");

            if (cur != null && SettingsAccess.IsEditable(cur))
                L(Ok(), kb ? "type a value, or browse for a file" : "open the on-screen keyboard to type a value");
            else
                L(mod ? Ok() : ColorKey(MenuAction.Green), SchemeName(scheme, MenuAction.Green) ?? "activate");
            AddSchemeColors(L, true);
            if (kb && cur != null && SettingsAccess.IsEditable(cur)) L("Delete", "remove an image file setting");

            if (SettingsExtras.IsActive) lines.Add("At the top of the list are Scan Songs, to rescan, and Add New Folder");
            if (Menu.CurrentName == "Music Library" && OverlayName() == null)
                lines.Add(kb ? "Delete deletes the song you are on, after asking" : (_dev == Device.Guitar ? "Select and strum down" : "LT") + " deletes the song you are on, after asking");
                lines.Add("A song is read as its name, artist, stars and favourite. Its details, the instruments with their difficulty, album, year, genre, length, charter, source and rating, are in the text review: " + ReviewKeysShort());
            if (HistoryNavigator.IsActive && OverlayName() == null)
            {
                L(ColorKey(MenuAction.Blue), "play this song yourself, in your Play Style");
                L(Side(), "jump to the previous or next day");
                if (kb) lines.Add("Home and End go to the first and last entry");
                if (kb) lines.Add("E exports the selected entry's replay as a file, and Delete deletes the entry after asking");
                else lines.Add((_dev == Device.Guitar ? "Select and strum down" : "LT") + " opens a list for the entry: Export Replay and Delete Entry");
                lines.Add("Enter watches the replay as it was played. Play With Replay plays along with it, your part in your Play Style. Each entry reads everything: stars, part, score, hits and misses, play style and when. Up from the first entry on the Import tab reaches Import Replay. While a replay plays, the keys control it (seek, speed, pause) and F1 lists them. The History and Replays tutorial explains replays.");
            }
            if (DifficultySelectExtras.IsActive) lines.Add("Below the last row is the song speed. " + Side() + " change it");

            L(mod ? BackKey() : ColorKey(MenuAction.Red), "go back");
            AddMusicKeys(L);
            return lines;
        }

        static bool kb() { return _dev == Device.Keyboard; }
        static string HelpWhat() { return kb() ? "this help" : "a press opens this help"; }

        static void AddSchemeColors(Action<string, string> L, bool skipGreenRed)
        {
            var scheme = ActiveScheme();
            if (scheme == null || scheme.Entries == null) return;
            var seen = new HashSet<MenuAction>();
            foreach (var e in scheme.Entries)
            {
                var a = e.Action;
                if (a == MenuAction.Up || a == MenuAction.Down || a == MenuAction.Left || a == MenuAction.Right) continue;
                if (skipGreenRed && a == MenuAction.Red) continue;
                if (!seen.Add(a)) continue;
                var tap = SafeName(e);
                string hold = e.HasHoldHandler ? SafeName(e, true) : null;
                bool tapUseful = !string.IsNullOrEmpty(tap) && !(hold != null && string.Equals(tap, hold, StringComparison.OrdinalIgnoreCase));
                if (skipGreenRed && a == MenuAction.Green) tapUseful = false;
                if (tapUseful && a == MenuAction.Select && !kb()) { L("Hold " + ColorKey(a), tap); continue; }
                if (tapUseful) L(ColorKey(a), tap);
                if (!string.IsNullOrEmpty(hold)) L(HoldKey(a), hold);
            }
        }

        static string OverlayName()
        {
            try
            {
                if (FiltersNavigator.IsActive) return "Filters";
                var popup = UnityEngine.Object.FindObjectOfType<YARG.Menu.MusicLibrary.PopupMenu>();
                if (popup != null && popup.gameObject.activeInHierarchy) return "More Options";
            }
            catch { }
            return null;
        }

        public static bool HasSchemeAction(MenuAction a)
        {
            try { return SchemeName(ActiveScheme(), a) != null; } catch { return false; }
        }

        static string SchemeName(NavigationScheme scheme, MenuAction a)
        {
            if (scheme == null || scheme.Entries == null) return null;
            foreach (var e in scheme.Entries)
                if (e.Action == a) { var n = SafeName(e); if (!string.IsNullOrEmpty(n)) return n; }
            return null;
        }

        static void AddMusicKeys(Action<string, string> L)
        {
            switch (_dev)
            {
                case Device.Keyboard:
                    L("F5", "say what music is playing");
                    L("F6", "play or pause the music");
                    L("F2", "read YARG's message of the day, what is new in this version");
                    L("F7", "previous song");
                    L("F8", "next song");
                    break;
                case Device.Gamepad:
                    L("RT", "read YARG's message of the day, what is new in this version");
                    L("Left stick click", "play or pause the music; hold it for the previous song");
                    L("Right stick click", "say what music is playing; hold it for the next song");
                    break;
                case Device.Guitar:
                    L("Select and strum up", "read YARG's message of the day, what is new in this version");
                    L("Select and Red", "previous song");
                    L("Select and Orange", "next song");
                    break;
            }
            AddReviewKeys(L);
            L(HelpKey(), HelpWhat());
        }

        static void AddReviewKeys(Action<string, string> L)
        {
            switch (_dev)
            {
                case Device.Keyboard:
                    L("Left and right brackets", "step back and forward through the last thing said, piece by piece");
                    L("Backslash", "read the last thing said again in full");
                    break;
                case Device.Gamepad:
                    L("Right stick left and right", "step back and forward through the last thing said, piece by piece");
                    L("Right stick down", "read the last thing said again in full");
                    break;
                default:
                    L("Hold Select and press Yellow or Blue", "step back and forward through the last thing said, piece by piece");
                    L("Hold Select and press Green", "read the last thing said again in full");
                    break;
            }
            if (_dev != Device.Keyboard) L("Text review buttons bound in the binding editor's menu binds", "work the same way");
        }

        static string ReviewKeysShort()
        {
            switch (_dev)
            {
                case Device.Gamepad: return "right stick left and right move through them, right stick down reads them all";
                case Device.Guitar:  return "hold Select and press Yellow or Blue to move through them, Select and Green for all";
                default:             return "left and right brackets move through them, backslash reads them all";
            }
        }

        static void AddPracticeKeys(Action<string, string> L)
        {
            L("F2", "speed, section and hit percentage");
            L("F3 and F4", "slower and faster");
            L("F8", "restart the section");
            L("F9", "section announcements on or off");
        }

        public static string ActionName(string action, bool hold = false)
        {
            try
            {
                var scheme = ActiveScheme();
                if (scheme == null || scheme.Entries == null) return null;
                var ma = (MenuAction) Enum.Parse(typeof(MenuAction), action);
                foreach (var e in scheme.Entries)
                    if (e.Action == ma) return SafeName(e, hold);
            }
            catch { }
            return null;
        }

        static string SafeName(NavigationScheme.Entry e, bool hold = false)
        {
            try
            {
                var raw = e.DisplayName;
                if (string.IsNullOrWhiteSpace(raw)) return null;

                var lines = TextUtil.CleanLines(raw);
                string s = null;
                if (lines.Length > 1)
                {
                    foreach (var l in lines)
                    {
                        bool isHoldLine = l.IndexOf("(hold)", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (isHoldLine == hold) { s = l; break; }
                    }
                }
                if (s == null) s = lines.Length > 0 ? lines[0] : TextUtil.Clean(raw);

                s = HoldMarker.Replace(s, "").Trim();
                if (s.Contains(".")) s = s.Substring(s.LastIndexOf('.') + 1);
                return Humanize(s);
            }
            catch { return null; }
        }

        static readonly System.Text.RegularExpressions.Regex HoldMarker =
            new System.Text.RegularExpressions.Regex(@"\(\s*hold\s*\)",
                System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        static string Humanize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(s[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }

        static NavigationScheme ActiveScheme()
        {
            try
            {
                var nav = UnityEngine.Object.FindObjectOfType<Navigator>();
                if (nav == null) return null;
                if (_schemeStackF == null) _schemeStackF = typeof(Navigator).GetField("_schemeStack", BindingFlags.NonPublic | BindingFlags.Instance);
                var stack = _schemeStackF?.GetValue(nav) as Stack<NavigationScheme>;
                if (stack == null || stack.Count == 0) return null;
                return stack.Peek();
            }
            catch { return null; }
        }

        static YARG.Menu.Settings.Visuals.BaseSettingVisual CurrentSetting()
        {
            try
            {
                var g = NavigationGroup.CurrentNavigationGroup;
                return g != null ? SettingsAccess.GetVisual(g.SelectedBehaviour) : null;
            }
            catch { return null; }
        }

        static bool CurrentIsButtonRow()
        {
            try
            {
                var g = NavigationGroup.CurrentNavigationGroup;
                var sel = g != null ? g.SelectedBehaviour : null;
                return sel != null && SettingsAccess.IsButtonRow(sel) && SettingsAccess.RowButtons(sel).Count > 1;
            }
            catch { return false; }
        }

        static bool HasTabs()
        {
            foreach (var ng in UnityEngine.Object.FindObjectsOfType<NavigationGroup>())
                if (ng.gameObject.name == "Tabs" && ng.Count > 1) return true;
            return false;
        }
    }
}
