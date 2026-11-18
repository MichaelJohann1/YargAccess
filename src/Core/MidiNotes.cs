using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace YargAccess
{
    // Note-ons from MIDI devices that have no binds, for the button tests and the practice.
    // YARG's Minis never raises MidiDevice.onWillNoteOn (its InvokeNoteOn has no caller), so the
    // notes are read from the device's input events instead. Reading the events, not the note state
    // each frame, also catches a key pressed and let go within one frame.
    public static class MidiNotes
    {
        static readonly Dictionary<int, Action<int, float>> _listeners = new Dictionary<int, Action<int, float>>();
        static bool _hooked;

        // onNote gets the MIDI note number and the velocity (0..1). It runs inside the input update.
        public static void Listen(Minis.MidiDevice device, Action<int, float> onNote)
        {
            if (device == null || onNote == null) return;
            _listeners[device.deviceId] = onNote;
            if (!_hooked) { InputSystem.onEvent += OnEvent; _hooked = true; }
        }

        public static void Stop(Minis.MidiDevice device)
        {
            if (device != null) _listeners.Remove(device.deviceId);
            if (_listeners.Count == 0 && _hooked) { InputSystem.onEvent -= OnEvent; _hooked = false; }
        }

        static void OnEvent(InputEventPtr e, InputDevice device)
        {
            Action<int, float> onNote;
            if (device == null || !_listeners.TryGetValue(device.deviceId, out onNote)) return;
            try
            {
                if (!e.IsA<StateEvent>() && !e.IsA<DeltaStateEvent>()) return;
                foreach (var c in e.EnumerateChangedControls(device))
                {
                    var note = c as Minis.MidiNoteControl;
                    if (note == null) continue;
                    float v;
                    if (note.ReadValueFromEvent(e, out v) && v > 0f) onNote(note.noteNumber, v);
                }
            }
            catch (Exception ex) { ModLog.Warn("MidiNotes.OnEvent", ex); }
        }
    }
}
