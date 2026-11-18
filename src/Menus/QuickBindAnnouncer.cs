using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Input;
using YARG.Menu.Dialogs;

namespace YargAccess
{
    public static class QuickBindAnnouncer
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static FriendlyBindingDialog _dialog;
        static string _lastControl = "", _lastState = "";
        static string _pendingSet;
        static bool _subscribed, _skipTold;

        public static bool IsWaiting { get; private set; }

        public static bool Tick()
        {
            var d = UnityEngine.Object.FindObjectOfType<FriendlyBindingDialog>();
            if (d == null || !d.isActiveAndEnabled)
            {
                if (_dialog != null) Close();
                return false;
            }
            if (d != _dialog) Open(d);
            try
            {
                string state = Get(d, "_state");
                var ct = typeof(FriendlyBindingDialog).GetField("ControlText", BF).GetValue(d) as TMP_Text;
                string control = ct != null ? TextUtil.Clean(ct.text) : "";
                IsWaiting = state == "Waiting";

                if (IsWaiting && control.Length > 0 && control != _lastControl)
                {
                    string skip = _skipTold ? "" : " Escape to skip.";
                    _skipTold = true;
                    Captions.Say(Take() + "Hit " + control + Kind(d, control) + "." + skip, true);
                }
                if (state == "Done" && _lastState != "Done" && _lastState != "")
                {
                    DialogNavigator.FocusLabel("Close");
                    Captions.Say(Take() + TextUtil.Clean(d.Message.text) + " " + Keys.Ok + " to close.", true);
                }
                _lastControl = control;
                _lastState = state;

                if (IsWaiting)
                {
                    var kb = Keyboard.current;
                    if (kb != null && kb.escapeKey.wasPressedThisFrame) Skip(d, control);
                    return true;
                }
            }
            catch (Exception ex) { ModLog.Warn("QuickBindAnnouncer.Tick", ex); }
            return false;
        }

        static void Open(FriendlyBindingDialog d)
        {
            _dialog = d;
            _lastControl = "";
            _lastState = "";
            _pendingSet = null;
            _skipTold = false;
            if (!_subscribed) { ControlBinding.BindingAdded += OnBindingAdded; _subscribed = true; }
        }

        static void Close()
        {
            _dialog = null;
            IsWaiting = false;
            if (_subscribed) { ControlBinding.BindingAdded -= OnBindingAdded; _subscribed = false; }
        }

        static void OnBindingAdded(ControlBinding binding, InputControl control)
        {
            if (_dialog == null || !IsWaiting) return;
            try
            {
                var coll = typeof(FriendlyBindingDialog).GetField("_bindingCollection", BF).GetValue(_dialog) as IEnumerable;
                bool ours = false;
                if (coll != null) foreach (ControlBinding b in coll) if (b == binding) { ours = true; break; }
                if (!ours) return;
            }
            catch { }
            string name = "";
            try { name = TextUtil.Clean(YARG.Localization.Localize.Key("Bindings", binding.Name)); } catch { }
            if (string.IsNullOrEmpty(name)) name = binding.Name;
            _pendingSet = name + " set to " + (control != null ? control.displayName : "?") + ". ";
        }

        static string Take()
        {
            var s = _pendingSet ?? "";
            _pendingSet = null;
            return s;
        }

        static void Skip(FriendlyBindingDialog d, string control)
        {
            try
            {
                var helper = UnityEngine.Object.FindObjectOfType<InputControlBindingHelper>();
                var possible = helper != null ? helper.GetType().GetField("_possibleControls", BF).GetValue(helper) as IList : null;
                if (possible != null) possible.Clear();
                var cts = typeof(FriendlyBindingDialog).GetField("_bindingTokenSource", BF).GetValue(d) as CancellationTokenSource;
                if (cts != null) cts.Cancel();
                _pendingSet = control + " skipped. ";
            }
            catch (Exception ex) { ModLog.Warn("QuickBindAnnouncer.Skip", ex); }
        }

        static string Kind(FriendlyBindingDialog d, string control)
        {
            try
            {
                var coll = typeof(FriendlyBindingDialog).GetField("_bindingCollection", BF).GetValue(d) as IEnumerable;
                if (coll == null) return "";
                foreach (ControlBinding b in coll)
                {
                    if (Localized(b.Name) != control && Localized(b.NameLefty) != control) continue;
                    string k = KindOf(b.Name);
                    return k.Length > 0 ? ", " + k : "";
                }
            }
            catch (Exception ex) { ModLog.Warn("QuickBindAnnouncer.Kind", ex); }
            return "";
        }

        static string Localized(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            try { return TextUtil.Clean(YARG.Localization.Localize.Key("Bindings", key)); } catch { return key; }
        }

        static readonly string[] NoteNames = { "C", "C sharp", "D", "D sharp", "E", "F", "F sharp", "G", "G sharp", "A", "A sharp", "B" };

        static string KindOf(string name)
        {
            switch (name)
            {
                case "Drums.Kick": return "the kick pedal";
                case "FourDrums.RedPad": case "FiveDrums.RedPad": return "the snare";
                case "FourDrums.YellowPad": return "the high tom";
                case "FourDrums.BluePad": case "FiveDrums.BluePad": return "the middle tom";
                case "FourDrums.GreenPad": case "FiveDrums.GreenPad": return "the low tom";
                case "FourDrums.YellowCymbal": case "FiveDrums.YellowCymbal": return "the hi-hat cymbal";
                case "FourDrums.BlueCymbal": return "the ride cymbal";
                case "FourDrums.GreenCymbal": return "the crash cymbal";
                case "FiveDrums.OrangeCymbal": return "the crash cymbal";
            }
            int n;
            if (name.StartsWith("ProKeys.Key") && int.TryParse(name.Substring(11), out n) && n >= 1)
                return NoteNames[(n - 1) % 12] + (n == 25 ? ", the top C" : "");
            return "";
        }

        static string Get(object o, string field)
        {
            var f = typeof(FriendlyBindingDialog).GetField(field, BF);
            var v = f != null ? f.GetValue(o) : null;
            return v != null ? v.ToString() : "";
        }
    }
}
