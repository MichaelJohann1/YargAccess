using System;
using System.Collections.Generic;
using MelonLoader;

namespace YargAccess
{
    // Logs each song once it starts: the song, practice / replay and speed, the mod's sound settings,
    // YARG's calibration, and every player (profile, instrument, difficulty, engine preset, modifiers,
    // devices). "It sounds wrong" and "the timing is off" reports mostly come down to these.
    public static class SongLog
    {
        static bool _logged;

        public static void Reset() => _logged = false;

        public static void Tick()
        {
            if (_logged || !GameplayContext.InGameplay) return;
            var gm = GameplayContext.Manager;
            if (gm == null || !gm.IsSongStarted) return;
            _logged = true;
            try { MelonLogger.Msg(Describe(gm)); }
            catch (Exception ex) { ModLog.Warn("SongLog", ex); }
        }

        static string Describe(YARG.Gameplay.GameManager gm)
        {
            var lines = new List<string>();
            var song = gm.Song;
            lines.Add("Song started: " + (song != null ? song.Name + " by " + song.Artist : "?")
                + (gm.IsPractice ? ", practice" : "") + (gm.IsReplay ? ", replay" : "")
                + ", speed " + Math.Round(gm.SongSpeed * 100) + " %");

            lines.Add("Mod settings: " + Try(() =>
                "Play Style " + (ModSettings.ListenAndRepeat ? "Listen and Repeat" : "Play What You Hear")
                + ", Call Length " + ModSettings.CallLengthSetting.Value
                + ", Duck Music " + (ModSettings.DuckMusic.Value ? ModSettings.DuckAmount.Value + "%" : "off")
                + ", Look-ahead " + Math.Round(ModSettings.LookAheadSeconds * 1000) + " ms"
                + ", Drums Look-ahead " + Math.Round(ModSettings.DrumLookAheadSeconds * 1000) + " ms"
                + ", Beat Ticks " + OnOff(ModSettings.BeatTicks.Value)
                + ", Cue Repeated Notes " + OnOff(ModSettings.CueRepeats.Value)
                + ", Pitch Guide " + OnOff(ModSettings.PitchGuide.Value)
                + ", Pitch Feedback " + OnOff(ModSettings.PitchFeedback.Value)
                + ", Speech During Songs " + ModSettings.InSongSpeech.Value
                + ", cues master volume " + ModSettings.CueVolume.Value));

            lines.Add("YARG settings: " + Try(() =>
            {
                var s = YARG.Settings.SettingsManager.Settings;
                return "audio calibration " + s.AudioCalibration.Value + " ms, video calibration "
                    + s.VideoCalibration.Value + " ms, account for hardware latency " + OnOff(s.AccountForHardwareLatency.Value)
                    + ", playback buffer " + s.PlaybackBufferLength.Value;
            }));

            int i = 0;
            foreach (var bp in gm.Players)
            {
                i++;
                var p = bp?.Player;
                if (p == null) continue;
                lines.Add("Player " + i + ": " + Try(() =>
                {
                    var pr = p.Profile;
                    return pr.Name + (pr.IsBot ? " (bot)" : "") + (p.IsReplay ? " (from the replay)" : "") + ", " + pr.CurrentInstrument + ", " + pr.CurrentDifficulty
                        + ", engine " + Try(() => p.EnginePreset != null ? p.EnginePreset.Name : "?")
                        + ", modifiers " + pr.CurrentModifiers
                        + (pr.LeftyFlip ? ", lefty flip" : "")
                        + ", input calibration " + pr.InputCalibrationMilliseconds + " ms"
                        + ", devices: " + (p.IsReplay ? "none" : Try(() => SessionLog.Devices(p)));
                }));
            }
            return string.Join("\n  ", lines);
        }

        static string OnOff(bool b) => b ? "on" : "off";

        static string Try(Func<string> f)
        {
            try { return f(); }
            catch (Exception ex) { return "unknown (" + ex.GetType().Name + ")"; }
        }
    }
}
