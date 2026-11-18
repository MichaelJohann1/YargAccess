using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Gameplay;
using YARG.Gameplay.HUD;

namespace YargAccess
{
    // Keys for watching a replay (2026-11-15). YARG's own replay bar (play / pause, time, speed, timeline) is
    // mouse-only; these drive the same ReplayController methods. Only while a replay is being watched
    // (GameManager.IsReplay: not Play With Replay) and its menu is closed: nobody is playing, so every
    // device is free.
    //   Keyboard: Space play / pause, Left / Right 5 s back / forward, Page Up / Page Down 30 s,
    //     Home start, Up / Down speed, F2 where you are, Backspace replay menu (Escape too, YARG's own), F1 keys.
    //   Pad: A play / pause, D-pad Left / Right 5 s, LB / RB 30 s, X start, D-pad Up / Down speed,
    //     Y where you are, B or Start replay menu, View keys.
    //   Guitar: Green play / pause, Yellow / Blue 5 s, strum up / down speed, Orange where you are,
    //     Red or Start replay menu, Select keys.
    // Start (and Escape) while the replay is paused with no menu (a pause from these keys, or the replay's
    // end) opens the replay menu: YARG would resume instead, which at the end does nothing
    // (ReplayStartPatch), so a controller could not leave a finished replay before.
    public static class ReplayControls
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        const float Step = 5f, BigStep = 30f, SpeedStep = 0.1f;

        static ReplayController _rc;
        static MethodInfo _setTime;

        public static bool Watching
        {
            get
            {
                var gm = GameplayContext.Manager;
                try { return gm != null && gm.IsReplay && gm.IsSongStarted; } catch { return false; }
            }
        }

        static ReplayController Controller
        {
            get
            {
                if (_rc == null) { try { _rc = UnityEngine.Object.FindObjectOfType<ReplayController>(); } catch { } }
                return _rc;
            }
        }

        public static void Tick()
        {
            if (!Watching || GameplayContext.Paused) return;
            if (!UnityEngine.Application.isFocused) return;
            var dm = YARG.Menu.Persistent.DialogManager.Instance;
            if (dm != null && dm.IsDialogShowing) return;
            try
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.spaceKey.wasPressedThisFrame) TogglePause();
                    if (kb.leftArrowKey.wasPressedThisFrame) Seek(-Step);
                    if (kb.rightArrowKey.wasPressedThisFrame) Seek(+Step);
                    if (kb.pageUpKey.wasPressedThisFrame) Seek(-BigStep);
                    if (kb.pageDownKey.wasPressedThisFrame) Seek(+BigStep);
                    if (kb.homeKey.wasPressedThisFrame) SeekTo(0);
                    if (kb.upArrowKey.wasPressedThisFrame) Speed(+SpeedStep);
                    if (kb.downArrowKey.wasPressedThisFrame) Speed(-SpeedStep);
                    if (kb.f2Key.wasPressedThisFrame) Status();
                    if (kb.backspaceKey.wasPressedThisFrame) OpenMenu();
                    if (kb.f1Key.wasPressedThisFrame) Help(ContextHelp.Device.Keyboard);
                }
                foreach (var dev in InputSystem.devices)
                {
                    if (dev == null || dev is Keyboard || dev is Mouse) continue;
                    var pad = dev as Gamepad;
                    if (pad != null) { Pad(pad); continue; }
                    if (dev.GetType().Name.IndexOf("Guitar", StringComparison.OrdinalIgnoreCase) >= 0) Guitar(dev);
                }
            }
            catch (Exception ex) { ModLog.Warn("ReplayControls.Tick", ex); }
        }

        static void Pad(Gamepad p)
        {
            if (p.buttonSouth.wasPressedThisFrame) TogglePause();
            if (p.dpad.left.wasPressedThisFrame) Seek(-Step);
            if (p.dpad.right.wasPressedThisFrame) Seek(+Step);
            if (p.leftShoulder.wasPressedThisFrame) Seek(-BigStep);
            if (p.rightShoulder.wasPressedThisFrame) Seek(+BigStep);
            if (p.buttonWest.wasPressedThisFrame) SeekTo(0);
            if (p.dpad.up.wasPressedThisFrame) Speed(+SpeedStep);
            if (p.dpad.down.wasPressedThisFrame) Speed(-SpeedStep);
            if (p.buttonNorth.wasPressedThisFrame) Status();
            if (p.buttonEast.wasPressedThisFrame) OpenMenu();
            if (p.selectButton.wasPressedThisFrame) Help(ContextHelp.Device.Gamepad);
        }

        static void Guitar(InputDevice d)
        {
            if (Pressed(d, "greenFret")) TogglePause();
            if (Pressed(d, "yellowFret")) Seek(-Step);
            if (Pressed(d, "blueFret")) Seek(+Step);
            if (Pressed(d, "orangeFret")) Status();
            if (Pressed(d, "redFret")) OpenMenu();
            var dpad = d.TryGetChildControl<DpadControl>("dpad");
            var up = d.TryGetChildControl<ButtonControl>("strumUp") ?? dpad?.up;
            var down = d.TryGetChildControl<ButtonControl>("strumDown") ?? dpad?.down;
            if (up != null && up.wasPressedThisFrame) Speed(+SpeedStep);
            if (down != null && down.wasPressedThisFrame) Speed(-SpeedStep);
            if (Pressed(d, "selectButton")) Help(ContextHelp.Device.Guitar);
        }

        static bool Pressed(InputDevice d, string name)
        {
            var b = d.TryGetChildControl<ButtonControl>(name);
            return b != null && b.wasPressedThisFrame;
        }

        static void Silence()
        {
            HighwaySound.Silence();
            DrumSound.Silence();
            ProKeysSound.Silence();
        }

        static void TogglePause()
        {
            var rc = Controller; var gm = GameplayContext.Manager;
            if (rc == null || gm == null) return;
            if (!gm.Paused) { rc.SetPaused(true); Silence(); Speech.Say(Loc.F("Paused, {0}", Clock(gm.SongTime)), true); return; }
            if (AtEnd(gm)) { Speech.Say(Loc.T("The replay has ended. Seek back first, or open the replay menu."), true); return; }
            rc.SetPaused(false);
            Speech.Say(Loc.T("Playing"), true);
        }

        static bool AtEnd(GameManager gm) => gm.SongTime >= gm.SongLength - 0.05;

        static void Seek(double by)
        {
            var gm = GameplayContext.Manager;
            if (gm == null) return;
            SeekTo(Math.Max(0, gm.SongTime) + by);
        }

        static void SeekTo(double t)
        {
            var rc = Controller; var gm = GameplayContext.Manager;
            if (rc == null || gm == null) return;
            if (_setTime == null) _setTime = typeof(ReplayController).GetMethod("SetReplayTime", BF);
            if (_setTime == null) { Speech.Say(Loc.T("Seeking is not available in this YARG version."), true); return; }
            // Just short of the end: at the end YARG stops the replay (EndSong) and it can't be resumed.
            t = Math.Max(0, Math.Min(t, gm.SongLength - 1.0));
            Silence();
            Audio.CuePlayer.ReplaySeeking = true;
            try { _setTime.Invoke(rc, new object[] { t }); }
            finally { Audio.CuePlayer.ReplaySeeking = false; }
            Speech.Say(gm.Paused ? Loc.F("{0}, paused", Clock(t)) : Clock(t), true);
        }

        static void Speed(float by)
        {
            var rc = Controller; var gm = GameplayContext.Manager;
            if (rc == null || gm == null) return;
            float target = (float) Math.Round((gm.SongSpeed + by) * 10f) / 10f;
            if (target < 0.1f || target > 4f) { Speech.Say(Loc.F("Speed {0}, the limit", Pct(gm.SongSpeed)), true); return; }
            Silence();
            // AdjustSpeed seeks to the current time too (ReplayController.SetSpeed reseeks).
            Audio.CuePlayer.ReplaySeeking = true;
            try { rc.AdjustSpeed(target - gm.SongSpeed); }
            finally { Audio.CuePlayer.ReplaySeeking = false; }
            Speech.Say(Loc.F("Speed {0}", Pct(gm.SongSpeed)), true);
        }

        static string Pct(float s) => Loc.F("{0} percent", Math.Round(s * 100f));

        static void Status()
        {
            var gm = GameplayContext.Manager;
            if (gm == null) return;
            string state = AtEnd(gm) ? Loc.T("Ended") : gm.Paused ? Loc.T("Paused") : Loc.T("Playing");
            Speech.Say(Loc.F("{0}, {1} of {2}, speed {3}", state, Clock(Math.Min(gm.SongTime, gm.SongLength)), Clock(gm.SongLength), Pct(gm.SongSpeed)), true);
        }

        public static void OpenMenu()
        {
            var gm = GameplayContext.Manager;
            if (gm == null || GameplayContext.Paused) return;
            Silence();
            gm.Pause(true);
        }

        static void Help(ContextHelp.Device device)
        {
            Speech.Say(HelpText(device), true);
        }

        public static string HelpText(ContextHelp.Device device)
        {
            switch (device)
            {
                case ContextHelp.Device.Gamepad:
                    return Loc.T("Replay keys. A, play or pause. D-pad Left and Right, 5 seconds back and forward. LB and RB, 30 seconds. X, back to the start. D-pad Up and Down, speed. Y, where you are. B or Start, the replay menu, with Back To Library.");
                case ContextHelp.Device.Guitar:
                    return Loc.T("Replay keys. Green, play or pause. Yellow and Blue, 5 seconds back and forward. Strum up and down, speed. Orange, where you are. Red or Start, the replay menu, with Back To Library.");
                default:
                    return Loc.T("Replay keys. Space, play or pause. Left and Right, 5 seconds back and forward. Page Up and Page Down, 30 seconds. Home, back to the start. Up and Down, speed. F2, where you are. Escape or Backspace, the replay menu, with Back To Library.");
            }
        }

        static string Clock(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ((int) ts.TotalMinutes) + ":" + ts.Seconds.ToString("00");
        }
    }
}

namespace YargAccess.Patches
{
    // Start (YARG's GameManager.OnNavigationEvent, the mod's UnboundStartPause) and Escape call SetPaused.
    // While a watched replay is paused with no menu, SetPaused(false) would resume, and at the replay's end
    // resuming does nothing (EndSong pauses it again), so a controller had no way out. Open the menu instead.
    [HarmonyPatch(typeof(YARG.Gameplay.GameManager), "SetPaused")]
    static class ReplayStartPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(YARG.Gameplay.GameManager), "SetPaused");

        static bool Prefix(YARG.Gameplay.GameManager __instance, bool paused)
        {
            try
            {
                if (!paused && __instance.IsReplay && __instance.Paused && !GameplayContext.Paused)
                {
                    ReplayControls.OpenMenu();
                    return false;
                }
            }
            catch (Exception ex) { ModLog.Warn("ReplayStartPatch", ex); }
            return true;
        }
    }
}
