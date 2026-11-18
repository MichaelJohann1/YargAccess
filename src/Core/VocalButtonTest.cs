using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Core;
using YARG.Core.Input;
using YARG.Input;
using YARG.Player;
using YargAccess.Audio;

namespace YargAccess
{
    public static class VocalButtonTest
    {
        public static bool IsOpen { get; private set; }
        public static bool HasMic => _mic != null;
        static YARG.Core.Audio.MicDevice _mic;

        public static bool KeyboardKeys => !_keyboardProfile && (Keys.Current == Keys.Device.Keyboard || _subscribed.Count == 0);

        static int _openedFrame = -1;
        static readonly List<YargPlayer> _subscribed = new List<YargPlayer>();
        static bool _keyboardProfile, _toldHit, _toldStar;
        static readonly Queue<GameInput> _inputs = new Queue<GameInput>();

        public static void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _inputs.Clear();
            _toldHit = _toldStar = false;
            Subscribe();
            if (GuidedPractice.Active) return;
            string how = Keys.Current == Keys.Device.Keyboard || _subscribed.Count == 0
                ? "On the computer keyboard, Backspace is Hit and Space is star power, unless your profile binds them elsewhere"
                : "Use your profile's Hit and star power buttons; tapping the microphone also counts as Hit";
            Captions.SayIn("Practice The Buttons", () => IsOpen, "Try the vocals buttons. Press a button to hear its sound in a song and what it does. " + how + ". With a microphone, sing and the note you hold is named. " + Keys.Finish + " to finish.", true);
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Unsubscribe();
            _inputs.Clear();
            Tutorials.TestClosed("Done trying the buttons.");
        }

        static readonly GameInputProcessed Handler = (ref GameInput input) => { if (IsOpen) _inputs.Enqueue(input); };

        // The microphone picked for a practice is used when a connected player has it.
        static bool MicConnected(string name)
        {
            foreach (var p in PlayerContainer.Players)
                if (p != null && p.Bindings != null && p.Bindings.Microphone != null && p.Bindings.Microphone.DisplayName == name) return true;
            return false;
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
                        p.Bindings.SubscribeToGameplayInputs(GameMode.Vocals, Handler);
                        _subscribed.Add(p);
                        foreach (var d in p.Bindings.InputDevices) if (d is Keyboard) _keyboardProfile = true;
                        if (_mic == null && p.Bindings.Microphone != null && (PracticeDevices.MicInUse == null || p.Bindings.Microphone.DisplayName == PracticeDevices.MicInUse || !MicConnected(PracticeDevices.MicInUse)))
                        {
                            _mic = p.Bindings.Microphone;
                            _mic.ClearOutputQueue();
                            _mic.IsRecordingOutput = true;
                        }
                    }
                    catch (Exception ex) { ModLog.Warn("VocalButtonTest: no vocals binds for " + p.Profile.Name, ex); }
                }
            }
            catch (Exception ex) { ModLog.Warn("VocalButtonTest.Subscribe", ex); }
            if (_mic == null && PracticeDevices.SharedMic != null)
            {
                _mic = PracticeDevices.SharedMic;
                _mic.ClearOutputQueue();
                _mic.IsRecordingOutput = true;
            }
        }

        static void Unsubscribe()
        {
            foreach (var p in _subscribed)
            {
                try { p.Bindings.UnsubscribeFromGameplayInputs(GameMode.Vocals, Handler); } catch { }
            }
            _subscribed.Clear();
            if (_mic != null)
            {
                try { _mic.IsRecordingOutput = false; _mic.ClearOutputQueue(); } catch { }
                _mic = null;
            }
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

                while (_inputs.Count > 0)
                {
                    var i = _inputs.Dequeue();
                    var a = i.GetAction<VocalsAction>();
                    if (a == VocalsAction.Pitch || !i.Button) continue;
                    if (a == VocalsAction.Hit) Hit();
                    else if (a == VocalsAction.StarPower) Star();
                }

                if (_mic != null)
                {
                    YARG.Core.Audio.MicOutputFrame frame;
                    while (_mic.DequeueOutputFrame(out frame))
                    {
                        if (!GuidedPractice.Active)
                        {
                            // Practice The Buttons: a tap counts as Hit, and singing names the note you hold.
                            if (frame.IsHit) Hit();
                            else SungNote(frame.PitchAsMidiNote);
                            continue;
                        }
                        if (frame.IsHit) GuidedPractice.Pressed("mic", "a tap on the microphone");
                        else GuidedPractice.Sung(frame.PitchAsMidiNote);
                    }
                }

                if (!_keyboardProfile && kb != null)
                {
                    if (kb.backspaceKey.wasPressedThisFrame) Hit();
                    if (kb.spaceKey.wasPressedThisFrame) Star();
                }
            }
            catch (Exception ex) { ModLog.Warn("VocalButtonTest.Tick", ex); }
            return true;
        }

        static readonly string[] NoteNames = { "C", "C sharp", "D", "E flat", "E", "F", "F sharp", "G", "A flat", "A", "B flat", "B" };
        static int _heldNote = -1, _saidNote = -1;
        static float _heldSince, _saidAt;

        // The note being sung, said once it has been held for a moment (any octave counts in YARG).
        static void SungNote(float midi)
        {
            if (float.IsNaN(midi) || float.IsInfinity(midi) || midi < 30f || midi > 100f) { _heldNote = -1; return; }
            int note = ((int) Math.Round(midi) % 12 + 12) % 12;
            float now = Time.unscaledTime;
            if (note != _heldNote) { _heldNote = note; _heldSince = now; return; }
            if (now - _heldSince < 0.35f || (note == _saidNote && now - _saidAt < 2f) || now - _saidAt < 0.6f) return;
            _saidNote = note;
            _saidAt = now;
            Captions.SayIn("Practice The Buttons", () => IsOpen, "Singing " + NoteNames[note], true);
        }

        static void Hit()
        {
            if (GuidedPractice.Pressed("hit", "Hit")) return;
            CueBank.Play("vocalperc", 1f);
            if (_toldHit) { Captions.SayIn("Practice The Buttons", () => IsOpen, "Hit", true); return; }
            _toldHit = true;
            Captions.SayIn("Practice The Buttons", () => IsOpen, "Hit. Plays percussion notes, such as a tambourine or cowbell: press it as soon as you hear this hard double knock in a song. Tapping the microphone counts too", true);
        }

        static void Star()
        {
            if (GuidedPractice.Pressed("sp", "star power")) return;
            CueBank.Play("spready", 0.8f);
            if (_toldStar) { Captions.SayIn("Practice The Buttons", () => IsOpen, "Star power", true); return; }
            _toldStar = true;
            Captions.SayIn("Practice The Buttons", () => IsOpen, "Star power. Turns star power on once it is ready, which this sparkle tells you in a song. It stays ready until you use it", true);
        }
    }
}
