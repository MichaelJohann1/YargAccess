using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using MelonLoader;
using YARG.Menu.Navigation;

namespace YargAccess
{
    public static class InputDriver
    {
        class Rep { public float pressedAt; public float lastFire; public bool active; }
        static readonly Rep _up = new Rep();
        static readonly Rep _down = new Rep();
        static readonly Rep _left = new Rep();
        static readonly Rep _right = new Rep();
        static readonly Rep _gpUp = new Rep();
        static readonly Rep _gpDown = new Rep();
        static readonly Rep _gpLeft = new Rep();
        static readonly Rep _gpRight = new Rep();
        static readonly Rep _guUp = new Rep();
        static readonly Rep _guDown = new Rep();
        static readonly Rep _guLeft = new Rep();
        static readonly Rep _guRight = new Rep();
        static readonly Rep _lb = new Rep();
        static readonly Rep _rb = new Rep();

        const float InitialDelay = 0.4f;
        const float RepeatInterval = 0.06f;

        static YARG.Menu.Navigation.Navigator _navHooked;

        static void HookNavigator()
        {
            if (_navHooked != null) return;
            var nav = UnityEngine.Object.FindObjectOfType<YARG.Menu.Navigation.Navigator>();
            if (nav == null) return;
            nav.NavigationEvent += ctx => MenuAnnouncer.CancelSuppress();
            _navHooked = nav;
        }

        public static void Tick()
        {
            HookNavigator();
            MenuAnnouncer.SafetyFlush();
            DropEmptyPanelGroup();
            ListMenuAnnouncer.Flush();
            MenuAnnouncer.TickGrace();

            bool inGameplay = GameplayContext.InGameplay;
            bool paused = inGameplay && GameplayContext.Paused;

            if (inGameplay) UnboundStartPause();
            if (inGameplay && !paused) { HelpList.Reset(); HighwaySound.Tick(); DrumSound.Tick(); ProKeysSound.Tick(); VocalSound.Tick(); BeatTicks.Tick(); ListenRepeat.Tick(); PracticeAssist.TickInSong(); GameplayContext.WatchReplayEnd(); ReplayControls.Tick(); return; }
            HighwaySound.Silence();
            DrumSound.Silence();
            ProKeysSound.Silence();

            DialogAnnouncer.Tick();
            ShowPickerNavigator.Tick();
            if (paused) PracticeSectionNavigator.Tick();
            if (!inGameplay)
            {
                MusicControl.Tick();
                CalibratorAnnouncer.Tick();
                CreditsNavigator.Tick();
                Tutorials.Tick();
                GuidedPractice.Tick();
                NvdaSpeech.FlushLog();
            }

            TrackLastDevice();
            if (!TextEdit.IsActive && GamepadLastUsed && Gamepad.current != null)
            {
                var f = FocusedField();
                if (f != null) TextEdit.Begin(f, FieldLabel(f), null);
            }

            if (TextEdit.Tick()) return;
            if (HelpList.Tick()) return;
            if (ButtonTest.Tick()) return;
            if (DrumButtonTest.Tick()) return;
            if (KeysButtonTest.Tick()) return;
            if (VocalButtonTest.Tick()) return;
            ScanAnnouncer.Tick();
            FileBrowserNavigator.Tick();
            if (QuickBindAnnouncer.Tick()) return;
            if (ReviewBinds.CaptureTick()) return;
            ProfileInfoNavigator.Tick();
            if (ProfileInfoNavigator.Listening)
            {
                var k = Keyboard.current;
                if (k != null && (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame)) Back();
                return;
            }
            if (ReviewAllowed()) ReviewBinds.Tick();

            PollGamepad(paused);
            PollGuitar();

            var kb = Keyboard.current;
            if (kb == null) return;
            bool typing = TextFieldFocused();
            try
            {
                if (Fire(kb.upArrowKey, _up)) MoveUp();
                if (Fire(kb.downArrowKey, _down)) MoveDown();
                if (Fire(kb.leftArrowKey, _left) && !typing) SliderLeft();
                if (Fire(kb.rightArrowKey, _right) && !typing) SliderRight();

                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Confirm();

                if (!paused && kb.tabKey.wasPressedThisFrame)
                {
                    if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) TabBackward();
                    else TabForward();
                }

                if ((kb.escapeKey.wasPressedThisFrame && !inGameplay) || (kb.backspaceKey.wasPressedThisFrame && !typing)) Back();

                if (kb.deleteKey.wasPressedThisFrame && !typing && !TabsCovered() && !EntryActions.Delete()) DeleteCurrent();

                if (kb.f1Key.wasPressedThisFrame) ContextHelp.Open(ContextHelp.Device.Keyboard);
                if (!typing && ReviewAllowed())
                {
                    if (Fire(kb.leftBracketKey, _rvPrev)) TextReview.Previous();
                    if (Fire(kb.rightBracketKey, _rvNext)) TextReview.Next();
                    if (kb.backslashKey.wasPressedThisFrame) TextReview.All();
                }
                if (FileBrowserNavigator.IsActive && kb.homeKey.wasPressedThisFrame) FileBrowserNavigator.Jump(false);
                if (FileBrowserNavigator.IsActive && kb.endKey.wasPressedThisFrame) FileBrowserNavigator.Jump(true);
                if (CreditsNavigator.IsActive && kb.homeKey.wasPressedThisFrame) CreditsNavigator.Jump(false);
                if (CreditsNavigator.IsActive && kb.endKey.wasPressedThisFrame) CreditsNavigator.Jump(true);
                if (kb.homeKey.wasPressedThisFrame && !typing && !FileBrowserNavigator.IsActive) HistoryNavigator.Edge(false);
                if (kb.endKey.wasPressedThisFrame && !typing && !FileBrowserNavigator.IsActive) HistoryNavigator.Edge(true);

                if (kb.eKey.wasPressedThisFrame && !typing && !FileBrowserNavigator.IsActive && !DialogNavigator.IsActive && HistoryNavigator.IsActive)
                    HistoryNavigator.ExportSelected();

                if (!paused)
                {
                    if (kb.f2Key.wasPressedThisFrame && !FileBrowserNavigator.IsActive) WhatsNew.Read();
                    if (kb.f5Key.wasPressedThisFrame) MusicControl.AnnounceNowPlaying();
                    if (kb.f6Key.wasPressedThisFrame) MusicControl.TogglePlay();
                    if (kb.f7Key.wasPressedThisFrame) MusicControl.Previous();
                    if (kb.f8Key.wasPressedThisFrame) MusicControl.Next();

                    if (!TextFieldFocused() && !CustomNavActive())
                    {
                        ColoredKey(kb.digit1Key, "Green",  _c1);
                        ColoredKey(kb.digit2Key, "Red",    _c2);
                        ColoredKey(kb.digit3Key, "Yellow", _c3);
                        ColoredKey(kb.digit4Key, "Blue",   _c4);
                        ColoredKey(kb.digit5Key, "Orange", _c5);
                    }
                }
            }
            catch (Exception ex) { ModLog.Warn("InputDriver.Tick", ex); }
        }

        public static bool Repeating { get; private set; }

        static bool Fire(ButtonControl key, Rep st)
        {
            if (key == null) return false;
            float now = Time.unscaledTime;
            if (key.wasPressedThisFrame) { st.active = true; st.pressedAt = now; st.lastFire = now; Repeating = false; return true; }
            if (st.active && key.isPressed)
            {
                if (now - st.pressedAt >= InitialDelay && now - st.lastFire >= RepeatInterval) { st.lastFire = now; Repeating = true; return true; }
                return false;
            }
            if (!key.isPressed) st.active = false;
            return false;
        }

        static readonly Rep _gsUp = new Rep(), _gsDown = new Rep(), _gsLeft = new Rep(), _gsRight = new Rep();
        static readonly Held _gpGreen = new Held(), _gpYellow = new Held(), _gpBlue = new Held(), _gpOrange = new Held();
        static readonly Held _guGreen = new Held(), _guYellow = new Held(), _guBlue = new Held(), _guOrange = new Held();

        static void ConfirmButton(ButtonControl b, Held st)
        {
            if (b == null) return;
            float secs;
            bool holdable = !GameplayContext.Paused && !TextFieldFocused() && !CustomNavActive()
                            && YargInput.HasHold("Green", out secs) && secs > 0f;
            if (b.wasPressedThisFrame)
            {
                if (!holdable) { Confirm(); return; }
                st.down = true; st.at = Time.unscaledTime; st.fired = false;
                return;
            }
            if (!st.down) return;
            if (b.isPressed)
            {
                float need;
                if (!st.fired && YargInput.HasHold("Green", out need) && Time.unscaledTime - st.at >= need)
                {
                    st.fired = true;
                    ColoredAction("Green", true);
                }
                return;
            }
            st.down = false;
            if (!st.fired) Confirm();
            st.fired = false;
        }

        internal static string LeftRightHint()
        {
            return Keys.LeftRight + " change it";
        }

        static void PollGamepad(bool paused)
        {
            var gp = Gamepad.current;
            if (gp == null) return;
            try
            {
                if (ShowPickerNavigator.IsActive)
                {
                    if (Fire(gp.dpad.up, _gpUp)) MoveUp();
                    if (Fire(gp.dpad.down, _gpDown)) MoveDown();
                    if (gp.dpad.left.wasPressedThisFrame || gp.dpad.right.wasPressedThisFrame) ShowPickerNavigator.SayTime();
                    if (gp.selectButton.wasPressedThisFrame) Back();
                    return;
                }
                HelpButton(gp.selectButton, _viewHeld, ContextHelp.Device.Gamepad, gp);
                if (ReviewAllowed()) ReviewStick(gp.rightStick.ReadValue());
                // The bumpers act once per press (no hold-repeat, like Tab), and only where there are tabs or
                // sections: RB in a menu without them moved to a hidden tab that LB, Orange there, could not leave.
                if (TabTargetActive() || PracticeSectionNavigator.IsActive || paused)
                {
                    if (gp.leftShoulder.wasPressedThisFrame) Bumper(-1, paused);
                    if (gp.rightShoulder.wasPressedThisFrame) Bumper(+1, paused);
                }
                else if (!paused && !TextFieldFocused() && !CustomNavActive())
                {
                    ColoredKey(gp.leftShoulder, "Orange", _gpOrange);
                    // History: LB is Play With Replay (Orange), so RB alone switches its two tabs.
                    if (gp.rightShoulder.wasPressedThisFrame && HistoryNavigator.IsActive && !FileBrowserNavigator.IsActive) TabForward();
                }
                if (!paused)
                {
                    if (!GameplayContext.InGameplay && gp.rightTrigger.wasPressedThisFrame) WhatsNew.Read();
                    // History: export / delete list; song library: delete the song.
                    if (!GameplayContext.InGameplay && gp.leftTrigger.wasPressedThisFrame) EntryActions.Open();
                    StickButton(gp.leftStickButton, _l3, MusicControl.TogglePlay, MusicControl.Previous);
                    StickButton(gp.rightStickButton, _r3, MusicControl.AnnounceNowPlaying, MusicControl.Next);
                }

                bool sw = gp is UnityEngine.InputSystem.Switch.SwitchProControllerHID;
                if (Fire(gp.dpad.up, _gpUp) | Fire(gp.leftStick.up, _gsUp)) MoveUp();
                if (Fire(gp.dpad.down, _gpDown) | Fire(gp.leftStick.down, _gsDown)) MoveDown();
                if (Fire(gp.dpad.left, _gpLeft) | Fire(gp.leftStick.left, _gsLeft)) SliderLeft();
                if (Fire(gp.dpad.right, _gpRight) | Fire(gp.leftStick.right, _gsRight)) SliderRight();
                ConfirmButton(sw ? gp.buttonEast : gp.buttonSouth, _gpGreen);
                if ((sw ? gp.buttonSouth : gp.buttonEast).wasPressedThisFrame) Back();
                if (!paused && !TextFieldFocused() && !CustomNavActive())
                {
                    ColoredKey(sw ? gp.buttonWest : gp.buttonNorth, "Yellow", _gpYellow);
                    ColoredKey(sw ? gp.buttonNorth : gp.buttonWest, "Blue", _gpBlue);
                }
            }
            catch (Exception ex) { ModLog.Warn("InputDriver.PollGamepad", ex); }
        }

        static void Bumper(int dir, bool paused)
        {
            if (!ModSettings.BumpersSwitchTabs.Value || DialogNavigator.IsActive || TextFieldFocused()) return;
            if (PracticeSectionNavigator.IsActive) { PracticeSectionNavigator.Move(dir); return; }
            if (paused) return;
            if (dir < 0) TabBackward(); else TabForward();
        }

        const float HelpHoldSeconds = 0.5f;
        static readonly Held _viewHeld = new Held(), _selectHeld = new Held();

        static void HelpButton(ButtonControl b, Held st, ContextHelp.Device dev, InputDevice input, bool holdOnRelease = false)
        {
            float now = Time.unscaledTime;
            if (b.wasPressedThisFrame) { st.down = true; st.at = now; st.fired = false; return; }
            if (!st.down) return;
            if (b.isPressed)
            {
                if (!holdOnRelease && !st.fired && now - st.at >= HelpHoldSeconds) { st.fired = true; HoldAction(dev, input); }
                return;
            }
            st.down = false;
            if (st.fired) return;
            if (holdOnRelease && now - st.at >= HelpHoldSeconds) HoldAction(dev, input);
            else ContextHelp.Open(dev, input);
        }

        static void HoldAction(ContextHelp.Device dev, InputDevice input)
        {
            if (ContextHelp.HasSchemeAction(YARG.Core.Input.MenuAction.Select)) YargInput.Invoke("Select");
            else ContextHelp.Open(dev, input);
        }

        static readonly Rep _rvPrev = new Rep(), _rvNext = new Rep(), _guRvPrev = new Rep(), _guRvNext = new Rep();

        static bool ReviewAllowed()
        {
            return !GameplayContext.InGameplay || GameplayContext.Paused;
        }

        public static bool GuitarSelectHeld { get; private set; }

        static bool _stickCentred;
        static int _stickDir;
        static float _stickAt, _stickLast;

        static void ReviewStick(Vector2 v)
        {
            float now = Time.unscaledTime;
            int dir = v.x < -0.6f ? -1 : v.x > 0.6f ? 1 : v.y < -0.6f ? 2 : 0;
            if (Mathf.Abs(v.x) < 0.3f && Mathf.Abs(v.y) < 0.3f) { _stickCentred = true; _stickDir = 0; return; }
            if (dir == 0) return;
            if (dir != _stickDir)
            {
                if (!_stickCentred) return;
                _stickCentred = false;
                _stickDir = dir;
                _stickAt = _stickLast = now;
                if (dir == 2) TextReview.All(); else if (dir < 0) TextReview.Previous(); else TextReview.Next();
                return;
            }
            if (dir != 2 && now - _stickAt >= InitialDelay && now - _stickLast >= RepeatInterval)
            {
                _stickLast = now;
                if (dir < 0) TextReview.Previous(); else TextReview.Next();
            }
        }

        // L3 / R3: a tap does one thing, holding half a second another. L3 tap = play / pause, hold = previous
        // song; R3 tap = now playing, hold = next song.
        const float StickHoldSeconds = 0.5f;
        static readonly Held _l3 = new Held(), _r3 = new Held();

        static void StickButton(ButtonControl b, Held st, Action tap, Action hold)
        {
            float now = Time.unscaledTime;
            if (b.wasPressedThisFrame) { st.down = true; st.at = now; st.fired = false; return; }
            if (!st.down) return;
            if (b.isPressed)
            {
                if (!st.fired && now - st.at >= StickHoldSeconds) { st.fired = true; hold(); }
                return;
            }
            st.down = false;
            if (!st.fired) tap();
        }

        // Each guitar keeps its own held / repeat state: with a guitar and a bass connected, one shared state
        // made a press on one look like a release on the other (Select opened help at once, holds fired as taps).
        class GuitarState
        {
            public readonly Held Select = new Held(), Green = new Held(), Yellow = new Held(), Blue = new Held(), Orange = new Held();
            public readonly Rep Up = new Rep(), Down = new Rep(), Left = new Rep(), Right = new Rep(), RvPrev = new Rep(), RvNext = new Rep();
        }
        static readonly Dictionary<int, GuitarState> _guitars = new Dictionary<int, GuitarState>();

        static GuitarState GuitarOf(InputDevice dev)
        {
            GuitarState gs;
            if (!_guitars.TryGetValue(dev.deviceId, out gs)) _guitars[dev.deviceId] = gs = new GuitarState();
            return gs;
        }

        static void PollGuitar()
        {
            bool anySelect = false;
            try
            {
                foreach (var dev in UnityEngine.InputSystem.InputSystem.devices)
                {
                    if (dev == null || dev is Keyboard || dev is Mouse || dev is Gamepad) continue;
                    if (dev.GetType().Name.IndexOf("Guitar", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var gs = GuitarOf(dev);
                    var held = dev.TryGetChildControl<ButtonControl>("selectButton");
                    if (held != null && held.isPressed) anySelect = true;
                    foreach (var c in dev.allControls)
                    {
                        var b = c as ButtonControl;
                        if (b != null && b.wasPressedThisFrame) { LastDevice = Keys.Device.Guitar; break; }
                    }

                    var sel = dev.TryGetChildControl<ButtonControl>("selectButton");
                    if (ShowPickerNavigator.IsActive)
                    {
                        var pd = dev.TryGetChildControl<DpadControl>("dpad");
                        if (pd != null && Fire(pd.up, gs.Up)) MoveUp();
                        if (pd != null && Fire(pd.down, gs.Down)) MoveDown();
                        if (pd != null && (pd.left.wasPressedThisFrame || pd.right.wasPressedThisFrame)) ShowPickerNavigator.SayTime();
                        if (sel != null && sel.wasPressedThisFrame) Back();
                        continue;
                    }
                    bool chord = sel != null && sel.isPressed;
                    if (chord && ReviewAllowed())
                    {
                        var rg = dev.TryGetChildControl<ButtonControl>("greenFret");
                        var ry = dev.TryGetChildControl<ButtonControl>("yellowFret");
                        var rb = dev.TryGetChildControl<ButtonControl>("blueFret");
                        bool used = false;
                        if (ry != null && Fire(ry, gs.RvPrev)) { TextReview.Previous(); used = true; }
                        if (rb != null && Fire(rb, gs.RvNext)) { TextReview.Next(); used = true; }
                        if (rg != null && rg.wasPressedThisFrame) { TextReview.All(); used = true; }
                        // Menu music: Select + Red = previous song, Select + Orange = next song.
                        if (!GameplayContext.InGameplay)
                        {
                            var rr = dev.TryGetChildControl<ButtonControl>("redFret");
                            var ro = dev.TryGetChildControl<ButtonControl>("orangeFret");
                            if (rr != null && rr.wasPressedThisFrame) { MusicControl.Previous(); used = true; }
                            if (ro != null && ro.wasPressedThisFrame) { MusicControl.Next(); used = true; }
                            // YARG's message of the day: Select + strum up.
                            var su = dev.TryGetChildControl<ButtonControl>("strumUp") ?? dev.TryGetChildControl<DpadControl>("dpad")?.up;
                            if (su != null && su.wasPressedThisFrame) { WhatsNew.Read(); used = true; }
                            // Select + strum down: History's export / delete list, or delete a library song.
                            var sd = dev.TryGetChildControl<ButtonControl>("strumDown") ?? dev.TryGetChildControl<DpadControl>("dpad")?.down;
                            if (sd != null && sd.wasPressedThisFrame && EntryActions.Open()) used = true;
                        }
                        if (used) gs.Select.fired = true;
                    }
                    if (sel != null) HelpButton(sel, gs.Select, ContextHelp.Device.Guitar, dev, true);
                    if (chord) continue;

                    bool pausedNow = GameplayContext.InGameplay && GameplayContext.Paused;
                    var yellow = dev.TryGetChildControl<ButtonControl>("yellowFret");
                    var orange = dev.TryGetChildControl<ButtonControl>("orangeFret");
                    bool colours = !pausedNow && !TextFieldFocused() && !CustomNavActive();
                    if (TabTargetActive())
                    {
                        if (yellow != null && yellow.wasPressedThisFrame) Bumper(-1, pausedNow);
                        if (orange != null && orange.wasPressedThisFrame) Bumper(+1, pausedNow);
                    }
                    else if (colours)
                    {
                        ColoredKey(yellow, "Yellow", gs.Yellow);
                        ColoredKey(orange, "Orange", gs.Orange);
                    }
                    if (colours) ColoredKey(dev.TryGetChildControl<ButtonControl>("blueFret"), "Blue", gs.Blue);

                    var dpad = dev.TryGetChildControl<DpadControl>("dpad");
                    if (dpad != null)
                    {
                        if (Fire(dpad.up, gs.Up)) MoveUp();
                        if (Fire(dpad.down, gs.Down)) MoveDown();
                        if (Fire(dpad.left, gs.Left)) SliderLeft();
                        if (Fire(dpad.right, gs.Right)) SliderRight();
                    }
                    ConfirmButton(dev.TryGetChildControl<ButtonControl>("greenFret"), gs.Green);
                    var red = dev.TryGetChildControl<ButtonControl>("redFret");
                    if (red != null && red.wasPressedThisFrame) Back();
                }
            }
            catch (Exception ex) { ModLog.Warn("InputDriver.PollGuitar", ex); }
            GuitarSelectHeld = anySelect;
        }

        static void UnboundStartPause()
        {
            try
            {
                var gm = GameplayContext.Manager;
                if (gm == null) return;
                // A controller used in another window must not pause YARG behind it.
                if (!UnityEngine.Application.isFocused) return;
                if (gm.IsPractice && (gm.PracticeManager == null || !gm.PracticeManager.HasSelectedSection)) return;
                if (gm.PlayerHasFailed || YARG.Menu.Persistent.DialogManager.Instance.IsDialogShowing) return;
                foreach (var dev in UnityEngine.InputSystem.InputSystem.devices)
                {
                    if (dev == null || dev is Keyboard || dev is Mouse) continue;
                    if (dev.GetType().Name.IndexOf("Midi", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    var start = dev is Gamepad ? ((Gamepad) dev).startButton : dev.TryGetChildControl<ButtonControl>("startButton");
                    if (start == null || !start.wasPressedThisFrame || BoundToPlayer(dev)) continue;
                    gm.SetPaused(!gm.Paused);
                    return;
                }
            }
            catch (Exception ex) { ModLog.Warn("InputDriver.UnboundStartPause", ex); }
        }

        static bool BoundToPlayer(InputDevice dev)
        {
            foreach (var p in YARG.Player.PlayerContainer.Players)
            {
                if (p == null || p.Bindings == null || p.Bindings.InputDevices == null) continue;
                foreach (var d in p.Bindings.InputDevices) if (d == dev) return true;
            }
            return false;
        }

        static bool NoProfile()
        {
            try { return YARG.Player.PlayerContainer.Players.Count == 0; } catch { return false; }
        }

        public static void MoveUp()
        {
            MenuAnnouncer.CancelSuppress();
            if (ShowPickerNavigator.IsActive) { ShowPickerNavigator.Move(-1); return; }
            if (DialogNavigator.IsActive) { DialogNavigator.Move(-1); return; }
            if (FileBrowserNavigator.IsActive) { FileBrowserNavigator.Move(-1); return; }
            if (PracticeSectionNavigator.IsActive) { PracticeSectionNavigator.Move(-1); return; }
            if (ScoreScreenNavigator.IsActive) { ScoreScreenNavigator.Move(-1); return; }
            if (GameplayContext.Paused) { var pg = GameplayContext.ActivePauseGroup(); if (pg != null && pg.Count > 0) pg.SelectPrevious(true); return; }
            if (ProfileInfoNavigator.IsActive) { ProfileInfoNavigator.Move(-1); return; }
            if (CreditsNavigator.IsActive) { CreditsNavigator.Move(-1); return; }
            if (CalibratorAnnouncer.IsActive) { ContextHelp.Speak(); return; }
            if (InProfilesList()) { ProfilesNavigator.Move(-1); return; }
            if (SettingsExtras.IsActive && SettingsExtras.MoveUp()) return;
            if (HistoryNavigator.IsActive && HistoryNavigator.MoveUp()) return;
            if (PresetsNavigator.IsActive && PresetsNavigator.MoveUp()) return;
            if (DifficultySelectExtras.MoveUp()) return;
            var g = NavigationGroup.CurrentNavigationGroup;
            if (g != null && g.Count > 0) { g.SelectPrevious(true); return; }
            if (ListMenuActive()) { YargInput.Invoke("Up"); ListMenuAnnouncer.CheckSoon(); return; }
            ButtonNavigator.Move(-1);
        }

        public static void MoveDown()
        {
            MenuAnnouncer.CancelSuppress();
            if (ShowPickerNavigator.IsActive) { ShowPickerNavigator.Move(+1); return; }
            if (DialogNavigator.IsActive) { DialogNavigator.Move(+1); return; }
            if (FileBrowserNavigator.IsActive) { FileBrowserNavigator.Move(+1); return; }
            if (PracticeSectionNavigator.IsActive) { PracticeSectionNavigator.Move(+1); return; }
            if (ScoreScreenNavigator.IsActive) { ScoreScreenNavigator.Move(+1); return; }
            if (GameplayContext.Paused) { var pg = GameplayContext.ActivePauseGroup(); if (pg != null && pg.Count > 0) pg.SelectNext(true); return; }
            if (ProfileInfoNavigator.IsActive) { ProfileInfoNavigator.Move(+1); return; }
            if (CreditsNavigator.IsActive) { CreditsNavigator.Move(+1); return; }
            if (CalibratorAnnouncer.IsActive) { ContextHelp.Speak(); return; }
            if (InProfilesList()) { ProfilesNavigator.Move(+1); return; }
            if (SettingsExtras.IsActive && SettingsExtras.MoveDown()) return;
            if (HistoryNavigator.IsActive && HistoryNavigator.MoveDown()) return;
            if (PresetsNavigator.IsActive && PresetsNavigator.MoveDown()) return;
            if (DifficultySelectExtras.MoveDown()) return;
            var g = NavigationGroup.CurrentNavigationGroup;
            if (g != null && g.Count > 0) { g.SelectNext(true); return; }
            if (ListMenuActive()) { YargInput.Invoke("Down"); ListMenuAnnouncer.CheckSoon(); return; }
            ButtonNavigator.Move(+1);
        }

        public static void Confirm()
        {
            MenuAnnouncer.CancelSuppress();
            if (ShowPickerNavigator.IsActive) { ShowPickerNavigator.Confirm(); return; }
            if (DialogNavigator.IsActive) { DialogNavigator.Confirm(); return; }
            if (FileBrowserNavigator.IsActive) { FileBrowserNavigator.Confirm(); return; }
            if (PracticeSectionNavigator.IsActive) { PracticeSectionNavigator.Confirm(); return; }
            if (ScoreScreenNavigator.IsActive) { ScoreScreenNavigator.Confirm(); return; }
            if (GameplayContext.Paused)
            {
                var pg = GameplayContext.ActivePauseGroup();
                if (pg == null) return;
                var psel = pg.SelectedBehaviour;
                if (PauseSettingsAccess.IsSetting(psel))
                {
                    if (PauseSettingsAccess.IsToggle(psel) && PauseSettingsAccess.Toggle(psel))
                        MenuAnnouncer.Say(PauseSettingsAccess.ValueText(psel));
                    return;
                }
                pg.ConfirmSelection();
                AnnounceToggleAfterConfirm(pg);
                return;
            }
            if (ProfileInfoNavigator.IsActive) { ProfileInfoNavigator.Confirm(); return; }
            if (CreditsNavigator.IsActive) { CreditsNavigator.Repeat(); return; }
            if (CalibratorAnnouncer.IsActive && CalibratorAnnouncer.Confirm()) return;
            if (InProfilesList()) { ProfilesNavigator.Confirm(); return; }
            if (SettingsExtras.IsActive && SettingsExtras.Confirm()) return;
            if (HistoryNavigator.IsActive && HistoryNavigator.Confirm()) return;
            if (PresetsNavigator.IsActive && PresetsNavigator.Confirm()) return;
            if (DifficultySelectExtras.Confirm()) return;
            if (FiltersNavigator.Confirm()) return;
            var g = NavigationGroup.CurrentNavigationGroup;
            var sel = g != null ? g.SelectedBehaviour : null;
            if (sel != null && SettingsAccess.IsButtonRow(sel) && SettingsAccess.ActivateButtonRow(sel)) return;
            var vis = SettingsAccess.GetVisual(sel);
            if (vis != null)
            {
                if (SettingsAccess.IsEditable(vis) && SettingsAccess.Activate(vis)) return;
                if (SettingsAccess.IsToggle(vis))
                {
                    if (SettingsAccess.ToggleValue(vis)) MenuAnnouncer.Say(SettingsAccess.ValueOnly(vis));
                    return;
                }
                if (SettingsAccess.IsAdjustable(vis)) { MenuAnnouncer.Say(LeftRightHint()); return; }
            }
            if (g != null && g.Count > 0)
            {
                if (DifficultySelectExtras.IsActive)
                {
                    var before = sel != null ? Describer.Describe(sel) : null;
                    MenuAnnouncer.BeginBatch();
                    g.ConfirmSelection();
                    MenuAnnouncer.DiscardBatch();
                    var after = NavigationGroup.CurrentNavigationGroup;
                    var nb = after != null ? after.SelectedBehaviour : null;
                    if (nb != null && Describer.Describe(nb) != before) MenuAnnouncer.Announce(nb, true);
                    return;
                }
                if (sel != null && sel.GetType().Name == "SettingCategoryView")
                {
                    var category = Describer.GetLabel(sel.gameObject);
                    MenuAnnouncer.BeginBatch();
                    g.ConfirmSelection();
                    MenuAnnouncer.EndBatchSettle(Patches.PushMenuPatch.AnnounceCurrent);
                    MenuAnnouncer.Say(category);
                    return;
                }
                g.ConfirmSelection();
                AnnounceToggleAfterConfirm(g);
                return;
            }
            if (ListMenuActive())
            {
                if (!HasConnectedProfile() && SongSelected()) { MenuAnnouncer.SayShown("Connect a profile first. Profiles is on the main menu."); return; }
                YargInput.Invoke("Green");
                return;
            }
            ButtonNavigator.Confirm();
        }

        static void AnnounceToggleAfterConfirm(NavigationGroup g)
        {
            try
            {
                var sel = g != null ? g.SelectedBehaviour : null;
                if (sel == null || !Describer.IsInPlaceToggle(sel.gameObject)) return;
                if (g.SelectedBehaviour == sel) MenuAnnouncer.Announce(sel, true);
            }
            catch (Exception ex) { ModLog.Warn("InputDriver.AnnounceToggleAfterConfirm", ex); }
        }

        static bool InProfilesList()
        {
            if (!ProfilesNavigator.IsActive) return false;
            var g = NavigationGroup.CurrentNavigationGroup;
            if (g != null && ((UnityEngine.MonoBehaviour)g).gameObject.name.IndexOf("Dialog", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return true;
        }

        static bool ListMenuActive()
        {
            return UnityEngine.Object.FindObjectOfType<YARG.Menu.MusicLibrary.MusicLibraryMenu>() != null
                || UnityEngine.Object.FindObjectOfType<YARG.Menu.History.HistoryMenu>() != null;
        }

        class Held { public bool down; public float at; public bool fired; }
        static readonly Held _c1 = new Held(), _c2 = new Held(), _c3 = new Held(), _c4 = new Held(), _c5 = new Held();

        static void ColoredKey(ButtonControl key, string action, Held st)
        {
            if (key == null) return;
            float now = Time.unscaledTime;

            if (key.wasPressedThisFrame)
            {
                float secs;
                if (!YargInput.HasHold(action, out secs) || secs <= 0f) { ColoredAction(action); return; }
                st.down = true; st.at = now; st.fired = false;
                return;
            }

            if (!st.down) return;

            float need;
            if (key.isPressed)
            {
                if (!st.fired && YargInput.HasHold(action, out need) && now - st.at >= need)
                {
                    st.fired = true;
                    ColoredAction(action, true);
                }
                return;
            }

            st.down = false;
            if (!st.fired) ColoredAction(action);
            st.fired = false;
        }

        static void ColoredAction(string action) { ColoredAction(action, false); }

        static void ColoredAction(string action, bool hold)
        {
            MenuAnnouncer.CancelSuppress();
            if (ShowPickerNavigator.PickColor(action)) return;
            if (!hold && HistoryNavigator.Colour(action)) return;
            if ((action == "Yellow" || action == "Blue" || action == "Orange") && !HasConnectedProfile())
            {
                MenuAnnouncer.SayShown("Connect a profile first.");
                return;
            }
            var name = ContextHelp.ActionName(action, hold);
            MenuAnnouncer.BeginSettle(null);
            ListMenuAnnouncer.ResetLast();
            int toasts = Patches.ToastPatch.Count;
            YargInput.Invoke(action, hold);
            if (!string.IsNullOrEmpty(name) && Patches.ToastPatch.Count == toasts) MenuAnnouncer.Say(name);
        }

        static bool SongSelected()
        {
            try
            {
                var ml = UnityEngine.Object.FindObjectOfType<YARG.Menu.MusicLibrary.MusicLibraryMenu>();
                return ml != null && ml.CurrentSelection is YARG.Menu.MusicLibrary.SongViewType;
            }
            catch { return false; }
        }

        public static bool SettingsOpen()
        {
            try { return UnityEngine.Object.FindObjectOfType<YARG.Menu.Settings.SettingsMenu>() != null; }
            catch { return false; }
        }

        static bool TabTargetActive()
        {
            return PracticeSectionNavigator.IsActive || ProfileInfoNavigator.IsActive || InProfilesList()
                || (SettingsOpen() && FindGroupByName("Tabs") != null);
        }

        public static bool PadOwnedByMod()
        {
            return DialogNavigator.IsActive || ProfileInfoNavigator.IsActive || InProfilesList()
                || CalibratorAnnouncer.IsActive || ScoreScreenNavigator.IsActive
                || (GameplayContext.InGameplay && GameplayContext.Paused)
                || FiltersNavigator.IsActive
                || CreditsNavigator.IsActive
                || SettingsExtras.IsActive
                || FileBrowserNavigator.IsActive;
        }

        static bool HasConnectedProfile()
        {
            try { return YARG.Player.PlayerContainer.Players.Count > 0; } catch { return true; }
        }

        public static Keys.Device LastDevice { get; private set; }
        public static bool GamepadLastUsed => LastDevice == Keys.Device.Gamepad;

        static void TrackLastDevice()
        {
            try
            {
                var kb = Keyboard.current;
                if (kb != null && kb.anyKey.wasPressedThisFrame) { LastDevice = Keys.Device.Keyboard; return; }
                var gp = Gamepad.current;
                if (gp == null) return;
                if (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame || gp.buttonWest.wasPressedThisFrame
                    || gp.buttonNorth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame
                    || gp.dpad.up.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame || gp.dpad.left.wasPressedThisFrame
                    || gp.dpad.right.wasPressedThisFrame || gp.leftShoulder.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame)
                    LastDevice = Keys.Device.Gamepad;
            }
            catch { }
        }

        static TMPro.TMP_InputField FocusedField()
        {
            foreach (var f in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_InputField>())
                if (f != null && f.isFocused) return f;
            return null;
        }

        static string FieldLabel(TMPro.TMP_InputField f)
        {
            try
            {
                var p = f.placeholder as TMPro.TMP_Text;
                var t = p != null ? TextUtil.Clean(p.text) : null;
                if (!string.IsNullOrEmpty(t)) return t;
            }
            catch { }
            return "text box";
        }

        static bool TextFieldFocused()
        {
            foreach (var f in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_InputField>())
                if (f != null && f.isFocused) return true;
            return false;
        }

        static bool CustomNavActive()
        {
            return DialogNavigator.IsActive || ProfileInfoNavigator.IsActive || InProfilesList() || CalibratorAnnouncer.IsActive || FileBrowserNavigator.IsActive;
        }

        public static void SliderLeft() { if (FileBrowserNavigator.IsActive) return; if (Tutorials.Turn(-1)) return; if (ShowPickerNavigator.IsActive) { ShowPickerNavigator.SayTime(); return; } if (GameplayContext.Paused) { AdjustPaused(-1); return; } if (InProfilesList()) { ProfilesNavigator.Adjust(-1); return; } if (!AdjustCurrent(-1)) SideStep(-1); }
        public static void SliderRight() { if (FileBrowserNavigator.IsActive) return; if (Tutorials.Turn(+1)) return; if (ShowPickerNavigator.IsActive) { ShowPickerNavigator.SayTime(); return; } if (GameplayContext.Paused) { AdjustPaused(+1); return; } if (InProfilesList()) { ProfilesNavigator.Adjust(+1); return; } if (!AdjustCurrent(+1)) SideStep(+1); }

        static void AdjustPaused(int dir)
        {
            if (PracticeAssist.TryAdjustAb(dir)) return;
            var sel = PauseSettingsAccess.Selected();
            if (sel != null && PauseSettingsAccess.Adjust(sel, dir))
            {
                MenuAnnouncer.Say(PauseSettingsAccess.ValueText(sel));
                return;
            }
            YargInput.Invoke(dir < 0 ? "Left" : "Right");
        }

        static void SideStep(int dir)
        {
            if (SettingsOpen()) return;
            YargInput.Invoke(dir < 0 ? "Left" : "Right");
        }

        static bool AdjustCurrent(int dir)
        {
            if (ScoreScreenNavigator.IsActive) { ScoreScreenNavigator.SwitchCard(dir); return true; }
            if (DifficultySelectExtras.Adjust(dir)) return true;
            if (HistoryNavigator.IsActive && HistoryNavigator.Jump(dir)) return true;
            if (PresetsNavigator.IsActive && PresetsNavigator.Adjust(dir)) return true;
            var g = NavigationGroup.CurrentNavigationGroup;
            if (g != null && g.SelectedBehaviour != null && SettingsAccess.IsButtonRow(g.SelectedBehaviour))
                return SettingsAccess.AdjustButtonRow(g.SelectedBehaviour, dir);
            var vis = g != null ? SettingsAccess.GetVisual(g.SelectedBehaviour) : null;
            if (vis != null && SettingsAccess.Adjust(vis, dir))
            {
                MenuAnnouncer.Say(SettingsAccess.ValueOnly(vis));
                return true;
            }
            return false;
        }

        // Tabs behind a dialog or the file browser stay where they are.
        static bool TabsCovered() => DialogNavigator.IsActive || FileBrowserNavigator.IsActive;

        public static void TabForward() { if (TabsCovered()) return; if (ProfileInfoNavigator.IsActive) { ProfileInfoNavigator.Tab(+1); return; } if (InProfilesList()) { ProfilesNavigator.Tab(+1); return; } SwitchTab(+1); }
        public static void TabBackward() { if (TabsCovered()) return; if (ProfileInfoNavigator.IsActive) { ProfileInfoNavigator.Tab(-1); return; } if (InProfilesList()) { ProfilesNavigator.Tab(-1); return; } SwitchTab(-1); }

        static void SwitchTab(int dir)
        {
            SoundGlossaryPage.Leave();
            SettingsExtras.ResetZone();
            PresetsNavigator.ResetZone();
            HistoryNavigator.ResetZone();
            var tabs = FindGroupByName("Tabs");
            if (tabs == null) return;
            int count = tabs.Count;
            if (count <= 1) return;
            int cur = tabs.SelectedIndex.HasValue ? tabs.SelectedIndex.Value : 0;
            int nv = Mathf.Clamp(cur + dir, 0, count - 1);
            if (nv == cur) return;

            MenuAnnouncer.BeginBatch();
            tabs.SelectAt(nv, SelectionOrigin.Programmatically);
            tabs.ConfirmSelection();
            MenuAnnouncer.DiscardBatch();

            var sel = tabs.SelectedBehaviour;
            var name = sel != null ? Describer.GetLabel(sel.gameObject) : ("Tab " + (nv + 1));
            var msg = name + Keys.Pos(nv, count);
            var g = NavigationGroup.CurrentNavigationGroup;
            var presets = PresetsNavigator.IsActive ? PresetsNavigator.EntryText() : null;
            if (presets != null) msg += ". " + presets;
            else if (g != null && g != tabs && g.SelectedBehaviour != null)
            {
                var first = Describer.Describe(g.SelectedBehaviour);
                if (!string.IsNullOrEmpty(first)) msg += ". " + first;
            }
            else if (ListMenuActive())
            {
                var row = ListMenuAnnouncer.TakeActiveLine();
                if (!string.IsNullOrEmpty(row)) msg += ". " + row;
            }
            MenuAnnouncer.Focused(msg);
        }

        public static void Back()
        {
            ListMenuAnnouncer.ResetLast();
            if (ShowPickerNavigator.Back()) return;
            if (DialogNavigator.Back()) return;
            if (FileBrowserNavigator.IsActive) { FileBrowserNavigator.Back(); return; }
            if (!DialogNavigator.IsActive && PracticeSectionNavigator.IsActive && PracticeSectionNavigator.Back()) return;
            if (!DialogNavigator.IsActive && CalibratorAnnouncer.IsActive && CalibratorAnnouncer.Back()) return;
            if (ProfileInfoNavigator.IsActive && ProfileInfoNavigator.Back()) return;
            if (InProfilesList() && ProfilesNavigator.Back()) return;
            if (YargInput.Invoke("Red")) return;
            try
            {
                var mm = UnityEngine.Object.FindObjectOfType<YARG.Menu.MenuManager>();
                if (mm != null) mm.PopMenu();
            }
            catch (Exception ex) { ModLog.Warn("InputDriver.Back", ex); }
        }

        public static void DeleteCurrent()
        {
            var g = NavigationGroup.CurrentNavigationGroup;
            var vis = g != null ? SettingsAccess.GetVisual(g.SelectedBehaviour) : null;
            if (vis != null && SettingsAccess.Remove(vis)) MenuAnnouncer.Focused(SettingsAccess.GetName(vis) + ", " + SettingsAccess.ValueOnly(vis));
        }

        static System.Reflection.FieldInfo _groupStackF;
        static void DropEmptyPanelGroup()
        {
            try
            {
                if (_groupStackF == null) _groupStackF = typeof(NavigationGroup).GetField("_navGroupsStack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var g = NavigationGroup.CurrentNavigationGroup;
                if (g != null && !g.gameObject.activeInHierarchy)
                {
                    var all = _groupStackF != null ? _groupStackF.GetValue(null) as System.Collections.IList : null;
                    if (all != null) all.Remove(g);
                    return;
                }
                if (g == null || g.Count > 0) return;
                bool panel = false;
                for (var t = g.transform; t != null; t = t.parent)
                    if (t.name.IndexOf("InformationPanel", StringComparison.OrdinalIgnoreCase) >= 0) { panel = true; break; }
                if (!panel) return;
                if (_groupStackF == null) _groupStackF = typeof(NavigationGroup).GetField("_navGroupsStack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var stack = _groupStackF != null ? _groupStackF.GetValue(null) as System.Collections.IList : null;
                if (stack != null && stack.Count > 1) stack.Remove(g);
            }
            catch (Exception ex) { ModLog.Warn("InputDriver.DropEmptyPanelGroup", ex); }
        }

        static NavigationGroup FindGroupByName(string n)
        {
            foreach (var ng in UnityEngine.Object.FindObjectsOfType<NavigationGroup>())
                if (ng.gameObject.name == n) return ng;
            return null;
        }
    }
}
