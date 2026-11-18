using System;
using System.Collections;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YargAccess
{
    public static class CalibratorAnnouncer
    {
        const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance;
        static Type _type;
        static FieldInfo _textField, _stateField, _timesField, _startField, _timeField, _playerField;
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
                            if (state == AudioWaiting) txt = "Press " + TapKey() + " when you are ready, then press it on every click you hear. " + Keys.Back + " to stop.";
                            if (state == AudioDone) txt = txt.Replace("Press back to exit.", "").Trim() + " Press " + Keys.Back + " to return to the start screen.";
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
            if (State(cal) == Starting) AnnounceStart(); else MenuAnnouncer.SayItem("Calibrating");
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
            MenuAnnouncer.Focused((string.IsNullOrEmpty(text) ? "Calibrate Audio" : text) +
                (ms != int.MinValue ? ". Current audio calibration " + ms + " milliseconds" : "") +
                ". Press " + Keys.Ok + " to start audio calibration, " + Keys.Back + " to go back.", true);
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

        static string TapKey() { return Keys.Current == Keys.Device.Keyboard ? "Space or Enter" : "any button"; }

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
            try { _type.GetMethod("BackButton").Invoke(cal, null); }
            catch (Exception ex) { ModLog.Warn("CalibratorAnnouncer.Back", ex); }
            return true;
        }

        static void ForwardTaps(MonoBehaviour cal, int state)
        {
            if (state != AudioWaiting && state != Audio) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            bool pressed = kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
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
    }
}
