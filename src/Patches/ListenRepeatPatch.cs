using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MelonLoader;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Core.Replays;

namespace YargAccess.Patches
{
    // Live play, replay playback and YARG's replay analysis all clone a track and run ApplyModifiers
    // on it, so cutting the listen notes here gives every one of them the same chart.
    [HarmonyPatch]
    static class ListenRepeatCutPatch
    {
        static readonly ConditionalWeakTable<object, object> _done = new ConditionalWeakTable<object, object>();

        static bool Prepare() => PatchGuard.Has(typeof(YargProfile), "ApplyModifiers");

        static IEnumerable<MethodBase> TargetMethods()
        {
            var m = AccessTools.Method(typeof(YargProfile), "ApplyModifiers");
            if (m == null || !m.IsGenericMethodDefinition) yield break;
            foreach (var t in new[] { typeof(GuitarNote), typeof(DrumNote), typeof(ProKeysNote) })
                yield return m.MakeGenericMethod(t);
        }

        static void Postfix(YargProfile __instance, object track, SyncTrack syncTrack)
        {
            try
            {
                if (__instance == null || track == null) return; // bots too (user, 2026-11-14)
                object seen;
                if (_done.TryGetValue(track, out seen)) return;
                _done.Add(track, track);
                var guitar = track as InstrumentDifficulty<GuitarNote>;
                var drums = track as InstrumentDifficulty<DrumNote>;
                var keys = track as InstrumentDifficulty<ProKeysNote>;
                bool cut = false;
                if (guitar != null) cut = ListenRepeat.Cut(guitar, syncTrack, __instance);
                else if (drums != null) cut = ListenRepeat.Cut(drums, syncTrack, __instance);
                else if (keys != null) cut = ListenRepeat.Cut(keys, syncTrack, __instance);
                if (cut) ListenRepeat.CutProfiles.Add(__instance.Id);
            }
            catch (Exception ex) { ModLog.Warn("ListenRepeatCutPatch", ex); }
        }
    }

    [HarmonyPatch(typeof(YargProfile), "ApplyVocalModifiers")]
    static class ListenRepeatVocalPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(YargProfile), "ApplyVocalModifiers");

        static readonly ConditionalWeakTable<object, object> _done = new ConditionalWeakTable<object, object>();

        static void Postfix(YargProfile __instance, VocalsPart vocalsPart)
        {
            try
            {
                if (__instance == null || vocalsPart == null) return; // bots too
                object seen;
                if (_done.TryGetValue(vocalsPart, out seen)) return;
                _done.Add(vocalsPart, vocalsPart);
                var gm = GameplayContext.Manager;
                if (gm == null || gm.Chart == null) return;
                ListenRepeat.CutPercussion(vocalsPart, gm.Chart.SyncTrack, __instance);
                if (ListenRepeat.PercussionCut) ListenRepeat.CutProfiles.Add(__instance.Id);
            }
            catch (Exception ex) { ModLog.Warn("ListenRepeatVocalPatch", ex); }
        }
    }

    [HarmonyPatch(typeof(YARG.Gameplay.PracticeManager), "SetPracticeSection", new[] { typeof(uint), typeof(uint), typeof(double), typeof(double) })]
    static class ListenRepeatPracticePatch
    {
        // The exact overload: another SetPracticeSection may stay when this one goes.
        static bool Prepare() => AccessTools.Method(typeof(YARG.Gameplay.PracticeManager), "SetPracticeSection",
            new[] { typeof(uint), typeof(uint), typeof(double), typeof(double) }) != null;

        static void Prefix(ref uint tickStart, ref uint tickEnd, ref double timeStart, ref double timeEnd)
        {
            try
            {
                if (!ListenRepeat.SongUsed) return;
                uint start = tickStart, end = tickEnd;
                ListenRepeat.AdjustSection(ref start, ref end);
                if (start == tickStart && end == tickEnd) return;
                var sync = GameplayContext.Manager.Chart.SyncTrack;
                if (start != tickStart) { tickStart = start; timeStart = sync.TickToTime(start); }
                if (end != tickEnd) { tickEnd = end; timeEnd = sync.TickToTime(end); }
            }
            catch (Exception ex) { ModLog.Warn("ListenRepeatPracticePatch", ex); }
        }
    }

    [HarmonyPatch(typeof(YARG.Gameplay.GameManager), "RecordScores")]
    static class ListenRepeatScorePatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(YARG.Gameplay.GameManager), "RecordScores");

        static void Postfix(YARG.Gameplay.GameManager __instance) { ListenRepeatScores.Record(__instance); }
    }

    // History's Analyze re-runs a replay outside a song; the chart is cut as the replay was, not by the
    // current Play Style.
    [HarmonyPatch]
    static class ListenRepeatAnalyzePatch
    {
        static MethodBase Target() => AccessTools.Method(AccessTools.TypeByName("YARG.Core.Replays.Analyzer.ReplayAnalyzer"), "AnalyzeReplay");

        static bool Prepare()
        {
            bool ok = AccessTools.TypeByName("YARG.Core.Replays.Analyzer.ReplayAnalyzer") != null && Target() != null;
            if (!ok) MelonLoader.MelonLogger.Msg("Patch skipped: ReplayAnalyzer.AnalyzeReplay not found (ListenRepeatAnalyzePatch)");
            return ok;
        }

        static MethodBase TargetMethod() => Target();

        static void Prefix(ReplayInfo info) { ListenRepeat.AnalyzingReplay = info != null ? info.FilePath : null; }

        static Exception Finalizer(Exception __exception) { ListenRepeat.AnalyzingReplay = null; return __exception; }
    }

    [HarmonyPatch(typeof(YARG.Gameplay.GameManager), "SaveReplay")]
    static class ListenRepeatReplayPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(YARG.Gameplay.GameManager), "SaveReplay");

        static void Postfix(ReplayInfo __result)
        {
            if (__result != null) ListenRepeat.SaveReplay(__result.FilePath);
        }
    }

    // The score card tags Full Combo by its own check (every note in one streak), not by the flag
    // ListenRepeat clears, so a Listen and Repeat player's card gets the tag it would have had
    // otherwise: High Score, or Cleared / Completed (user, 2026-11-14).
    [HarmonyPatch]
    static class ListenRepeatScoreCardPatch
    {
        // An empty target list makes Harmony throw inside PatchAll, so the class skips itself instead.
        static bool Prepare()
        {
            bool ok = AccessTools.TypeByName("YARG.Menu.ScoreScreen.ScoreCard`1") != null;
            if (!ok) MelonLoader.MelonLogger.Msg("Patch skipped: ScoreCard`1 not found (ListenRepeatScoreCardPatch)");
            return ok;
        }

        static IEnumerable<MethodBase> TargetMethods()
        {
            var open = AccessTools.TypeByName("YARG.Menu.ScoreScreen.ScoreCard`1");
            if (open == null) yield break;
            foreach (var stats in new[] { typeof(YARG.Core.Engine.Guitar.GuitarStats), typeof(YARG.Core.Engine.Drums.DrumsStats), typeof(YARG.Core.Engine.Keys.KeysStats), typeof(YARG.Core.Engine.Vocals.VocalsStats) })
            {
                MethodBase m = null;
                try { m = AccessTools.Method(open.MakeGenericType(stats), "SetCardContents"); } catch { }
                if (m != null) yield return m;
            }
        }

        static void Postfix(object __instance)
        {
            try
            {
                var t = __instance.GetType();
                var player = AccessTools.Property(t, "Player")?.GetValue(__instance, null) as YARG.Player.YargPlayer;
                var stats = AccessTools.Field(t, "Stats")?.GetValue(__instance) as YARG.Core.Engine.BaseStats;
                if (player == null || stats == null || player.Profile.IsBot || player.IsReplay) return;
                if (!stats.IsFullCombo || !ListenRepeat.CutProfiles.Contains(player.Profile.Id)) return;
                bool high = (bool) AccessTools.Field(t, "IsHighScore").GetValue(__instance);
                string tag = high ? "High Score"
                    : YARG.Settings.SettingsManager.Settings.NoFail.Value.ToString() != "Off" ? "Completed" : "Cleared";
                AccessTools.Method(t, "ShowTag")?.Invoke(__instance, new object[] { tag });
                var colorizer = AccessTools.Field(t, "_colorizer")?.GetValue(__instance) as YARG.Menu.ScoreScreen.ScoreCardColorizer;
                if (colorizer != null) colorizer.SetCardColor(YARG.Menu.ScoreScreen.ScoreCardColorizer.ScoreCardColor.Blue);
            }
            catch (Exception ex) { ModLog.Warn("ListenRepeatScoreCardPatch", ex); }
        }
    }
}
