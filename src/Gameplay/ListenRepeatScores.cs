using System;
using System.Collections.Generic;
using System.Globalization;
using MelonLoader;
using YARG.Core;
using YARG.Core.Song;
using YARG.Player;

namespace YargAccess
{
    // Listen and Repeat plays about half of a chart, so its best scores are kept apart from YARG's,
    // per profile, song, instrument, difficulty and pattern length.
    public static class ListenRepeatScores
    {
        class Best { public int Score, Stars, Percent; public bool Fc; }

        static Dictionary<string, Best> _all;
        static readonly List<string> _lastSong = new List<string>();

        static string Key(Guid profile, string hash, Instrument inst, Difficulty diff, int length)
            => profile + "|" + hash + "|" + inst + "|" + diff + "|" + length;

        static Dictionary<string, Best> All()
        {
            if (_all != null) return _all;
            _all = new Dictionary<string, Best>();
            var raw = ModSettings.ListenRepeatBest != null ? ModSettings.ListenRepeatBest.Value : "";
            foreach (var item in (raw ?? "").Split(';'))
            {
                int eq = item.LastIndexOf('=');
                if (eq <= 0) continue;
                var v = item.Substring(eq + 1).Split(',');
                int score, stars, pct;
                if (v.Length < 4 || !int.TryParse(v[0], out score) || !int.TryParse(v[1], out stars) || !int.TryParse(v[2], out pct)) continue;
                _all[item.Substring(0, eq)] = new Best { Score = score, Stars = stars, Percent = pct, Fc = v[3] == "1" };
            }
            return _all;
        }

        static void Save()
        {
            var parts = new List<string>();
            foreach (var kv in All())
                parts.Add(kv.Key + "=" + kv.Value.Score + "," + kv.Value.Stars + "," + kv.Value.Percent + "," + (kv.Value.Fc ? "1" : "0"));
            ModSettings.ListenRepeatBest.Value = string.Join(";", parts.ToArray());
            ModSettings.Save();
        }

        // After a song: keeps each human player's result if it beats their best.
        public static void Record(YARG.Gameplay.GameManager gm)
        {
            _lastSong.Clear();
            if (!ListenRepeat.SongUsed || gm == null) return;
            try
            {
                var song = YARG.GlobalVariables.State.CurrentSong;
                if (song == null) return;
                bool changed = false;
                foreach (var p in gm.Players)
                {
                    var prof = p.Player != null ? p.Player.Profile : null;
                    if (prof == null || prof.IsBot || p.Player.IsReplay || prof.GameMode == GameMode.Vocals) continue;
                    int size = ListenRepeat.SizeOf(prof);
                    if (size <= 0) continue;
                    if (!YARG.Scores.ScoreContainer.IsSoloScoreValid(gm.SongSpeed, p.Player)) continue;
                    float pctRaw = p.BaseStats.Percent;
                    int pct = (int) Math.Floor(pctRaw <= 1.0001f ? pctRaw * 100f : pctRaw);
                    var now = new Best { Score = p.Score, Stars = (int) Math.Floor(p.Stars), Percent = pct, Fc = p.IsFc };
                    var key = Key(prof.Id, song.Hash.ToString(), prof.CurrentInstrument, prof.CurrentDifficulty, size);
                    Best old;
                    bool better = !All().TryGetValue(key, out old) || now.Score > old.Score;
                    if (better) { All()[key] = now; changed = true; }
                    var b = better ? now : old;
                    _lastSong.Add(better ? Loc.F("Listen and Repeat, new best: {0}", Describe(b, prof.CurrentDifficulty)) : Loc.F("Listen and Repeat best: {0}", Describe(b, prof.CurrentDifficulty)));
                }
                if (changed) Save();
            }
            catch (Exception ex) { ModLog.Warn("ListenRepeatScores.Record", ex); }
        }

        // Spoken on the score screen after a Listen and Repeat song.
        public static string ScoreLine() => _lastSong.Count == 0 ? "" : string.Join(". ", _lastSong.ToArray());

        // The song review: the single human player's Listen and Repeat best on their current part.
        public static string ForSong(SongEntry e)
        {
            try
            {
                YargPlayer human = null;
                int humans = 0;
                foreach (var p in PlayerContainer.Players)
                    if (p != null && p.Profile != null && !p.Profile.IsBot) { human = p; humans++; }
                if (humans != 1 || e == null) return null;
                var prof = human.Profile;
                Best best = null;
                Difficulty bestDiff = prof.CurrentDifficulty;
                foreach (Difficulty d in Enum.GetValues(typeof(Difficulty)))
                {
                    Best b;
                    if (All().TryGetValue(Key(prof.Id, e.Hash.ToString(), prof.CurrentInstrument, d, ModSettings.CallLength), out b) && (best == null || b.Score > best.Score))
                    { best = b; bestDiff = d; }
                }
                return best == null ? null : Loc.F("Listen and Repeat: {0}", Describe(best, bestDiff));
            }
            catch (Exception ex) { ModLog.Warn("ListenRepeatScores.ForSong", ex); return null; }
        }

        static string Describe(Best b, Difficulty d)
        {
            string score = b.Score.ToString("N0", CultureInfo.CurrentCulture);
            // The difficulty in YARG's words when YARG isn't in English (English keeps the enum name as before).
            string diff = d.ToString();
            if (!Loc.English) { try { diff = YARG.Localization.Localize.ToLocalizedName(d); } catch { } }
            string line = b.Stars == 1
                ? Loc.F("{0} star, {1} on {2}, {3}%", b.Stars, score, diff, b.Percent)
                : Loc.F("{0} stars, {1} on {2}, {3}%", b.Stars, score, diff, b.Percent);
            return b.Fc ? Loc.F("{0}, full combo", line) : line;
        }
    }
}
