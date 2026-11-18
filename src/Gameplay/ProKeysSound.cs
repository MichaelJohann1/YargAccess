using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.Keys;
using YARG.Gameplay;
using YARG.Gameplay.Player;
using YargAccess.Audio;

namespace YargAccess
{
    public static class ProKeysSound
    {
        static bool _beginner;

        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        const double MinHold = 0.25;
        const double MinClickGap = 0.07;
        const double MinGap = 0.044;

        class Event
        {
            public double Time, End, Safe, RunEnd, RunSafe, Cue, Gap = double.PositiveInfinity;
            public readonly List<int> Keys = new List<int>();
            public readonly List<double> KeySafe = new List<double>();
            public int Mask;
            public bool Star, StarStart, Repeat;
            public bool Gliss;
            public double Call = double.NaN, Response = double.NaN;
            public bool HasCall => !double.IsNaN(Call);
            public int GlissSample;
            public double GlissEnd;
            public int GlissFrom, GlissTo;
        }

        class Hold { public int Channel; public double StopAt; }

        static ProKeysPlayer _player;
        static ProKeysEngine _engine;
        static GameManager _gm;
        static readonly List<Event> _events = new List<Event>();
        static readonly List<double> _releases = new List<double>();
        static readonly List<Hold> _holds = new List<Hold>();
        static readonly List<int> _order = new List<int>();
        static readonly List<int> _lastCue = new List<int>();
        static int _ei, _ci, _ri, _generation = -1;
        static double _lastTime = double.NaN, _lastCueTime = -999, _lastSpark = -999;
        static double _builtLead = -1, _builtSpeed = -1;
        static float _levelAt = -1, _dropAt = -1, _missAt = -1;
        static int _hits, _misses, _overhits;
        static double _offsetSum, _absOffsetSum;
        static string _songName, _keyName;
        static object _notesRef;

        internal static int MidiOf(int key) => 60 + key;
        internal static float PanOf(int key) => -0.9f + 1.8f * Mathf.Clamp(key, 0, 24) / 24f;

        static readonly string[] PitchNames = { "C", "C sharp", "D", "E flat", "E", "F", "F sharp", "G", "A flat", "A", "B flat", "B" };

        internal static string NameOf(int key)
        {
            if (key == 0) return "low C";
            if (key == 12) return "middle C";
            if (key == 24) return "high C";
            return PitchNames[key % 12] + " " + (key / 12 + 1);
        }

        public static void Reset()
        {
            LogSummary();
            Silence();
            Detach();
            _gm = null;
            _player = null;
            _events.Clear(); _releases.Clear(); _order.Clear(); _lastCue.Clear();
            _builtLead = _builtSpeed = -1;
            _ei = _ci = _ri = 0;
            _lastTime = double.NaN;
        }

        public static void Silence()
        {
            _lastTime = double.NaN;
            _levelAt = _dropAt = -1;
            if (_holds.Count == 0) return;
            foreach (var h in _holds) CuePlayer.FadeOut(h.Channel, 30);
            _holds.Clear();
        }

        static void Detach()
        {
            if (_engine == null) return;
            try
            {
                _engine.OnNoteHit -= OnHit;
                _engine.OnNoteMissed -= OnMiss;
                _engine.OnOverhit -= OnOverhit;
                _engine.OnStarPowerPhraseHit -= OnPhraseHit;
                _engine.OnStarPowerPhraseMissed -= OnPhraseMissed;
                _engine.OnStarPowerStatus -= StarPowerSpeech.OnStatus;
                _engine.OnStarPowerReady -= StarPowerSpeech.OnReady;
                _engine.OnSustainEnd -= OnSustainEnd;
            }
            catch { }
            _engine = null;
        }

        static List<ProKeysNote> NotesOf(ProKeysPlayer p)
        {
            var prop = typeof(TrackPlayer<ProKeysEngine, ProKeysNote>).GetProperty("Notes", BF);
            return prop != null ? prop.GetValue(p, null) as List<ProKeysNote> : null;
        }

        static float _retryAt;

        static bool Attach()
        {
            if (_player != null && _player) { CheckPracticeSection(); return true; }
            if (UnityEngine.Time.unscaledTime < _retryAt) return false;
            _retryAt = UnityEngine.Time.unscaledTime + 0.25f;
            try
            {
                _gm = GameplayContext.Manager;
                if (_gm == null) return false;
                bool humanLoading = false;
                for (int pass = 0; pass < 2 && !(pass == 1 && humanLoading); pass++)
                foreach (var bp in _gm.Players)
                {
                    var p = bp as ProKeysPlayer;
                    if (p == null || p.Player == null || p.Player.Profile.IsBot != (pass == 1) || SoundOwner.Skip(p.Player)) continue;
                    var notes = NotesOf(p);
                    if (notes == null || notes.Count == 0) { humanLoading |= pass == 0; continue; }
                    _player = p;
                    _beginner = p.Player.Profile.CurrentDifficulty == YARG.Core.Difficulty.Beginner;
                    _keyName = HighwaySound.ApplyKey(_gm.Chart);
                    Wire(notes);
                    _hits = _misses = _overhits = 0; _offsetSum = _absOffsetSum = 0;
                    try { _songName = YARG.GlobalVariables.State.CurrentSong.Name + " (" + p.Player.Profile.CurrentDifficulty + ")"; } catch { _songName = "?"; }
                    MelonLogger.Msg("ProKeysSound: " + _events.Count + " notes for " + p.Player.Profile.Name + ", " + _keyName);
                    return true;
                }
            }
            catch (NullReferenceException) { }
            catch (Exception ex) { ModLog.Warn("ProKeysSound.Attach", ex); }
            return false;
        }

        static void Wire(List<ProKeysNote> notes)
        {
            _notesRef = notes;
            Build(notes);
            Detach();
            _engine = _player.Engine;
            if (_engine != null)
            {
                _engine.OnNoteHit += OnHit;
                _engine.OnNoteMissed += OnMiss;
                _engine.OnOverhit += OnOverhit;
                _engine.OnStarPowerPhraseHit += OnPhraseHit;
                _engine.OnStarPowerPhraseMissed += OnPhraseMissed;
                if (!_player.Player.Profile.IsBot) _engine.OnStarPowerStatus += StarPowerSpeech.OnStatus;
                if (!_player.Player.Profile.IsBot) _engine.OnStarPowerReady += StarPowerSpeech.OnReady;
                _engine.OnSustainEnd += OnSustainEnd;
            }
        }

        static void CheckPracticeSection()
        {
            try
            {
                var notes = NotesOf(_player);
                if (notes == null || (ReferenceEquals(notes, _notesRef) && ReferenceEquals(_player.Engine, _engine) && _generation == CueBank.Generation)) return;
                Silence();
                Wire(notes);
                MelonLogger.Msg("ProKeysSound: notes rebuilt, " + _events.Count + " notes");
            }
            catch (Exception ex) { ModLog.Warn("ProKeysSound.CheckPracticeSection", ex); }
        }

        static void Build(List<ProKeysNote> notes)
        {
            CueBank.FreeGlisses();
            _events.Clear(); _releases.Clear();
            var sync = _gm.Chart.SyncTrack;
            uint burst = sync.Resolution / 4;
            var midis = new HashSet<int>();
            foreach (var n in notes)
            {
                var e = new Event { Time = n.Time, End = n.Time, Star = n.IsStarPower, StarStart = n.IsStarPowerStart,
                                    Call = ListenRepeat.CallTime(n), Response = ListenRepeat.ResponseStart(n) };
                e.Gliss = n.IsGlissando && !e.HasCall;
                foreach (var c in n.AllNotes)
                {
                    if (c.Key < 0 || c.Key > 24 || e.Keys.Contains(c.Key)) continue;
                    e.Keys.Add(c.Key);
                    e.Mask |= 1 << c.Key;
                    midis.Add(MidiOf(c.Key));
                    double safe = c.TickLength >= burst ? Math.Max(c.Time, sync.TickToTime(c.TickEnd - burst)) : c.TimeEnd;
                    e.KeySafe.Add(safe);
                    if (c.TimeEnd > e.End) e.End = c.TimeEnd;
                    if (safe > e.Safe) e.Safe = safe;
                }
                if (e.Keys.Count == 0) continue;
                if (e.Safe < e.Time) e.Safe = e.Time;
                e.RunEnd = e.End; e.RunSafe = e.Safe;
                _events.Add(e);
                if (e.End - e.Time >= MinHold) _releases.Add(e.Safe);
            }
            _releases.Sort();
            CueBank.PrepareKeys(midis);

            for (int i = 0; i < _events.Count; i++)
            {
                if (!_events[i].Gliss || (i > 0 && _events[i - 1].Gliss)) continue;
                int j = i;
                while (j + 1 < _events.Count && _events[j + 1].Gliss) j++;
                var head = _events[i];
                head.GlissFrom = MidiOf(head.Keys[0]);
                head.GlissTo = MidiOf(_events[j].Keys[0]);
                head.GlissEnd = _events[j].Time;
                double len = Math.Max(0.15, Math.Min(1.0, head.GlissEnd - head.Time));
                head.GlissSample = CueBank.LoadGliss(head.GlissFrom, head.GlissTo, len);
            }

            double prevEnd = double.NegativeInfinity;
            int runStart = -1;
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                if (i > 0) e.Gap = e.Time - _events[i - 1].Time;
                if (i > 0 && !e.Gliss && e.Time - prevEnd < 0.6) e.Repeat = e.Mask == _events[i - 1].Mask && !_events[i - 1].Gliss;
                if (e.Repeat && runStart >= 0)
                {
                    _events[runStart].RunEnd = Math.Max(_events[runStart].RunEnd, e.End);
                    _events[runStart].RunSafe = Math.Max(_events[runStart].RunSafe, e.Safe);
                }
                else runStart = i;
                prevEnd = Math.Max(prevEnd, e.End);
            }
            _generation = CueBank.Generation;
            Schedule();
        }

        static void Schedule()
        {
            double lead = ModSettings.LookAheadSeconds;
            double speed = SongSpeed();
            foreach (var e in _events) e.Cue = e.HasCall ? e.Call : e.Time - lead * speed;
            _order.Clear();
            for (int i = 0; i < _events.Count; i++) _order.Add(i);
            _order.Sort((a, b) => { int c = _events[a].Cue.CompareTo(_events[b].Cue); return c != 0 ? c : a.CompareTo(b); });
            CueBank.EnsureRiser(lead);
            _builtLead = lead;
            _builtSpeed = speed;
            _ei = _ci = _ri = 0;
            _lastTime = double.NaN;
        }

        static double SongSpeed()
        {
            try { return _gm != null && _gm.SongSpeed > 0 ? _gm.SongSpeed : 1.0; } catch { return 1.0; }
        }

        public static void Tick()
        {
            if (!ModSettings.NoteSounds.Value) return;
            if (!CuePlayer.Ready || !CueBank.EnsureLoaded()) return;
            if (!Attach()) return;
            try
            {
                double now = GameplayContext.CueTime(_gm, _player);
                if (ModSettings.LookAheadSeconds != _builtLead || SongSpeed() != _builtSpeed) { Silence(); Schedule(); }
                if (double.IsNaN(_lastTime) || now < _lastTime - 0.05 || now > _lastTime + 0.5) Seek(now);
                _lastTime = now;

                float real = Time.unscaledTime;
                if (_levelAt > 0 && real >= _levelAt)
                {
                    _levelAt = -1;
                    try { CueBank.PlayLevel((int) Math.Round(_engine.GetStarPowerBarAmount() * 4), 0.8f); } catch { }
                }
                if (_dropAt > 0 && real >= _dropAt) { _dropAt = -1; CueBank.Play("dropped", 0.6f); }

                for (int h = _holds.Count - 1; h >= 0; h--)
                    if (_holds[h].StopAt <= now) { CuePlayer.FadeOut(_holds[h].Channel, 25); _holds.RemoveAt(h); }

                double lead = _builtLead * _builtSpeed;
                while (_ei < _order.Count && _events[_order[_ei]].Cue <= now)
                {
                    int i = _order[_ei];
                    var e = _events[i];
                    if (e.Time > now)
                    {
                        if (e.Gliss) { if (e.GlissSample != 0) CueBank.PlayGliss(e.GlissSample, 0.8f); }
                        else if (ModSettings.CueRepeats.Value || !e.Repeat || e.HasCall) Fire(e);
                        if (e.Star) Sparkle(e);
                        if (!e.Gliss && !e.HasCall && e.Gap >= lead + 0.02) CueBank.PlayRiser(_builtLead, 0.8f);
                    }
                    _ei++;
                }
                while (_ci < _events.Count && _events[_ci].Time <= now)
                {
                    var e = _events[_ci];
                    if (!e.Gliss && e.Time > now - 0.05 && (e.HasCall || (e.Gap < lead + 0.02 && e.Gap >= MinClickGap)))
                        CueBank.Play("click", 0.7f);
                    _ci++;
                }
                while (_ri < _releases.Count && _releases[_ri] <= now)
                {
                    if (_releases[_ri] > now - 0.05) CueBank.Play("release", 0.5f);
                    _ri++;
                }
            }
            catch (Exception ex) { ModLog.Warn("ProKeysSound.Tick", ex); }
        }

        static void Seek(double now)
        {
            Silence();
            _ei = 0; while (_ei < _order.Count && _events[_order[_ei]].Cue <= now) _ei++;
            _ci = 0; while (_ci < _events.Count && _events[_ci].Time <= now) _ci++;
            _ri = 0; while (_ri < _releases.Count && _releases[_ri] <= now) _ri++;
            _lastCue.Clear();
            _lastCueTime = -999;
            _lastSpark = -999;
        }

        static void Fire(Event e)
        {
            double gap = e.Time - _lastCueTime;
            if (gap < MinGap) return;
            if (gap < 0.11) foreach (var ch in _lastCue) CuePlayer.FadeOut(ch, 5);
            _lastCue.Clear();
            bool runs = !ModSettings.CueRepeats.Value && !e.HasCall && !_beginner;
            for (int k = 0; k < e.Keys.Count; k++)
            {
                int key = e.Keys[k];
                int ch = CueBank.PlayKey(MidiOf(key), PanOf(key), 0.9f);
                if (ch != 0) _lastCue.Add(ch);
                double stop = runs && e.RunSafe > e.Safe ? e.RunSafe : e.KeySafe[k];
                double end = runs ? e.RunEnd : e.End;
                if (e.HasCall) stop = Math.Min(stop - (e.Time - e.Call), e.Response - 0.05);
                // A call's tone stops before the note's own hit time, so compare with when the call plays.
                if (end - e.Time >= MinHold && stop > (e.HasCall ? e.Call : e.Time))
                {
                    int hc = CueBank.StartKeyHold(MidiOf(key), PanOf(key));
                    if (hc != 0) _holds.Add(new Hold { Channel = hc, StopAt = stop });
                }
            }
            _lastCueTime = e.Time;
        }

        static void Sparkle(Event e)
        {
            bool active = false;
            try { active = _engine.EngineStats.IsStarPowerActive; } catch { }
            if (e.StarStart && HighwaySound.PhraseMakesReady(_engine, active)) { _lastSpark = e.Time; CueBank.Play("spstart", 0.8f); return; }
            if (e.Time - _lastSpark < 0.15) return;
            _lastSpark = e.Time;
            CueBank.Play("spnote", 0.8f);
        }

        static void LogSummary()
        {
            if ((object) _player == null || _hits + _misses + _overhits == 0) return;
            string timing = _hits > 0
                ? ", hits on average " + Math.Round(Math.Abs(_offsetSum / _hits) * 1000) + " ms " + (_offsetSum >= 0 ? "late" : "early")
                  + ", typically " + Math.Round(_absOffsetSum / _hits * 1000) + " ms off"
                : "";
            MelonLogger.Msg("ProKeysSound summary, " + _songName + ": " + _hits + " keys hit, " + _misses + " missed, " + _overhits + " overhits" + timing
                + ", look-ahead " + Math.Round(ModSettings.LookAheadSeconds * 1000) + " ms, repeats " + (ModSettings.CueRepeats.Value ? "cued" : "held")
                + ", " + _keyName);
        }

        static void OnHit(int index, ProKeysNote note)
        {
            if (!ModSettings.NoteSounds.Value || _gm == null || _gm.Paused) return;
            try
            {
                _dropAt = -1;
                double off = GameplayContext.JudgeTime(_gm, _player) - note.Time;
                _hits++; _offsetSum += off; _absOffsetSum += Math.Abs(off);
                CueBank.PlayKeyHit(MidiOf(note.Key), PanOf(note.Key), 0.8f);
            }
            catch { }
        }

        static void OnMiss(int index, ProKeysNote note)
        {
            if (!ModSettings.NoteSounds.Value || _gm == null || _gm.Paused) return;
            _misses++;
            float now = Time.unscaledTime;
            if (now - _missAt < 0.06f) return;
            _missAt = now;
            CueBank.Play("miss", 0.9f);
        }

        static void OnOverhit(int key)
        {
            if (!ModSettings.NoteSounds.Value || _gm == null || _gm.Paused) return;
            _overhits++;
            CueBank.Play("overstrum", 0.5f);
            if (key >= 0 && key <= 24) CueBank.PlayKey(MidiOf(key), PanOf(key), 0.35f);
        }

        static void OnPhraseHit(ProKeysNote note)
        {
            if (_gm == null || _gm.Paused) return;
            _levelAt = Time.unscaledTime + 0.5f;
        }

        static void OnPhraseMissed(ProKeysNote note)
        {
            if (_gm == null || _gm.Paused) return;
            CueBank.Play("splost", 0.6f);
        }

        static void OnSustainEnd(ProKeysNote note, double timeEnded, bool finished)
        {
            if (finished || _gm == null || _gm.Paused) return;
            _dropAt = Time.unscaledTime + 0.05f;
        }
    }
}
