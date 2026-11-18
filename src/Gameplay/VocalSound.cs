using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.Vocals;
using YARG.Gameplay;
using YARG.Gameplay.Player;
using YargAccess.Audio;

namespace YargAccess
{
    public static class VocalSound
    {
        const double GuideLead = 0.1;
        const double GuideRest = 0.3;
        const float SingHold = 0.2f;
        const float OffDelay = 0.12f, SweepEvery = 0.35f;

        class Note { public double Time, End; public float Pitch; public int Guide; public bool Cued, Star, StarStart; }

        static VocalsPlayer _player;
        static VocalsEngine _engine;
        static GameManager _gm;
        static readonly List<Note> _notes = new List<Note>();
        // Percussion notes: hit time, and the Listen and Repeat call time (NaN without one).
        static readonly List<KeyValuePair<double, double>> _perc = new List<KeyValuePair<double, double>>();

        static double PercCue(int i, double lead) => double.IsNaN(_perc[i].Value) ? _perc[i].Key - lead : _perc[i].Value;
        static readonly List<double> _talk = new List<double>();
        static int _gi, _ni, _pi, _ti;
        static double _lastTime = double.NaN;
        static float _lastSing = -1, _offSince = -1, _lastSweep = -1, _levelAt = -1;
        static double _lastSpark = -999;
        static int _shift;
        static string _songName;
        static int _sweeps, _phrases, _phrasesHit;

        public static void Reset()
        {
            if ((object) _player != null && _phrases > 0)
                MelonLogger.Msg("VocalSound summary, " + _songName + ": " + _phrasesHit + " of " + _phrases + " phrases hit, " + _sweeps + " off-pitch sweeps");
            Detach();
            _gm = null;
            _player = null;
            _notes.Clear(); _perc.Clear(); _talk.Clear();
            _gi = _ni = _pi = _ti = 0;
            _lastTime = double.NaN;
            _lastSing = _offSince = _lastSweep = _levelAt = -1;
            _lastSpark = -999;
            _sweeps = _phrases = _phrasesHit = 0;
        }

        static void Detach()
        {
            if (_engine == null) return;
            try
            {
                _engine.OnSing -= OnSing;
                _engine.OnPhraseHit -= OnPhraseHit;
                _engine.OnNoteHit -= OnNoteHit;
                _engine.OnNoteMissed -= OnNoteMissed;
                _engine.OnStarPowerPhraseHit -= OnStarPhraseHit;
                _engine.OnStarPowerPhraseMissed -= OnStarPhraseMissed;
                _engine.OnStarPowerStatus -= StarPowerSpeech.OnStatus;
                _engine.OnStarPowerReady -= StarPowerSpeech.OnReady;
            }
            catch { }
            _engine = null;
        }

        static float _retryAt;

        static bool Attach()
        {
            if (_player != null && _player)
            {
                if (!ReferenceEquals(_player.Engine, _engine)) Wire();
                return true;
            }
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
                    var p = bp as VocalsPlayer;
                    if (p == null || p.Player == null || p.Player.Profile.IsBot != (pass == 1) || SoundOwner.Skip(p.Player)) continue;
                    if (p.Engine == null) { humanLoading |= pass == 0; continue; }
                    var track = _gm.Chart.GetVocalsTrack(p.Player.Profile.CurrentInstrument);
                    int part = p.Player.Profile.HarmonyIndex;
                    if (track == null || part < 0 || part >= track.Parts.Count) continue;
                    _player = p;
                    // The hit sound and star power chimes follow the song's key, as in the other modes.
                    try { HighwaySound.ApplyKey(_gm.Chart); } catch { }
                    Build(track.Parts[part]);
                    Wire();
                    try { _songName = YARG.GlobalVariables.State.CurrentSong.Name + " (" + p.Player.Profile.CurrentInstrument + " part " + (part + 1) + ", " + p.Player.Profile.CurrentDifficulty + ")"; } catch { _songName = "?"; }
                    bool mic = false;
                    try { mic = p.Player.Bindings.Microphone != null; } catch { }
                    MelonLogger.Msg("VocalSound: " + _notes.Count + " pitched notes, " + _perc.Count + " percussion, " + _talk.Count + " spoken, shifted " + _shift + " semitones" + (mic ? "" : ", no microphone"));
                    return true;
                }
            }
            catch (NullReferenceException) { }
            catch (Exception ex) { ModLog.Warn("VocalSound.Attach", ex); }
            return false;
        }

        static void Wire()
        {
            Detach();
            _engine = _player.Engine;
            if (_engine != null)
            {
                _engine.OnSing += OnSing;
                _engine.OnPhraseHit += OnPhraseHit;
                _engine.OnNoteHit += OnNoteHit;
                _engine.OnNoteMissed += OnNoteMissed;
                _engine.OnStarPowerPhraseHit += OnStarPhraseHit;
                _engine.OnStarPowerPhraseMissed += OnStarPhraseMissed;
                if (!_player.Player.Profile.IsBot) _engine.OnStarPowerStatus += StarPowerSpeech.OnStatus;
                if (!_player.Player.Profile.IsBot) _engine.OnStarPowerReady += StarPowerSpeech.OnReady;
            }
        }

        static void Build(VocalsPart part)
        {
            _notes.Clear(); _perc.Clear(); _talk.Clear();
            double talkEnd = double.NegativeInfinity;
            bool lastSpoken = false;
            foreach (var ph in part.NotePhrases)
            {
                foreach (var n in ph.PhraseParentNote.ChildNotes)
                {
                    if (n.IsPercussion) { _perc.Add(new KeyValuePair<double, double>(n.Time, ListenRepeat.CallTime(n))); continue; }
                    if (!ph.IsLyric || ph.IsEmpty) continue;
                    if (n.IsNonPitched)
                    {
                        if (!lastSpoken || n.Time - talkEnd >= GuideRest) _talk.Add(n.Time);
                        talkEnd = n.TotalTimeEnd;
                    }
                    lastSpoken = n.IsNonPitched;
                }
                if (!ph.IsLyric || ph.IsEmpty) continue;
                bool first = true;
                foreach (var n in ph.PhraseParentNote.ChildNotes)
                    if (!n.IsPercussion && !n.IsNonPitched)
                    {
                        _notes.Add(new Note { Time = n.Time, End = n.TotalTimeEnd, Pitch = n.Pitch, Star = ph.IsStarPower, StarStart = ph.IsStarPower && first });
                        first = false;
                    }
            }
            _notes.Sort((a, b) => a.Time.CompareTo(b.Time));
            _perc.Sort((a, b) => a.Key.CompareTo(b.Key));
            _talk.Sort();

            _shift = 0;
            if (_notes.Count > 0)
            {
                var pitches = new List<float>();
                foreach (var n in _notes) pitches.Add(n.Pitch);
                pitches.Sort();
                _shift = 12 * (int) Math.Round((62 - pitches[pitches.Count / 2]) / 12.0);
            }
            double lastEnd = double.NegativeInfinity;
            int lastPitch = int.MinValue;
            foreach (var n in _notes)
            {
                n.Guide = (int) Math.Round(n.Pitch) + _shift;
                n.Cued = n.Guide != lastPitch || n.Time - lastEnd >= GuideRest;
                lastPitch = n.Guide;
                lastEnd = n.End;
            }
            CueBank.PrepareGuides(_notes.ConvertAll(n => n.Guide));
            _gi = _ni = _pi = _ti = 0;
            _lastTime = double.NaN;
        }

        public static void Tick()
        {
            if (!ModSettings.NoteSounds.Value) return;
            if (!Attach()) return;
            try
            {
                double now = GameplayContext.CueTime(_gm, _player);
                if (double.IsNaN(_lastTime) || now < _lastTime - 0.05 || now > _lastTime + 0.5) Seek(now);
                _lastTime = now;

                if (!CuePlayer.Ready || !CueBank.EnsureLoaded()) return;
                float real = Time.unscaledTime;
                if (_levelAt > 0 && real >= _levelAt)
                {
                    _levelAt = -1;
                    try { CueBank.PlayLevel((int) Math.Round(_engine.GetStarPowerBarAmount() * 4), 0.8f); } catch { }
                }
                double speed = SongSpeed();
                while (_gi < _notes.Count && _notes[_gi].Time - GuideLead * speed <= now)
                {
                    var n = _notes[_gi];
                    if (n.Time > now - 0.05)
                    {
                        if (n.Cued && ModSettings.PitchGuide.Value) CueBank.PlayGuide(n.Guide, 0.8f);
                        if (n.Star) Sparkle(n);
                    }
                    _gi++;
                }

                double percLead = ModSettings.DrumLookAheadSeconds * speed;
                while (_pi < _perc.Count && PercCue(_pi, percLead) <= now)
                {
                    if (_perc[_pi].Key > now - 0.05) CueBank.Play("vocalperc", 1f);
                    _pi++;
                }
                while (_ti < _talk.Count && _talk[_ti] - GuideLead * speed <= now)
                {
                    if (_talk[_ti] > now - 0.05 && ModSettings.PitchGuide.Value) CueBank.Play("vocaltalk", 0.8f);
                    _ti++;
                }

                if (ModSettings.PitchFeedback.Value) Feedback(GameplayContext.JudgeTime(_gm, _player));
            }
            catch (Exception ex) { ModLog.Warn("VocalSound.Tick", ex); }
        }

        static void Seek(double now)
        {
            _gi = 0;
            while (_gi < _notes.Count && _notes[_gi].Time - GuideLead * SongSpeed() <= now) _gi++;
            _ni = 0;
            _pi = 0;
            while (_pi < _perc.Count && PercCue(_pi, ModSettings.DrumLookAheadSeconds * SongSpeed()) <= now) _pi++;
            _ti = 0;
            while (_ti < _talk.Count && _talk[_ti] - GuideLead * SongSpeed() <= now) _ti++;
            _offSince = -1;
        }

        static void Feedback(double now)
        {
            float real = Time.unscaledTime;
            if (_lastSing < 0 || real - _lastSing > SingHold) { _offSince = -1; return; }
            while (_ni < _notes.Count && _notes[_ni].End < now) _ni++;
            if (_ni >= _notes.Count || _notes[_ni].Time > now) { _offSince = -1; return; }
            float window;
            try { window = _player.EngineParams.PitchWindow; } catch { return; }
            float target = _notes[_ni].Pitch;
            float d = _engine.PitchSang - target;
            d = ((d % 12f) + 18f) % 12f - 6f;
            if (Math.Abs(d) <= window) { _offSince = -1; return; }
            if (_offSince < 0) { _offSince = real; return; }
            if (real - _offSince < OffDelay || real - _lastSweep < SweepEvery) return;
            _lastSweep = real;
            _sweeps++;
            CueBank.Play(d > 0 ? "pitchdown" : "pitchup", 0.7f);
        }

        static void OnSing(bool singing)
        {
            if (singing) _lastSing = Time.unscaledTime;
        }

        static void OnPhraseHit(double percent, bool fullPoints, bool isLastPhrase)
        {
            if (_gm == null || _gm.Paused) return;
            _phrases++;
            if (!fullPoints) return;
            _phrasesHit++;
            CueBank.Play("hit", 0.9f);
        }

        static void OnNoteHit(int index, VocalNote note)
        {
            if (_gm == null || _gm.Paused || note == null || !note.IsPercussion) return;
            CueBank.Play("hit", 0.9f);
        }

        static void OnNoteMissed(int index, VocalNote note)
        {
            if (_gm == null || _gm.Paused || note == null || !note.IsPercussion) return;
            CueBank.Play("miss", 0.9f);
        }

        static void Sparkle(Note n)
        {
            bool active = false;
            try { active = _engine.EngineStats.IsStarPowerActive; } catch { }
            if (n.StarStart && HighwaySound.PhraseMakesReady(_engine, active)) { _lastSpark = n.Time; CueBank.Play("spstart", 0.8f); }
            else if (n.Time - _lastSpark >= 0.15) { _lastSpark = n.Time; CueBank.Play("spnote", 0.8f); }
        }

        static void OnStarPhraseHit(VocalNote note)
        {
            if (_gm == null || _gm.Paused) return;
            _levelAt = Time.unscaledTime + 0.5f;
        }

        static void OnStarPhraseMissed(VocalNote note)
        {
            if (_gm == null || _gm.Paused) return;
            CueBank.Play("splost", 0.6f);
        }

        public static bool Active => _player != null && _player && !_player.Player.Profile.IsBot;

        static double SongSpeed()
        {
            try { return _gm != null && _gm.SongSpeed > 0 ? _gm.SongSpeed : 1.0; } catch { return 1.0; }
        }
    }
}
