using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Core.Input;
using YARG.Gameplay;
using YARG.Gameplay.Player;
using YargAccess.Audio;

namespace YargAccess
{
    public static class DrumSound
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        const double MinGap = 0.044;

        class Event
        {
            public double Time;
            public double Cue;
            public double Gap;
            public List<int> Pads = new List<int>();
            public List<float> Volumes = new List<float>();
            // Per pad: 1 = ghost (a whisper with it), 2 = accent (a crack with it), 0 = plain.
            public List<int> Marks = new List<int>();
            public int Mask;
            public bool Star;
            public bool StarStart;
            public bool Activator;
            public double Call = double.NaN;
            public bool HasCall => !double.IsNaN(Call);
        }

        class Lane { public double Start, End, Cue; public bool Swell; public List<int> Pads = new List<int>(); }
        static readonly List<Lane> _lanes = new List<Lane>();
        class Fill { public double Start, End, Cue, EndCue; }
        static readonly List<Fill> _fills = new List<Fill>();
        static int _li, _fi, _lastCueMask;

        class Hold { public int Channel; public double StopAt; }
        static readonly List<Hold> _holds = new List<Hold>();

        static DrumsPlayer _player;
        static DrumsEngine _engine;
        static GameManager _gm;
        static bool _fiveLane, _pro;
        static readonly List<Event> _events = new List<Event>();
        static readonly List<int> _order = new List<int>();
        static int _ei;
        static double _lastTime = double.NaN, _lastCueTime = -999, _lastSpark = -999;
        static double _builtLead = -1, _builtSpeed = -1;
        static float _levelAt = -1, _missAt = -1;

        static int _hits, _misses, _overhits;
        static int _overEarly, _overLate;
        static double _overEarlySum, _overLateSum;
        static double _offsetSum, _absOffsetSum;
        static readonly int[] _overhitsByPad = new int[10];
        static string _songName, _keyName;
        static object _notesRef;

        public static void Reset()
        {
            LogSummary();
            Silence();
            Detach();
            _gm = null;
            _player = null;
            _lanePan = null;
            _events.Clear(); _order.Clear();
            _builtLead = _builtSpeed = -1;
            _ei = 0;
            _lastTime = double.NaN;
            _levelAt = -1;
        }

        public static void Silence()
        {
            _lastTime = double.NaN;
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
                _engine.OnPadHit -= OnPad;
                _engine.OnOverhit -= OnOverhit;
                _engine.OnStarPowerPhraseHit -= OnPhraseHit;
                _engine.OnStarPowerPhraseMissed -= OnPhraseMissed;
                _engine.OnStarPowerStatus -= StarPowerSpeech.OnStatus;
                _engine.OnStarPowerReady -= StarPowerSpeech.OnReady;
            }
            catch { }
            _engine = null;
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
                    var p = bp as DrumsPlayer;
                    if (p == null || p.Player == null || p.Player.Profile.IsBot != (pass == 1) || SoundOwner.Skip(p.Player)) continue;
                    var notesProp = typeof(TrackPlayer<DrumsEngine, DrumNote>).GetProperty("Notes", BF);
                    var notes = notesProp != null ? notesProp.GetValue(p, null) as List<DrumNote> : null;
                    if (notes == null || notes.Count == 0) { humanLoading |= pass == 0; continue; }
                    _player = p;
                    var inst = p.Player.Profile.CurrentInstrument;
                    _fiveLane = inst == Instrument.FiveLaneDrums;
                    _pro = inst == Instrument.ProDrums;
                    _keyName = HighwaySound.ApplyKey(_gm.Chart);
                    BuildLanePans();
                    Wire(notes);
                    _hits = _misses = _overhits = 0; _offsetSum = _absOffsetSum = 0;
                    _overEarly = _overLate = 0; _overEarlySum = _overLateSum = 0;
                    Array.Clear(_overhitsByPad, 0, _overhitsByPad.Length);
                    try { _songName = YARG.GlobalVariables.State.CurrentSong.Name + " (" + inst + ", " + p.Player.Profile.CurrentDifficulty + ")"; } catch { _songName = "?"; }
                    MelonLogger.Msg("DrumSound: " + _events.Count + " notes for " + p.Player.Profile.Name + ", " + inst + ", " + _keyName);
                    return true;
                }
            }
            catch (NullReferenceException) { }
            catch (Exception ex) { ModLog.Warn("DrumSound.Attach", ex); }
            return false;
        }

        static void Wire(List<DrumNote> notes)
        {
            _notesRef = notes;
            Build(notes);
            Detach();
            _engine = _player.Engine;
            if (_engine != null)
            {
                _engine.OnNoteHit += OnHit;
                _engine.OnNoteMissed += OnMiss;
                _engine.OnPadHit += OnPad;
                _engine.OnOverhit += OnOverhit;
                _engine.OnStarPowerPhraseHit += OnPhraseHit;
                _engine.OnStarPowerPhraseMissed += OnPhraseMissed;
                if (!_player.Player.Profile.IsBot) _engine.OnStarPowerStatus += StarPowerSpeech.OnStatus;
                if (!_player.Player.Profile.IsBot) _engine.OnStarPowerReady += StarPowerSpeech.OnReady;
            }
        }

        static void CheckPracticeSection()
        {
            try
            {
                var notesProp = typeof(TrackPlayer<DrumsEngine, DrumNote>).GetProperty("Notes", BF);
                var notes = notesProp != null ? notesProp.GetValue(_player, null) as List<DrumNote> : null;
                if (notes == null || (ReferenceEquals(notes, _notesRef) && ReferenceEquals(_player.Engine, _engine))) return;
                Wire(notes);
                MelonLogger.Msg("DrumSound: practice section changed, " + _events.Count + " notes");
            }
            catch (Exception ex) { ModLog.Warn("DrumSound.CheckPracticeSection", ex); }
        }

        static string SoundOf(int pad) => SoundOf(pad, _fiveLane);
        static float[] _lanePan;
        static float PanOf(int pad) => _lanePan != null && pad > 0 && pad < _lanePan.Length && !float.IsNaN(_lanePan[pad]) ? _lanePan[pad] : PanOf(pad, _fiveLane);

        static void BuildLanePans()
        {
            _lanePan = null;
            try
            {
                var m = _player.GetType().GetMethod("GetHighwayOrderingInfo", BF);
                if (m == null) return;
                int[] pads = _fiveLane ? new[] { 1, 2, 3, 4, 5 } : _pro ? new[] { 1, 2, 3, 4, 5, 6, 7 } : new[] { 1, 2, 3, 4 };
                var pos = new Dictionary<int, int>();
                var distinct = new List<int>();
                foreach (var pad in pads)
                {
                    var info = m.Invoke(_player, new object[] { pad });
                    int p = (int) info.GetType().GetProperty("Position").GetValue(info, null);
                    // YARG answers -1 for a pad that has no lane on this highway.
                    if (p < 0) continue;
                    pos[pad] = p;
                    if (!distinct.Contains(p)) distinct.Add(p);
                }
                // Fewer than two lanes would put every pad in the centre: keep the fixed pans instead.
                if (distinct.Count < 2)
                {
                    MelonLogger.Msg("DrumSound: highway gave " + distinct.Count + " lane positions, using the fixed pans");
                    return;
                }
                distinct.Sort();
                var pans = new float[10];
                for (int i = 0; i < pans.Length; i++) pans[i] = float.NaN;
                foreach (var kv in pos)
                {
                    int rank = distinct.IndexOf(kv.Value);
                    pans[kv.Key] = distinct.Count > 1 ? -0.8f + 1.6f * rank / (distinct.Count - 1) : 0f;
                }
                _lanePan = pans;
            }
            catch (Exception ex) { ModLog.Warn("DrumSound.BuildLanePans", ex); }
        }
        static int PadOfAction(DrumsAction a) => PadOfAction(a, _fiveLane, _pro);

        internal static string SoundOf(int pad, bool fiveLane)
        {
            if (pad == 0) return "kick";
            if (fiveLane)
            {
                switch (pad)
                {
                    case 1: return "padred";
                    case 2: return "cymyellow";
                    case 3: return "padblue";
                    case 4: return "cymgreen";
                    case 5: return "padgreen5";
                    default: return "padblue";
                }
            }
            switch (pad)
            {
                case 1: return "padred";
                case 2: return "padyellow";
                case 3: return "padblue";
                case 4: return "padgreen";
                case 5: return "cymyellow";
                case 6: return "cymblue";
                case 7: return "cymgreen";
                default: return "padblue";
            }
        }

        internal static float PanOf(int pad, bool fiveLane)
        {
            if (pad == 0 || pad == 9) return 0f;
            if (fiveLane)
            {
                switch (pad) { case 1: return -0.8f; case 2: return -0.4f; case 3: return 0f; case 4: return 0.4f; default: return 0.8f; }
            }
            switch (pad)
            {
                case 1: return -0.75f;
                case 2: case 5: return -0.25f;
                case 3: case 6: return 0.25f;
                default: return 0.75f;
            }
        }

        internal static int PadOfAction(DrumsAction a, bool fiveLane, bool pro)
        {
            if (a == DrumsAction.Kick) return 0;
            int n = (int) a;
            if (fiveLane)
            {
                switch (n) { case 0: return 1; case 4: return 2; case 2: return 3; case 5: return 4; case 3: return 5; default: return 3; }
            }
            if (n <= 3) return n + 1;
            if (n <= 6) return pro ? n + 1 : n - 2;
            return 3;
        }

        static void Build(List<DrumNote> notes)
        {
            _events.Clear(); _lanes.Clear(); _fills.Clear();
            Event prev = null;
            Lane lane = null;
            foreach (var n in notes)
            {
                var e = new Event { Time = n.Time, Star = n.IsStarPower, StarStart = n.IsStarPowerStart, Call = ListenRepeat.CallTime(n) };
                foreach (var c in n.AllNotes)
                {
                    if (c.IsLane && !e.HasCall)
                    {
                        if (c.IsLaneStart || lane == null)
                        {
                            if (lane != null) _lanes.Add(lane);
                            lane = new Lane { Start = c.Time, Swell = c.IsTrill };
                        }
                        if (!lane.Pads.Contains(c.Pad) && lane.Pads.Count < 2) lane.Pads.Add(c.Pad);
                        lane.End = c.Time;
                        if (c.IsLaneEnd) { _lanes.Add(lane); lane = null; }
                        continue;
                    }
                    if (c.IsStarPowerActivator) e.Activator = true;
                    if (e.Pads.Contains(c.Pad)) continue;
                    e.Pads.Add(c.Pad);
                    // Ghost: soft with a whisper. Accent: loud with a crack (user, 2026-11-14: tell them apart clearly).
                    e.Volumes.Add(c.IsGhost ? 0.35f : c.IsAccent ? 1f : 0.75f);
                    e.Marks.Add(c.IsGhost ? 1 : c.IsAccent ? 2 : 0);
                    if (c.Pad >= 0 && c.Pad < 30) e.Mask |= 1 << c.Pad;
                }
                if (e.Pads.Count == 0) continue;
                e.Gap = prev != null ? e.Time - prev.Time : double.PositiveInfinity;
                _events.Add(e);
                prev = e;
            }
            if (lane != null) _lanes.Add(lane);
            try
            {
                var trackProp = typeof(TrackPlayer<DrumsEngine, DrumNote>).GetProperty("NoteTrack", BF);
                var track = trackProp != null ? trackProp.GetValue(_player, null) as InstrumentDifficulty<DrumNote> : null;
                if (track != null)
                    foreach (var ph in track.Phrases)
                        if (ph.Type == PhraseType.DrumFill) _fills.Add(new Fill { Start = ph.Time, End = ph.TimeEnd });
                _fills.Sort((a, b) => a.Start.CompareTo(b.Start));
            }
            catch (Exception ex) { ModLog.Warn("DrumSound fills", ex); }
            // Every drum note plays its own pads (user, 2026-11-14): no held runs and no "same again" tick, so a
            // repeating rhythm is heard as it is; busy on Expert, which is the part to learn.
            Schedule();
        }

        static void Schedule()
        {
            double lead = ModSettings.DrumLookAheadSeconds;
            double speed = SongSpeed();
            foreach (var e in _events) e.Cue = e.HasCall ? e.Call : e.Time - lead * speed;
            _order.Clear();
            for (int i = 0; i < _events.Count; i++) _order.Add(i);
            _order.Sort((a, b) => { int c = _events[a].Cue.CompareTo(_events[b].Cue); return c != 0 ? c : a.CompareTo(b); });
            foreach (var l in _lanes) l.Cue = l.Start - lead * speed;
            foreach (var f in _fills)
            {
                f.Cue = f.Start - lead * speed;
                f.EndCue = f.End - lead * speed;
                foreach (var e in _events)
                {
                    if (!e.Activator || Math.Abs(e.Time - f.End) > 0.01 || !e.HasCall) continue;
                    double shift = e.Time - e.Call;
                    f.Cue = f.Start - shift;
                    f.EndCue = e.Call;
                    break;
                }
            }
            _li = _fi = 0;
            _builtLead = lead;
            _builtSpeed = speed;
            _ei = 0;
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
                if (ModSettings.DrumLookAheadSeconds != _builtLead || SongSpeed() != _builtSpeed) { Silence(); Schedule(); }
                if (double.IsNaN(_lastTime) || now < _lastTime - 0.05 || now > _lastTime + 0.5) Seek(now);
                _lastTime = now;

                float real = Time.unscaledTime;
                if (_levelAt > 0 && real >= _levelAt)
                {
                    _levelAt = -1;
                    try { CueBank.PlayLevel((int) Math.Round(_engine.GetStarPowerBarAmount() * 4), 0.8f); } catch { }
                }

                for (int h = _holds.Count - 1; h >= 0; h--)
                    if (_holds[h].StopAt <= now) { CuePlayer.FadeOut(_holds[h].Channel, 25); _holds.RemoveAt(h); }

                double lead = _builtLead * _builtSpeed;
                while (_ei < _order.Count && _events[_order[_ei]].Cue <= now)
                {
                    var e = _events[_order[_ei]];
                    if (e.Time > now) Fire(e, lead);
                    _ei++;
                }
                while (_li < _lanes.Count && _lanes[_li].Cue <= now)
                {
                    var l = _lanes[_li++];
                    if (l.End > now) StartLane(l);
                }
                while (_fi < _fills.Count && _fills[_fi].Cue <= now)
                {
                    var f = _fills[_fi++];
                    if (f.EndCue > now && CanActivate()) CueBank.PlayFill((f.EndCue - now) / (_builtSpeed > 0 ? _builtSpeed : 1.0), 0.7f);
                }

            }
            catch (Exception ex) { ModLog.Warn("DrumSound.Tick", ex); }
        }

        static void Seek(double now)
        {
            Silence();
            _ei = 0;
            while (_ei < _order.Count && _events[_order[_ei]].Cue <= now) _ei++;
            _li = 0;
            while (_li < _lanes.Count && _lanes[_li].End <= now) _li++;
            _fi = 0;
            while (_fi < _fills.Count && _fills[_fi].Cue <= now) _fi++;
            _lastCueMask = 0;
            _lastCueTime = -999;
            _lastSpark = -999;
        }

        static bool CanActivate()
        {
            try { return _engine != null && _engine.CanStarPowerActivate; } catch { return false; }
        }

        static void StartLane(Lane l)
        {
            for (int i = 0; i < l.Pads.Count; i++)
            {
                int ch = CueBank.StartRoll(SoundOf(l.Pads[i]), l.Swell, i == 1, PanOf(l.Pads[i]), 0.7f);
                if (ch != 0) _holds.Add(new Hold { Channel = ch, StopAt = l.End });
            }
        }

        static void Fire(Event e, double lead)
        {
            bool active = false;
            try { active = _engine.EngineStats.IsStarPowerActive; } catch { }
            if (e.Activator && CanActivate()) CueBank.Play("drumactivator", 0.8f);
            bool soon = e.Time - _lastCueTime < MinGap;
            if (!soon || (e.Mask & ~_lastCueMask) != 0)
            {
                _lastCueMask = soon ? _lastCueMask | e.Mask : e.Mask;
                for (int i = 0; i < e.Pads.Count; i++)
                {
                    float pan = PanOf(e.Pads[i]);
                    CueBank.PlayDrum(SoundOf(e.Pads[i]), pan, e.Volumes[i]);
                    if (e.Marks[i] != 0) CueBank.PlayDrumMark(e.Marks[i] == 1, pan, 0.9f);
                }
                _lastCueTime = e.Time;
            }
            if (e.StarStart && HighwaySound.PhraseMakesReady(_engine, active)) { _lastSpark = e.Time; CueBank.Play("spstart", 0.8f); }
            else if (e.Star && e.Time - _lastSpark >= 0.15) { _lastSpark = e.Time; CueBank.Play("spnote", 0.8f); }
        }

        static void LogSummary()
        {
            if ((object) _player == null || _hits + _misses + _overhits == 0) return;
            string timing = _hits > 0
                ? ", hits on average " + Math.Round(Math.Abs(_offsetSum / _hits) * 1000) + " ms " + (_offsetSum >= 0 ? "late" : "early")
                  + ", typically " + Math.Round(_absOffsetSum / _hits * 1000) + " ms off"
                : "";
            var wrong = new List<string>();
            for (int pad = 0; pad < _overhitsByPad.Length; pad++)
                if (_overhitsByPad[pad] > 0) wrong.Add(SoundOf(pad) + " " + _overhitsByPad[pad]);
            string near = _overEarly + _overLate > 0
                ? ", overhits near a note: " + _overEarly + " early (average " + Math.Round(_overEarly > 0 ? _overEarlySum / _overEarly * 1000 : 0) + " ms), "
                  + _overLate + " late (average " + Math.Round(_overLate > 0 ? _overLateSum / _overLate * 1000 : 0) + " ms)"
                : "";
            MelonLogger.Msg("DrumSound summary, " + _songName + ": " + _hits + " hit, " + _misses + " missed, " + _overhits + " overhits" + near
                + (wrong.Count > 0 ? " (" + string.Join(", ", wrong.ToArray()) + ")" : "") + timing
                + ", " + _lanes.Count + " rolls and swells, " + _fills.Count + " fills"
                + ", look-ahead " + Math.Round(ModSettings.DrumLookAheadSeconds * 1000) + " ms, " + _keyName);
        }

        static void OnHit(int index, DrumNote note)
        {
            if (_gm == null || _gm.Paused) return;
            try
            {
                double off = GameplayContext.JudgeTime(_gm, _player) - note.Time;
                _hits++; _offsetSum += off; _absOffsetSum += Math.Abs(off);
                CueBank.PlayDrumHit(SoundOf(note.Pad), PanOf(note.Pad), 0.9f);
            }
            catch { }
        }

        static void OnMiss(int index, DrumNote note)
        {
            if (_gm == null || _gm.Paused) return;
            _misses++;
            float now = Time.unscaledTime;
            if (now - _missAt < 0.06f) return;
            _missAt = now;
            CueBank.Play("miss", 0.9f);
        }

        static void OnPad(DrumsAction action, bool noteWasHit, bool bonus, bool overhitInLane, DrumNoteType type, float velocity)
        {
            // Only remembered here: YARG forgives extra strokes inside a roll, a swell or a big rock ending
            // (OnPadHit fires, Overhit() then returns with no penalty). OnOverhit, fired right after for a
            // real overhit, plays the buzz.
            _overPad = noteWasHit ? -1 : PadOfAction(action);
        }

        static int _overPad = -1;

        static void OverhitSound()
        {
            int pad = _overPad;
            _overPad = -1;
            if (pad < 0) return;
            if (pad < _overhitsByPad.Length) _overhitsByPad[pad]++;
            try
            {
                double t = GameplayContext.JudgeTime(_gm, _player), best = double.MaxValue;
                foreach (var e in _events) if (Math.Abs(e.Time - t) < Math.Abs(best)) best = t - e.Time;
                if (best != double.MaxValue && Math.Abs(best) < 0.5)
                {
                    if (best < 0) { _overEarly++; _overEarlySum += -best; } else { _overLate++; _overLateSum += best; }
                }
            }
            catch { }
            CueBank.Play("overstrum", 0.5f);
            CueBank.PlayDrum(SoundOf(pad), PanOf(pad), 0.35f);
        }

        static void OnOverhit()
        {
            if (_gm == null || _gm.Paused) { _overPad = -1; return; }
            _overhits++;
            OverhitSound();
        }

        static void OnPhraseHit(DrumNote note)
        {
            if (_gm == null || _gm.Paused) return;
            _levelAt = Time.unscaledTime + 0.5f;
        }

        static void OnPhraseMissed(DrumNote note)
        {
            if (_gm == null || _gm.Paused) return;
            CueBank.Play("splost", 0.6f);
        }
    }
}
