using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.Data;
using YARG.Menu.Dialogs;
using YARG.Menu.Main;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.Settings;

namespace YargAccess
{
    public static class Tutorials
    {
        // A page can have a second text for Listen and Repeat; the Play Style setting picks which one is shown.
        class TutorialPage
        {
            public string Key, Hear, Repeat;
            public string Value => Repeat != null && ModSettings.ListenAndRepeat ? Repeat : Hear;
        }

        class Tutorial
        {
            public string Id, Name, Practice;
            public readonly List<TutorialPage> Pages = new List<TutorialPage>();
            public Tutorial Page(string heading, string text, string repeatText = null)
            {
                Pages.Add(new TutorialPage { Key = heading, Hear = text, Repeat = repeatText });
                return this;
            }
        }

        static readonly string PracticeText = Loc.T("Practice plays each kind of note the right way, then lets you play it. A miss says how early or late you were, and you try again.{practicerepeat}");
        static readonly string VocalPracticeText = Loc.T("Practice plays each kind of note the right way, then lets you sing or play it, with a scale and a short tune to sing note by note. A miss says which note and why, and you try again.{practicerepeatvocals}");
        static readonly string GlossaryEntry = Loc.T("Sound Glossary");
        static readonly string ButtonsEntry = Loc.T("Practice The Buttons");

        static readonly List<Tutorial> All = Build();

        static Dialog _focusDialog;
        static string _focusLabel;
        static Dialog _pageDialog;
        static int _tutorial = -1, _page;
        static int _entry;
        static bool _testFromList;
        static int _glossaryFrame;
        static float _listAt = -1f;
        static float _readySince = -1f;

        static int EntryCount => All.Count + 2;

        static string EntryName(int i)
        {
            if (i < All.Count) return All[i].Name;
            return i == All.Count ? GlossaryEntry : ButtonsEntry;
        }

        static string Text(string label) => ModSettings.TextKey + label;

        // One page made of several lines that are keys of their own, so each keeps its translation.
        static string J(params string[] parts) => string.Join(" ", parts);

        // The label a dialog button made with Text(label) shows, to find it by: the mod's localization of the key.
        static string Shown(string label)
        {
            string v;
            return ModSettings.TryLocalize(Text(label), out v) && v != null ? v : label;
        }

        static DialogManager Manager() => DialogManager.Instance;

        static void ClearQuietly()
        {
            Manager().ClearDialog();
            MenuAnnouncer.DropSettle();
        }

        public static void InstallButton(MainMenu menu)
        {
            try
            {
                var options = menu.transform.Find("Menu Options");
                if (options == null || options.Find("Tutorials") != null) return;
                var credits = options.Find("Credits");
                var practice = options.Find("Practice");
                var group = options.GetComponent<NavigationGroup>();
                if (credits == null || practice == null || group == null) return;

                var go = UnityEngine.Object.Instantiate(credits.gameObject, options);
                go.name = "Tutorials";
                go.transform.SetSiblingIndex(practice.GetSiblingIndex() + 1);
                foreach (var lt in go.GetComponentsInChildren<YARG.Localization.LocalizeText>(true))
                {
                    var label = lt.GetComponent<TMP_Text>();
                    UnityEngine.Object.DestroyImmediate(lt);
                    if (label != null) label.text = Loc.T("Tutorials");
                }

                var nb = go.GetComponent<NavigatableButton>();
                AccessTools.Field(typeof(NavigatableButton), "_onClick").SetValue(nb, new Button.ButtonClickedEvent());
                nb.SetOnClickEvent(() => ShowList(0));

                var selected = group.SelectedBehaviour;
                group.AddNavigatable(nb);
                var list = AccessTools.Field(typeof(NavigationGroup), "_navigatables").GetValue(group) as List<NavigatableBehaviour>;
                var practiceNb = practice.GetComponent<NavigatableBehaviour>();
                if (list != null && practiceNb != null)
                {
                    list.Remove(nb);
                    list.Insert(list.IndexOf(practiceNb) + 1, nb);
                    if (selected != null)
                        AccessTools.Property(typeof(NavigationGroup), "SelectedIndex").SetValue(group, (int?) list.IndexOf(selected), null);
                }
            }
            catch (Exception ex) { ModLog.Warn("Tutorials.InstallButton", ex); }
        }

        public static void ShowList(int focus)
        {
            var dm = Manager();
            if (dm == null) return;
            try
            {
                ClearQuietly();
                _pageDialog = null;
                _tutorial = -1;
                var d = dm.ShowList(Loc.T("Tutorials"));
                d.Title.text = Loc.T("Tutorials") + ".\n<size=60%>" + Loc.F("In a tutorial, {0} turn the pages.", Keys.LeftRight) + "</size>";
                d.ClearButtons();
                d.AddDialogButton(Text("Close"), MenuData.Colors.CancelButton, () =>
                {
                    ClearQuietly();
                    Patches.PushMenuPatch.AnnounceCurrent();
                });
                for (int i = 0; i < EntryCount; i++)
                {
                    int entry = i;
                    d.AddListButton(EntryName(i), () => Open(entry), false);
                }
                Focus(d, EntryName(Mathf.Clamp(focus, 0, EntryCount - 1)));
            }
            catch (Exception ex) { ModLog.Warn("Tutorials.ShowList", ex); }
        }

        static string P(string keyboard, string pad, string guitar)
        {
            switch (Keys.Current)
            {
                case Keys.Device.Gamepad: return pad;
                case Keys.Device.Guitar: return guitar;
                default: return keyboard;
            }
        }

        // Page text names the buttons of the device pressed last: keyboard, controller or guitar.
        static string DeviceText(string s)
        {
            if (s.IndexOf('{') < 0) return s;
            bool repeat = ModSettings.ListenAndRepeat;
            return s
                .Replace("{practice}", PracticeText)
                .Replace("{vocalpractice}", VocalPracticeText)
                .Replace("{style}", repeat ? Loc.T("Listen and Repeat") : Loc.T("Play What You Hear"))
                .Replace("{practicerepeat}", repeat ? Loc.T(" With Listen and Repeat on, the practice teaches that play style: each note plays as a call after the listen sound, you play it back when the your-turn sound ends, and two patterns come last.") : "")
                .Replace("{practicerepeatvocals}", repeat ? Loc.T(" With Listen and Repeat on, the percussion note plays as a call to play back, and a pattern of knocks comes last.") : "")
                .Replace("{device}", P(Loc.T("keyboard"), Loc.T("controller"), Loc.T("guitar")))
                .Replace("{move}", Keys.UpDown)
                .Replace("{ok}", Keys.Ok)
                .Replace("{back}", P(Loc.T("Escape or Backspace"), "B", Loc.T("Red")))
                .Replace("{tap}", P(Loc.T("press Space or Enter"), Loc.T("press A"), Loc.T("strum")))
                .Replace("{tabs}", Keys.Tabs)
                .Replace("{adjust}", Keys.LeftRight)
                .Replace("{pause}", P(Loc.T("Escape"), "Start", "Start"))
                .Replace("{help}", P(Loc.T("F1 lists the keys for the menu you are in."),
                    Loc.T("A tap of View lists the buttons for the menu you are in; holding it does YARG's own View action."),
                    Loc.T("A tap of Select lists the buttons for the menu you are in; holding it does YARG's own Select action.")))
                .Replace("{review}", P(Loc.T("Left bracket and right bracket step through the last thing spoken, a piece at a time, and backslash reads all of it again."),
                    Loc.T("Right stick left and right step through the last thing spoken, a piece at a time, and right stick down reads all of it again."),
                    Loc.T("Hold Select and press Yellow or Blue to step through the last thing spoken, a piece at a time; Select and Green reads all of it again.")))
                .Replace("{whatsnew}", P(Loc.T("F2 reads YARG's message of the day, what is new in this version."),
                    Loc.T("RT reads YARG's message of the day, what is new in this version."),
                    Loc.T("Hold Select and strum up to read YARG's message of the day, what is new in this version.")))
                .Replace("{music}", P(Loc.T("F5 names the song playing in the menus, F6 pauses or plays it, F7 goes back to the one before and F8 skips to the next one."),
                    Loc.T("R3 names the song playing in the menus and L3 pauses or plays it. Hold L3 to go back to the song before, or R3 to skip to the next one."),
                    Loc.T("Hold Select and press Red to go back to the song playing before in the menus, or Orange to skip to the next one. On the computer keyboard, F5 names the song and F6 pauses or plays it.")))
                .Replace("{hold1}", P(Loc.T("Hold 1"), Loc.T("Hold A"), Loc.T("Hold Green")))
                .Replace("{lookahead}", ((int) Math.Round(ModSettings.LookAheadSeconds * 1000)).ToString())
                .Replace("{drumlookahead}", ((int) Math.Round(ModSettings.DrumLookAheadSeconds * 1000)).ToString())
                .Replace("{yellow}", P("3", "Y", Loc.T("Yellow")))
                .Replace("{blue}", P("4", "X", Loc.T("Blue")))
                .Replace("{orange}", P("5", "LB", Loc.T("Orange")))
                .Replace("{entrykeys}", P(Loc.T("E exports the entry you are on, and Delete deletes it."),
                    Loc.T("LT opens a list for the entry you are on: Export Replay and Delete Entry."),
                    Loc.T("Hold Select and strum down to open a list for the entry you are on: Export Replay and Delete Entry.")))
                .Replace("{songdelete}", P(Loc.T("Delete deletes the song you are on."), Loc.T("LT deletes the song you are on."),
                    Loc.T("Hold Select and strum down to delete the song you are on.")))
                .Replace("{side}", P(Loc.T("Left and Right"), Loc.T("D-pad Left and Right"), Loc.T("D-pad Left and Right")))
                .Replace("{playwithreplay}", P("5", Loc.T("The first item of the LT list"), Loc.T("Orange")))
                .Replace("{historytabs}", P(Loc.T("Tab and Shift+Tab switch between them."),
                    Loc.T("LB and RB switch between them, and so does the last item of the LT list."),
                    Loc.T("The last item of the list that Select and strum down opens switches between them.")))
                .Replace("{starpowerkey}", P(Loc.T("On the computer keyboard, star power is Backspace on guitar, bass and keys, and Space on vocals."),
                    Loc.T("Your star power button is in your profile's binds."),
                    Loc.T("On a guitar, tilt it up or press Select to turn star power on.")))
                .Replace("{playkeys}", P(Loc.T("No guitar? The computer keyboard can play: on five-fret parts 1 to 5 are the frets and Up or Down strums; on drums Space is the kick, Z, X, C and V are the pads and S, D and F the cymbals."),
                    Loc.T("No guitar? A gamepad can play five-fret parts and drums with YARG's default buttons for it."),
                    Loc.T("A guitar plays five-fret parts: guitar, bass, rhythm and five-lane keys.")))
                .Replace("{colours}", P(Loc.T("3 is Play A Show, 4 opens Filters and 5 More Options"),
                    Loc.T("Y is Play A Show, X opens Filters and LB More Options"),
                    Loc.T("Yellow is Play A Show, Blue opens Filters and Orange More Options")))
                .Replace("{replaykeys}", WithoutHeading(ReplayControls.HelpText((ContextHelp.Device) (int) Keys.Current)))
                .Replace("{historyhomeend}", P(Loc.T("In the History menu, Home and End go to the first and last entry."),
                    Loc.T("On the computer keyboard, in the History menu, Home and End go to the first and last entry."),
                    Loc.T("On the computer keyboard, in the History menu, Home and End go to the first and last entry.")))
                .Replace("{practicef2}", P(Loc.T("In a practice song, F2 says the section, speed and hit percentage."),
                    Loc.T("On the computer keyboard, in a practice song, F2 says the section, speed and hit percentage."),
                    Loc.T("On the computer keyboard, in a practice song, F2 says the section, speed and hit percentage.")));
        }

        // The replay keys without their first sentence ("Replay keys."), in any language: the text up to the
        // first full stop is dropped.
        static string WithoutHeading(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            int i = s.IndexOf(". ", StringComparison.Ordinal);
            int j = s.IndexOf('\u3002');
            if (j >= 0 && (i < 0 || j < i)) return s.Substring(j + 1).TrimStart();
            return i >= 0 ? s.Substring(i + 2) : s;
        }

        // For other menus' dialogs: the button to start on, read with the dialog.
        public static void FocusNext(Dialog d, string label) => Focus(d, label);

        static void Focus(Dialog d, string label)
        {
            _focusDialog = d;
            _focusLabel = label;
            DialogNavigator.Reset();
            DialogNavigator.FocusLabel(label);
        }

        public static string FocusOnOpen(Dialog d)
        {
            if (d == null || d != _focusDialog || _focusLabel == null) return null;
            var label = _focusLabel;
            _focusDialog = null;
            _focusLabel = null;
            DialogNavigator.FocusLabel(label);
            return label + Keys.Role("button");
        }

        static void Open(int entry)
        {
            _entry = entry;
            if (entry < All.Count) { ShowPage(entry, 0, true); return; }
            if (entry != All.Count)
            {
                // The list stays open, focused on Practice The Buttons, while the reason is spoken.
                if (ButtonTest.NoHumanProfile()) { ChooseTestMode(entry); return; }
                var problem = ButtonTest.ProfileProblem();
                if (problem != null) { MenuAnnouncer.SayShown(problem); return; }
            }
            ClearQuietly();
            if (entry == All.Count) { OpenGlossary(); return; }
            _testFromList = true;
            ButtonTest.OpenForProfile();
            if (!ButtonTest.IsOpen && !DrumButtonTest.IsOpen && !KeysButtonTest.IsOpen && !VocalButtonTest.IsOpen)
            {
                _testFromList = false;
                ShowListLater(entry, 4f);
            }
        }

        static void ShowPage(int t, int p, bool named = false)
        {
            var dm = Manager();
            if (dm == null) return;
            try
            {
                var tut = All[t];
                int last = tut.Pages.Count - 1;
                ClearQuietly();
                var page = tut.Pages[p];
                string title = named ? Loc.F("{0}, page {1} of {2}: {3}", tut.Name, p + 1, tut.Pages.Count, page.Key)
                    : Loc.F("Page {0} of {1}: {2}", p + 1, tut.Pages.Count, page.Key);
                var d = dm.ShowMessage(title, DeviceText(page.Value));
                d.ClearButtons();
                if (p > 0) d.AddDialogButton(Text("Previous"), MenuData.Colors.BrightButton, () => ShowPage(t, p - 1));
                string main;
                if (p < last)
                {
                    main = "Next"; // also a key: Text("Next")
                    d.AddDialogButton(Text(main), MenuData.Colors.ConfirmButton, () => ShowPage(t, p + 1));
                }
                else if (tut.Practice != null) main = "Practice";
                else
                {
                    main = "Done"; // also a key: Text("Done")
                    d.AddDialogButton(Text(main), MenuData.Colors.ConfirmButton, () => ShowList(t));
                }
                // Every page of an instrument tutorial has Practice; it returns to the page it was started from.
                if (tut.Practice != null)
                    d.AddDialogButton(Text("Practice"), p < last ? MenuData.Colors.BrightButton : MenuData.Colors.ConfirmButton, () => StartPractice(t, p));
                d.AddDialogButton(Text("Back"), MenuData.Colors.CancelButton, () => ShowList(t));
                _pageDialog = d;
                _tutorial = t;
                _page = p;
                _mainLabel = Shown(main);
                Focus(d, _mainLabel);
            }
            catch (Exception ex) { ModLog.Warn("Tutorials.ShowPage", ex); }
        }

        static string _mainLabel;
        static bool _pressing;

        // Enter on a sentence of a tutorial page presses its main button (Next, Practice or Done), so
        // reading the page sentence by sentence does not change what Enter does.
        public static bool PressMain()
        {
            if (_pressing || _pageDialog == null || !_pageDialog.gameObject.activeInHierarchy || _mainLabel == null) return false;
            _pressing = true;
            try
            {
                DialogNavigator.FocusLabel(_mainLabel);
                DialogNavigator.Confirm();
            }
            finally { _pressing = false; }
            return true;
        }

        public static bool Turn(int dir)
        {
            if (_pageDialog == null || !_pageDialog.gameObject.activeInHierarchy || _tutorial < 0) return false;
            if (InputDriver.Repeating) return true;
            var tut = All[_tutorial];
            int to = _page + dir;
            if (to < 0) { MenuAnnouncer.Say(Loc.T("First page")); return true; }
            if (to >= tut.Pages.Count)
            {
                MenuAnnouncer.Say(Loc.T("Last page"));
                return true;
            }
            ShowPage(_tutorial, to);
            return true;
        }

        static void StartPractice(int t, int p)
        {
            var tut = All[t];
            var choices = PracticeDevices.Choices(tut.Practice);
            // The microphone is always asked, with No Microphone to only listen.
            if (choices.Count > 1 || tut.Practice == "vocals") { ChooseDevice(t, p, choices); return; }
            // One fitting device: use it, whichever device opened the practice.
            RunPractice(t, p, choices.Count == 1 ? choices[0] : null);
        }

        // Kits asked about this session: true = it has cymbals.
        static readonly Dictionary<string, bool> _kitCymbals = new Dictionary<string, bool>();

        // A four-lane or MIDI kit may have no cymbals (Rock Band 1 and 2 kits): ask once, and without
        // cymbals the practice leaves out the cymbal steps. The computer keyboard and
        // five-lane kits are not asked.
        static bool MayLackCymbals(PracticeDevices.Choice choice)
        {
            var d = choice != null ? choice.Device : null;
            if (d == null) return false;
            return d is PlasticBand.Devices.FourLaneDrumkit || (d is Minis.MidiDevice && PracticeDevices.IsDrumKit(d));
        }

        static void AskCymbals(int t, int p, PracticeDevices.Choice choice)
        {
            var dm = Manager();
            if (dm == null) return;
            try
            {
                ClearQuietly();
                _pageDialog = null;
                string kit = choice.Name;
                var d = dm.ShowMessage(Loc.T("Cymbals"), Loc.T("Does your drum kit have cymbals? Pro drums uses three cymbals, Yellow, Blue and Green, "
                    + "above the pads. Kits from Rock Band 1 and 2 have only the four pads. Without cymbals the practice leaves out the cymbal steps."));
                d.ClearButtons();
                d.AddDialogButton(Text("Back"), MenuData.Colors.CancelButton, () => ShowPage(t, p));
                d.AddDialogButton(Text("No Cymbals"), MenuData.Colors.BrightButton, () => { _kitCymbals[kit] = false; RunPractice(t, p, choice); });
                d.AddDialogButton(Text("It Has Cymbals"), MenuData.Colors.ConfirmButton, () => { _kitCymbals[kit] = true; RunPractice(t, p, choice); });
                Focus(d, Shown("It Has Cymbals"));
            }
            catch (Exception ex) { ModLog.Warn("Tutorials.AskCymbals", ex); }
        }

        static void RunPractice(int t, int p, PracticeDevices.Choice choice)
        {
            var tut = All[t];
            bool cymbals = true;
            if (tut.Practice == "drums" && MayLackCymbals(choice))
            {
                if (!_kitCymbals.TryGetValue(choice.Name, out cymbals)) { AskCymbals(t, p, choice); return; }
            }
            GuidedPractice.NoCymbals = !cymbals;
            ClearQuietly();
            _pageDialog = null;
            PracticeDevices.Chosen = choice;
            GuidedPractice.Start(tut.Practice, Loc.F("{0} practice", tut.Name), done => ShowPage(t, p));
        }

        // More than one device fits: the player picks the one to practise on.
        static void ChooseDevice(int t, int p, List<PracticeDevices.Choice> choices)
        {
            PickDevice(All[t].Practice, choices, pick => RunPractice(t, p, pick), () => ShowPage(t, p), false);
        }

        // A list of the devices that fit a mode; for vocals also No Microphone (listen only).
        static void PickDevice(string mode, List<PracticeDevices.Choice> choices, Action<PracticeDevices.Choice> pick, Action back, bool trying)
        {
            var dm = Manager();
            if (dm == null) return;
            try
            {
                if (mode == "vocals") choices.Add(new PracticeDevices.Choice { Name = Loc.T("No Microphone"), MicName = "" });
                string head, line;
                switch (mode)
                {
                    case "drums":
                        head = Loc.T("Choose a drum kit");
                        line = trying ? Loc.T("Choose the drum kit to try.") : Loc.T("Choose the drum kit to practise on.");
                        break;
                    case "prokeys":
                        head = Loc.T("Choose a keyboard");
                        line = trying ? Loc.T("Choose the keyboard to try.") : Loc.T("Choose the keyboard to practise on.");
                        break;
                    case "vocals":
                        head = Loc.T("Choose a microphone");
                        line = trying ? Loc.T("Choose the microphone to try.") : Loc.T("Choose the microphone to practise on.");
                        break;
                    default:
                        head = Loc.T("Choose a guitar or controller");
                        line = trying ? Loc.T("Choose the guitar or controller to try.") : Loc.T("Choose the guitar or controller to practise on.");
                        break;
                }
                ClearQuietly();
                _pageDialog = null;
                var d = dm.ShowList(head);
                d.Title.text = line;
                d.ClearButtons();
                d.AddDialogButton(Text("Back"), MenuData.Colors.CancelButton, () => back());
                foreach (var c in choices)
                {
                    var choice = c;
                    d.AddListButton(c.Name, () => pick(choice), false);
                }
                Focus(d, choices[0].Name);
            }
            catch (Exception ex) { ModLog.Warn("Tutorials.PickDevice", ex); pick(null); }
        }

        // Practice The Buttons with no human profile: pick a game mode, then the test runs on a temporary
        // practice profile with the device found for it (or the computer keyboard), removed when it closes.
        static bool _tempForTest;
        internal static bool TryingButtons => _tempForTest;

        static void ChooseTestMode(int entry)
        {
            var dm = Manager();
            if (dm == null) return;
            try
            {
                ClearQuietly();
                var d = dm.ShowList(ButtonsEntry);
                d.Title.text = Loc.T("No profile is connected, so the buttons are tried on a practice profile that is removed afterwards. Choose the kind of controller");
                d.ClearButtons();
                d.AddDialogButton(Text("Back"), MenuData.Colors.CancelButton, () => ShowList(entry));
                d.AddListButton(Loc.T("Five-Fret Guitar"), () => PickTestDevice(entry, "fivefret"), false);
                d.AddListButton(Loc.T("Drums"), () => PickTestDevice(entry, "drums"), false);
                d.AddListButton(Loc.T("Pro Keys"), () => PickTestDevice(entry, "prokeys"), false);
                d.AddListButton(Loc.T("Vocals"), () => PickTestDevice(entry, "vocals"), false);
                Focus(d, Loc.T("Five-Fret Guitar"));
            }
            catch (Exception ex) { ModLog.Warn("Tutorials.ChooseTestMode", ex); }
        }

        static void PickTestDevice(int entry, string mode)
        {
            var choices = PracticeDevices.Choices(mode);
            if (choices.Count > 1 || mode == "vocals") PickDevice(mode, choices, pick => RunTest(entry, mode, pick), () => ChooseTestMode(entry), true);
            else RunTest(entry, mode, choices.Count == 1 ? choices[0] : null);
        }

        static void RunTest(int entry, string mode, PracticeDevices.Choice choice)
        {
            ClearQuietly();
            _entry = entry;
            PracticeDevices.Chosen = choice;
            _tempForTest = true;
            string device = PracticeDevices.Ensure(mode, true);
            _testFromList = true;
            switch (mode)
            {
                case "drums": DrumButtonTest.Open(); break;
                case "prokeys": KeysButtonTest.Open(); break;
                case "vocals": VocalButtonTest.Open(); break;
                default: ButtonTest.Open(); break;
            }
            if (!string.IsNullOrEmpty(device)) Speech.Say(device, false);
        }

        public static void TestClosed(string doneText)
        {
            if (_tempForTest) { _tempForTest = false; PracticeDevices.Release(); }
            if (GuidedPractice.Active) { GuidedPractice.Closed(); return; }
            if (_testFromList) { _testFromList = false; ShowListLater(_entry, 0f); return; }
            Captions.Say(doneText, true);
        }

        static void ShowListLater(int entry, float delay)
        {
            _entry = entry;
            _listAt = Time.unscaledTime + delay;
        }

        static void OpenGlossary()
        {
            try
            {
                MenuAnnouncer.SuppressFor(1f);
                SettingsMenu.Instance.gameObject.SetActive(true);
                _glossaryFrame = Time.frameCount + 3;
            }
            catch (Exception ex) { ModLog.Warn("Tutorials.OpenGlossary", ex); }
        }

        public static void Tick()
        {
            if (_glossaryFrame > 0 && Time.frameCount >= _glossaryFrame)
            {
                _glossaryFrame = 0;
                SoundGlossaryPage.Open(ModSettings.GlossaryPage, () =>
                {
                    try { SettingsMenu.Instance.gameObject.SetActive(false); }
                    catch (Exception ex) { ModLog.Warn("Tutorials glossary close", ex); }
                    ShowListLater(_entry, 0.1f);
                });
            }

            var dm = Manager();
            if (_listAt >= 0f && Time.unscaledTime >= _listAt && dm != null && !dm.IsDialogShowing)
            {
                _listAt = -1f;
                ShowList(_entry);
            }

            OfferTick(dm);
        }

        static void OfferTick(DialogManager dm)
        {
            if (ModSettings.TutorialsOffered == null || ModSettings.TutorialsOffered.Value) return;
            bool ready = dm != null && !dm.IsDialogShowing && MainMenuShowing() && !GameplayContext.InGameplay;
            try { if (YARG.LoadingScreen.IsActive) ready = false; } catch { }
            if (ButtonTest.IsOpen || DrumButtonTest.IsOpen || KeysButtonTest.IsOpen || VocalButtonTest.IsOpen || GuidedPractice.Active) ready = false;
            float now = Time.unscaledTime;
            if (!ready) { _readySince = -1f; return; }
            if (_readySince < 0f) { _readySince = now; return; }
            if (now - _readySince < 2f) return;
            Offer(dm);
        }

        static bool MainMenuShowing()
        {
            var g = NavigationGroup.CurrentNavigationGroup;
            if (g == null || g.name != "Menu Options" || g.GetComponentInParent<MainMenu>() == null) return false;
            var settings = SettingsMenu.Instance;
            return settings == null || !settings.gameObject.activeInHierarchy;
        }

        static void Offer(DialogManager dm)
        {
            ModSettings.TutorialsOffered.Value = true;
            ModSettings.Save();
            try
            {
                var d = dm.ShowMessage(Loc.T("Tutorials"), Loc.T("Welcome to YargAccess. Would you like to open the Tutorials? They explain each game mode, "
                    + "the difficulties, scoring, star power and profiles, with a practice for each instrument. "
                    + "You can open them any time from Tutorials on the main menu, below Practice."));
                d.ClearButtons();
                d.AddDialogButton(Text("Not Now"), MenuData.Colors.CancelButton, () =>
                {
                    ClearQuietly();
                    Patches.PushMenuPatch.AnnounceCurrent();
                });
                d.AddDialogButton(Text("Open Tutorials"), MenuData.Colors.ConfirmButton, () => ShowList(0));
                Focus(d, Shown("Open Tutorials"));
            }
            catch (Exception ex) { ModLog.Warn("Tutorials.Offer", ex); }
        }

        static List<Tutorial> Build()
        {
            var list = new List<Tutorial>();

            // Getting Started is one walk from the first start to the first song: songs, profiles, how a song
            // works, play styles, the library, then playing with others. Pages made of J(...) parts join lines
            // that are keys of their own, so they keep their existing translations.
            list.Add(new Tutorial { Name = Loc.T("Getting Started") }
                .Page(Loc.T("Welcome"),
                    Loc.T("YargAccess makes YARG playable by ear. Menus are spoken, and during a song the notes are played to you as sounds, so you know what to play. "
                    + "This tutorial takes you from the first start to your first song: getting songs, setting up a profile, how a song works, the two play styles, setting your timing and the song library. "
                    + "Each page is read one sentence at a time: {move} go through the sentences again, {ok} goes to the next page, {side} turn pages, and {back} goes back to the list."))
                .Page(Loc.T("Moving around"),
                    Loc.T("These pages name the buttons of the {device}, the last thing you pressed. To hear the buttons of another keyboard, controller or guitar, use that device to move back to this page. "
                    + "{move} move. {ok} selects. {back} goes back. {tabs} switch tabs. {adjust} change a value, such as a slider or a list. "
                    + "A drum kit or MIDI keyboard moves through the menus with the menu buttons bound on its profile."))
                .Page(Loc.T("Help and other keys"),
                    J(Loc.T("Two things help while you learn. {help} {review}"),
                    Loc.T("{music} {whatsnew}")))
                .Page(Loc.T("Your first song"),
                    Loc.T("Five steps get you to your first song. First, get some songs. Second, set up a profile for your keyboard, controller or guitar. "
                    + "Third, learn what happens in a song and which play style you want. "
                    + "Fourth, run YARG's calibrator so your timing matches your speakers or headphones. "
                    + "Last, choose Quickplay on the main menu, pick a song, then your part and difficulty, and the song starts. "
                    + "The pages that follow take these steps in order."))
                .Page(Loc.T("Getting songs"),
                    Loc.T("YARG comes with no songs, and its menus are silent until you have some, because the menu music comes from your songs. "
                    + "The YARG Access launcher gets them for you: its DLC tab has YARG's official setlists, and its Download songs through Chorus tab finds single songs made by players. "
                    + "Songs you already have, such as songs from Clone Hero or ones a friend gave you, go in a song folder that you add in YARG's Settings."))
                .Page(Loc.T("Songs from the launcher"),
                    Loc.T("Setlists from the launcher's DLC tab go in the launcher's content folder, where YARG finds them on its own. They are not in YARG's song folders list, and removing a song folder never removes them. "
                    + "Songs from the Chorus tab are saved in the launcher's Chorus folder. A download made while YARG is closed adds that folder to YARG's song folders if it is missing. "
                    + "You can change the folder, or turn the adding off, on the launcher's Settings tab. "
                    + "If you remove the Chorus folder in YARG, its songs leave the library until the next download adds it back, or until you add it yourself."))
                .Page(Loc.T("Song folders"),
                    J(Loc.T("YARG looks for songs in the song folders listed in its Settings, on the Songs tab, and in any folder inside them. "
                    + "The folder controls sit above the first setting: from the first setting, move up. "
                    + "First come Add New Folder, Scan Songs, and Errors Found when YARG could not read some songs. Below them, each song folder has three items: its path, Browse, and Remove folder."),
                    Loc.T("Choose Add New Folder. A new row is added with no folder chosen yet, and Browse on that row is selected. "
                    + "Choose Browse to open the Windows folder picker, which your screen reader reads. Pick the folder that holds your songs and choose Select Folder. "
                    + "Then choose Scan Songs.")))
                .Page(Loc.T("Scanning and removing"),
                    J(Loc.T("YARG only finds new or changed songs when it scans. Choose Scan Songs after adding a folder, after copying songs into one, or after the launcher downloads a song while YARG is open. "
                    + "YARG also scans each time it starts. A long scan says Scanning songs, and at the end you hear how many songs are in the library."),
                    Loc.T("Remove folder takes a folder off the list. A folder with a path asks first; choose Remove to confirm, or Cancel. "
                    + "Nothing is deleted from your computer: the songs simply leave the library at the next scan. To get them back, add the folder again and scan.")))
                .Page(Loc.T("Profiles"),
                    J(Loc.T("A profile is one player: a name, a game mode, the part you like to play, a difficulty, the buttons you use and a few settings. "
                    + "YARG needs at least one connected profile before you can play, and each connected profile is one player in the band. "
                    + "Open Profiles from the main menu."),
                    Loc.T("{move} move through your profiles; each reads its name, its part and whether it is connected. "
                    + "{tabs} move through the toolbar. Add Profile makes a new profile, ready to connect, and Add Bot makes a bot. "
                    + "The toolbar changes with the profile you are on: a connected profile has more items than one that is not.")))
                .Page(Loc.T("Connecting"),
                    Loc.T("{ok} on a profile that is not connected connects it, and so does Connect on the toolbar. YARG then lists your devices: "
                    + "choose the keyboard, controller, guitar, drum kit, MIDI keyboard or microphone it uses. "
                    + "Disconnect frees the profile. Add Device and Remove Device change its devices later, and Devices lists them. "
                    + "A new device starts with YARG's default buttons, so you only need the binding editor if you want different ones."))
                .Page(Loc.T("The game modes"),
                    J(Loc.T("Each profile has a game mode: the kind of controller it plays. The game mode decides which parts of a song the profile can play."),
                    Loc.T("Five-fret guitar: a guitar controller with five coloured frets and a strum bar, for guitar, bass, rhythm guitar, co-op guitar and five-lane keys. "
                    + "Four-lane drums: four pads and a kick pedal, and pro drums adds three cymbals. "
                    + "Five-lane drums: three pads, two cymbals and a kick. "
                    + "Pro keys: a 25-key piano keyboard, usually a MIDI keyboard. "
                    + "Vocals: a microphone, for one singer, or up to three singing harmonies. "
                    + "Six-fret guitar and pro guitar are listed too, but YARG cannot play them yet. {playkeys}")))
                .Page(Loc.T("Choosing a game mode"),
                    J(Loc.T("To set the game mode, connect the profile, then open Profile Settings on the toolbar. The game mode is the Instrument Select setting: {adjust} change it. "
                    + "Careful: YARG calls the game mode Instrument Select, and calls the part just Instrument; they are different settings."),
                    Loc.T("Within a game mode you can play more than one part: guitar or bass, for example. Instrument on the toolbar picks the part you prefer, with {adjust}. "
                    + "Before each song, the difficulty screen asks for your part and difficulty. The profile remembers the difficulty; the part starts at your Instrument choice each time, when the song has it.")))
                .Page(Loc.T("Binding buttons"),
                    J(Loc.T("{ok} on a connected profile, or Edit Binds on the toolbar, opens the binding editor. Its Overview tab has Quick Bind, which asks you to press each pad or button in turn. "
                    + "Its Binds tab starts with a game mode list: pick the mode whose buttons you want to change. Then come the actions with their buttons. "
                    + "Add New waits for you to press the button you want; pressing {ok} twice on a button removes it."),
                    Loc.T("Practice The Buttons, at the end of the Tutorials list, plays each button's sound in a song and says what it does, so you can check your binds without a song. "
                    + "It uses your connected profile. With no profile connected, or only bots, it asks for a game mode and then a device, and makes a practice profile with YARG's default buttons that is removed when you finish. "
                    + "For vocals it asks which microphone to use, or none. With a microphone, a tap on it counts as Hit, and when you sing the note you hold is named. "
                    + "The practice at the end of each instrument tutorial also asks for the device, and always for the microphone.")))
                .Page(Loc.T("The highway"),
                    J(Loc.T("The next pages explain what happens in a song, for anyone who has never played Guitar Hero, Rock Band, Clone Hero or YARG."),
                    Loc.T("Each player has a highway: a track that runs from the back of the screen to the front, with one lane for each fret, pad or key. "
                    + "Notes appear far away and travel down their lanes towards you. Across the near end is the hit line, which YARG calls the strike line, and you play a note at the moment it reaches that line. "
                    + "YARG's default rules give you 70 milliseconds either side of that moment. Lines across the highway mark the beats and the bars of the song. "
                    + "The notes written for one part of a song are called its chart.")))
                .Page(Loc.T("Guitar and bass"),
                    Loc.T("On five-fret guitar and bass, hold the fret or frets for the note and strum: flick the strum bar up or down. A chord is several frets at once, and an open note is a strum with no fret held. "
                    + "A sustain, or held note, is a note with a long tail: keep holding its fret until the tail ends; the longer you hold, the more points. "
                    + "Right after a note you hit, a hammer-on or pull-off can be played by pressing or lifting a fret, with no strum. Taps can be played with no strum even after a miss."))
                .Page(Loc.T("Drums, keys and vocals"),
                    Loc.T("On drums, hit the pad for each lane in time, and the kick pedal for the kick notes. Pro drums also tells toms and cymbals apart. A roll is one pad hit as fast as you can; a swell is two pads in turn. "
                    + "Five-lane keys work like the five frets, with no strum; pro keys uses 25 real piano keys. "
                    + "On vocals you sing into a microphone. YARG scores your pitch and timing, not the words, and any octave counts. The song is split into phrases, and each phrase gets a rating when it ends."))
                .Page(Loc.T("Star power, solos and failing"),
                    J(Loc.T("Some groups of notes are star power phrases. Hit every note of one to fill part of the star power bar, then turn star power on to double your points for a while. The Star Power tutorial explains it."),
                    Loc.T("Some songs mark a solo section. Hit 60 percent of its notes or more and you earn a bonus when the solo ends. The more of its notes you hit, the bigger the bonus."),
                    Loc.T("The rock meter rises while you play well and falls when you miss. If it runs out, the song fails. YARG then offers to restart, or to carry on with No Fail turned on; with No Fail the score is not saved. "
                    + "No Fail then stays on for later songs until you turn it off. "
                    + "No Fail is in Settings, General tab: On stops failing, and No Meter also hides the meter. The Easy and Casual rock meter presets in Profile Settings make failing less likely; Hard and Unfair make it more likely.")))
                .Page(Loc.T("Two ways to play"),
                    Loc.T("YargAccess turns the highway into sound in one of two play styles. Play Style on the YargAccess tab in Settings picks one, and a change starts with the next song. You have {style} on. "
                    + "Listen and Repeat, the default, plays a few bars of the part, then you play those bars back. "
                    + "Play What You Hear plays every note of the chart just before you hit it: harder, and you play the song as charted. "
                    + "In both, beat ticks follow the beat lines: a soft tick on every beat and a stronger one on the first beat of each bar. "
                    + "The other tutorials follow your Play Style, in their pages and in their practice."))
                .Page(Loc.T("Play What You Hear"),
                    Loc.T("Each note plays its sound a short time before it reaches the hit line. That time is the Look-ahead: you have {lookahead} milliseconds on guitar, bass and keys, and {drumlookahead} on drums. "
                    + "On guitar, bass and keys a rising hit bar sound follows and ends at the moment to play. On drums, hit as soon as you hear the sound. "
                    + "Look-ahead and Drums Look-ahead on the YargAccess tab change the time. You play every note of the song."))
                .Page(Loc.T("Listen and Repeat"),
                    J(Loc.T("A falling sound means listen. From the next bar line, a few bars of notes play with their normal sounds, in the song's rhythm, over the music. You don't play these."),
                    Loc.T("A rising sound ends where your turn starts, on the bar line right after the pattern. Play the same notes in the same rhythm, in time with the song. "
                    + "Each note you hit plays the hit sound; a miss plays the miss sound. On guitar, bass and keys a click marks each note's moment."),
                    Loc.T("If the music covers the pattern, turn on Duck Music During Listen on the YargAccess tab: the song gets quieter while a pattern plays.")))
                .Page(Loc.T("Pattern length and scores"),
                    J(Loc.T("Call Length sets how many bars each pattern lasts: 1, 2 or 4, and 2 is recommended. Your turn is the same number of bars, so a busy part has more notes to remember than a sparse one. "
                    + "Your Listen and Repeat best is kept apart and read after your normal best in the song details, which the text review keys read in the song library. Replays play back the same way."),
                    Loc.T("On vocals only percussion notes use Listen and Repeat. You sing the whole part as usual in both play styles."),
                    Loc.T("In practice mode, which the Practice Mode tutorial explains, a section is stretched to start where its pattern is played to you and to end after you have played it back.")))
                .Page(Loc.T("Limits of Listen and Repeat"),
                    Loc.T("The notes in the bars where a pattern plays are taken out of the chart, so you play about half of the notes, "
                    + "for example 213 of 410 on Sweet Victory, Hard guitar. Fewer notes means fewer points, so your score is lower than a full play of the same song, "
                    + "and it can't be compared with scores played the whole way through. "
                    + "A full combo needs every note of the chart, so full combos are only possible with Play What You Hear. "
                    + "With Listen and Repeat, YARG shows no Full Combo and none is saved with the score."))
                .Page(Loc.T("Setting your timing"),
                    Loc.T("Speakers and headphones play sound a little late, and Bluetooth headphones much later. YARG's calibrator measures that delay so your hits count when you mean them. "
                    + "Run it once before your first song, with the speakers or headphones you play on, and again if you change them. "
                    + "Open Settings on the main menu and choose Open Calibrator on the General tab. It reads your current audio calibration, and {ok} starts. "
                    + "When you are ready, {tap} once, then again on every click you hear, in time, until the clicks stop. {back} stops early. "
                    + "At the end you hear the new calibration in milliseconds, and YARG uses it from then on. If it says there was not enough data, start again and keep going until the last click. "
                    + "{back} returns to the start screen, and {back} again leaves the calibrator."))
                .Page(Loc.T("The song library"),
                    J(Loc.T("Quickplay on the main menu opens the song library. {move} move through the songs; each reads its name, artist, stars and whether it is a favourite. "
                    + "{ok} picks a song, then you choose your instrument and difficulty."),
                    Loc.T("A setlist is a queue of songs played one after another. {hold1} to add a song to it. {colours}. "
                    + "The text review keys read a song's full details: instruments, album, year, length and charter. "
                    + "{songdelete} It asks first; the song's files go to the Recycle Bin and your scores for it stay in History. A song inside a pack file with other songs can't be deleted on its own.")))
                .Page(Loc.T("During a song"),
                    Loc.T("{pause} pauses the song. The pause menu has Resume, Restart, Practice, Save Replay, Settings and Back To Library; Back To Library ends the song without a score. "
                    + "YARG's most important pop-ups, Star Power Ready, Full Combo and New High Score, are spoken; Speech During Songs on the YargAccess tab in Settings can speak all of them, or none. "
                    + "After the song the score screen reads your results."))
                .Page(Loc.T("Playing with others"),
                    Loc.T("Several players can play one song together on one computer, each with their own profile and device. Their scores add up to a band score, and each player's star power raises the score of the others too. "
                    + "YargAccess is made for one person playing and does not support co-op: only player one, the first human in the band, gets note sounds. "
                    + "With you on guitar and a bot on drums, you hear only the guitar sounds."))
                .Page(Loc.T("Bots and profile options"),
                    Loc.T("A bot plays a part by itself; Add Bot in Profiles makes one. When only bots play, the first bot gets the note sounds, so a bot alone lets you listen to a whole part. "
                    + "YARG never saves a bot's score; the score screen after the song shows it, and Left and Right there move between the players. "
                    + "In Profiles, Rename changes a profile's name, Move Player Up and Move Player Down change the order of the players, and Delete Profile asks you to press {ok} again before it deletes. "
                    + "The Difficulties, Scoring and Stars, and Star Power tutorials go further into how a song is scored.")));

            list.Add(new Tutorial { Name = Loc.T("Settings") }
                .Page(Loc.T("The YargAccess tab"),
                    Loc.T("Settings on the main menu has YARG's tabs, General, Songs, Sound, Graphics and Presets, then YargAccess and All Settings. {tabs} switch tabs. "
                    + "The YargAccess tab holds the mod's own settings. Each one reads its description when you reach it. "
                    + "{adjust} change a value, {ok} turns a checkbox on or off, and every change is saved at once."))
                .Page(Loc.T("Play Style and Call Length"),
                    Loc.T("Play Style picks Listen and Repeat or Play What You Hear; the change starts with the next song. You have {style} on. The Getting Started tutorial explains both. "
                    + "Call Length sets how many bars each Listen and Repeat pattern lasts: 1, 2 or 4, and 2 is recommended. Your turn is the same number of bars right after it. "
                    + "Duck Music During Listen makes the song quieter while a pattern plays, so its notes stand out, and Ducking Amount sets how much quieter. It is off at first."))
                .Page(Loc.T("Look-ahead"),
                    Loc.T("With Play What You Hear, each note's sound plays a little before you must hit it. Look-ahead sets how long before, on guitar, bass and keys: you have {lookahead} milliseconds. "
                    + "Drums Look-ahead does the same on drums: you have {drumlookahead}. "
                    + "If your hits keep coming late, raise it; if they come early, lower it. 300 milliseconds is recommended for guitar, and 200 for drums, which have no fret to reach."))
                .Page(Loc.T("Vocals"),
                    Loc.T("Pitch Guide plays a soft tone at each new note's pitch just before you sing it. "
                    + "Pitch Feedback plays a repeating sweep while you are too far off the note to score: rising means sing higher, falling means sing lower, and silence means you are on pitch. "
                    + "Both are on until you turn them off, and Pitch Feedback needs a microphone on your profile."))
                .Page(Loc.T("Beats and repeated notes"),
                    Loc.T("Beat Ticks plays a soft tick on every beat and a stronger one on the first beat of each bar, in every mode. "
                    + "Cue Repeated Notes is off at first: a run of the same fret or chord plays only its first note and holds its tone through the run. "
                    + "Turn it on to hear every note. Drums always play every note."))
                .Page(Loc.T("Mono Sound"),
                    Loc.T("Each lane's sound plays in its own place, from far left for the leftmost lane to far right for the rightmost. "
                    + "If you hear with one ear or play on one speaker, turn on Mono Sound: every cue then plays in the centre, the same in both ears. "
                    + "The lanes still sound different, since their pitch rises from left to right. YARG's own sounds and the song stay as they are."))
                .Page(Loc.T("Speech"),
                    Loc.T("Speech During Songs picks which of YARG's pop-ups are spoken in a song. All reads note streaks, Hot Start and the rest. "
                    + "Important only, the default, reads Star Power Ready, Full Combo, New High Score and star power on and off. Off reads nothing. "
                    + "Read Position In Lists says where you are, such as 3 of 10. Read Control Types says what each item is, such as button or slider. "
                    + "Announce Menu Music names each song the menu music starts."))
                .Page(Loc.T("Volumes"),
                    Loc.T("Accessibility Cues Master Volume sets the volume of all the mod's sounds together. "
                    + "Below it come four pages: Five-Fret Volumes, Drum Volumes, Vocal Volumes and Shared Volumes. {ok} opens one, and each has a slider for every sound. "
                    + "Changing a slider plays its sound, so you hear the new level. "
                    + "Sound Glossary, at the end of the Tutorials list, plays every sound and says what it means."))
                .Page(Loc.T("Profile Settings for playing"),
                    Loc.T("Each profile has its own settings: open Profile Settings on the Profiles toolbar of a connected profile. Lefty Flip mirrors the lanes for left-handed players, and the note sounds move with them. "
                    + "Engine Preset sets the hit rules: Default has a 70 millisecond window either side of each note; Casual keeps that window but forgives extra fret presses and early hammer-ons; Precision narrows the window in fast passages, down to 20 milliseconds; and Solo Taps lets solos be played without strumming. "
                    + "Rock Meter Preset sets how quickly you fail: Casual, Easy, Normal, Hard or Unfair. Input Calibration corrects this profile's timing in milliseconds."))
                .Page(Loc.T("Settings for the picture"),
                    Loc.T("Note Speed, Highway Length, Theme, Color Profile, Camera Preset, Highway Preset and Range Disable change only what is drawn; the sounds stay the same. "
                    + "On drums, Activation picks which hit at the end of a fill turns star power on: Rightmost Note, the rightmost pad of the fill's last note, or All Notes, any pad of it. Use Cymbal Models and Open Lane Display Type change only the picture too. "
                    + "The drum lane order editor can only be used with a mouse. Each value of the picture settings has its own description in the menu."))
                .Page(Loc.T("YARG settings to know"),
                    Loc.T("A few of YARG's own settings matter most when you play by ear. "
                    + "The Sound tab has YARG's volumes; turn the music down if the note sounds are hard to hear over it. "
                    + "No Fail, on the General tab, stops songs from failing while you learn, but scores played with it are not saved. "
                    + "Open Calibrator, also on the General tab, is explained in the Getting Started tutorial.")));

            list.Add(new Tutorial { Name = Loc.T("Five-Fret Guitar and Bass"), Practice = "fivefret" }
                .Page(Loc.T("The frets"),
                    Loc.T("Guitar, bass, rhythm, co-op guitar and five-lane keys use five frets: Green, Red, Yellow, Blue and Orange, from left to right. "
                    + "Each fret has its own sound and its own place: Green is far left and lowest, Orange is far right and highest. Hold a fret and strum to play a note. {starpowerkey}"))
                .Page(Loc.T("How the notes sound"),
                    Loc.T("When the song's key is known, each fret plays a note of the song's scale, so the chart sounds like a tune. "
                    + "An open note, a strum with no fret held, is a low note in the centre. "
                    + "Strum when the hit bar sound ends: it rises from the note's sound to the moment to strum. In fast passages a click marks each hit instead."),
                    Loc.T("When the song's key is known, each fret plays a note of the song's scale, so the chart sounds like a tune. "
                    + "An open note, a strum with no fret held, is a low note in the centre. "
                    + "With Listen and Repeat on, each pattern plays its notes first; then play them back in the same rhythm. The notes you play back have no sound of their own, only the hit or miss sound. "
                    + "A click marks the moment of each note you play back."))
                .Page(Loc.T("Chords"),
                    Loc.T("A chord is several frets played together. Its notes sound together with a quick rake, five short bursts rising from dark to bright. "
                    + "Hold all of its frets and strum once."))
                .Page(Loc.T("Hammer-ons, pull-offs and taps"),
                    Loc.T("A hammer-on slides up into its note and a pull-off slides down. Play them by pressing or lifting the fret without strumming, right after a note you hit. "
                    + "A tap starts with two wooden knocks: press the fret with no strum. Taps work even after a miss."))
                .Page(Loc.T("Held notes"),
                    Loc.T("A held note keeps sounding. Keep the fret down until the Let go chimes play, then let go; letting go late does no harm. "
                    + "Letting go too early plays Hold dropped. On a held star power note, the whammy bar adds to the star power bar."),
                    Loc.T("In a pattern, a held note keeps sounding and the Let go chimes mark its end. When you play it back, hold the fret just as long; letting go late does no harm. "
                    + "Letting go too early plays Hold dropped. On a held star power note, the whammy bar adds to the star power bar."))
                .Page(Loc.T("Repeated notes"),
                    Loc.T("A run of the same fret or chord plays only its first note, then holds its tone through the run. Keep the fret down and strum on each hit bar sound. "
                    + "To hear every note, turn on Cue Repeated Notes on the YargAccess tab."),
                    Loc.T("With Listen and Repeat on, every note of a pattern plays, repeated notes too, so you hear exactly what to play back."))
                .Page(Loc.T("Mistakes and bass"),
                    Loc.T("A missed note plays the Miss sound. Strumming when there is no note is an overstrum: a buzz, and your streak breaks. "
                    + "Bass plays the same way as guitar, and its multiplier, which multiplies your points, goes up to 6 instead of 4. Lefty Flip in your profile mirrors the frets and their sounds. "
                    + "{practice}")));

            list.Add(new Tutorial { Name = Loc.T("Drums"), Practice = "drums" }
                .Page(Loc.T("The kit"),
                    Loc.T("Four-lane drums have a kick pedal and four pads: Red, Yellow, Blue and Green, from left to right. "
                    + "Each pad plays a note of the song's scale in its place, rising from left to right. The kick is a low note in the centre."))
                .Page(Loc.T("Pro drums and five-lane"),
                    Loc.T("Pro drums adds cymbals: Yellow, Blue and Green notes can each be a pad or a cymbal. A cymbal plays its lane's note an octave higher, with a shimmer. "
                    + "Five-lane drums has a Red pad, Yellow cymbal, Blue pad, Orange cymbal and Green pad, and the kick."))
                .Page(Loc.T("Hitting notes"),
                    Loc.T("Each note plays its sound {drumlookahead} milliseconds before you hit it, the Drums Look-ahead. Hit what you hear right away. "
                    + "Pads hit together sound together. A ghost note plays softly with a whisper, and an accent plays loudly with a sharp crack. "
                    + "Every note plays its own pads, repeated ones too, so a beat sounds the way it is played. On Expert that gets busy; learning it by ear is the point."),
                    Loc.T("With Listen and Repeat on, each pattern plays its pads first; then hit the same pads in the same rhythm, in time with the song. "
                    + "Pads hit together sound together. A ghost note plays softly with a whisper, and an accent plays loudly with a sharp crack. The hits you play back have no sound of their own, only the hit or miss sound."))
                .Page(Loc.T("Flams, rolls and swells"),
                    Loc.T("A flam is two pads hit a split second apart, one hand just behind the other: both pads play, the second right after the first. Hit them almost together. "
                    + "A roll is one pad hit quickly: its sound repeats fast, so keep hitting that pad, fast and steady, until it stops. "
                    + "A swell does the same on two pads in turn: their sounds alternate, and you hit the two pads one after the other until it stops. "
                    + "In a roll or swell the exact timing does not matter, only that you keep going. They are on Expert, and on Hard in some songs. The practice has all three."),
                    Loc.T("A flam is two pads hit a split second apart, one hand just behind the other: both pads play, the second right after the first. Hit them almost together. "
                    + "With Listen and Repeat on, a roll or swell plays as its separate fast hits in the pattern; play it back as fast as you can until it ends. "
                    + "A roll is one pad, a swell two pads in turn. They are on Expert, and on Hard in some songs. The practice has all three."))
                .Page(Loc.T("Fills and star power"),
                    Loc.T("On drums, star power turns on in a fill. Fills appear only while star power is ready: play anything you like, then hit the last note: its rightmost pad, or any of its pads when Activation in Profile Settings is All Notes. "
                    + "A slow rise plays through the fill and ends on that note's star power hit sound."))
                .Page(Loc.T("Mistakes"),
                    Loc.T("A missed note plays the Miss sound. Hitting a pad with no note is an overhit: a buzz, then the pad you hit plays quietly. "
                    + "{practice}")));

            list.Add(new Tutorial { Name = Loc.T("Pro Keys"), Practice = "prokeys" }
                .Page(Loc.T("The keyboard"),
                    Loc.T("Pro keys uses 25 keys, two octaves from low C to high C, like a real piano keyboard. Most players use a MIDI keyboard. "
                    + "Each key plays its own pitch an octave up, panned from low C on the left to high C on the right."))
                .Page(Loc.T("Playing notes"),
                    Loc.T("Each note plays its pitch {lookahead} milliseconds before it reaches the hit bar, the same Look-ahead as guitar. Press the key as the hit bar sound ends. "
                    + "A chord's keys play together; press them together."),
                    Loc.T("With Listen and Repeat on, each pattern plays its keys first, at their own pitches; then press the same keys in the same rhythm. "
                    + "A chord's keys play together; press them together. A click marks the moment of each note you play back."))
                .Page(Loc.T("Held notes and glissandos"),
                    Loc.T("A held key keeps sounding. Let go after the Let go chimes play. "
                    + "A glissando is a run along the white keys: it plays as one quick slide. Sweep along the keys while it lasts."))
                .Page(Loc.T("Mistakes and range shifts"),
                    Loc.T("Pressing a key with no note is an overhit: a buzz, then the key you pressed plays quietly. "
                    + "YARG sometimes shifts the highway to show a different range of keys. That changes only the picture; each sound is always the real key. "
                    + "{practice}")));

            list.Add(new Tutorial { Name = Loc.T("Vocals"), Practice = "vocals" }
                .Page(Loc.T("Singing"),
                    Loc.T("Vocals needs a microphone on your profile. YARG scores your pitch and your timing, never the words, and any octave counts. "
                    + "YargAccess does not read the lyrics: sing along with the song."))
                .Page(Loc.T("Pitch guide"),
                    Loc.T("Just before each new note, a soft tone plays at the pitch to sing. "
                    + "Spoken words, which score at any pitch, play two soft breaths instead. You can turn Pitch Guide off on the YargAccess tab."))
                .Page(Loc.T("Pitch feedback"),
                    Loc.T("While you sing too far from the note to score, a sweep repeats. A rising sweep means sing higher, a falling sweep means sing lower, and silence means you are on pitch."))
                .Page(Loc.T("Phrases"),
                    Loc.T("Vocals score one phrase at a time. When a phrase counts as hit, rated Awesome, the hit sound plays. "
                    + "YARG's rating for each phrase is spoken: Awesome, Strong, Good, Okay, Messy or Awful. Harmonies score the same way, with up to three singers, but the Pitch Guide and Pitch Feedback follow only the first singer."))
                .Page(Loc.T("Percussion and star power"),
                    Loc.T("Some songs have percussion notes, such as a tambourine. A woodblock knock plays just before each one: press your Hit button or tap the microphone. "
                    + "When star power is ready a sparkle plays; press your star power button to turn it on. "
                    + "{vocalpractice}"),
                    Loc.T("Some songs have percussion notes, such as a tambourine. With Listen and Repeat on they come in patterns: a few woodblock knocks play, then play them back with your Hit button or a tap on the microphone. "
                    + "The singing is not changed: you sing the whole part. "
                    + "When star power is ready a sparkle plays; press your star power button to turn it on. "
                    + "{vocalpractice}")));

            list.Add(new Tutorial { Name = Loc.T("Difficulties") }
                .Page(Loc.T("The difficulties"),
                    Loc.T("YARG has Beginner, Easy, Medium, Hard and Expert, and on drums Expert+. "
                    + "You choose for each song on the difficulty screen, after picking the song and your instrument. Your profile remembers the difficulty."))
                .Page(Loc.T("Beginner to Expert"),
                    Loc.T("On Beginner every note can be hit with any fret or pad, so you only play in time; pro keys has no Beginner, and on vocals Beginner accepts any pitch. "
                    + "On guitar, Easy usually uses three frets, Medium four, and Hard and Expert all five. Each step up adds notes and faster passages."))
                .Page(Loc.T("Expert+ and vocals"),
                    Loc.T("Expert+ is Expert drums with double kick notes, for two kick pedals. "
                    + "On vocals the pitch window narrows as the difficulty rises: on Beginner any pitch counts, and on Expert you need to be within about four fifths of a semitone."))
                .Page(Loc.T("Modifiers and presets"),
                    Loc.T("Modifiers change the chart, for example All Strums, No Kicks or Unpitched Only. Pick them under Modifiers on the difficulty screen. "
                    + "Your profile's engine preset sets how forgiving the hit window is, and its rock meter preset sets how quickly you fail.")));

            list.Add(new Tutorial { Name = Loc.T("Scoring and Stars") }
                .Page(Loc.T("Points"),
                    Loc.T("Each note you hit scores 50 points on guitar, bass, five-lane keys and drums. Pro drums scores 60, and each key on pro keys 120. "
                    + "Each note of a chord scores. Held notes add 25 points a beat. "
                    + "On vocals each phrase scores by difficulty, and a percussion note scores 100. A missed note scores nothing."))
                .Page(Loc.T("Streak and multiplier"),
                    Loc.T("Your streak counts the notes you hit in a row. Every 10 notes in a row raise your multiplier by one, up to 4 times, or 6 times on bass. "
                    + "A miss, an overstrum or an overhit sets the streak back to zero and the multiplier back to 1."))
                .Page(Loc.T("Stars"),
                    Loc.T("Your score earns stars, from one to five, by how it compares with the chart's base score. Above five stars come gold stars. "
                    + "YARG plays a sound each time you earn a star during the song."))
                .Page(Loc.T("After the song"),
                    Loc.T("The score screen reads your score, stars, accuracy, notes hit and missed, best streak and star power phrases. "
                    + "The song library then shows your best result for each song.")));

            list.Add(new Tutorial { Name = Loc.T("Star Power") }
                .Page(Loc.T("Star power phrases"),
                    Loc.T("Star power phrases are groups of notes that play with a sparkle. Hit every note in a phrase and a quarter of the star power bar fills. "
                    + "A four-note climbing chime marks the first note of the phrase that would make star power ready."))
                .Page(Loc.T("Star power ready"),
                    Loc.T("After a phrase, one to four rising steps say how full the bar is. With half the bar or more, star power is ready, and a bright sparkle plays. "
                    + "On guitar, bass, keys and vocals, press your star power button. On drums, hit the last note of a fill. {starpowerkey}"))
                .Page(Loc.T("Using it"),
                    Loc.T("While star power runs, your multiplier doubles. YargAccess says Star power on, then Star power off when the bar runs out. "
                    + "Miss a note in a phrase and that phrase is lost: a falling slide plays.")));

            list.Add(new Tutorial { Name = Loc.T("History and Replays") }
                .Page(Loc.T("The History menu"),
                    Loc.T("History on the main menu lists every song you have finished, newest first, in groups: Today, Yesterday, This Week, This Month, Last Three Months, This Year and More Than A Year. "
                    + "Each entry reads the song, artist, stars, part, percent, score, hits and misses, the play style, the speed if it was not 100 percent, any bots, and the date and time. "
                    + "With several players it reads the band score and each player. {side} jump to the previous or next group. {historyhomeend}"))
                .Page(Loc.T("Replays"),
                    Loc.T("YARG saves a replay of every song you finish: each button you pressed and when. Watching it plays the song again exactly as you played it, with the same note sounds and hit and miss sounds, at the speed you played. "
                    + "{ok} on an entry watches its replay. A replay needs the same chart: if the song was deleted or changed, YARG says the song is unavailable or the replay can't be played."))
                .Page(Loc.T("Other ways to use an entry"),
                    Loc.T("{playwithreplay} is Play With Replay: you play the song yourself while the replay's players play alongside you, as if they were in the band. Pick your part and difficulty first, as for any song. "
                    + "Only player one has note sounds: you, or the first bot when only bots play along. The replay's players play without them, so you hear the song and your own part. "
                    + "{blue} plays the song yourself, alone, in your Play Style, as from the song library. "
                    + "{yellow} is YARG's own Analyze: it replays the entry and checks it still gives the same score with this version of the game. The result is only written to YARG's log file; nothing is shown or said."))
                .Page(Loc.T("Exporting and deleting"),
                    Loc.T("{entrykeys} Export saves a copy of the replay as a file you can share or keep. "
                    + "Deleting asks first. It removes the entry and its score from History for good, and moves its replay to the Recycle Bin. "
                    + "On the Import tab, the same key removes an entry from that list and keeps the replay file. "
                    + "A Listen and Repeat replay is saved with a second file ending in .yargaccess: keep it next to the replay, or the replay plays back the whole chart."))
                .Page(Loc.T("Watching a replay"),
                    Loc.T("While a replay plays, nobody is playing, so the buttons control the replay: {replaykeys} "
                    + "The replay menu has Resume, Restart, Settings and Back To Library. Seeking and speed work while paused too. {help}"))
                .Page(Loc.T("When a replay ends"),
                    Loc.T("At the end of the song a replay stops and stays paused on its last moment, and YargAccess says Replay finished. There is no score screen, because the score was already saved when you played. "
                    + "Open the replay menu ({pause}, or {back}) and choose Back To Library, or seek back to watch part of it again."))
                .Page(Loc.T("Imported replays"),
                    Loc.T("History has two tabs: History, and Import. {historytabs} "
                    + "The Import tab lists replay files that came from somewhere else: a replay you saved in the middle of a song with Save Replay in the pause menu, or a replay file someone sent you. "
                    + "On the Import tab, moving up past the first entry reaches Import Replay, which opens a file browser to add a replay file. "
                    + "In the file browser, Backspace goes to the folder above and Escape cancels; when saving, Save here uses the file name shown, and File name lets you type another. "
                    + "A replay from someone else plays only if you have the same song and chart.")));

            list.Add(new Tutorial { Name = Loc.T("Practice Mode") }
                .Page(Loc.T("Practice"),
                    Loc.T("Practice on the main menu picks a song, then a section, or a range from a start section to an end section. "
                    + "The song then loops that part, and you can slow it down."))
                .Page(Loc.T("Practice keys"),
                    Loc.T("{practicef2} F3 slows the song and F4 speeds it up. "
                    + "F8 restarts the section. F9 turns section announcements on or off. F1 lists these keys. "
                    + "The note sounds keep their Look-ahead in real time, so a slow song stays playable.")));

            return list;
        }
    }

    [HarmonyPatch(typeof(MainMenu), "Start")]
    static class MainMenuTutorialsPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(MainMenu), "Start");

        static void Postfix(MainMenu __instance) { Tutorials.InstallButton(__instance); }
    }
}
