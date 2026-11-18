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
    public static class KeysButtonTest
    {
        public static bool IsOpen { get; private set; }
        public static bool KeyboardKeys => !_keyboardProfile && (Keys.Current == Keys.Device.Keyboard || _subscribed.Count == 0);

        static int _openedFrame = -1;
        static readonly List<YargPlayer> _subscribed = new List<YargPlayer>();
        static bool _keyboardProfile, _toldLayout;
        static readonly Queue<GameInput> _inputs = new Queue<GameInput>();

        public static void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _inputs.Clear();
            _toldLayout = false;
            Subscribe();
            if (GuidedPractice.Active) return;
            string how;
            if (Keys.Current == Keys.Device.Keyboard && !_keyboardProfile)
                how = Loc.T("On the computer keyboard: Z S X D C and on along the bottom two rows for the lower keys, Q 2 W 3 E and on along the top two rows for the upper ones");
            else how = _subscribed.Count > 0 ? Loc.T("Use your profile's keys") : Loc.T("Use the computer keyboard: Z S X D C and on for the lower keys, Q 2 W 3 E and on for the upper ones");
            Captions.SayIn(Loc.T("Practice The Buttons"), () => IsOpen, Loc.F("Try the keys. Press a key to hear its sound in a song and its name. {0}. {1} to finish.", how, Keys.Finish), true);
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Unsubscribe();
            _inputs.Clear();
            _midiNotes.Clear();
            Tutorials.TestClosed(Loc.T("Done trying the keys."));
        }

        static readonly GameInputProcessed Handler = (ref GameInput input) => { if (IsOpen) _inputs.Enqueue(input); };

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
                        p.Bindings.SubscribeToGameplayInputs(GameMode.ProKeys, Handler);
                        _subscribed.Add(p);
                        foreach (var d in p.Bindings.InputDevices)
                        {
                            if (d is Keyboard) _keyboardProfile = true;
                            var midi = d as Minis.MidiDevice;
                            if (midi != null && !p.Bindings.ContainsBindingsForDevice(d)) ListenMidi(midi);
                            foreach (var sib in PracticeDevices.MidiSiblings(d)) ListenMidi(sib);
                        }
                    }
                    catch (Exception ex) { ModLog.Warn("KeysButtonTest: no pro keys binds for " + p.Profile.Name, ex); }
                }
            }
            catch (Exception ex) { ModLog.Warn("KeysButtonTest.Subscribe", ex); }
        }

        static void Unsubscribe()
        {
            foreach (var p in _subscribed)
            {
                try { p.Bindings.UnsubscribeFromGameplayInputs(GameMode.ProKeys, Handler); } catch { }
            }
            _subscribed.Clear();
            foreach (var d in _midi) MidiNotes.Stop(d);
            _midi.Clear();
        }

        static readonly List<Minis.MidiDevice> _midi = new List<Minis.MidiDevice>();
        static readonly Queue<int> _midiNotes = new Queue<int>();
        static readonly Action<int, float> MidiHandler = (note, velocity) => { if (IsOpen && velocity > 0f) _midiNotes.Enqueue(note); };

        static void ListenMidi(Minis.MidiDevice device)
        {
            if (_midi.Contains(device)) return;
            MidiNotes.Listen(device, MidiHandler);
            _midi.Add(device);
        }

        static void PressMidi(int note)
        {
            int key = note - 48;
            if (key < 0 || key > 24) key = ((note % 12) + 12) % 12;
            if (GuidedPractice.Pressed("pc:" + (note % 12), ProKeysSound.NameOf(key))) return;
            Press(key);
        }

        internal static readonly Key[] Lower = { Key.Z, Key.S, Key.X, Key.D, Key.C, Key.V, Key.G, Key.B, Key.H, Key.N, Key.J, Key.M, Key.Comma, Key.L, Key.Period, Key.Semicolon, Key.Slash };
        internal static readonly Key[] Upper = { Key.Q, Key.Digit2, Key.W, Key.Digit3, Key.E, Key.Digit4, Key.R, Key.T, Key.Digit6, Key.Y, Key.Digit7, Key.U, Key.I, Key.Digit9, Key.O, Key.Digit0, Key.P, Key.Minus, Key.LeftBracket, Key.RightBracket };

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
                    if (!i.Button) continue;
                    var a = i.GetAction<ProKeysAction>();
                    if ((int) a >= 0 && (int) a <= 24) Press((int) a);
                    else if (a == ProKeysAction.StarPower) StarPower();
                }
                while (_midiNotes.Count > 0) PressMidi(_midiNotes.Dequeue());

                if (!_keyboardProfile && kb != null)
                {
                    for (int k = 0; k < Lower.Length; k++) if (kb[Lower[k]].wasPressedThisFrame) Press(k);
                    for (int k = 0; k < Upper.Length; k++) if (kb[Upper[k]].wasPressedThisFrame) Press(k + 5);
                    if (kb.backspaceKey.wasPressedThisFrame) StarPower();
                }
            }
            catch (Exception ex) { ModLog.Warn("KeysButtonTest.Tick", ex); }
            return true;
        }

        static void StarPower()
        {
            if (GuidedPractice.Pressed("sp", Loc.T("star power"))) return;
            Captions.SayIn(Loc.T("Practice The Buttons"), () => IsOpen, Loc.T("Star power"), true);
        }

        static void Press(int key)
        {
            string name = ProKeysSound.NameOf(key);
            if (GuidedPractice.Pressed("key:" + key, name)) return;
            CueBank.PlayKey(ProKeysSound.MidiOf(key), ProKeysSound.PanOf(key), 0.9f);
            if (!_toldLayout)
            {
                _toldLayout = true;
                name = Loc.F("{0}. 25 keys, from low C on the left to high C on the right; each plays its own pitch", name);
            }
            Captions.SayIn(Loc.T("Practice The Buttons"), () => IsOpen, name, true);
        }
    }
}
