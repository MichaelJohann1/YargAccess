using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Core.Game;
using YARG.Player;

namespace YargAccess
{
    public static class ReviewBinds
    {
        public enum Kind { Previous, Next, All }

        public class Bind
        {
            public string Profile;
            public Kind Kind;
            public string Layout;
            public string Path;
            public string Hold;
            public string Name;
            internal bool Was;
            internal float At, Last;
        }

        static readonly List<Bind> _binds = new List<Bind>();
        static MelonPreferences_Entry<string> _saved;

        public static readonly string[] Names = { Loc.T("Text review, previous piece"), Loc.T("Text review, next piece"), Loc.T("Text review, read it all") };

        static void Load()
        {
            if (_saved != null) return;
            var cat = MelonPreferences.CreateCategory("YargAccessReviewBinds", "YargAccess review binds");
            _saved = cat.CreateEntry("Binds", "");
            _binds.Clear();
            foreach (var line in _saved.Value.Split('\n'))
            {
                var f = line.Split('\t');
                if (f.Length < 6) continue;
                int k;
                if (!int.TryParse(f[1], out k) || k < 0 || k > 2) continue;
                _binds.Add(new Bind { Profile = f[0], Kind = (Kind) k, Layout = f[2], Path = f[3], Hold = f[4].Length > 0 ? f[4] : null, Name = f[5] });
            }
        }

        static void Save()
        {
            var lines = new List<string>();
            foreach (var b in _binds)
                lines.Add(string.Join("\t", new[] { b.Profile, ((int) b.Kind).ToString(), b.Layout, b.Path, b.Hold ?? "", b.Name }));
            _saved.Value = string.Join("\n", lines.ToArray());
            MelonPreferences.Save();
        }

        public static List<Bind> For(YargProfile profile, Kind kind)
        {
            Load();
            var res = new List<Bind>();
            if (profile == null) return res;
            string id = profile.Id.ToString();
            foreach (var b in _binds) if (b.Profile == id && b.Kind == kind) res.Add(b);
            return res;
        }

        public static void Remove(Bind b)
        {
            Load();
            _binds.Remove(b);
            Save();
        }

        public static string Defaults(YargProfile profile, Kind kind)
        {
            bool kb = false, pad = false, guitar = false;
            foreach (var d in Devices(profile))
            {
                if (d is Keyboard) kb = true;
                else if (d is Gamepad) pad = true;
                else if (d.GetType().Name.IndexOf("Guitar", StringComparison.OrdinalIgnoreCase) >= 0) guitar = true;
            }
            if (!kb && !pad && !guitar) kb = true;
            var parts = new List<string>();
            if (kb) parts.Add(kind == Kind.Previous ? Loc.T("left bracket") : kind == Kind.Next ? Loc.T("right bracket") : Loc.T("backslash"));
            if (pad) parts.Add(kind == Kind.Previous ? Loc.T("right stick left") : kind == Kind.Next ? Loc.T("right stick right") : Loc.T("right stick down"));
            if (guitar) parts.Add(kind == Kind.Previous ? Loc.T("hold Select and press Yellow") : kind == Kind.Next ? Loc.T("hold Select and press Blue") : Loc.T("hold Select and press Green"));
            return string.Join("; ", parts.ToArray());
        }

        internal static List<InputDevice> Devices(YargProfile profile)
        {
            var res = new List<InputDevice>();
            try
            {
                var player = profile != null ? PlayerContainer.GetPlayerFromProfile(profile) : null;
                if (player != null && player.Bindings != null && player.Bindings.InputDevices != null)
                    foreach (var d in player.Bindings.InputDevices) if (d != null) res.Add(d);
            }
            catch { }
            return res;
        }

        const float InitialDelay = 0.5f, RepeatInterval = 0.12f;

        public static void Tick()
        {
            Load();
            if (_binds.Count == 0 || Capturing) return;
            float now = Time.unscaledTime;
            try
            {
                foreach (var player in PlayerContainer.Players)
                {
                    if (player == null || player.Profile == null || player.Bindings == null || player.Bindings.InputDevices == null) continue;
                    string id = player.Profile.Id.ToString();
                    foreach (var b in _binds)
                    {
                        if (b.Profile != id) continue;
                        bool down = false;
                        foreach (var d in player.Bindings.InputDevices)
                            if (d != null && d.layout == b.Layout && Down(d, b)) { down = true; break; }
                        if (down && !b.Was) { b.At = b.Last = now; Fire(b.Kind); }
                        else if (down && b.Kind != Kind.All && now - b.At >= InitialDelay && now - b.Last >= RepeatInterval) { b.Last = now; Fire(b.Kind); }
                        b.Was = down;
                    }
                }
            }
            catch (Exception ex) { ModLog.Warn("ReviewBinds.Tick", ex); }
        }

        static bool Down(InputDevice d, Bind b)
        {
            var c = d.TryGetChildControl<ButtonControl>(b.Path);
            if (c == null || !c.isPressed) return false;
            if (b.Hold == null) return true;
            var h = d.TryGetChildControl<ButtonControl>(b.Hold);
            return h != null && h.isPressed;
        }

        static void Fire(Kind k)
        {
            if (k == Kind.Previous) TextReview.Previous();
            else if (k == Kind.Next) TextReview.Next();
            else TextReview.All();
        }

        static YargProfile _capProfile;
        static Kind _capKind;
        static float _capStart;
        static readonly HashSet<ButtonControl> _resting = new HashSet<ButtonControl>();
        static ButtonControl _first;

        public static bool Capturing => _capProfile != null;

        public static void BeginCapture(YargProfile profile, Kind kind)
        {
            Load();
            var devs = Devices(profile);
            if (devs.Count == 0) { MenuAnnouncer.SayShown(Loc.T("This profile has no device connected. Connect it on the profile list first.")); return; }
            _capProfile = profile;
            _capKind = kind;
            _capStart = Time.unscaledTime;
            _first = null;
            _resting.Clear();
            foreach (var c in Candidates(devs)) if (c.isPressed) _resting.Add(c);
            MenuAnnouncer.SayShown(HasKeyboard(devs)
                ? Loc.F("Press the button for {0}. To use a combination, hold one button and press another. Escape cancels.", Names[(int) kind])
                : Loc.F("Press the button for {0}. To use a combination, hold one button and press another. Nothing pressed for 10 seconds cancels.", Names[(int) kind]));
        }

        static bool HasKeyboard(List<InputDevice> devs)
        {
            foreach (var d in devs) if (d is Keyboard) return true;
            return false;
        }

        internal static bool AnyHeld(YargProfile profile)
        {
            try { foreach (var c in Candidates(Devices(profile))) if (c.isPressed) return true; }
            catch { }
            return false;
        }

        static IEnumerable<ButtonControl> Candidates(List<InputDevice> devs)
        {
            foreach (var d in devs)
                foreach (var c in d.allControls)
                {
                    var b = c as ButtonControl;
                    if (b == null || b.children.Count > 0 || b.noisy) continue;
                    if (b.name == "anyKey" || (d is Keyboard && b.name == "escape")) continue;
                    yield return b;
                }
        }

        public static bool CaptureTick()
        {
            if (!Capturing) return false;
            try
            {
                var kb = Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame) { Cancel(Loc.T("Canceled")); return true; }
                if (_first == null && Time.unscaledTime - _capStart > 10f) { Cancel(Loc.T("Nothing pressed. Canceled")); return true; }
                var devs = Devices(_capProfile);
                foreach (var c in Candidates(devs))
                {
                    if (_resting.Contains(c)) { if (!c.isPressed) _resting.Remove(c); continue; }
                    if (!c.isPressed) continue;
                    if (_first == null) { _first = c; return true; }
                    if (c != _first && _first.isPressed) { Finish(c, _first); return true; }
                }
                if (_first != null && !_first.isPressed) Finish(_first, null);
            }
            catch (Exception ex) { ModLog.Warn("ReviewBinds.CaptureTick", ex); Cancel(Loc.T("Canceled")); }
            return true;
        }

        static void Finish(ButtonControl c, ButtonControl hold)
        {
            var d = c.device;
            string dev = d.displayName;
            // Saved with the bind and read back as it is, so it is kept in the language it was set in.
            string name = (hold != null ? Loc.F("hold {0} and press {1}", hold.displayName, c.displayName) : c.displayName) + ", " + dev;
            var b = new Bind
            {
                Profile = _capProfile.Id.ToString(), Kind = _capKind, Layout = d.layout,
                Path = Relative(c), Hold = hold != null ? Relative(hold) : null, Name = name,
                Was = true,
            };
            _binds.Add(b);
            Save();
            var kind = _capKind;
            _capProfile = null;
            _first = null;
            MenuAnnouncer.SayShown(Loc.F("{0} set to {1}", Names[(int) kind], name));
        }

        static string Relative(InputControl c)
        {
            var p = c.path;
            var dp = c.device.path;
            return p.StartsWith(dp + "/") ? p.Substring(dp.Length + 1) : c.name;
        }

        static void Cancel(string say)
        {
            _capProfile = null;
            _first = null;
            MenuAnnouncer.SayShown(say);
        }
    }
}
