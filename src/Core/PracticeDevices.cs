using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using PlasticBand.Devices;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Game;
using YARG.Menu.ProfileList;
using YARG.Player;

namespace YargAccess
{
    public static class PracticeDevices
    {
        // One device a practice could be played on: a device or microphone, and the connected player
        // that already has it (null when nobody does).
        public class Choice
        {
            public string Name;
            public InputDevice Device;
            public int MicId;
            public string MicName;
            public YargPlayer Player;
            // The computer keyboard: the practice reads its keys directly, with no practice profile.
            public bool Keyboard;
        }

        // Set by the device picker; the next Ensure uses it once.
        public static Choice Chosen;

        // The microphone the vocals practice listens to, when one was picked.
        public static string MicInUse;

        // Every device that fits the mode: those on connected players of that mode, then unused ones, and
        // the computer keyboard (with the profile that has it, or last). The kind of device pressed last
        // comes first, so the picker starts on it.
        public static List<Choice> Choices(string mode)
        {
            var list = new List<Choice>();
            try
            {
                if (mode == "vocals")
                {
                    var used = new HashSet<string>();
                    foreach (var p in PlayerContainer.Players)
                    {
                        if (p == null || p.Bindings == null || p.Bindings.Microphone == null) continue;
                        used.Add(p.Bindings.Microphone.DisplayName);
                        if (p.Profile != null && !p.Profile.IsBot && Plays(p, mode))
                            list.Add(new Choice { Name = Loc.F("Microphone, {0}, on {1}", p.Bindings.Microphone.DisplayName, p.Profile.Name), MicName = p.Bindings.Microphone.DisplayName, Player = p });
                    }
                    foreach (var m in GlobalAudioHandler.GetAllInputDevices())
                        if (!used.Contains(m.Item2)) list.Add(new Choice { Name = Loc.F("Microphone, {0}", m.Item2), MicId = m.Item1, MicName = m.Item2 });
                    return list;
                }
                var seen = new HashSet<InputDevice>();
                bool keyboard = false;
                foreach (var p in PlayerContainer.Players)
                {
                    if (p == null || p.Profile == null || p.Profile.IsBot || p.Bindings == null) continue;
                    foreach (var d in p.Bindings.InputDevices)
                    {
                        if (d is Keyboard)
                        {
                            if (!keyboard) list.Add(new Choice { Name = Loc.F("Computer keyboard, on {0}", p.Profile.Name), Keyboard = true, Player = p });
                            keyboard = true;
                            continue;
                        }
                        if (Plays(p, mode) && FitsPractice(d, mode) && seen.Add(d))
                            list.Add(new Choice { Name = Loc.F("{0}, {1}, on {2}", Cap(Kind(d)), d.displayName, p.Profile.Name), Device = d, Player = p });
                    }
                }
                var free = new List<InputDevice>();
                foreach (var d in InputSystem.devices)
                    if (d != null && d.enabled && !seen.Contains(d) && !PlayerContainer.IsDeviceTaken(d) && FitsPractice(d, mode) && !MidiChannelDevice(d)) free.Add(d);
                // A MIDI keyboard shows up as several ports and channels: it is offered once, by its own name
                // (its main port, not "MIDIIN...", is the one lent); the practice listens to all its ports.
                var named = new HashSet<string>();
                for (int rank = 0; rank <= 2; rank++)
                    foreach (var d in free)
                    {
                        if (Rank(d) != rank) continue;
                        if (d is Minis.MidiDevice) { if (named.Add(MidiBase(d))) list.Add(new Choice { Name = Cap(Kind(d)) + ", " + MidiBase(d), Device = d }); }
                        else list.Add(new Choice { Name = Cap(Kind(d)) + ", " + d.displayName, Device = d });
                    }
                if (!keyboard && UnityEngine.InputSystem.Keyboard.current != null)
                    list.Add(new Choice { Name = Loc.T("Computer keyboard"), Keyboard = true });
                int last = list.FindIndex(UsedLast);
                if (last > 0) { var c = list[last]; list.RemoveAt(last); list.Insert(0, c); }
            }
            catch (Exception ex) { ModLog.Warn("PracticeDevices.Choices", ex); }
            return list;
        }

        static bool UsedLast(Choice c)
        {
            switch (Keys.Current)
            {
                case Keys.Device.Keyboard: return c.Keyboard;
                case Keys.Device.Gamepad: return c.Device is Gamepad;
                case Keys.Device.Guitar: return c.Device is FiveFretGuitar;
                default: return false;
            }
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static bool FitsPractice(InputDevice d, string mode)
        {
            if (mode == "fivefret") return d is FiveFretGuitar || d is Gamepad;
            return Fits(d, mode);
        }

        static string Kind(InputDevice d)
        {
            if (d is FiveFretGuitar) return Loc.T("guitar");
            if (d is FourLaneDrumkit || d is FiveLaneDrumkit) return Loc.T("drum kit");
            if (d is ProKeyboard) return Loc.T("pro keys keyboard");
            if (d is Minis.MidiDevice) return DrumName(d) ? Loc.T("drum kit") : Loc.T("MIDI keyboard");
            if (d is Gamepad) return Loc.T("controller");
            if (d is Keyboard) return Loc.T("computer keyboard");
            return Loc.T("controller");
        }

        static bool Plays(YargPlayer p, string mode)
        {
            var gm = p.Profile.GameMode;
            switch (mode)
            {
                case "fivefret": return gm == GameMode.FiveFretGuitar;
                case "drums": return gm == GameMode.FourLaneDrums || gm == GameMode.FiveLaneDrums;
                case "prokeys": return gm == GameMode.ProKeys;
                default: return gm == GameMode.Vocals;
            }
        }

        static bool HasDevice(YargPlayer p, string mode)
        {
            if (p.Bindings == null) return false;
            if (mode == "vocals") return p.Bindings.Microphone != null;
            foreach (var d in p.Bindings.InputDevices) if (Fits(d, mode)) return true;
            return false;
        }

        static bool Fits(InputDevice d, string mode)
        {
            if (d == null) return false;
            if (mode == "drums") return d is FourLaneDrumkit || d is FiveLaneDrumkit || (d is Minis.MidiDevice && DrumName(d));
            return d is ProKeyboard || (d is Minis.MidiDevice && !DrumName(d));
        }

        static readonly string[] DrumWords = { "drum", "kit", "td-", "nitro", "strike", "surge", "e-drum" };

        public static bool IsDrumKit(InputDevice d) => DrumName(d);

        static bool DrumName(InputDevice d)
        {
            var n = (d.displayName ?? "").ToLowerInvariant();
            foreach (var w in DrumWords) if (n.Contains(w)) return true;
            return false;
        }

        // "MIDIIN2 (KONTROL S61 MK3) 1 (All Channels)" and "KONTROL S61 MK3 0 Channel 0" are both "KONTROL S61 MK3".
        internal static string MidiBase(InputDevice d)
        {
            string n = d.displayName ?? "";
            n = System.Text.RegularExpressions.Regex.Replace(n, @"\s*(\(All Channels\)|Channel \d+)\s*$", "");
            n = System.Text.RegularExpressions.Regex.Replace(n, @"\s+\d+$", "");
            var m = System.Text.RegularExpressions.Regex.Match(n, @"^MIDIIN\d+ \((.*)\)$");
            if (m.Success) n = m.Groups[1].Value;
            return n.Trim();
        }

        // Minis makes one device per port for all channels, and one more per channel once a note arrives on it.
        static bool MidiChannelDevice(InputDevice d)
        {
            return d is Minis.MidiDevice && !(d.displayName ?? "").EndsWith("(All Channels)", StringComparison.Ordinal);
        }

        // The other ports of the same MIDI keyboard or kit: a key press can arrive on any of them, so the
        // practice and the button tests read them all, or presses on the other ports are missed.
        public static List<Minis.MidiDevice> MidiSiblings(InputDevice d)
        {
            var list = new List<Minis.MidiDevice>();
            if (!(d is Minis.MidiDevice)) return list;
            try
            {
                string name = MidiBase(d);
                foreach (var o in InputSystem.devices)
                {
                    var m = o as Minis.MidiDevice;
                    if (m == null || m == d || MidiChannelDevice(m) || MidiBase(m) != name) continue;
                    // The lent device's own port, when the lent device is one channel of it, would double every note.
                    if (MidiChannelDevice(d) && (d.displayName ?? "").StartsWith((m.displayName ?? "").Replace(" (All Channels)", ""), StringComparison.Ordinal)) continue;
                    if (PlayerContainer.IsDeviceTaken(m)) continue;
                    list.Add(m);
                }
            }
            catch (Exception ex) { ModLog.Warn("PracticeDevices.MidiSiblings", ex); }
            return list;
        }

        static int Rank(InputDevice d)
        {
            if (!(d is Minis.MidiDevice)) return 0;
            return (d.displayName ?? "").StartsWith("MIDIIN", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        }


        // A tutorial practice runs on its own profile: made when the practice starts, with
        // the practice device and YARG's default binds, never saved on purpose, and removed when the practice
        // ends. The user's own profiles are not changed. A profile left behind (the game closed mid-practice
        // after YARG saved) is removed at the next start by its fixed Id.
        static readonly Guid TempId = new Guid("59a2acce-55a0-4d0d-9a77-7d7070a1c7ce");
        const string TempName = "Tutorial Practice";

        public static YargPlayer TempPlayer { get; private set; }
        static YargProfile _tempProfile;

        // The device the practice is played on (on the tutorial profile), or null for the computer keyboard.
        public static InputDevice Device { get; private set; }

        // A microphone another connected player already has open; the vocals practice listens to it there.
        public static MicDevice SharedMic { get; private set; }

        // While a practice runs, only the tutorial profile's buttons count.
        public static bool Listens(YargPlayer p) => !GuidedPractice.Active || (TempPlayer != null && p == TempPlayer);

        // Makes the tutorial profile with the practice device and says which device it is. trying: the line is
        // worded for Practice The Buttons ("try the buttons") instead of a tutorial practice.
        public static string Ensure(string mode, bool trying = false)
        {
            var chosen = Chosen;
            Chosen = null;
            Release();
            MicInUse = null;
            SharedMic = null;
            Device = null;
            try
            {
                if (mode == "vocals") return EnsureVocals(chosen, trying);
                if (chosen != null && chosen.Keyboard)
                    return (trying ? Loc.T("Trying the buttons on the computer keyboard.") : Loc.T("Practising on the computer keyboard.")) + " ";
                var d = chosen != null && chosen.Device != null ? chosen.Device : Find(mode);
                if (d == null)
                    return (trying
                        ? (mode == "fivefret" ? Loc.T("No guitar or controller was found, so try the buttons on the computer keyboard.")
                            : mode == "drums" ? Loc.T("No drum kit was found, so try the buttons on the computer keyboard.")
                            : Loc.T("No MIDI or pro keys keyboard was found, so try the buttons on the computer keyboard."))
                        : (mode == "fivefret" ? Loc.T("No guitar or controller was found, so practise on the computer keyboard.")
                            : mode == "drums" ? Loc.T("No drum kit was found, so practise on the computer keyboard.")
                            : Loc.T("No MIDI or pro keys keyboard was found, so practise on the computer keyboard."))) + " ";
                GameMode gm;
                Instrument inst;
                if (mode == "fivefret") { gm = GameMode.FiveFretGuitar; inst = Instrument.FiveFretGuitar; }
                else if (mode == "drums") { bool five = d is FiveLaneDrumkit; gm = five ? GameMode.FiveLaneDrums : GameMode.FourLaneDrums; inst = five ? Instrument.FiveLaneDrums : Instrument.ProDrums; }
                else { gm = GameMode.ProKeys; inst = Instrument.ProKeys; }
                var p = CreateTemp(gm, inst);
                if (p == null) return "";
                p.Bindings.AddDevice(d);
                p.Bindings.SetDefaultBinds(d);
                Device = d;
                string dn = d is Minis.MidiDevice ? MidiBase(d) : d.displayName;
                string text = (trying ? Loc.F("Trying the buttons on your {0}, {1}, with a practice profile that is removed afterwards.", Kind(d), dn)
                    : Loc.F("Practising on your {0}, {1}, with a practice profile that is removed afterwards.", Kind(d), dn)) + " ";
                if (d is Minis.MidiDevice && !p.Bindings.ContainsBindingsForDevice(d))
                    text += (mode == "drums" ? Loc.T("It has no pads bound, so the practice reads it directly.") : Loc.T("It has no keys bound, so the practice reads it directly.")) + " ";
                return text;
            }
            catch (Exception ex)
            {
                ModLog.Warn("PracticeDevices.Ensure", ex);
                return "";
            }
        }

        // Five-fret: a guitar, or a controller when one was pressed last (or there is no guitar but the
        // controller was used last). Drums and pro keys: the best fitting device. Devices on the user's own
        // profiles count too: the tutorial profile only borrows them.
        static InputDevice Find(string mode)
        {
            InputDevice guitar = null, pad = null, best = null;
            foreach (var d in InputSystem.devices)
            {
                if (d == null || !d.enabled) continue;
                if (mode == "fivefret")
                {
                    if (d is FiveFretGuitar) { if (guitar == null) guitar = d; }
                    else if (d is Gamepad && pad == null) pad = d;
                }
                else if (Fits(d, mode) && !MidiChannelDevice(d) && (best == null || Rank(d) < Rank(best))) best = d;
            }
            if (mode != "fivefret") return best;
            if (Keys.Current == Keys.Device.Gamepad && pad != null) return pad;
            return guitar;
        }

        static string EnsureVocals(Choice chosen, bool trying)
        {
            var p = CreateTemp(GameMode.Vocals, Instrument.Vocals);
            if (p == null) return "";
            string text;
            int id = -1;
            string name = null;
            if (chosen != null && chosen.MicName == "") { }
            else if (chosen != null && chosen.MicName != null) { name = chosen.MicName; id = chosen.Player == null ? chosen.MicId : -1; }
            else
            {
                foreach (var m in GlobalAudioHandler.GetAllInputDevices()) { id = m.Item1; name = m.Item2; break; }
            }
            bool none = chosen != null && chosen.MicName == "";
            if (name == null) text = (Tutorials.TryingButtons
                    ? (none ? Loc.T("No microphone, so only the buttons are tried.") : Loc.T("No microphone was found, so only the buttons are tried."))
                    : (none ? Loc.T("No microphone, so the sung and spoken notes only play for you to hear.") : Loc.T("No microphone was found, so the sung and spoken notes only play for you to hear."))) + " ";
            else
            {
                MicInUse = name;
                foreach (var other in PlayerContainer.Players)
                    if (other != null && other != p && other.Bindings != null && other.Bindings.Microphone != null && other.Bindings.Microphone.DisplayName == name)
                        SharedMic = other.Bindings.Microphone;
                if (SharedMic == null)
                {
                    if (id < 0) foreach (var m in GlobalAudioHandler.GetAllInputDevices()) if (m.Item2 == name) id = m.Item1;
                    var mic = id >= 0 ? GlobalAudioHandler.CreateInputDevice(id, name) : null;
                    if (mic != null) p.Bindings.AddMicrophone(mic);
                }
                text = (trying ? Loc.F("Trying the buttons with your microphone, {0}, with a practice profile that is removed afterwards.", name)
                    : Loc.F("Practising with your microphone, {0}, with a practice profile that is removed afterwards.", name)) + " ";
            }
            // The Hit and star power buttons: a controller when one was pressed last, else the computer keyboard.
            if (Keys.Current == Keys.Device.Gamepad)
                foreach (var d in InputSystem.devices)
                    if (d is Gamepad && d.enabled)
                    {
                        p.Bindings.AddDevice(d);
                        p.Bindings.SetDefaultBinds(d);
                        Device = d;
                        break;
                    }
            return text;
        }

        static YargPlayer CreateTemp(GameMode gm, Instrument inst)
        {
            var profile = new YargProfile
            {
                Name = Loc.T("Tutorial Practice"),
                NoteSpeed = 5f,
                HighwayLength = 1f,
                GameMode = gm
            };
            profile.Id = TempId;
            profile.CurrentInstrument = inst;
            if (!PlayerContainer.AddProfile(profile)) return null;
            _tempProfile = profile;
            TempPlayer = PlayerContainer.CreatePlayerFromProfile(profile, false);
            return TempPlayer;
        }

        // Removes the tutorial profile and its player. Called when a practice ends.
        public static void Release()
        {
            try
            {
                if (TempPlayer != null) PlayerContainer.DisposePlayer(TempPlayer);
                if (_tempProfile != null) { PlayerContainer.RemoveProfile(_tempProfile); ForgetBindings(_tempProfile.Id); }
            }
            catch (Exception ex) { ModLog.Warn("PracticeDevices.Release", ex); }
            TempPlayer = null;
            _tempProfile = null;
            Device = null;
            SharedMic = null;
        }

        // A tutorial profile still in the saved list (the game closed during a practice) is removed.
        public static void CleanLeftovers()
        {
            try
            {
                if (GuidedPractice.Active) return;
                var prof = PlayerContainer.GetProfileById(TempId);
                if (prof == null) return;
                var player = PlayerContainer.GetPlayerFromProfile(prof);
                if (player != null) PlayerContainer.DisposePlayer(player);
                PlayerContainer.RemoveProfile(prof);
                ForgetBindings(TempId);
                PlayerContainer.SaveProfiles();
                MelonLogger.Msg("Removed a leftover tutorial practice profile.");
            }
            catch (Exception ex) { ModLog.Warn("PracticeDevices.CleanLeftovers", ex); }
        }

        static void ForgetBindings(Guid id)
        {
            try
            {
                var t = typeof(PlayerContainer).Assembly.GetType("YARG.Input.Bindings.BindingsContainer");
                var f = t?.GetField("_bindings", BindingFlags.NonPublic | BindingFlags.Static);
                var dict = f?.GetValue(null) as System.Collections.IDictionary;
                if (dict != null && dict.Contains(id)) dict.Remove(id);
            }
            catch (Exception ex) { ModLog.Warn("PracticeDevices.ForgetBindings", ex); }
        }

        // The names of the controls bound to an action on the practice device, e.g. "A", "D-pad Down",
        // "Green Fret"; null when there is no practice device or nothing is bound.
        public static string ButtonName(GameMode gm, int action)
        {
            if (TempPlayer == null || Device == null) return null;
            try
            {
                var names = new List<string>();
                foreach (var cb in TempPlayer.Bindings[gm])
                {
                    if (cb.Action != action) continue;
                    var list = cb.GetType().GetProperty("Bindings")?.GetValue(cb, null) as System.Collections.IEnumerable;
                    if (list == null) continue;
                    foreach (var single in list)
                    {
                        var ctl = single.GetType().GetProperty("Control")?.GetValue(single, null) as InputControl;
                        if (ctl == null || ctl.device != Device) continue;
                        var n = Friendly(ctl);
                        if (!names.Contains(n)) names.Add(n);
                    }
                }
                if (names.Count == 0) return null;
                string all = names[0];
                for (int i = 1; i < names.Count; i++) all = Loc.F("{0} or {1}", all, names[i]);
                return all;
            }
            catch (Exception ex) { ModLog.Warn("PracticeDevices.ButtonName", ex); }
            return null;
        }

        static string Friendly(InputControl c)
        {
            string n = string.IsNullOrEmpty(c.displayName) ? c.name : c.displayName;
            // A guitar's strum bar is named "D-Pad Down/Strum Down": the name after the slash is the one to say.
            int slash = n.LastIndexOf('/');
            if (slash >= 0 && slash < n.Length - 1) return n.Substring(slash + 1).Trim();
            if (c.device is Gamepad)
            {
                switch (c.name)
                {
                    case "buttonSouth": return "A";
                    case "buttonEast": return "B";
                    case "buttonWest": return "X";
                    case "buttonNorth": return "Y";
                    case "leftShoulder": return "LB";
                    case "rightShoulder": return "RB";
                    case "leftTrigger": return "LT";
                    case "rightTrigger": return "RT";
                    case "select": return "View";
                    case "start": return "Menu";
                }
            }
            var parent = c.parent;
            if (parent != null && parent != c.device)
            {
                string pn = string.IsNullOrEmpty(parent.displayName) ? parent.name : parent.displayName;
                if (parent.name == "dpad") pn = "D-pad";
                if (!n.StartsWith(pn, StringComparison.OrdinalIgnoreCase)) n = pn + " " + n;
            }
            return n;
        }

        // "controller", "guitar", "drum kit", "MIDI keyboard" for the practice device.
        public static string DeviceKind => Device == null ? null : Kind(Device);
    }
}
