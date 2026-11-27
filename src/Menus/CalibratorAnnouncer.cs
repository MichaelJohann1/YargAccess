using System;
using System.Collections;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace YargAccess
{
    public static class CalibratorAnnouncer
    {
        const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance;
        static Type _type;
        static FieldInfo _textField, _stateField, _timesField, _startField, _timeField, _playerField, _whammyField;
        static MethodInfo _updateForState;
        static string _last;
        static int _lastState = -1;
        static int _snapState = -1;
        static int _snapTaps = -1;
        static bool _announcedEntry;

        const int Starting = 0, AudioWaiting = 1, Audio = 2, AudioDone = 3;

        static MonoBehaviour Find()
        {
            if (_type == null) _type = Type.GetType("YARG.Menu.Calibrator.Calibrator, Assembly-CSharp");
            if (_type == null) return null;
            if (_textField == null)
            {
                _textField = _type.GetField("_audioCalibrateText", BF);
                _stateField = _type.GetField("_state", BF);
                _timesField = _type.GetField("_calibrationTimes", BF);
                _startField = _type.GetField("_startingStateContainer", BF);
                _timeField = _type.GetField("_time", BF);
                _playerField = _type.GetField("_player", BF);
                _whammyField = _type.GetField("_wasWhammyEnabled", BF);
                _updateForState = _type.GetMethod("UpdateForState", BF);
            }
            var cal = UnityEngine.Object.FindObjectOfType(_type) as MonoBehaviour;
            return cal != null && cal.isActiveAndEnabled ? cal : null;
        }

        public static bool IsActive { get { try { return Find() != null; } catch { return false; } } }

        static int State(MonoBehaviour cal)
        {
            try { return Convert.ToInt32(_stateField.GetValue(cal)); } catch { return -1; }
        }

        static int Taps(MonoBehaviour cal)
        {
            try { var l = _timesField.GetValue(cal) as ICollection; return l != null ? l.Count : 0; } catch { return 0; }
        }

        public static void Tick()
        {
            try
            {
                var cal = Find();
                if (cal == null) { _last = null; _lastState = -1; _snapState = -1; _announcedEntry = false; return; }

                int state = State(cal);
                if (!_announcedEntry && state == Starting) { _announcedEntry = true; AnnounceCurrent(); }
                if (state != _lastState) { _lastState = state; _last = null; if (state == Starting && _announcedEntry && _snapState >= 0) AnnounceStart(); }

                if (state != Starting && state != Audio)
                {
                    var tmp = _textField != null ? _textField.GetValue(cal) as TMPro.TMP_Text : null;
                    var txt = tmp != null ? TextUtil.Clean(tmp.text) : null;
                    if (!string.IsNullOrWhiteSpace(txt) && txt != _last)
                    {
                        _last = txt;
                        if (txt.Length > 2 && txt != "Text goes here")
                        {
                            if (state == AudioWaiting) txt = Loc.F("Press {0} when you are ready, then press it on every click you hear. {1} to stop.", TapKey(), Keys.Back);
                            if (state == AudioDone) txt = ResultText(txt) + " " + Loc.F("Press {0} to return to the start screen.", Keys.Back);
                            MenuAnnouncer.Say(txt);
                        }
                    }
                }

                ForwardTaps(cal, state);
                _snapState = State(cal);
                _snapTaps = Taps(cal);
            }
            catch (Exception ex) { ModLog.Warn("CalibratorAnnouncer", ex); }
        }

        public static void AnnounceCurrent()
        {
            var cal = Find();
            if (cal == null) return;
            Menu.OnEnter("Calibrator");
            if (State(cal) == Starting) AnnounceStart(); else MenuAnnouncer.SayItem(Loc.T("Calibrating"));
        }

        static void AnnounceStart()
        {
            var cal = Find();
            string text = "";
            try
            {
                var root = _startField != null ? _startField.GetValue(cal) as GameObject : null;
                if (root != null)
                {
                    var parts = new System.Collections.Generic.List<string>();
                    foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
                    {
                        var s = TextUtil.Clean(t.text);
                        if (!string.IsNullOrEmpty(s) && !parts.Contains(s)) parts.Add(s);
                    }
                    text = string.Join(". ", parts.ToArray());
                }
            }
            catch { }
            int ms = CurrentCalibration();
            MenuAnnouncer.Focused((string.IsNullOrEmpty(text) ? Loc.T("Calibrate Audio") : text) +
                (ms != int.MinValue ? ". " + Loc.F("Current audio calibration {0} milliseconds", ms) : "") +
                ". " + Loc.F("Press {0} to start audio calibration, {1} to go back.", Keys.Ok, Keys.Back), true);
        }

        // YARG's success line ("Calibration set to 25ms!") is hard-coded English, so it is rebuilt from the
        // saved value; its not-enough-data line is translated by YARG and is read as it is.
        static string ResultText(string txt)
        {
            int ms = CurrentCalibration();
            if (txt.StartsWith("Calibration set to", StringComparison.Ordinal) && ms != int.MinValue)
                return Loc.F("Calibration set to {0} milliseconds.", ms);
            return StripBackToExit(txt);
        }

        // YARG ends its result line with "Press back to exit." (hard-coded English after a calibration,
        // its own translation in Menu.Calibrator.NotEnoughData): both are taken off.
        static string StripBackToExit(string txt)
        {
            txt = txt.Replace("Press back to exit.", "");
            try
            {
                var lines = YARG.Localization.Localize.Key("Menu.Calibrator.NotEnoughData").Split('\n');
                var tail = TextUtil.Clean(lines[lines.Length - 1]);
                if (lines.Length > 1 && !string.IsNullOrEmpty(tail)) txt = txt.Replace(tail, "");
            }
            catch { }
            return txt.Trim();
        }

        static int CurrentCalibration()
        {
            try { return YARG.Settings.SettingsManager.Settings.AudioCalibration.Value; } catch { return int.MinValue; }
        }

        internal static int Phase()
        {
            var cal = Find();
            return cal != null ? State(cal) : -1;
        }

        // True while the clicks are wanted: waiting for the first press, or the clicks playing.
        internal static bool Tapping { get { int p = Phase(); return p == AudioWaiting || p == Audio; } }

        internal static string TapKey() { return Keys.Current == Keys.Device.Keyboard ? Loc.T("Space or Enter") : Loc.F("any button except {0}", Keys.Back); }

        public static bool Confirm()
        {
            var cal = Find();
            if (cal == null) return false;
            if (State(cal) == Starting)
            {
                try { _type.GetMethod("StartAudioMode").Invoke(cal, null); }
                catch (Exception ex) { ModLog.Warn("CalibratorAnnouncer.Confirm", ex); }
            }
            return true;
        }

        public static bool Back()
        {
            var cal = Find();
            if (cal == null) return false;
            int before = State(cal);
            try { _type.GetMethod("BackButton").Invoke(cal, null); }
            catch (Exception ex) { ModLog.Warn("CalibratorAnnouncer.Back", ex); }
            // YARG turns Whammy FX off while the clicks play and only turns it back on when they finish;
            // its own menu has no way out mid-way, the mod's Back does, so the setting is put back here.
            if (before == AudioWaiting || before == Audio)
            {
                try
                {
                    if (_whammyField != null)
                        YARG.Settings.SettingsManager.Settings.UseWhammyFx.Value = (bool)_whammyField.GetValue(cal);
                }
                catch (Exception ex) { ModLog.Warn("CalibratorAnnouncer.RestoreWhammy", ex); }
            }
            return true;
        }

        static void ForwardTaps(MonoBehaviour cal, int state)
        {
            if (state != AudioWaiting && state != Audio) return;
            var kb = Keyboard.current;
            bool pressed = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                || ControllerTap();
            if (!pressed) return;
            bool yargHandled = state != _snapState || Taps(cal) != _snapTaps;
            if (yargHandled) return;
            try
            {
                if (state == AudioWaiting)
                {
                    _stateField.SetValue(cal, Enum.ToObject(_stateField.FieldType, Audio));
                    _updateForState.Invoke(cal, null);
                }
                else
                {
                    var times = _timesField.GetValue(cal) as System.Collections.Generic.List<double>;
                    double start = (double)_timeField.GetValue(cal);
                    if (times != null) times.Add(Time.realtimeSinceStartupAsDouble - start);
                    var tmp = _textField.GetValue(cal) as TMPro.TMP_Text;
                    if (tmp != null) { tmp.color = Color.green; tmp.text = YARG.Localization.Localize.Key("Menu.Calibrator.Detected"); }
                }
            }
            catch (Exception ex) { ModLog.Warn("CalibratorAnnouncer.ForwardTaps", ex); }
        }
        // YARG only hears controllers that are on a connected profile; the mod drives pads and guitars in
        // every menu without one, so their presses are counted here too (YARG's own count wins, see
        // ForwardTaps). The back button and View / Select (help) are not taps.
        static readonly string[] GuitarTaps = { "greenFret", "yellowFret", "blueFret", "orangeFret", "strumUp", "strumDown", "startButton" };

        static bool ControllerTap()
        {
            foreach (var dev in InputSystem.devices)
            {
                if (dev == null || dev is Keyboard || dev is Mouse) continue;
                var gp = dev as Gamepad;
                if (gp != null)
                {
                    bool sw = gp is UnityEngine.InputSystem.Switch.SwitchProControllerHID;
                    var back = sw ? gp.buttonSouth : gp.buttonEast;
                    var buttons = new ButtonControl[] { gp.buttonSouth, gp.buttonEast, gp.buttonWest, gp.buttonNorth, gp.leftShoulder, gp.rightShoulder,
                        gp.leftTrigger, gp.rightTrigger, gp.dpad.up, gp.dpad.down, gp.dpad.left, gp.dpad.right, gp.startButton };
                    foreach (var b in buttons)
                        if (b != null && b != back && b.wasPressedThisFrame) return true;
                    continue;
                }
                if (dev.GetType().Name.IndexOf("Guitar", StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (var n in GuitarTaps)
                {
                    var b = dev.TryGetChildControl<ButtonControl>(n);
                    if (b != null && b.wasPressedThisFrame) return true;
                }
                var dp = dev.TryGetChildControl<DpadControl>("dpad");
                if (dp != null && (dp.up.wasPressedThisFrame || dp.down.wasPressedThisFrame)) return true;
            }
            return false;
        }
    }
}
