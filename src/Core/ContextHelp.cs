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
                MenuAnnouncer.SayShown(Loc.T("Up and Down to move. Enter to activate. Escape to go back."));
            }
        }

        public static void Open(Device dev, InputDevice input = null)
        {
            _dev = dev;
            _input = input;
            List<string> lines;
            bool titled;
            try { lines = Build(); titled = _firstIsTitle; }
            catch (Exception ex)
            {
                ModLog.Warn("ContextHelp", ex);
                lines = new List<string> { Move() + ": " + Loc.T("move"), Ok() + ": " + Loc.T("activate"), BackKey() + ": " + Loc.T("go back") };
                titled = false;
            }
            HelpList.Open(lines, DeviceName(), titled);
        }

        static string DeviceName()
        {
            switch (_dev)
            {
                case Device.Gamepad: return Loc.T("controller");
                case Device.Guitar:  return Loc.T("guitar");
                default:             return Loc.T("keyboard");
            }
        }

        static string Move()    { return _dev == Device.Keyboard ? Loc.T("Up and Down arrows") : _dev == Device.Gamepad ? Loc.T("D-pad up and down") : Loc.T("Strum up and down"); }
        static string Side()    { return _dev == Device.Keyboard ? Loc.T("Left and Right arrows") : Loc.T("D-pad left and right"); }
        static string Ok()      { return _dev == Device.Keyboard ? Loc.T("Enter") : _dev == Device.Gamepad ? "A" : Loc.T("Green fret"); }
        static string BackKey() { return _dev == Device.Keyboard ? Loc.T("Escape or Backspace") : _dev == Device.Gamepad ? "B" : Loc.T("Red fret"); }
        static string Tabs()    { return _dev == Device.Keyboard ? Loc.T("Tab and Shift+Tab") : _dev == Device.Gamepad ? Loc.T("LB and RB") : Loc.T("Yellow and Orange frets"); }
        static string HelpKey() { return _dev == Device.Keyboard ? "F1" : _dev == Device.Gamepad ? "View" : "Select"; }

        static string ColorKey(MenuAction a)
        {
            if (_dev == Device.Keyboard)
            {
                switch (a)
                {
                    case MenuAction.Green:  return Loc.T("Enter or 1");
                    case MenuAction.Red:    return Loc.T("Escape, Backspace or 2");
                    case MenuAction.Yellow: return "3";
                    case MenuAction.Blue:   return "4";
                    case MenuAction.Orange: return "5";
                    case MenuAction.Start:  return Loc.T("Enter");
                    case MenuAction.Select: return Loc.T("Backspace");
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
                default:                return Loc.F("{0} fret", Keys.Color(a.ToString()));
            }
        }

        static string HoldKey(MenuAction a)
        {
            var k = ColorKey(a);
            if (_dev == Device.Keyboard && a == MenuAction.Green) k = "1";
            return Loc.F("Hold {0}", k);
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
                    if (names.Count == 0) return Loc.T("unbound");
                    var joined = names[0];
                    for (int i = 1; i < names.Count; i++) joined = Loc.F("{0} or {1}", joined, names[i]);
                    return joined;
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
            _firstIsTitle = true;
            Action<string, string> L = (keys, what) => lines.Add(keys + ": " + what);
            bool kb = _dev == Device.Keyboard;

            if (ShowPickerNavigator.IsActive)
            {
                lines.Add(Loc.T("Play A Show song picker. Five choices, one per colour"));
                lines.Add(Loc.T("When the timer runs out, the most voted choice joins the show and new choices appear. With no votes, the choices just change"));
                L(Move(), Loc.T("read the choices"));
                L(Side(), Loc.T("seconds left before the choices change"));
                if (kb)
                {
                    L(Loc.T("Enter"), Loc.T("add the choice you are on"));
                    L(Loc.T("1, 2, 3, 4 and 5"), Loc.T("add the Green, Red, Yellow, Blue or Orange choice"));
                    L(Loc.T("Escape or Backspace"), Loc.T("done picking"));
                }
                else
                {
                    foreach (var c in new[] { MenuAction.Green, MenuAction.Red, MenuAction.Yellow, MenuAction.Blue, MenuAction.Orange })
                        L(ColorKey(c), Loc.F("vote for the {0} choice", Keys.Color(c.ToString())));
                    L(HelpKey(), Loc.T("done picking"));
                }
                return lines;
            }

            if (DialogNavigator.IsActive)
            {
                lines.Add(Loc.T("Dialog"));
                L(Move(), Loc.T("move through the title, message, checkboxes and buttons"));
                L(Ok(), Loc.T("activate a button or flip a checkbox"));
                L(BackKey(), Loc.T("close the dialog"));
                AddReviewKeys(L);
                return lines;
            }

            int cal = CalibratorAnnouncer.IsActive ? CalibratorAnnouncer.Phase() : -1;
            if (cal >= 0)
            {
                lines.Add(Loc.T("Audio calibrator"));
                var tap = kb ? Loc.T("Space or Enter") : Ok();
                if (cal == 0) { L(Ok(), Loc.T("start audio calibration: you will hear clicks")); L(BackKey(), Loc.T("return to Settings")); }
                else if (cal == 1) { L(tap, Loc.T("press when you are ready, then on every click you hear")); L(BackKey(), Loc.T("stop")); }
                else if (cal == 2) { L(tap, Loc.T("press on every click")); L(BackKey(), Loc.T("stop")); }
                else L(BackKey(), Loc.T("return to the start screen"));
                AddReviewKeys(L);
                return lines;
            }

            if (FileBrowserNavigator.IsActive)
            {
                lines.Add(Loc.T("Folder and file picker"));
                L(Move(), Loc.T("move through the folder: choose this folder, the folder above, its folders and files, drives and places, cancel"));
                if (_dev == Device.Keyboard) L(Loc.T("Home and End"), Loc.T("first and last row"));
                L(Ok(), Loc.T("open a folder, choose a file, or choose this folder"));
                L(BackKey(), Loc.T("the folder above; at the top of a drive, the drives and places list; there, cancel"));
                AddReviewKeys(L);
                return lines;
            }

            if (CreditsNavigator.IsActive)
            {
                lines.Add(Loc.T("Credits"));
                L(Move(), Loc.T("read the entries one at a time"));
                if (_dev == Device.Keyboard) L(Loc.T("Home and End"), Loc.T("first and last entry"));
                L(Ok(), Loc.T("read the entry again"));
                L(BackKey(), Loc.T("go back"));
                AddMusicKeys(L);
                return lines;
            }

            if (ScoreScreenNavigator.IsActive)
            {
                lines.Add(Loc.T("Score screen"));
                L(Move(), Loc.T("read the stats"));
                L(Side(), Loc.T("switch between players in a band"));
                AddSchemeColors(L, false);
                AddReviewKeys(L);
                return lines;
            }

            if (PracticeSectionNavigator.IsActive)
            {
                lines.Add(Loc.T("Choose practice sections"));
                L(Move(), Loc.T("move through the song's sections"));
                if (!kb) L(Tabs(), Loc.T("also move through the sections"));
                L(Ok(), Loc.T("the first press sets the first section, a second press on the last section starts"));
                L(kb ? Loc.T("Backspace") : BackKey(), Loc.T("clear the first section, or leave practice if none is set"));
                AddReviewKeys(L);
                return lines;
            }

            if (GameplayContext.InGameplay)
            {
                bool practice = PracticeAssist.InPractice;
                if (!GameplayContext.Paused)
                {
                    lines.Add(practice ? Loc.T("Practice song") : Loc.T("Song"));
                    L(Loc.T("Escape"), Loc.T("pause"));
                    if (practice) AddPracticeKeys(L);
                    return lines;
                }
                lines.Add(Menu.Heading);
                L(Move(), Loc.T("move"));
                L(Ok(), Loc.T("activate, or flip a checkbox"));
                if (practice && Menu.CurrentName == "Practice") L(Side(), Loc.T("move the selected A or B loop point"));
                else L(Side(), Loc.T("change a value"));
                L(kb ? Loc.T("Backspace") : BackKey(), Loc.T("go back, or resume from the first pause menu"));
                L(kb ? Loc.T("Escape") : "Start", Loc.T("resume the song"));
                AddReviewKeys(L);
                return lines;
            }

            if (ProfileInfoNavigator.IsActive)
            {
                if (ProfileInfoNavigator.ChoosingControl)
                {
                    _firstIsTitle = false;
                    lines.Add(Loc.T("Choose the control to bind: several responded at once, such as a stick's direction and its axis"));
                    L(Move(), Loc.T("read them"));
                    L(Ok(), Loc.T("bind the one you are on"));
                    L(BackKey(), Loc.T("cancel"));
                    return lines;
                }
                lines.Add(Loc.T("Profile binding editor"));
                L(Tabs(), Loc.T("switch between the Overview and Binds tabs"));
                L(Move(), Loc.T("move"));
                L(Ok(), Loc.T("choose a game mode, start Add New or Quick Bind, or remove a binding (press twice)"));
                lines.Add(Loc.T("The Binds tab lists the game mode's binds, then the menu binds. Each action reads what it is bound to, then its binds and Add New"));
                lines.Add(Loc.T("After Add New, let go of every button, then press the control to bind. Until it is set, every button is the bind: help, music and text review keys do nothing. Escape or Backspace on the keyboard cancels"));
                lines.Add(Loc.T("Quick Bind on the Overview tab asks for each bind in turn and says what was set"));
                lines.Add(Loc.T("The menu binds end with the text review actions: Add New there binds your own button, or a pair, hold one and press another"));
                L(BackKey(), Loc.T("go back"));
                AddReviewKeys(L);
                return lines;
            }

            if (ProfilesNavigator.IsActive && !TextEdit.IsActive)
            {
                var who = ProfilesNavigator.ProfileNameOr(Loc.T("this profile"));
                if (ProfilesNavigator._inSettings)
                {
                    lines.Add(Loc.F("Profile settings for {0}", who));
                    L(Move(), Loc.T("move"));
                    L(Side(), Loc.T("change a choice or a number"));
                    L(Ok(), Loc.T("flip a checkbox, or type a number"));
                    L(BackKey(), Loc.T("return to the action toolbar"));
                }
                else if (ProfilesNavigator._inDevices)
                {
                    lines.Add(Loc.F("Devices of {0}", who));
                    L(Move(), Loc.T("read them"));
                    L(BackKey(), Loc.T("return to the action toolbar"));
                    lines.Add(Loc.T("Add Device and Remove Device on the toolbar change them"));
                }
                else
                {
                    lines.Add(Loc.T("Profiles"));
                    L(Move(), Loc.T("choose a profile"));
                    L(Ok(), Loc.T("on a connected profile, edit its binds; on a disconnected one, connect it; on the toolbar, do the action"));
                    L(Tabs(), Loc.T("move through the actions for the selected profile"));
                    var actions = ProfilesNavigator.Actions();
                    lines.Add(Loc.F("The actions are {0}", string.Join(", ", actions.ConvertAll(ActionWord).ToArray())));
                    if (actions.Contains("Instrument")) L(Side(), Loc.T("on Instrument, change the instrument"));
                    L(BackKey(), Loc.T("go back"));
                }
                AddReviewKeys(L);
                return lines;
            }

            if (PresetsNavigator.IsActive && PresetsNavigator.InZone)
            {
                lines.Add(Loc.T("Presets"));
                L(Move(), PresetsNavigator.GroupEmpty ? Loc.T("move through preset type, preset, and the actions") : Loc.T("move through preset type, preset, and the actions, then the preset's own settings"));
                L(Side(), Loc.T("change the preset type or the preset"));
                L(Ok(), Loc.T("do the action"));
                lines.Add(Loc.T("Built-in presets can't be changed: Copy one to make an editable preset"));
                L(Tabs(), Loc.T("switch tabs"));
                L(BackKey(), Loc.T("go back"));
                AddReviewKeys(L);
                return lines;
            }

            var page = SoundGlossaryPage.Title;
            if (page != null)
            {
                lines.Add(Loc.F("{0} page", page));
                L(Move(), Loc.T("move"));
                // The glossary page's own title (translated), so this works in every language.
                if (ModSettings.GlossaryPage != null && page == ModSettings.GlossaryPage.Title)
                {
                    L(Side(), Loc.T("play each sound of this list and say what it means"));
                    L(Ok(), Loc.T("play it again"));
                }
                else L(Side(), Loc.T("change the volume; the sound plays at the new level"));
                L(BackKey(), Loc.T("return to the YargAccess tab"));
                AddMusicKeys(L);
                return lines;
            }

            var scheme = ActiveScheme();
            lines.Add(OverlayName() ?? (string.IsNullOrEmpty(Menu.CurrentName) ? Loc.T("Menu") : Menu.Heading));
            bool mod = ModDriven();
            L(Move(), Loc.T("move"));

            // History writes its own lines for the keys the mod changes there, so YARG's names for them are not
            // read as well. Otherwise Enter, Left / Right and the colours would each be said twice.
            bool history = HistoryNavigator.IsActive && OverlayName() == null;

            var cur = CurrentSetting();
            if (history) L(Side(), Loc.T("jump to the previous or next day"));
            else if (cur != null && SettingsAccess.IsAdjustable(cur)) L(Side(), Loc.T("change the value"));
            else if (CurrentIsButtonRow()) L(Side(), Loc.T("choose a button in this row"));
            else
            {
                var left = SchemeName(scheme, MenuAction.Left);
                if (left != null && !InputDriver.SettingsOpen()) L(Side(), left);
            }
            // A guitar's Yellow and Orange are Analyze and Play With Replay in History; its list switches tabs there.
            if (HasTabs() && !(history && _dev == Device.Guitar)) L(Tabs(), history ? Loc.T("switch between the History and Import tabs") : Loc.T("switch tabs"));

            if (history)
            {
                L(mod ? Ok() : ColorKey(MenuAction.Green), Loc.T("watch the replay as it was played"));
                L(ColorKey(MenuAction.Blue), Loc.T("play this song yourself, in your Play Style"));
                if (_dev != Device.Gamepad) L(ColorKey(MenuAction.Orange), Loc.T("Play With Replay: play along with the replay, your part in your Play Style"));
            }
            else if (cur != null && SettingsAccess.IsEditable(cur))
                L(Ok(), kb ? Loc.T("type a value, or browse for a file") : Loc.T("open the on-screen keyboard to type a value"));
            else
                L(mod ? Ok() : ColorKey(MenuAction.Green), SchemeName(scheme, MenuAction.Green) ?? Loc.T("activate"));
            AddSchemeColors(L, true);
            if (kb && cur != null && SettingsAccess.IsEditable(cur)) L(Loc.T("[key] Delete"), Loc.T("remove an image file setting"));

            if (SettingsExtras.IsActive) lines.Add(Loc.T("At the top of the list are Scan Songs, to rescan, and Add New Folder"));
            if (Menu.CurrentName == "Music Library" && OverlayName() == null)
            {
                L(kb ? Loc.T("[key] Delete") : _dev == Device.Guitar ? Loc.T("Select and strum down") : "LT", Loc.T("delete the song you are on, after asking"));
                lines.Add(Loc.T("The text review holds the song's details: the instruments with their difficulty, album, year, genre, length, charter, source and rating"));
            }
            if (history)
            {
                if (kb)
                {
                    L(Loc.T("Home and End"), Loc.T("first and last entry"));
                    L("E", Loc.T("export the entry's replay as a file"));
                    L(Loc.T("[key] Delete"), Loc.T("delete the entry, after asking"));
                }
                else if (_dev == Device.Gamepad) L("LT", Loc.T("a list for the entry: Play With Replay, Export Replay and Delete Entry"));

                else L(Loc.T("Select and strum down"), Loc.T("a list for the entry: Export Replay, Delete Entry, and switching between the History and Import tabs"));
                lines.Add(Loc.T("Up from the first entry on the Import tab reaches Import Replay"));
                lines.Add(Loc.F("While a replay plays, its own keys seek, change speed and pause, and {0} lists them. The History and Replays tutorial explains replays", HelpKey()));
            }
            if (DifficultySelectExtras.IsActive) lines.Add(Loc.F("Below the last row is the song speed. {0} change it", Side()));

            if (Menu.CurrentName != "Main") L(mod ? BackKey() : ColorKey(MenuAction.Red), Loc.T("go back"));
            AddMusicKeys(L);
            return lines;
        }

        static bool kb() { return _dev == Device.Keyboard; }

        // Whether Build's first line is the help's title. HelpList can't tell from ": ", which a
        // translated title can hold.
        static bool _firstIsTitle;

        // ProfilesNavigator.Actions() gives English names (it switches on them); this is how they are read.
        static string ActionWord(string a)
        {
            switch (a)
            {
                case "Add Profile": return Loc.T("Add Profile");
                case "Add Bot": return Loc.T("Add Bot");
                case "Disconnect": return Loc.T("Disconnect");
                case "Edit Binds": return Loc.T("Edit Binds");
                case "Rename": return Loc.T("Rename");
                case "Profile Settings": return Loc.T("Profile Settings");
                case "Add Device": return Loc.T("Add Device");
                case "Remove Device": return Loc.T("Remove Device");
                case "Move Player Up": return Loc.T("Move Player Up");
                case "Move Player Down": return Loc.T("Move Player Down");
                case "Connect": return Loc.T("Connect");
                case "Delete Profile": return Loc.T("Delete Profile");
                case "Instrument": return Loc.T("Instrument");
                case "Devices": return Loc.T("Devices");
                default: return a;
            }
        }

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
                // On the keyboard Backspace is the mod's Back, so YARG's Select action can't be reached from it.
                if (a == MenuAction.Select && kb()) continue;
                // History lists its own Blue and Orange (Build); on a pad LB switches tabs there instead.
                if ((a == MenuAction.Blue || a == MenuAction.Orange) && HistoryNavigator.IsActive) continue;
                if (!seen.Add(a)) continue;
                var tap = SafeName(e);
                string hold = e.HasHoldHandler ? SafeName(e, true) : null;
                bool tapUseful = !string.IsNullOrEmpty(tap) && !(hold != null && string.Equals(tap, hold, StringComparison.OrdinalIgnoreCase));
                if (skipGreenRed && a == MenuAction.Green) tapUseful = false;
                if (tapUseful && a == MenuAction.Select && !kb()) { L(Loc.F("Hold {0}", ColorKey(a)), tap); continue; }
                if (tapUseful) L(ColorKey(a), tap);
                if (!string.IsNullOrEmpty(hold)) L(HoldKey(a), hold);
            }
        }

        static string OverlayName()
        {
            try
            {
                if (FiltersNavigator.IsActive) return Loc.T("Filters");
                var popup = UnityEngine.Object.FindObjectOfType<YARG.Menu.MusicLibrary.PopupMenu>();
                if (popup != null && popup.gameObject.activeInHierarchy) return Loc.T("More Options");
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
                    L("F5", Loc.T("say what music is playing"));
                    L("F6", Loc.T("play or pause the music"));
                    L("F2", Loc.T("read YARG's message of the day, what is new in this version"));
                    L("F7", Loc.T("previous song"));
                    L("F8", Loc.T("next song"));
                    break;
                case Device.Gamepad:
                    L("RT", Loc.T("read YARG's message of the day, what is new in this version"));
                    L(Loc.T("Left stick click"), Loc.T("play or pause the music; hold it for the previous song"));
                    L(Loc.T("Right stick click"), Loc.T("say what music is playing; hold it for the next song"));
                    break;
                case Device.Guitar:
                    L(Loc.T("Select and strum up"), Loc.T("read YARG's message of the day, what is new in this version"));
                    L(Loc.T("Select and Red"), Loc.T("previous song"));
                    L(Loc.T("Select and Orange"), Loc.T("next song"));
                    break;
            }
            AddReviewKeys(L);
        }

        static void AddReviewKeys(Action<string, string> L)
        {
            switch (_dev)
            {
                case Device.Keyboard:
                    L(Loc.T("Left and right brackets"), Loc.T("step back and forward through the last thing said, piece by piece"));
                    L(Loc.T("Backslash"), Loc.T("read the last thing said again in full"));
                    break;
                case Device.Gamepad:
                    L(Loc.T("Right stick left and right"), Loc.T("step back and forward through the last thing said, piece by piece"));
                    L(Loc.T("Right stick down"), Loc.T("read the last thing said again in full"));
                    break;
                default:
                    L(Loc.T("Hold Select and press Yellow or Blue"), Loc.T("step back and forward through the last thing said, piece by piece"));
                    L(Loc.T("Hold Select and press Green"), Loc.T("read the last thing said again in full"));
                    break;
            }
            if (_dev != Device.Keyboard) L(Loc.T("Text review buttons bound in the binding editor's menu binds"), Loc.T("work the same way"));
        }

        static string ReviewKeysShort()
        {
            switch (_dev)
            {
                case Device.Gamepad: return Loc.T("right stick left and right move through them, right stick down reads them all");
                case Device.Guitar:  return Loc.T("hold Select and press Yellow or Blue to move through them, Select and Green for all");
                default:             return Loc.T("left and right brackets move through them, backslash reads them all");
            }
        }

        static void AddPracticeKeys(Action<string, string> L)
        {
            L("F2", Loc.T("speed, section and hit percentage"));
            L("F3 and F4", Loc.T("slower and faster"));
            L("F8", Loc.T("restart the section"));
            L("F9", Loc.T("section announcements on or off"));
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
