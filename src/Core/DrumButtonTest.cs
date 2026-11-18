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
    public static class DrumButtonTest
    {
        public static bool IsOpen { get; private set; }
        public static bool KeyboardKeys => !_keyboardProfile && (Keys.Current == Keys.Device.Keyboard || _subscribed.Count == 0);

        public static void PracticeKit(out bool fiveLane, out bool pro)
        {
            fiveLane = false;
            pro = true;
            // A kit lent to the practice decides the layout, whichever device was used last in the menus.
            if (PracticeDevices.Device == null && KeyboardKeys) return;
            foreach (var k in _kits.Values) { fiveLane = k.FiveLane; pro = k.Pro; return; }
        }

        static int _openedFrame = -1;
        static readonly List<KeyValuePair<YargPlayer, GameMode>> _subscribed = new List<KeyValuePair<YargPlayer, GameMode>>();
        static readonly Dictionary<YargPlayer, Kit> _kits = new Dictionary<YargPlayer, Kit>();
        static bool _keyboardProfile;
        static readonly Queue<KeyValuePair<Kit, GameInput>> _inputs = new Queue<KeyValuePair<Kit, GameInput>>();
        static readonly HashSet<string> _told = new HashSet<string>();

        class Kit { public bool FiveLane, Pro, Lefty; }
        static readonly Kit KeyboardKit = new Kit { Pro = true };

        internal static readonly string[] FourNames = { "Kick", "Red pad", "Yellow pad", "Blue pad", "Green pad", "Yellow cymbal", "Blue cymbal", "Green cymbal" };
        internal static readonly string[] FiveNames = { "Kick", "Red pad", "Yellow cymbal", "Blue pad", "Orange cymbal", "Green pad" };
        // The same names as spoken (FourNames / FiveNames stay English for the checks on them).
        static readonly string[] FourSaid = { Loc.T("Kick"), Loc.T("Red pad"), Loc.T("Yellow pad"), Loc.T("Blue pad"), Loc.T("Green pad"), Loc.T("Yellow cymbal"), Loc.T("Blue cymbal"), Loc.T("Green cymbal") };
        static readonly string[] FiveSaid = { Loc.T("Kick"), Loc.T("Red pad"), Loc.T("Yellow cymbal"), Loc.T("Blue pad"), Loc.T("Orange cymbal"), Loc.T("Green pad") };

        internal static string Said(bool fiveLane, int pad)
        {
            var said = fiveLane ? FiveSaid : FourSaid;
            return pad >= 0 && pad < said.Length ? said[pad] : Loc.T("Pad");
        }

        public static void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _inputs.Clear();
            _told.Clear();
            Subscribe();
            if (GuidedPractice.Active) return;
            string how;
            if (Keys.Current == Keys.Device.Keyboard && !_keyboardProfile)
                how = Loc.T("On the keyboard: Z X C V for the pads, S D F for the cymbals, Space for the kick");
            else how = _subscribed.Count > 0 ? Loc.T("Use your profile's drums") : Loc.T("Use the keyboard: Z X C V for the pads, S D F for the cymbals, Space for the kick");
            Captions.SayIn(Loc.T("Practice The Buttons"), () => IsOpen, Loc.F("Try the drums. Hit a pad, cymbal or the kick to hear its sound in a song. {0}. {1} to finish.", how, Keys.Finish), true);
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Unsubscribe();
            _inputs.Clear();
            _midiHits.Clear();
            Tutorials.TestClosed(Loc.T("Done trying the drums."));
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
                    var inst = p.Profile.CurrentInstrument;
                    var kit = new Kit { FiveLane = inst == Instrument.FiveLaneDrums, Pro = inst == Instrument.ProDrums, Lefty = p.Profile.LeftyFlip };
                    var mode = kit.FiveLane ? GameMode.FiveLaneDrums : GameMode.FourLaneDrums;
                    try
                    {
                        _kits[p] = kit;
                        p.Bindings.SubscribeToGameplayInputs(mode, OnInput(kit));
                        _subscribed.Add(new KeyValuePair<YargPlayer, GameMode>(p, mode));
                        foreach (var d in p.Bindings.InputDevices)
                        {
                            if (d is Keyboard) _keyboardProfile = true;
                            var midi = d as Minis.MidiDevice;
                            if (midi != null && !p.Bindings.ContainsBindingsForDevice(d)) ListenMidi(midi, kit);
                            foreach (var sib in PracticeDevices.MidiSiblings(d)) ListenMidi(sib, kit);
                        }
                    }
                    catch (Exception ex) { ModLog.Warn("DrumButtonTest: no drum binds for " + p.Profile.Name, ex); }
                }
            }
            catch (Exception ex) { ModLog.Warn("DrumButtonTest.Subscribe", ex); }
        }

        static readonly Dictionary<Kit, GameInputProcessed> _handlers = new Dictionary<Kit, GameInputProcessed>();

        static GameInputProcessed OnInput(Kit kit)
        {
            GameInputProcessed h;
            if (!_handlers.TryGetValue(kit, out h))
            {
                h = (ref GameInput input) => { if (IsOpen) _inputs.Enqueue(new KeyValuePair<Kit, GameInput>(kit, input)); };
                _handlers[kit] = h;
            }
            return h;
        }

        static void Unsubscribe()
        {
            foreach (var s in _subscribed)
            {
                try
                {
                    Kit kit;
                    if (_kits.TryGetValue(s.Key, out kit)) s.Key.Bindings.UnsubscribeFromGameplayInputs(s.Value, OnInput(kit));
                }
                catch { }
            }
            _subscribed.Clear();
            _kits.Clear();
            _handlers.Clear();
            foreach (var m in _midi) MidiNotes.Stop(m.Key);
            _midi.Clear();
        }

        static readonly List<KeyValuePair<Minis.MidiDevice, Action<int, float>>> _midi = new List<KeyValuePair<Minis.MidiDevice, Action<int, float>>>();
        static readonly Queue<KeyValuePair<Kit, KeyValuePair<int, float>>> _midiHits = new Queue<KeyValuePair<Kit, KeyValuePair<int, float>>>();

        static void ListenMidi(Minis.MidiDevice device, Kit kit)
        {
            foreach (var m in _midi) if (m.Key == device) return;
            Action<int, float> h = (note, velocity) =>
            {
                if (IsOpen && velocity > 0f) _midiHits.Enqueue(new KeyValuePair<Kit, KeyValuePair<int, float>>(kit, new KeyValuePair<int, float>(note, velocity)));
            };
            MidiNotes.Listen(device, h);
            _midi.Add(new KeyValuePair<Minis.MidiDevice, Action<int, float>>(device, h));
        }

        static int MidiPad(int note, Kit kit)
        {
            switch (note)
            {
                case 35: case 36: return 0;
                case 37: case 38: case 39: case 40: return 1;
                case 48: case 50: return kit.FiveLane ? 3 : 2;
                case 45: case 47: return 3;
                case 41: case 43: return kit.FiveLane ? 5 : 4;
                case 42: case 44: case 46: return kit.FiveLane ? 2 : kit.Pro ? 5 : 2;
                case 51: case 53: case 59: return kit.FiveLane ? 4 : kit.Pro ? 6 : 3;
                case 49: case 52: case 55: case 57: return kit.FiveLane ? 4 : kit.Pro ? 7 : 4;
                default: return -1;
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
                    float v = i.Value.Axis;
                    if (v > 0) Hit(i.Key, i.Value.GetAction<DrumsAction>(), v);
                }
                while (_midiHits.Count > 0)
                {
                    var m = _midiHits.Dequeue();
                    int pad = MidiPad(m.Value.Key, m.Key);
                    if (pad >= 0) HitPad(m.Key, pad, m.Value.Value);
                }

                if (!_keyboardProfile && kb != null)
                {
                    var keys = new[] { kb.zKey, kb.xKey, kb.cKey, kb.vKey, kb.sKey, kb.dKey, kb.fKey, kb.spaceKey };
                    for (int a = 0; a < keys.Length; a++)
                        if (keys[a].wasPressedThisFrame) Hit(KeyboardKit, (DrumsAction) a, 1f);
                }
            }
            catch (Exception ex) { ModLog.Warn("DrumButtonTest.Tick", ex); }
            return true;
        }

        static void Hit(Kit kit, DrumsAction action, float velocity)
        {
            HitPad(kit, DrumSound.PadOfAction(action, kit.FiveLane, kit.Pro), velocity);
        }

        static void HitPad(Kit kit, int pad, float velocity)
        {
            var names = kit.FiveLane ? FiveNames : FourNames;
            // The names stay English for the checks; they are translated where spoken.
            string name = pad >= 0 && pad < names.Length ? names[pad] : "Pad";
            if (GuidedPractice.Pressed("pad:" + pad, name == "Kick" ? Loc.T("the kick") : Loc.F("the {0}", Said(kit.FiveLane, pad)))) return;
            string id = DrumSound.SoundOf(pad, kit.FiveLane);
            float pan = DrumSound.PanOf(pad, kit.FiveLane);
            CueBank.PlayDrum(id, kit.Lefty ? -pan : pan, 0.8f * Mathf.Clamp(velocity, 0.4f, 1f));
            var cue = CueBank.Get(id);
            string said = Said(kit.FiveLane, pad);
            string full = said;
            if (cue != null)
            {
                int colon = cue.Meaning.IndexOf(':');
                string sound = colon > 0 ? cue.Meaning.Substring(0, colon) : cue.Meaning;
                full = said + ": " + sound + ".";
            }
            Captions.SayIn(Loc.T("Practice The Buttons"), () => IsOpen, _told.Add((kit.FiveLane ? "5" : "4") + pad) ? full : said, true);
        }
    }
}
