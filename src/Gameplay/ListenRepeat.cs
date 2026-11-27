using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using MelonLoader;
using YARG.Core.Chart;
using YargAccess.Audio;

namespace YargAccess
{
    // Listen and Repeat: the chart is split into windows of Call Length bars (1, 2 or 4). The notes of one
    // window play as a call in the window before it, whole bars earlier so the rhythm is the song's, and
    // the chart's notes in that listen window are taken out. The player then plays the window back at its
    // real time. Listen and answer both start on a bar line.
    public static class ListenRepeat
    {
        public class Group
        {
            public uint CallStartTick, StartTick, EndTick;
            public double CallStart, CallEnd, Start, End;
            // The profile whose part this group belongs to (the swells play for one listener only).
            public object Owner;
        }

        class Mark { public double Call; public Group Group; }

        sealed class RefEq : IEqualityComparer<object>
        {
            public static readonly RefEq Instance = new RefEq();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }

        const double MarkerLength = 0.4;

        static readonly Dictionary<object, Mark> _marks = new Dictionary<object, Mark>(RefEq.Instance);
        static readonly List<Group> _groups = new List<Group>();
        static object _sync;

        // Settings of the run being built: the live settings, or a replay's saved ones.
        public static bool SongUsed { get; private set; }
        public static int SongCallLength { get; private set; }

        public static IList<Group> Groups => _groups;

        // Profiles whose part was cut this song (the score screen drops their Full Combo tag).
        public static readonly HashSet<Guid> CutProfiles = new HashSet<Guid>();

        public static double CallTime(object note)
        {
            Mark m;
            return note != null && _marks.TryGetValue(note, out m) ? m.Call : double.NaN;
        }

        public static double ResponseStart(object note)
        {
            Mark m;
            return note != null && _marks.TryGetValue(note, out m) ? m.Group.Start : double.NaN;
        }

        static void NewSong(SyncTrack sync)
        {
            if (ReferenceEquals(sync, _sync)) return;
            _sync = sync;
            _marks.Clear();
            _groups.Clear();
            _cutPlayers.Clear();
            CutProfiles.Clear();
            _sizes.Clear();
            _listener = null;
            _listenerHuman = false;
            _listenerRank = 0;
            _swellCount = -1;
            _percussionCut = false;
            SongUsed = false;
            SongCallLength = 0;
            _mi = _ti = 0;
            _lastTime = double.NaN;
        }

        // Returns the group size to use for this run, or 0 for the whole chart. A replay's players use the
        // replay's saved setting; with Play With Replay the live players use the current Play Style.
        static int RunCallLength(YARG.Core.Game.YargProfile profile)
        {
            try
            {
                // History's Analyze checks a replay outside a song: cut the chart as that replay was.
                if (AnalyzingReplay != null) return ReadReplay(AnalyzingReplay, profile);
                var state = YARG.GlobalVariables.State;
                var replay = state.CurrentReplay;
                if (replay != null && !(state.PlayingWithReplay && IsLive(profile))) return ReadReplay(replay.FilePath, profile);
            }
            catch { }
            return ModSettings.ListenAndRepeat ? ModSettings.CallLength : 0;
        }

        // The replay file History's Analyze is checking (set by ListenRepeatAnalyzePatch), else null.
        public static string AnalyzingReplay;

        // A profile of a player in this session (not one read from the replay file).
        static bool IsLive(YARG.Core.Game.YargProfile profile)
        {
            if (profile == null) return false;
            foreach (var p in YARG.Player.PlayerContainer.Players)
                if (p != null && ReferenceEquals(p.Profile, profile)) return true;
            return false;
        }

        // Each profile's group size this song (0 = the whole chart), and whose groups the swells follow:
        // a live player before a replay's, a human before a bot.
        static readonly Dictionary<object, int> _sizes = new Dictionary<object, int>(RefEq.Instance);
        static object _listener;
        static bool _listenerHuman;
        static int _listenerRank;
        static readonly object NoProfile = new object();

        public static int SizeOf(YARG.Core.Game.YargProfile profile)
        {
            int size;
            return profile != null && _sizes.TryGetValue(profile, out size) ? size : 0;
        }

        static void Own(YARG.Core.Game.YargProfile profile, int size, int g0)
        {
            object owner = profile ?? NoProfile;
            for (int i = 0; i < _groups.Count; i++) if (_groups[i].Owner == null) _groups[i].Owner = owner;
            if (profile != null) _sizes[profile] = _groups.Count > g0 ? size : 0;
            if (_groups.Count == g0) return;
            bool human = profile != null && !profile.IsBot;
            bool live = human && IsLive(profile);
            // Play With Replay: live players come first, a human before a bot, then the replay's players.
            int rank = (IsLive(profile) ? 2 : 0) + (human ? 1 : 0);
            if (_listener == null || rank > _listenerRank) { _listener = owner; _listenerHuman = human; _listenerRank = rank; }
            // The size a saved replay is marked with: a live player's, else the first one cut.
            if (IsLive(profile) && SongCallLength == 0) SongCallLength = size;
            SongUsed = true;
            _swellCount = -1;
        }

        public static bool ReplayIsListenRepeat(string replayPath, out int size)
        {
            size = ReadReplay(replayPath, null);
            return size > 0;
        }

        // Returns whether this track was cut.
        public static bool Cut<TNote>(InstrumentDifficulty<TNote> track, SyncTrack sync, YARG.Core.Game.YargProfile profile = null) where TNote : Note<TNote>
        {
            NewSong(sync);
            int size = RunCallLength(profile);
            if (size <= 0 || track == null || track.Notes.Count == 0) { if (profile != null) _sizes[profile] = 0; return false; }

            var notes = track.Notes;
            int n = notes.Count;
            var ticks = new uint[n];
            var ends = new uint[n];
            var times = new double[n];
            var fixedNotes = new bool[n];
            for (int i = 0; i < n; i++)
            {
                uint end = notes[i].Tick;
                foreach (var c in notes[i].AllNotes) if (c.TickEnd > end) end = c.TickEnd;
                ticks[i] = notes[i].Tick;
                ends[i] = end;
                times[i] = notes[i].Time;
                fixedNotes[i] = notes[i].IsBigRockEnding;
            }
            int g0 = _groups.Count;
            var marks = Plan(ticks, ends, times, fixedNotes, sync, size);
            var keep = new bool[n];
            for (int i = 0; i < n; i++)
            {
                keep[i] = fixedNotes[i] || marks[i] != null;
                if (marks[i] != null) foreach (var c in notes[i].AllNotes) _marks[c] = marks[i];
            }
            Own(profile, size, g0);
            Remove(track, keep);
            int lost = DropEmptyStarPower(track);
            MelonLogger.Msg("ListenRepeat: " + track.Instrument + " " + track.Difficulty + ", " + (_groups.Count - g0) + " groups of " + SizeText(size) + " bars"
                + ", " + track.Notes.Count + " of " + n + " notes kept" + (lost > 0 ? ", " + lost + " star power phrases left with no notes taken out" : ""));
            return _groups.Count > g0;
        }

        // Vocals: only the percussion notes, which are played like drums. Sung and spoken notes stay.
        public static void CutPercussion(VocalsPart part, SyncTrack sync, YARG.Core.Game.YargProfile profile = null)
        {
            if (part == null || sync == null) return;
            NewSong(sync);
            int size = RunCallLength(profile);
            if (size <= 0) return;
            var perc = new List<VocalNote>();
            foreach (var ph in part.NotePhrases)
                foreach (var c in ph.PhraseParentNote.ChildNotes)
                    if (c.IsPercussion) perc.Add(c);
            if (perc.Count == 0) return;
            perc.Sort((a, b) => a.Tick.CompareTo(b.Tick));
            int n = perc.Count;
            var ticks = new uint[n];
            var times = new double[n];
            for (int i = 0; i < n; i++) { ticks[i] = perc[i].Tick; times[i] = perc[i].Time; }
            int g0 = _groups.Count;
            var marks = Plan(ticks, ticks, times, new bool[n], sync, size);
            Own(profile, size, g0);
            var drop = new HashSet<object>(RefEq.Instance);
            for (int i = 0; i < n; i++)
            {
                if (marks[i] != null) _marks[perc[i]] = marks[i];
                else drop.Add(perc[i]);
            }
            if (drop.Count > 0) _percussionCut = true;
            for (int i = part.NotePhrases.Count - 1; i >= 0; i--)
            {
                var ph = part.NotePhrases[i];
                ph.PhraseParentNote.ChildNotes.RemoveAll(c => drop.Contains(c));
                if (ph.IsEmpty) part.NotePhrases.RemoveAt(i);
            }
            MelonLogger.Msg("ListenRepeat: vocal percussion, " + (_groups.Count - g0) + " groups of " + SizeText(size) + " bars, " + (n - drop.Count) + " of " + n + " notes kept");
        }

        // A size of NoteGroups or more is an old replay's note count (NoteGroups + 2, 4 or 8). Those
        // replays grouped by that many notes and are cut the same way.
        public const int NoteGroups = 100;

        // Returns each kept note's call, or null for a note taken out.
        static Mark[] Plan(uint[] ticks, uint[] ends, double[] times, bool[] fixedNotes, SyncTrack sync, int size)
        {
            if (size >= NoteGroups) return PlanNotes(ticks, ends, times, fixedNotes, sync, size - NoteGroups);
            var marks = PlanBars(ticks, ends, fixedNotes, sync, size);
            _groups.Sort((a, b) => a.CallStart.CompareTo(b.CallStart));
            return marks;
        }

        // Groups by bars, since a pattern that starts and ends mid-bar is hard to follow.
        // The listen is `size` whole bars starting on a bar line; the answer is the next `size` bars, so
        // both start on a bar line, and each listen bar is as long as its answer bar (PickWindow). The
        // answer's notes play in the listen bars, the same distance into the bar (in ticks, so tempo changes
        // keep the rhythm), and the chart's own notes in the listen bars are taken out. Then the next listen starts after the answer. A rest of a
        // whole window or more starts the next listen where the notes come back.
        // Fixed notes (big rock endings) are never grouped or taken out.
        static Mark[] PlanBars(uint[] ticks, uint[] ends, bool[] fixedNotes, SyncTrack sync, int size)
        {
            int n = ticks.Length;
            var marks = new Mark[n];
            uint last = 0;
            for (int i = 0; i < n; i++) last = Math.Max(last, Math.Max(ticks[i], ends[i]));
            var bars = BarTicks(sync, n > 0 ? ticks[0] : 0, last, 2 * size + 2);
            uint res = sync.Resolution > 0 ? sync.Resolution : 480;
            uint tol = Math.Max(1u, res / 8);
            int free = 0; // the first bar the next listen may use
            int k = 0;
            while (k < n)
            {
                if (fixedNotes[k]) { k++; continue; }
                int s, sz;
                if (!PickWindow(bars, BarOf(bars, ticks[k]), free, size, tol, out s, out sz)) break;
                uint callStart = bars[s - sz], start = bars[s], end = bars[s + sz];
                // Notes before the answer fall in the listen bars: taken out.
                int j = k;
                while (j < n && ticks[j] < start) j++;
                var members = new List<int>();
                var callTicks = new List<uint>();
                uint realEnd = end - 1;
                for (; j < n && ticks[j] < end; j++)
                {
                    if (fixedNotes[j]) continue;
                    // Each answer bar's notes play in the matching listen bar, the same distance into it.
                    int b = BarOf(bars, ticks[j]) - s;
                    uint ct = bars[s - sz + b] + (ticks[j] - bars[s + b]);
                    // A listen bar a little shorter than its answer bar can't hold this note's call: taken out.
                    if (ct >= bars[s - sz + b + 1]) continue;
                    members.Add(j);
                    callTicks.Add(ct);
                    realEnd = Math.Max(realEnd, ends[j]);
                }
                // The next listen starts on the first bar line after the answer, or after a held note that
                // runs past it.
                free = BarOf(bars, realEnd) + 1;
                k = j;
                if (members.Count == 0) continue;
                var g = new Group
                {
                    CallStartTick = callStart,
                    StartTick = start,
                    CallStart = sync.TickToTime(callStart),
                    CallEnd = sync.TickToTime(start),
                    Start = sync.TickToTime(start),
                    EndTick = realEnd,
                    End = sync.TickToTime(realEnd),
                };
                for (int i = 0; i < members.Count; i++) marks[members[i]] = new Mark { Call = sync.TickToTime(callTicks[i]), Group = g };
                _groups.Add(g);
            }
            return marks;
        }

        // The answer's first bar (s) and the window's length in bars (sz) for the next group, whose notes
        // start in bar `from`. The listen is the sz bars just before the answer and must not start before
        // bar `free`. Each listen bar must be as long as its answer bar, or a time signature change puts
        // the call off the beat: at a change the window gets shorter, or starts a bar later (that bar's
        // notes are then in the listen and taken out). Returns false when no window fits.
        static bool PickWindow(List<uint> bars, int from, int free, int size, uint tol, out int s, out int sz)
        {
            for (; from + 1 < bars.Count; from++)
            {
                for (sz = size; sz >= 1; sz--)
                {
                    s = Math.Max(from, free + sz);
                    if (s + sz >= bars.Count) continue;
                    bool ok = true;
                    for (int i = 0; i < sz && ok; i++)
                    {
                        uint a = bars[s - sz + i + 1] - bars[s - sz + i], b = bars[s + i + 1] - bars[s + i];
                        ok = (a > b ? a - b : b - a) <= tol;
                    }
                    if (ok) return true;
                }
            }
            s = sz = 0;
            return false;
        }

        // The tick of every bar line from before the first note to `extra` bars past the last tick: the
        // chart's measure lines, else bars worked out from its time signatures, and past the end the last
        // bar's length repeated.
        static List<uint> BarTicks(SyncTrack sync, uint first, uint last, int extra)
        {
            var bars = new List<uint>();
            try
            {
                foreach (var b in sync.Beatlines)
                    if (b.Type == BeatlineType.Measure && (bars.Count == 0 || b.Tick > bars[bars.Count - 1])) bars.Add(b.Tick);
            }
            catch { bars.Clear(); }
            uint res = sync.Resolution > 0 ? sync.Resolution : 480;
            if (bars.Count < 2)
            {
                bars.Clear();
                var ts = sync.TimeSignatures;
                for (int i = 0; ts != null && i < ts.Count; i++)
                {
                    uint len = BarLength(ts[i].Numerator, ts[i].Denominator, res);
                    uint stop = i + 1 < ts.Count ? ts[i + 1].Tick : last + 1;
                    for (uint t = ts[i].Tick; t < stop; t += len)
                        if (bars.Count == 0 || t > bars[bars.Count - 1]) bars.Add(t);
                }
                if (bars.Count == 0) bars.Add(0);
                if (bars.Count < 2) bars.Add(bars[0] + 4 * res);
            }
            while (bars[0] > first)
            {
                uint len = bars[1] - bars[0];
                bars.Insert(0, bars[0] > len ? bars[0] - len : 0);
            }
            uint lastLen = bars[bars.Count - 1] - bars[bars.Count - 2];
            int lastBar = BarOf(bars, last);
            while (bars.Count <= lastBar + extra + 1) bars.Add(bars[bars.Count - 1] + lastLen);
            return bars;
        }

        static uint BarLength(uint numerator, uint denominator, uint res)
        {
            if (numerator == 0 || denominator == 0) return 4 * res;
            return Math.Max(1u, (uint) Math.Round(4.0 * res * numerator / denominator));
        }

        // The bar a tick falls in: the last bar line at or before it.
        static int BarOf(List<uint> bars, uint tick)
        {
            int lo = 0, hi = bars.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (bars[mid] <= tick) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        // The note grouping, kept for old replays saved with it: groups of `size` notes.
        static Mark[] PlanNotes(uint[] ticks, uint[] ends, double[] times, bool[] fixedNotes, SyncTrack sync, int size)
        {
            int n = ticks.Length;
            uint res = sync.Resolution;
            var marks = new Mark[n];
            long prevEnd = long.MinValue;
            int k = 0;
            while (k < n)
            {
                if (fixedNotes[k]) { k++; continue; }
                var members = new List<int> { k };
                int j = k + 1;
                while (j < n && members.Count < size && !fixedNotes[j])
                {
                    if (ticks[j] - ticks[j - 1] > 4 * res) break;
                    if (ticks[j] - ticks[k] > 8 * res) break;
                    members.Add(j++);
                }
                uint first = ticks[k];
                uint lastEnd = first;
                foreach (int m in members) lastEnd = Math.Max(lastEnd, Math.Min(ends[m], ticks[m] + res));
                uint beats = (uint) Math.Ceiling((lastEnd - first + (double) res) / res);
                uint shift = beats * res;
                long callStart = (long) first - shift;
                if (callStart < 0 || (prevEnd != long.MinValue && callStart < prevEnd + res / 2)) { k++; continue; }

                var g = new Group
                {
                    CallStartTick = (uint) callStart,
                    StartTick = first,
                    CallStart = sync.TickToTime((uint) callStart),
                    CallEnd = sync.TickToTime(lastEnd - shift),
                    Start = times[k],
                };
                uint realEnd = first;
                foreach (int m in members)
                {
                    realEnd = Math.Max(realEnd, ends[m]);
                    marks[m] = new Mark { Call = sync.TickToTime(ticks[m] - shift), Group = g };
                }
                g.EndTick = realEnd;
                g.End = sync.TickToTime(realEnd);
                _groups.Add(g);
                prevEnd = realEnd;
                k = j;
            }
            _groups.Sort((a, b) => a.CallStart.CompareTo(b.CallStart));
            return marks;
        }

        // Takes out the listen notes the way YARG's own No Kicks does: star power, solo and activator
        // flags move to the nearest kept note, and the notes are linked again.
        static void Remove<TNote>(InstrumentDifficulty<TNote> track, bool[] keep) where TNote : Note<TNote>
        {
            var notes = track.Notes;
            var kept = new List<TNote>(notes.Count);
            for (int i = 0; i < notes.Count; i++)
            {
                var r = notes[i];
                if (keep[i]) { kept.Add(r); continue; }
                TNote next = null, prev = kept.Count > 0 ? kept[kept.Count - 1] : null;
                for (int x = i + 1; x < notes.Count; x++) if (keep[x]) { next = notes[x]; break; }
                if (r.IsStarPowerStart && next != null && next.IsStarPower && !EndsBefore(notes, i, next)) Flag(next, NoteFlags.StarPowerStart);
                if (r.IsStarPowerEnd && prev != null && prev.IsStarPower) Flag(prev, NoteFlags.StarPowerEnd);
                if (r.IsSoloStart && next != null && next.IsSolo) Flag(next, NoteFlags.SoloStart);
                if (r.IsSoloEnd && prev != null && prev.IsSolo) Flag(prev, NoteFlags.SoloEnd);
                var drum = r as DrumNote;
                var nextDrum = next as DrumNote;
                if (drum != null && drum.IsStarPowerActivator && nextDrum != null)
                    foreach (var c in nextDrum.AllNotes) c.ActivateFlag(DrumNoteFlags.StarPowerActivator);
            }
            notes.Clear();
            notes.AddRange(kept);
            for (int i = 0; i < notes.Count; i++)
            {
                notes[i].PreviousNote = i > 0 ? notes[i - 1] : null;
                notes[i].NextNote = i + 1 < notes.Count ? notes[i + 1] : null;
            }
        }

        // A star power phrase whose notes were all taken out can't be earned, so it is taken out of the
        // track's phrases too: YARG counts the phrases there for the score screen's total.
        static int DropEmptyStarPower<TNote>(InstrumentDifficulty<TNote> track) where TNote : Note<TNote>
        {
            int lost = 0;
            var notes = track.Notes;
            for (int p = track.Phrases.Count - 1; p >= 0; p--)
            {
                var ph = track.Phrases[p];
                if (ph.Type != PhraseType.StarPower) continue;
                bool any = false;
                foreach (var note in notes)
                {
                    if (note.Tick >= ph.Tick + Math.Max(ph.TickLength, 1u)) break;
                    if (note.Tick >= ph.Tick && note.IsStarPower) { any = true; break; }
                }
                if (any) continue;
                track.Phrases.RemoveAt(p);
                lost++;
            }
            return lost;
        }

        // True when the phrase the removed note starts ends before the next kept note.
        static bool EndsBefore<TNote>(List<TNote> notes, int i, TNote next) where TNote : Note<TNote>
        {
            for (int x = i; x < notes.Count && !ReferenceEquals(notes[x], next); x++) if (notes[x].IsStarPowerEnd) return true;
            return false;
        }

        static void Flag<TNote>(TNote note, NoteFlags flag) where TNote : Note<TNote>
        {
            foreach (var c in note.AllNotes) c.ActivateFlag(flag);
        }

        // Practice: a section starts at the beginning of the listen it falls in and ends after the whole response.
        public static void AdjustSection(ref uint start, ref uint end)
        {
            foreach (var g in _groups)
            {
                if (start >= g.CallStartTick && start <= g.EndTick) start = Math.Min(start, g.CallStartTick);
                if (end > g.CallStartTick && end <= g.EndTick) end = Math.Max(end, g.EndTick + 1);
            }
        }

        // Replays keep the settings they were played with in a file next to the replay.
        public static void SaveReplay(string replayPath)
        {
            if (string.IsNullOrEmpty(replayPath) || !SongUsed) return;
            try
            {
                // First line: the run's size (what the History menu reads). Then one line per profile, live
                // players first, so a replay with players on different settings is cut the way each one played.
                // "ListenAndRepeatBars" holds sizes in bars; old replays have "ListenAndRepeat" with note counts.
                // A replay player cut by notes (Play With Replay on an old replay) is written with an n.
                var sb = new System.Text.StringBuilder("ListenAndRepeatBars " + SizeText(SongCallLength));
                for (int pass = 0; pass < 2; pass++)
                    foreach (var kv in _sizes)
                    {
                        var prof = kv.Key as YARG.Core.Game.YargProfile;
                        if (prof != null && IsLive(prof) == (pass == 0)) sb.Append("\n" + prof.Id + " " + SizeText(kv.Value));
                    }
                File.WriteAllText(replayPath + ".yargaccess", sb.ToString());
            }
            catch (Exception ex) { ModLog.Warn("ListenRepeat.SaveReplay", ex); }
        }

        static int ReadReplay(string replayPath, YARG.Core.Game.YargProfile profile)
        {
            try
            {
                var path = replayPath + ".yargaccess";
                if (!File.Exists(path)) return 0;
                var lines = File.ReadAllText(path).Replace("\r", "").Trim().Split('\n');
                var parts = lines[0].Trim().Split(' ');
                if (parts.Length != 2 || (parts[0] != "ListenAndRepeat" && parts[0] != "ListenAndRepeatBars")) return 0;
                bool notes = parts[0] == "ListenAndRepeat";
                int size = ParseSize(parts[1], notes);
                if (size <= 0) return 0;
                if (profile != null)
                {
                    string id = profile.Id.ToString();
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var p = lines[i].Trim().Split(' ');
                        if (p.Length == 2 && p[0] == id) return ParseSize(p[1], notes);
                    }
                }
                return size;
            }
            catch { return 0; }
        }

        // A size as a replay file writes it: bars as a number, an old note count with an n.
        static string SizeText(int size) => size >= NoteGroups ? (size - NoteGroups) + "n" : size.ToString();

        static int ParseSize(string s, bool notes)
        {
            if (s.EndsWith("n")) { notes = true; s = s.Substring(0, s.Length - 1); }
            int v;
            if (!int.TryParse(s, out v) || v <= 0) return 0;
            return notes ? v + NoteGroups : v;
        }

        // How a run's size is read out: "4 bars", or an old replay's "4 notes".
        public static string SizeName(int size) => size >= NoteGroups
            ? Loc.F("{0} notes", size - NoteGroups)
            : size == 1 ? Loc.T("1 bar") : Loc.F("{0} bars", size);

        // Listen and your-turn swells.
        static int _mi, _ti;
        static double _lastTime = double.NaN;

        // A full combo needs every note of the chart, so a player whose part was cut never has one:
        // no Full Combo pop-up or ring, and the score YARG saves is not marked full combo.
        // YARG sets IsFc back to true on a practice restart or a replay seek; this runs every frame.
        static readonly Dictionary<object, bool> _cutPlayers = new Dictionary<object, bool>(RefEq.Instance);
        static bool _percussionCut;
        public static bool PercussionCut => _percussionCut;
        static System.Reflection.FieldInfo _playersField;
        static System.Reflection.PropertyInfo _isFc;

        static void BlockFullCombo(YARG.Gameplay.GameManager gm)
        {
            if (_playersField == null)
            {
                _playersField = HarmonyLib.AccessTools.Field(typeof(YARG.Gameplay.GameManager), "_players");
                _isFc = HarmonyLib.AccessTools.Property(typeof(YARG.Gameplay.Player.BasePlayer), "IsFc");
            }
            var players = _playersField?.GetValue(gm) as System.Collections.IEnumerable;
            if (players == null || _isFc == null) return;
            foreach (var o in players)
            {
                var p = o as YARG.Gameplay.Player.BasePlayer;
                if (p == null || !p.IsFc || !IsCut(p)) continue;
                _isFc.SetValue(p, false, null);
                var meter = HarmonyLib.AccessTools.Field(p.GetType(), "ComboMeter")?.GetValue(p) as YARG.Gameplay.Visuals.ComboMeter;
                if (meter != null) meter.SetFullCombo(false);
            }
        }

        static bool IsCut(YARG.Gameplay.Player.BasePlayer p)
        {
            bool cut;
            if (_cutPlayers.TryGetValue(p, out cut)) return cut;
            if (p is YARG.Gameplay.Player.VocalsPlayer) cut = _percussionCut;
            else
            {
                var notes = HarmonyLib.AccessTools.Property(p.GetType(), "Notes")?.GetValue(p, null) as System.Collections.IEnumerable;
                if (notes == null) return false;
                bool any = false;
                foreach (var n in notes)
                {
                    any = true;
                    if (!double.IsNaN(CallTime(n))) { cut = true; break; }
                }
                if (!any) return false;
            }
            _cutPlayers[p] = cut;
            return cut;
        }

        // The listener's groups, by call start (listen swell) and by answer start (your-turn swell).
        static readonly List<Group> _calls = new List<Group>(), _answers = new List<Group>();
        static int _swellCount = -1;

        public static void Tick()
        {
            if (!SongUsed || _groups.Count == 0) return;
            var gm = GameplayContext.Manager;
            // Until the song has started the last song's groups may still be here and YARG's clock is not set up.
            if (gm == null || !gm.IsSongStarted) return;
            if (_swellCount != _groups.Count)
            {
                _swellCount = _groups.Count;
                _calls.Clear();
                _answers.Clear();
                // The swells follow player one, the player who gets the note sounds (SoundOwner).
                var one = SoundOwner.PlayerOne();
                object listener = one != null && one.Profile != null ? one.Profile : _listener;
                foreach (var g in _groups) if (ReferenceEquals(g.Owner, listener)) { _calls.Add(g); _answers.Add(g); }
                _calls.Sort((a, b) => a.CallStart.CompareTo(b.CallStart));
                _answers.Sort((a, b) => a.Start.CompareTo(b.Start));
                _lastTime = double.NaN;
            }
            try { BlockFullCombo(gm); }
            catch (Exception ex) { ModLog.Warn("ListenRepeat full combo", ex); }
            try
            {
                double now = GameplayContext.CueTime(gm);
                double speed = gm.SongSpeed > 0 ? gm.SongSpeed : 1.0;
                double lead = MarkerLength * speed;
                if (double.IsNaN(_lastTime) || now < _lastTime - 0.05 || now > _lastTime + 0.5)
                {
                    _mi = 0; while (_mi < _calls.Count && _calls[_mi].CallStart - lead <= now) _mi++;
                    _ti = 0; while (_ti < _answers.Count && _answers[_ti].Start - lead <= now) _ti++;
                }
                _lastTime = now;
                while (_mi < _calls.Count && _calls[_mi].CallStart - lead <= now)
                {
                    if (_calls[_mi].CallStart > now) CueBank.Play("listen", 0.7f);
                    _mi++;
                }
                while (_ti < _answers.Count && _answers[_ti].Start - lead <= now)
                {
                    if (_answers[_ti].Start > now) CueBank.Play("yourturn", 0.7f);
                    _ti++;
                }
                Duck(gm, now, speed);
            }
            catch (Exception ex) { ModLog.Warn("ListenRepeat.Tick", ex); }
        }

        // Duck Music During Listen: the song's mixer goes down by Ducking Amount while a
        // call plays and back up for the answer, with a short ramp on each side. YARG keeps the mixer at 1.0
        // and fades it only when a song fails or Auto Calibrate Video changes on pause, so nothing is
        // touched while paused or failed, and the mixer is only set when the level changes.
        const double DuckRamp = 0.25;
        static readonly System.Reflection.FieldInfo _mixerField =
            HarmonyLib.AccessTools.Field(typeof(YARG.Gameplay.GameManager), "_mixer");
        static object _duckMixer;
        static double _duckApplied = 1.0;

        static void Duck(YARG.Gameplay.GameManager gm, double now, double speed)
        {
            if (gm.Paused || gm.PlayerHasFailed || _mixerField == null) return;
            var mixer = _mixerField.GetValue(gm) as YARG.Core.Audio.StemMixer;
            if (mixer == null) return;
            if (!ReferenceEquals(mixer, _duckMixer)) { _duckMixer = mixer; _duckApplied = 1.0; }
            double depth = 0;
            if (ModSettings.DuckMusic != null && ModSettings.DuckMusic.Value)
            {
                double ramp = DuckRamp * speed;
                foreach (var g in _calls)
                {
                    if (g.CallStart - ramp > now) break;
                    if (g.CallEnd <= now) continue;
                    // Fully down at the call's start, fully up at the answer's start.
                    double d = Math.Min((now - (g.CallStart - ramp)) / ramp, (g.CallEnd - now) / ramp);
                    depth = Math.Max(depth, Math.Min(1.0, d));
                }
            }
            double amount = ModSettings.DuckAmount != null ? ModSettings.DuckAmount.Value / 100.0 : 0.5;
            double level = 1.0 - amount * depth;
            if (Math.Abs(level - _duckApplied) < 0.002) return;
            _duckApplied = level;
            mixer.SetVolume(level);
        }

        public static double DuckLevel => _duckApplied;

    }
}
