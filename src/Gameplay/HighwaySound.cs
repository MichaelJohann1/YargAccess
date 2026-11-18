using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Keys;
using YARG.Gameplay;
using YARG.Assets.Script.Gameplay.Player;
using YARG.Gameplay.Player;
using YargAccess.Audio;

namespace YargAccess
{
    public static class HighwaySound
    {
        static bool _beginner;

        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        const double MinHold = 0.25;
        const double MinClickGap = 0.07;

        class Event
        {
            public double Time;
            public double End;
            public double RunEnd;
            public int Mask;
            public CueBank.Kind Kind;
            public bool Star;
            public bool StarStart;
            public double Safe;
            public double RunSafe;
            public double Cue;
            public bool Repeat;
            public bool Open;
            public double Gap;
            public double Call = double.NaN, Response = double.NaN;
            public bool HasCall => !double.IsNaN(Call);
        }

        class Hold { public int Channel; public double StopAt; }

        static TrackPlayer _player;
        static GuitarEngine _engine;
        static FiveLaneKeysEngine _keysEngine;
        static BaseEngine Engine => (BaseEngine) _engine ?? _keysEngine;
        static bool IsKeys => _player is FiveLaneKeysPlayer;
        static GameManager _gm;
        static readonly List<Event> _events = new List<Event>();
        static readonly List<double> _releases = new List<double>();
        static readonly List<Hold> _holds = new List<Hold>();
        static int _ei, _ci, _ri;
        static double _lastTime = double.NaN;
        static readonly List<int> _lastCue = new List<int>();
        static double _lastCueTime = -999;
        static double _lastSpark = -999;

        static float _levelAt = -1, _dropAt = -1;

        static int _hits, _misses, _overstrums;
        static double _offsetSum, _absOffsetSum;
        static string _songName, _keyName;
        static object _notesRef;

        static readonly List<int> _order = new List<int>();
        static double _builtLead = -1, _builtSpeed = -1;

        public static void Reset()
        {
            CueBank.Mirror = false;
            LogSummary();
            Silence();
            Detach();
            _gm = null;
            _player = null;
            _events.Clear(); _releases.Clear(); _order.Clear();
            _builtLead = _builtSpeed = -1;
            _ei = _ci = _ri = 0;
            _lastTime = double.NaN;
            _lastCue.Clear();
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
            try
            {
                if (_engine != null)
                {
                    _engine.OnNoteHit -= OnHit;
                    _engine.OnNoteMissed -= OnMiss;
                    _engine.OnOverstrum -= OnOverstrum;
                    _engine.OnStarPowerPhraseHit -= OnPhraseHit;
                    _engine.OnStarPowerPhraseMissed -= OnPhraseMissed;
                    _engine.OnStarPowerStatus -= StarPowerSpeech.OnStatus;
                    _engine.OnStarPowerReady -= StarPowerSpeech.OnReady;
                    _engine.OnSustainEnd -= OnSustainEnd;
                }
                if (_keysEngine != null)
                {
                    _keysEngine.OnNoteHit -= OnHit;
                    _keysEngine.OnNoteMissed -= OnMiss;
                    _keysEngine.OnOverhit -= OnOverhit;
                    _keysEngine.OnStarPowerPhraseHit -= OnPhraseHit;
                    _keysEngine.OnStarPowerPhraseMissed -= OnPhraseMissed;
                    _keysEngine.OnStarPowerStatus -= StarPowerSpeech.OnStatus;
                    _keysEngine.OnStarPowerReady -= StarPowerSpeech.OnReady;
                    _keysEngine.OnSustainEnd -= OnSustainEnd;
                }
            }
            catch { }
            _engine = null;
            _keysEngine = null;
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
                    var p = bp as TrackPlayer;
                    if (!(p is FiveFretGuitarPlayer || p is FiveLaneKeysPlayer)) continue;
                    if (p.Player == null || p.Player.Profile.IsBot != (pass == 1) || SoundOwner.Skip(p.Player)) continue;
                    var notes = NotesOf(p);
                    if (notes == null || notes.Count == 0) { humanLoading |= pass == 0; continue; }
                    _player = p;
                    _beginner = p.Player.Profile.CurrentDifficulty == YARG.Core.Difficulty.Beginner;
                    CueBank.Mirror = p.Player.Profile.LeftyFlip;
                    SetKey();
                    Wire(notes);
                    _hits = _misses = _overstrums = 0; _offsetSum = _absOffsetSum = 0;
                    try { _songName = YARG.GlobalVariables.State.CurrentSong.Name + " (" + p.Player.Profile.CurrentDifficulty + ")"; } catch { _songName = "?"; }
                    MelonLogger.Msg("HighwaySound: " + _events.Count + " notes for " + p.Player.Profile.Name + ", " + _keyName);
                    return true;
                }
            }
            catch (NullReferenceException) { }
            catch (Exception ex) { ModLog.Warn("HighwaySound.Attach", ex); }
            return false;
        }

        static List<GuitarNote> NotesOf(TrackPlayer p)
        {
            var type = p is FiveLaneKeysPlayer ? typeof(TrackPlayer<FiveLaneKeysEngine, GuitarNote>) : typeof(TrackPlayer<GuitarEngine, GuitarNote>);
            var notesProp = type.GetProperty("Notes", BF);
            return notesProp != null ? notesProp.GetValue(p, null) as List<GuitarNote> : null;
        }

        static BaseEngine EngineOf(TrackPlayer p)
        {
            var g = p as FiveFretGuitarPlayer;
            if (g != null) return g.Engine;
            var k = p as FiveLaneKeysPlayer;
            return k != null ? k.Engine : null;
        }

        static void Wire(List<GuitarNote> notes)
        {
            _notesRef = notes;
            Build(notes);
            Detach();
            var keysPlayer = _player as FiveLaneKeysPlayer;
            if (keysPlayer != null)
            {
                _keysEngine = keysPlayer.Engine;
                if (_keysEngine != null)
                {
                    _keysEngine.OnNoteHit += OnHit;
                    _keysEngine.OnNoteMissed += OnMiss;
                    _keysEngine.OnOverhit += OnOverhit;
                    _keysEngine.OnStarPowerPhraseHit += OnPhraseHit;
                    _keysEngine.OnStarPowerPhraseMissed += OnPhraseMissed;
                    if (!_player.Player.Profile.IsBot) _keysEngine.OnStarPowerStatus += StarPowerSpeech.OnStatus;
                    if (!_player.Player.Profile.IsBot) _keysEngine.OnStarPowerReady += StarPowerSpeech.OnReady;
                    _keysEngine.OnSustainEnd += OnSustainEnd;
                }
                return;
            }
            _engine = ((FiveFretGuitarPlayer) _player).Engine;
            if (_engine != null)
            {
                _engine.OnNoteHit += OnHit;
                _engine.OnNoteMissed += OnMiss;
                _engine.OnOverstrum += OnOverstrum;
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
                if (notes == null || (ReferenceEquals(notes, _notesRef) && ReferenceEquals(EngineOf(_player), Engine))) return;
                Silence();
                Wire(notes);
                MelonLogger.Msg("HighwaySound: practice section changed, " + _events.Count + " notes");
            }
            catch (Exception ex) { ModLog.Warn("HighwaySound.CheckPracticeSection", ex); }
        }

        static void SetKey() { _keyName = ApplyKey(_gm.Chart); }

        internal static string ApplyKey(SongChart chart)
        {
            KeyDetect.Result key = null;
            try { key = KeyDetect.Detect(chart); } catch { }
            if (key != null)
            {
                CueBank.SetKey(key.Tonic, key.Minor);
                return "key of " + key.Name + " (from " + key.Source + ")";
            }
            CueBank.SetKey(9, true);
            return "key unknown, A minor pentatonic";
        }

        static void Build(List<GuitarNote> notes)
        {
            _events.Clear(); _releases.Clear();
            var sync = _gm.Chart.SyncTrack;
            uint burst = sync.Resolution / 4;
            int prevTop = -1;
            foreach (var n in notes)
            {
                if (n.IsBigRockEnding) continue;
                var e = new Event { Time = n.Time, End = n.Time, Star = n.IsStarPower, StarStart = n.IsStarPowerStart,
                                    Call = ListenRepeat.CallTime(n), Response = ListenRepeat.ResponseStart(n) };
                uint tickEnd = n.Tick;
                foreach (var c in n.AllNotes)
                {
                    int f = c.Fret;
                    if (f == (int) FiveFretGuitarFret.Open) { e.Mask |= 0x3E; e.Open = true; }
                    else if (f == (int) FiveFretGuitarFret.Wildcard) e.Mask |= 1 << 3;
                    else if (f >= 1 && f <= 5) e.Mask |= 1 << f;
                    if (c.TimeEnd > e.End) { e.End = c.TimeEnd; tickEnd = c.TickEnd; }
                }
                if (e.Mask == 0) continue;
                e.Safe = tickEnd - n.Tick >= burst ? Math.Max(e.Time, sync.TickToTime(tickEnd - burst)) : e.End;
                int topFret = e.Open ? 0 : HighestFret(e.Mask);
                if (IsKeys) e.Kind = CueBank.Kind.Strum;
                else if (n.IsTap) e.Kind = CueBank.Kind.Tap;
                else if (n.IsHopo) e.Kind = prevTop >= 0 && topFret < prevTop ? CueBank.Kind.PullOff : CueBank.Kind.HammerOn;
                else e.Kind = CueBank.Kind.Strum;
                prevTop = topFret;
                e.RunEnd = e.End;
                e.RunSafe = e.Safe;
                _events.Add(e);
                if (e.End - e.Time >= MinHold) _releases.Add(e.Safe);
            }
            _releases.Sort();

            var beats = Beatlines();
            double prevEnd = double.NegativeInfinity;
            int runStart = -1;
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                e.Gap = i > 0 ? e.Time - _events[i - 1].Time : double.PositiveInfinity;
                double rest = Math.Max(BeatLength(beats, e.Time), 0.6);
                if (i > 0 && e.Time - prevEnd < rest)
                    e.Repeat = e.Mask == _events[i - 1].Mask && e.Open == _events[i - 1].Open && (e.Kind == CueBank.Kind.Strum) == (_events[i - 1].Kind == CueBank.Kind.Strum);
                if (e.Repeat && runStart >= 0)
                {
                    _events[runStart].RunEnd = Math.Max(_events[runStart].RunEnd, e.End);
                    _events[runStart].RunSafe = Math.Max(_events[runStart].RunSafe, e.Safe);
                }
                else runStart = i;
                prevEnd = Math.Max(prevEnd, e.End);
            }
            Schedule();
        }

        static void Schedule()
        {
            double lead = ModSettings.LookAheadSeconds;
            double speed = SongSpeed();
            foreach (var e in _events) e.Cue = e.HasCall ? e.Call : e.Time - lead * speed;
            _order.Clear();
            for (int i = 0; i < _events.Count; i++) _order.Add(i);
            _order.Sort((a, b) =>
            {
                int c = _events[a].Cue.CompareTo(_events[b].Cue);
                return c != 0 ? c : a.CompareTo(b);
            });
            CueBank.EnsureRiser(lead);
            _builtLead = lead;
            _builtSpeed = speed;
            _ei = _ci = _ri = 0;
            _lastTime = double.NaN;
        }

        static int HighestFret(int mask)
        {
            for (int f = 5; f >= 1; f--) if ((mask & (1 << f)) != 0) return f;
            return 0;
        }

        static double SongSpeed()
        {
            try { return _gm != null && _gm.SongSpeed > 0 ? _gm.SongSpeed : 1.0; } catch { return 1.0; }
        }

        static List<Beatline> Beatlines()
        {
            try
            {
                var f = typeof(TrackPlayer).GetField("Beatlines", BF);
                var list = f != null ? f.GetValue(_player) as List<Beatline> : null;
                if (list != null && list.Count > 0) return list;
            }
            catch { }
            return null;
        }

        static double BeatLength(List<Beatline> beats, double t)
        {
            if (beats == null || beats.Count < 2) return 0.5;
            int lo = 0, hi = beats.Count - 1;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (beats[mid].Time <= t) lo = mid; else hi = mid - 1; }
            int a = Math.Min(lo, beats.Count - 2);
            return Math.Max(0.05, beats[a + 1].Time - beats[a].Time);
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
                    try { CueBank.PlayLevel((int) Math.Round(Engine.GetStarPowerBarAmount() * 4), 0.8f); } catch { }
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
                        if (ModSettings.CueRepeats.Value || !e.Repeat || e.HasCall) PlayEvent(i, lead);
                        else if (e.Star) Sparkle(e);
                        if (!e.HasCall && e.Gap >= lead + 0.02) CueBank.PlayRiser(_builtLead, 0.8f);
                    }
                    _ei++;
                }
                while (_ci < _events.Count && _events[_ci].Time <= now)
                {
                    var e = _events[_ci];
                    if (e.Time > now - 0.05 && (e.HasCall || (e.Gap < lead + 0.02 && e.Gap >= MinClickGap)))
                        CueBank.Play("click", 0.7f);
                    _ci++;
                }
                while (_ri < _releases.Count && _releases[_ri] <= now)
                {
                    if (_releases[_ri] > now - 0.05) CueBank.Play("release", 0.5f);
                    _ri++;
                }
            }
            catch (Exception ex) { ModLog.Warn("HighwaySound.Tick", ex); }
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

        static void PlayEvent(int i, double lead)
        {
            var e = _events[i];
            double gap = e.Time - _lastCueTime;
            if (gap < 0.044) return;
            if (gap < 0.11) foreach (var ch in _lastCue) CuePlayer.FadeOut(ch, 5);
            _lastCue.Clear();

            // Beginner (user, 2026-11-14): a run of one fret does not hold its tone through the run;
            // each later note gets only the hit bar sound.
            bool each = ModSettings.CueRepeats.Value || e.HasCall || _beginner;
            double holdEnd = each ? e.End : e.RunEnd;
            double holdStop = each ? e.Safe : e.RunSafe;
            if (e.HasCall) holdStop = Math.Min(holdStop - (e.Time - e.Call), e.Response - 0.05);
            bool held = holdEnd - e.Time >= MinHold && holdStop > (e.HasCall ? e.Call : e.Time);
            for (int f = 0; f <= 5; f++)
            {
                if (f == 0 ? !e.Open : (e.Open || (e.Mask & (1 << f)) == 0)) continue;
                int ch = CueBank.PlayNote(f, e.Kind);
                if (ch != 0) _lastCue.Add(ch);
                if (held)
                {
                    int hc = CueBank.StartHold(f);
                    if (hc != 0) _holds.Add(new Hold { Channel = hc, StopAt = holdStop });
                }
            }
            if (e.Star) Sparkle(e);
            if (!e.Open && (e.Mask & (e.Mask - 1)) != 0) CueBank.Play("chord", 0.8f);
            _lastCueTime = e.Time;
        }

        static void Sparkle(Event e)
        {
            bool active = false;
            try { active = Engine.BaseStats.IsStarPowerActive; } catch { }
            if (e.StarStart && PhraseMakesReady(Engine, active)) { _lastSpark = e.Time; CueBank.Play("spstart", 0.8f); return; }
            if (e.Time - _lastSpark < 0.15) return;
            _lastSpark = e.Time;
            CueBank.Play("spnote", 0.8f);
        }

        internal static bool PhraseMakesReady(YARG.Core.Engine.BaseEngine engine, bool active)
        {
            try
            {
                if (engine == null || active || engine.CanStarPowerActivate) return false;
                return engine.GetStarPowerBarAmount() + 0.25 >= 0.5 - 1e-6;
            }
            catch { return false; }
        }

        static void LogSummary()
        {
            if ((object) _player == null || _hits + _misses + _overstrums == 0) return;
            string timing = _hits > 0
                ? ", hits on average " + Math.Round(Math.Abs(_offsetSum / _hits) * 1000) + " ms " + (_offsetSum >= 0 ? "late" : "early")
                  + ", typically " + Math.Round(_absOffsetSum / _hits * 1000) + " ms off"
                : "";
            MelonLogger.Msg("HighwaySound summary, " + _songName + ": " + _hits + " hit, " + _misses + " missed, "
                + _overstrums + " overstrums" + timing + ", look-ahead " + Math.Round(ModSettings.LookAheadSeconds * 1000) + " ms, repeats "
                + (ModSettings.CueRepeats.Value ? "cued" : "skipped") + ", " + _keyName);
        }

        static void OnHit(int index, GuitarNote parent)
        {
            if (!ModSettings.NoteSounds.Value || _gm == null || _gm.Paused) return;
            try
            {
                _dropAt = -1;
                double off = GameplayContext.JudgeTime(_gm, _player) - parent.Time;
                _hits++; _offsetSum += off; _absOffsetSum += Math.Abs(off);
                foreach (var c in parent.AllNotes)
                {
                    if (c.Fret == (int) FiveFretGuitarFret.Open) CueBank.PlayHit(0, 0.9f);
                    else if (c.Fret >= 1 && c.Fret <= 5) CueBank.PlayHit(c.Fret, 0.9f);
                    else CueBank.PlayHit(3, 0.9f);
                }
            }
            catch { }
        }

        static void OnMiss(int index, GuitarNote parent)
        {
            if (!ModSettings.NoteSounds.Value || _gm == null || _gm.Paused) return;
            _misses++;
            CueBank.Play("miss", 0.9f);
        }

        static void OnPhraseHit(GuitarNote note)
        {
            if (_gm == null || _gm.Paused) return;
            _levelAt = Time.unscaledTime + 0.5f;
        }

        static void OnPhraseMissed(GuitarNote note)
        {
            if (_gm == null || _gm.Paused) return;
            CueBank.Play("splost", 0.6f);
        }

        static void OnSustainEnd(GuitarNote note, double timeEnded, bool finished)
        {
            if (finished || _gm == null || _gm.Paused) return;
            _dropAt = Time.unscaledTime + 0.05f;
        }

        static void OnOverhit(int key) { OnOverstrum(); }

        static void OnOverstrum()
        {
            if (!ModSettings.NoteSounds.Value || _gm == null || _gm.Paused) return;
            _overstrums++;
            CueBank.Play("overstrum", 0.5f);
        }
    }
}
