using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Core;
using YARG.Core.Input;
using YARG.Player;
using YargAccess.Audio;

namespace YargAccess
{
    public static class ButtonTest
    {
        public static bool IsOpen { get; private set; }
        public static bool KeyboardKeys => !_keyboardProfile && (Keys.Current == Keys.Device.Keyboard || _subscribed.Count == 0);

        static int _openedFrame = -1;
        static readonly List<YargPlayer> _subscribed = new List<YargPlayer>();
        static bool _keyboardProfile;
        static readonly Queue<GameInput> _inputs = new Queue<GameInput>();
        static int _held;
        static float _whammyAt = -10f;
        static int _channel;
        static readonly List<int> _chord = new List<int>();
        static int _lastFret;
        static bool _starPower;

        static readonly string[] FretNames = { Loc.T("Open"), Loc.T("Green"), Loc.T("Red"), Loc.T("Yellow"), Loc.T("Blue"), Loc.T("Orange") };

        static readonly HashSet<string> _told = new HashSet<string>();

        static void Tell(string key, string full, string brief)
        {
            Captions.SayIn(Loc.T("Practice The Buttons"), () => IsOpen, _told.Add(key) ? full : brief, true);
        }
        static readonly string[] Places = { "", Loc.T("far left, the lowest"), Loc.T("left"), Loc.T("centre"), Loc.T("right"), Loc.T("far right, the highest") };

        // "A and B and C", one key per pair so the word order can change.
        static string And(IEnumerable<string> names)
        {
            string all = null;
            foreach (var n in names) all = all == null ? n : Loc.F("{0} and {1}", all, n);
            return all ?? "";
        }

        // No human profile is connected (none at all, or only bots): Practice The Buttons then uses a
        // temporary practice profile (user, 2026-11-17).
        public static bool NoHumanProfile()
        {
            try
            {
                foreach (var p in PlayerContainer.Players)
                    if (p != null && p.Profile != null && !p.Profile.IsBot) return false;
            }
            catch { }
            return true;
        }

        // Why Practice The Buttons can't open, or null when it can.
        public static string ProfileProblem()
        {
            try
            {
                foreach (var p in PlayerContainer.Players)
                {
                    if (p == null || p.Profile == null || p.Profile.IsBot) continue;
                    var gm = p.Profile.GameMode;
                    if (gm == GameMode.FourLaneDrums || gm == GameMode.FiveLaneDrums || gm == GameMode.FiveFretGuitar || gm == GameMode.ProKeys || gm == GameMode.Vocals)
                        return null;
                    return Loc.F("No sounds yet for {0}. Practice The Buttons works with five-fret, drums, pro keys and vocals profiles.", gm);
                }
            }
            catch { }
            return Loc.T("No profile is connected. Practice The Buttons uses your connected profile's buttons: connect one in Profiles first. "
                + "The Practice button in each instrument tutorial works without one.");
        }

        public static void OpenForProfile()
        {
            var mode = GameMode.FiveFretGuitar;
            try
            {
                foreach (var p in PlayerContainer.Players)
                    if (p != null && p.Profile != null && !p.Profile.IsBot) { mode = p.Profile.GameMode; break; }
            }
            catch { }
            switch (mode)
            {
                case GameMode.FourLaneDrums:
                case GameMode.FiveLaneDrums:
                    DrumButtonTest.Open();
                    return;
                case GameMode.FiveFretGuitar:
                    Open();
                    return;
                case GameMode.ProKeys:
                    KeysButtonTest.Open();
                    return;
                case GameMode.Vocals:
                    VocalButtonTest.Open();
                    return;
                default:
                    Captions.Say(Loc.F("No sounds yet for {0}. Practice The Buttons works with five-fret, drums, pro keys and vocals profiles.", mode), true);
                    return;
            }
        }

        public static void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _held = 0;
            _lastFret = 0;
            _inputs.Clear();
            Subscribe();
            _starPower = false;
            _told.Clear();
            if (GuidedPractice.Active) return;
            string how;
            if (Keys.Current == Keys.Device.Keyboard && !_keyboardProfile)
                how = Loc.T("On the keyboard: 1 to 5 for the frets, Up and Down to strum, Backspace for star power");
            else how = _subscribed.Count > 0 ? Loc.T("Use your profile's five-fret buttons") : Loc.T("Use the keyboard: 1 to 5 for the frets, Up and Down to strum, Backspace for star power");
            Captions.SayIn(Loc.T("Practice The Buttons"), () => IsOpen, Loc.F("Try the buttons. Press a fret, strum, or press star power to hear its sound and what it does in a song. "
                + "{0}. {1} to finish.", how, Keys.Finish), true);
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Unsubscribe();
            _inputs.Clear();
            CuePlayer.Stop(_channel);
            _starPower = false;
            CueBank.Mirror = false;
            Tutorials.TestClosed(Loc.T("Done trying the buttons."));
        }

        static void Subscribe()
        {
            Unsubscribe();
            _keyboardProfile = false;
            try
            {
                foreach (var p in PlayerContainer.Players)
                {
                    if (p == null || p.Profile == null || p.Profile.IsBot || p.Bindings == null || !PracticeDevices.Listens(p)) continue;
                    try
                    {
                        p.Bindings.SubscribeToGameplayInputs(GameMode.FiveFretGuitar, OnInput);
                        if (p.Profile.LeftyFlip) CueBank.Mirror = true;
                        _subscribed.Add(p);
                        foreach (var d in p.Bindings.InputDevices) if (d is Keyboard) _keyboardProfile = true;
                    }
                    catch (Exception ex) { ModLog.Warn("ButtonTest: no five-fret binds for " + p.Profile.Name, ex); }
                }
            }
            catch (Exception ex) { ModLog.Warn("ButtonTest.Subscribe", ex); }
        }

        static void Unsubscribe()
        {
            foreach (var p in _subscribed)
            {
                try { p.Bindings.UnsubscribeFromGameplayInputs(GameMode.FiveFretGuitar, OnInput); } catch { }
            }
            _subscribed.Clear();
        }

        static void OnInput(ref GameInput input)
        {
            if (IsOpen) _inputs.Enqueue(input);
        }

        public static bool Tick()
        {
            if (!IsOpen) return false;
            if (Time.frameCount == _openedFrame) return true;
            try
            {
                var kb = Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame) { Close(); return true; }
                var gp = Gamepad.current;
                if (gp != null && gp.startButton.wasPressedThisFrame) { Close(); return true; }
                foreach (var dev in InputSystem.devices)
                {
                    if (dev == null || dev is Keyboard || dev is Mouse || dev is Gamepad) continue;
                    var start = dev.TryGetChildControl<ButtonControl>("startButton");
                    if (start != null && start.wasPressedThisFrame) { Close(); return true; }
                }

                while (_inputs.Count > 0) Handle(_inputs.Dequeue());

                if (!_keyboardProfile && kb != null)
                {
                    var frets = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key };
                    for (int i = 0; i < 5; i++)
                    {
                        if (frets[i].wasPressedThisFrame) Fret(i + 1, true);
                        if (frets[i].wasReleasedThisFrame) Fret(i + 1, false);
                    }
                    if (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Strum();
                    if (kb.backspaceKey.wasPressedThisFrame) StarPower();
                    if (kb.semicolonKey.wasPressedThisFrame) Whammy();
                }
            }
            catch (Exception ex) { ModLog.Warn("ButtonTest.Tick", ex); }
            return true;
        }

        static void Handle(GameInput input)
        {
            var a = input.GetAction<GuitarAction>();
            switch (a)
            {
                case GuitarAction.StrumUp:
                case GuitarAction.StrumDown:
                    if (input.Button) Strum();
                    return;
                case GuitarAction.StarPower:
                    if (input.Button) StarPower();
                    return;
                case GuitarAction.Whammy:
                    Whammy();
                    return;
            }
            int n = (int) a;
            if (n >= 10 && n <= 14) n -= 10;
            if (n >= 0 && n <= 4) Fret(n + 1, input.Button);
        }

        static void Play(int channel)
        {
            CuePlayer.Stop(_channel);
            foreach (var c in _chord) CuePlayer.Stop(c);
            _chord.Clear();
            _channel = channel;
        }

        static int TopHeld()
        {
            for (int f = 5; f >= 1; f--) if ((_held & (1 << f)) != 0) return f;
            return 0;
        }

        static void Sparkle()
        {
            if (_starPower) _chord.Add(CueBank.Play("spnote", 0.8f));
        }

        static void Fret(int f, bool down)
        {
            if (!down)
            {
                bool wasTop = TopHeld() == f;
                _held &= ~(1 << f);
                int to = TopHeld();
                if (wasTop && to > 0)
                {
                    _lastFret = to;
                    if (GuidedPractice.Pressed("lift:" + to, Loc.F("a pull-off to {0}", FretNames[to]))) return;
                    Play(CueBank.PlayNote(to, CueBank.Kind.PullOff));
                    Sparkle();
                    Tell("lift", Loc.F("Lifting {0} with {1} held, no strum: a pull-off. The note slides down to {2}.",
                        FretNames[f], FretNames[to], FretNames[to].ToLowerInvariant()), Loc.F("Pull-off to {0}", FretNames[to]));
                }
                return;
            }
            _held |= 1 << f;
            // A fret below one already held is an anchor: the highest held fret is the note, so it plays nothing.
            if (GuidedPractice.Active && TopHeld() != f) return;
            bool pull = _lastFret > f;
            if (!GuidedPractice.Active)
            {
                Play(CueBank.PlayNote(f, pull ? CueBank.Kind.PullOff : CueBank.Kind.HammerOn));
                Sparkle();
            }
            string kind = pull
                ? Loc.F("a pull-off, because it is lower than {0}. The note slides down into place", FretNames[_lastFret].ToLowerInvariant())
                : (_lastFret > 0 && _lastFret < f
                    ? Loc.F("a hammer-on, because it is higher than {0}. The note slides up into place", FretNames[_lastFret].ToLowerInvariant())
                    : Loc.T("a hammer-on. The note slides up into place"));
            _lastFret = f;
            if (GuidedPractice.Pressed("fret:" + f, Loc.F("the {0} fret with no strum", FretNames[f]))) return;
            Tell("fret" + f + (pull ? "p" : "h"), Loc.F("{0} fret, {1}. Pressing it without a strum sounds like {2}.", FretNames[f], Places[f], kind),
                FretNames[f] + ", " + (pull ? Loc.T("pull-off") : Loc.T("hammer-on")));
        }

        static void Strum()
        {
            var frets = new List<int>();
            for (int f = 1; f <= 5; f++) if ((_held & (1 << f)) != 0) frets.Add(f);
            Play(0);
            if (frets.Count == 0)
            {
                _lastFret = 0;
                if (GuidedPractice.Pressed("strum:", Loc.T("an open strum, with no fret held"))) return;
                _channel = CueBank.PlayNote(0, CueBank.Kind.Strum);
                Sparkle();
                Tell("open", Loc.T("Strum with no fret held: an open note, the low sound in the centre."), Loc.T("Open note"));
                return;
            }
            _lastFret = frets[frets.Count - 1];
            var names = new List<string>();
            foreach (var f in frets) names.Add(FretNames[f]);
            if (!GuidedPractice.Active)
            {
                foreach (var f in frets) _chord.Add(CueBank.PlayNote(f, CueBank.Kind.Strum));
                Sparkle();
                if (frets.Count > 1) _chord.Add(CueBank.Play("chord"));
            }
            if (GuidedPractice.Pressed("strum:" + string.Join("+", frets.ConvertAll(x => x.ToString()).ToArray()), Loc.F("a strum with {0}", And(names)))) return;
            if (frets.Count > 1)
            {
                var chord = And(names);
                Tell("chord", Loc.F("Strum, {0}: a chord. A chord's notes play together with the chord sound.", chord), Loc.F("Chord, {0}", chord));
            }
            else Tell("strum", Loc.F("Strum, {0}: a strummed note. Strum when the hit bar sound ends.", names[0]),
                Loc.F("Strum, {0}", names[0]));
        }

        static void StarPower()
        {
            if (GuidedPractice.Pressed("sp", Loc.T("star power"))) return;
            _starPower = !_starPower;
            Play(CueBank.Play(_starPower ? "yarg_spdeploy" : "yarg_sprelease"));
            if (_starPower)
                Tell("star", Loc.T("Star power notes on: every note now plays as a star power note. "
                    + "In a song, star power notes make a phrase; hit every one to fill a quarter of the bar. "
                    + "Once the bar is half full this button activates star power and your points double until the bar runs out. Press it again here to turn star power notes off."),
                    Loc.T("Star power notes on."));
            else Captions.SayIn(Loc.T("Practice The Buttons"), () => IsOpen, Loc.T("Star power notes off."), true);
        }

        static void Whammy()
        {
            if (GuidedPractice.Active) return;
            float now = Time.unscaledTime;
            if (now - _whammyAt < 2f) return;
            _whammyAt = now;
            Tell("whammy", Loc.T("Whammy. Bend a held note. On a held star power note it adds to the star power bar."), Loc.T("Whammy"));
        }
    }
}
