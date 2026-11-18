using UnityEngine;
using YARG.Menu.Navigation;

namespace YargAccess
{
    public static class GameplayContext
    {
        static YARG.Gameplay.GameManager _gm;
        static YARG.Gameplay.HUD.PauseMenuManager _pm;

        public static bool InGameplay
        {
            get
            {
                if (_gm == null) { try { _gm = Object.FindObjectOfType<YARG.Gameplay.GameManager>(); } catch { } }
                return _gm != null;
            }
        }

        public static YARG.Gameplay.GameManager Manager => InGameplay ? _gm : null;

        public static YARG.Gameplay.HUD.PauseMenuManager PauseManager
        {
            get
            {
                if (_pm == null) { try { _pm = Object.FindObjectOfType<YARG.Gameplay.HUD.PauseMenuManager>(); } catch { } }
                return _pm;
            }
        }

        // A replay stops at its end paused, with no menu and no score screen (GameManager.EndSong:
        // Pause(showMenu: false)), which was silent. Said once per replay.
        static YARG.Gameplay.GameManager _endedReplay;

        public static void WatchReplayEnd()
        {
            var gm = Manager;
            if (gm == null || gm == _endedReplay) return;
            try
            {
                if (gm.IsReplay && gm.IsSongStarted && gm.Paused && !Paused && gm.SongTime >= gm.SongLength)
                {
                    _endedReplay = gm;
                    string menu = InputDriver.LastDevice == Keys.Device.Gamepad ? "B or Start"
                        : InputDriver.LastDevice == Keys.Device.Guitar ? "Red or Start" : "Escape or Backspace";
                    MenuAnnouncer.SayShown("Replay finished. " + menu + " opens the replay menu, with Back To Library and Restart.");
                }
            }
            catch { }
        }

        public static bool Paused
        {
            get { try { var pm = PauseManager; return pm != null && pm.IsOpen; } catch { return false; } }
        }

        // The clock cue sounds are timed on (2026-11-14). YARG keeps the music on SongTime = InputTime + audio
        // calibration (plus hardware latency when that setting is on), so the cues, played on the same BASS
        // device, come out in step with the song. A player's profile Input Calibration moves when YARG judges
        // their presses (BasePlayer.InputCalibration = -InputCalibrationSeconds is added to the input time), so
        // it moves their cues too: press as the cue tells you and the press lands on the note. Video
        // calibration only moves the picture and is not used. The cues used VisualTime before, which took the
        // video calibration and ignored the other two.
        public static double CueTime(YARG.Gameplay.GameManager gm, YARG.Gameplay.Player.BasePlayer p)
        {
            return gm.SongTime + (p != null ? p.InputCalibration : 0);
        }

        // The time YARG judges a player's input against (InputTime plus their profile's input calibration).
        public static double JudgeTime(YARG.Gameplay.GameManager gm, YARG.Gameplay.Player.BasePlayer p)
        {
            return gm.InputTime + (p != null ? p.InputCalibration : 0);
        }

        // The cue clock with no player: player one's (who gets the note sounds), else plain SongTime.
        public static double CueTime(YARG.Gameplay.GameManager gm)
        {
            try
            {
                var one = SoundOwner.PlayerOne();
                if (one != null)
                    foreach (var p in gm.Players)
                        if (p != null && ReferenceEquals(p.Player, one)) return CueTime(gm, p);
            }
            catch { }
            return gm.SongTime;
        }

        public static NavigationGroup ActivePauseGroup()
        {
            var pm = PauseManager;
            if (pm == null) return null;
            foreach (var ng in pm.GetComponentsInChildren<NavigationGroup>(false))
                if (ng != null && ng.isActiveAndEnabled && ng.Count > 0) return ng;
            return null;
        }
    }
}
