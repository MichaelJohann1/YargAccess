using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Input;
using YargAccess.Audio;

namespace YargAccess
{
    public static class GuidedPractice
    {
        class Step
        {
            public string Text, Prompt, Hint;
            public string[] Any, All;
            public Action Sound, Hit;
            public double Lead, Length;
            public bool Riser, Listen, Talk;
            // Pressed whenever the player is ready (star power): YARG has no timing on it, so none is judged.
            public bool Untimed;
            // Listen and Repeat: the note plays as a call, then is played back; Click adds the hit bar click on the answer.
            public bool Call, Click;
            public float Sing = float.NaN;
            // A Listen and Repeat pattern: notes at these offsets play as a call, then are played back.
            public double[] Offsets;
            public string[] Tokens, Names;
            public Action<int> NoteSound, NoteHit;
            public bool Pattern => Offsets != null;
            // A sung passage: notes at these MIDI pitches, starting at Starts (seconds) for Lens seconds.
            public int[] Melody;
            public double[] Starts, Lens;
            public bool Sings => Melody != null;
            // A step answered with a button press (not singing or speaking): its timing is judged.
            // A roll or swell: these pads are hit fast for RollLength seconds; only how many hits is judged.
            public int[] RollPads;
            public double RollLength;
            public bool Roll => RollPads != null;
            public bool Presses => !Pattern && !Sings && !Untimed && !Roll && (Any != null || All != null);
        }

        const double PatternShift = 2.5, PatternWindow = 0.2, Swell = 0.4;
        // A press within HitWindow of the note counts (YARG's own window is 0.07 s either side). Further off
        // it is a miss that says how early or late, and the try plays again: a press that is called late never
        // passes. A second miss in a row plays the note right first.
        const double HitWindow = 0.1;
        static int _fails;
        static double[] _expect;
        static int _pk;

        public static bool Active { get; private set; }

        class Pending { public float At, Gap, QuietFrom = -1f; public bool AfterSpeech; public Action Run; }

        // Pause between the end of speech and a demo sound.
        const float SoundGap = 2f;

        static readonly List<Step> _steps = new List<Step>();
        static readonly List<Pending> _queue = new List<Pending>();
        static readonly List<int> _holds = new List<int>();
        static readonly HashSet<string> _got = new HashSet<string>();
        static int _index, _serial, _voiced;
        static bool _armed, _completed;
        static float _hitAt, _gotSince;
        static Action _close;
        static Action<bool> _done;
        static string _title, _mode;
        // The practice follows Play Style: Listen and Repeat teaches calls, Play What You Hear the look-ahead.
        static bool _lr;

        public static void Start(string mode, string title, Action<bool> done)
        {
            if (Active) return;
            _steps.Clear();
            _queue.Clear();
            _got.Clear();
            _index = -1;
            _armed = _completed = false;
            _close = null;
            _title = title;
            _mode = mode;
            _done = done;
            _lr = ModSettings.ListenAndRepeat;
            Active = true;
            string device = "";
            try
            {
                device = PracticeDevices.Ensure(mode);
                CueBank.SetKey(9, true);
                switch (mode)
                {
                    case "fivefret": ButtonTest.Open(); _close = ButtonTest.Close; FiveFret(); break;
                    case "drums": DrumButtonTest.Open(); _close = DrumButtonTest.Close; Drums(); break;
                    case "prokeys": KeysButtonTest.Open(); _close = KeysButtonTest.Close; ProKeys(); break;
                    case "vocals": VocalButtonTest.Open(); _close = VocalButtonTest.Close; Vocals(); break;
                }
            }
            catch (Exception ex) { ModLog.Warn("GuidedPractice.Start", ex); }
            if (_close == null || _steps.Count == 0) { Finish(); return; }

            string how;
            switch (mode)
            {
                case "drums": how = Loc.T("hit it as soon as you hear it."); break;
                case "vocals": how = Loc.T("sing it or press its button."); break;
                default: how = Loc.T("play it when the hit bar sound ends."); break;
            }
            string intro = Loc.F("Each kind of note plays twice: first played right, with the hit sound, then for you: {0}", how) + " ";
            if (_lr && mode == "vocals")
                intro += Loc.T("With Listen and Repeat on, the percussion note comes as a call: after the listen sound the knock plays, and when the your-turn sound ends you play it back.") + " ";
            else if (_lr)
                intro = Loc.T("This practice teaches Listen and Repeat, the play style set on the YargAccess tab. After the listen sound each kind of note plays as a call; "
                    + "when the your-turn sound ends, play it back. Each plays twice: first played right, with the hit sound, then for you.") + " ";
            Say(device + intro + Loc.F("{0} stops.", Keys.Finish));
            int serial = _serial;
            AfterSpeech(0.1f, () => { if (serial == _serial) Ask(0); });
        }

        public static void Closed()
        {
            Active = false;
            _queue.Clear();
            _armed = false;
            _close = null;
            StopHolds();
            PracticeDevices.Release();
            var d = _done;
            _done = null;
            if (d != null) d(_completed);
        }

        static void Finish()
        {
            var c = _close;
            if (c != null) c();
            else Closed();
        }

        public static void Tick()
        {
            if (!Active || _queue.Count == 0) return;
            float now = Time.unscaledTime;
            bool quiet = !Speech.Busy;
            // Plain timed actions due this frame may speak ("Right."); the ones that wait for speech then wait a frame.
            for (int i = 0; i < _queue.Count; i++)
                if (!_queue[i].AfterSpeech && _queue[i].At <= now) { quiet = false; break; }
            var due = new List<Action>();
            for (int i = 0; i < _queue.Count; i++)
            {
                var p = _queue[i];
                if (p.At > now) continue;
                if (p.AfterSpeech)
                {
                    if (!quiet) { p.QuietFrom = -1f; continue; }
                    if (p.QuietFrom < 0f) p.QuietFrom = now;
                    if (now - p.QuietFrom < p.Gap) continue;
                    if (p.Gap > 0f && NvdaSpeech.LogTimes)
                        MelonLogger.Msg("[practice] " + DateTime.Now.ToString("HH:mm:ss.fff") + " sound starts " + (int) ((now - p.QuietFrom) * 1000f) + " ms after speech ended");
                }
                due.Add(p.Run);
                _queue.RemoveAt(i);
                i--;
            }
            foreach (var a in due)
            {
                if (!Active) return;
                try { if (a != null) a(); }
                catch (Exception ex) { ModLog.Warn("GuidedPractice.Tick", ex); }
            }
        }

        static void Ask(int i)
        {
            _index = i;
            _armed = false;
            int serial = ++_serial;
            if (i >= _steps.Count) { Speeds(); return; }
            _fails = 0;
            var s = _steps[i];
            Say(s.Text + (string.IsNullOrEmpty(s.Hint) ? "" : " " + s.Hint));
            AfterSpeech(0.1f, () => { if (serial == _serial) Demo(s, serial); }, SoundGap);
        }

        static bool ShowsHit(Step s) => !s.Listen && s.Hit != null && float.IsNaN(s.Sing) && !s.Talk;

        static void Demo(Step s, int serial)
        {
            if (s.Pattern) { PatternCall(s, serial, true); return; }
            if (s.Sings) { MelodyDemo(s, serial); return; }
            if (s.Call) { NoteCall(s, serial, true); return; }
            if (s.Sound != null) s.Sound();
            if (s.Riser) CueBank.PlayRiser(s.Lead, 0.8f);
            if (ShowsHit(s)) Later((float) (s.Roll ? s.Lead + s.Length : s.Lead), () => { if (serial == _serial) s.Hit(); });
            float end = (float) (s.Lead + s.Length) + 0.5f;
            if (s.Listen) Later(end, () => { if (serial == _serial) Ask(_index + 1); });
            else Later(end + 0.3f, () => { if (serial == _serial) Cue(s, serial); });
        }

        static void Cue(Step s, int serial)
        {
            if (s.Roll) { RollTry(s, serial); return; }
            if (s.Pattern) { PatternCall(s, serial, false); return; }
            if (s.Sings) { MelodyTry(s, serial); return; }
            if (s.Call) { NoteCall(s, serial, false); return; }
            _got.Clear();
            _voiced = 0;
            if (s.Sound != null) s.Sound();
            if (s.Riser) CueBank.PlayRiser(s.Lead, 0.8f);
            _hitAt = Time.unscaledTime + (float) s.Lead;
            _armed = true;
            if (s.Presses) Later((float) (s.Lead + HitWindow) + 0.6f, () => { if (serial == _serial && _armed) Missed(s, double.NaN); });
            else Later((float) s.Lead + 6f, () => { if (serial == _serial && _armed) Demo(s, serial); });
        }

        static bool Matches(string spec, string token) => Array.IndexOf(spec.Split('|'), token) >= 0;

        static bool Wants(Step s, string token)
        {
            if (s.Any != null) foreach (var a in s.Any) if (Matches(a, token)) return true;
            if (s.All != null) foreach (var a in s.All) if (Matches(a, token)) return true;
            return false;
        }

        // Holding the frets a hammer-on or pull-off starts from, and lifts on any other note, are not answers.
        static bool Setup(Step s, string token)
        {
            bool wantsLift = false;
            int target = 0;
            foreach (var spec in s.Any)
                foreach (var t in spec.Split('|'))
                {
                    if (t.StartsWith("lift:", StringComparison.Ordinal)) wantsLift = true;
                    else if (t.StartsWith("fret:", StringComparison.Ordinal)) int.TryParse(t.Substring(5), out target);
                }
            if (token.StartsWith("lift:", StringComparison.Ordinal)) return !wantsLift;
            if (!token.StartsWith("fret:", StringComparison.Ordinal)) return false;
            if (wantsLift) return true;
            int n;
            return target > 0 && int.TryParse(token.Substring(5), out n) && n < target;
        }

        // A strum with lower frets held under the note ("strum:1+2" for Red) hits the single note, as in YARG.
        static string Anchored(Step s, string token)
        {
            if (!token.StartsWith("strum:", StringComparison.Ordinal) || token.IndexOf('+') < 0) return token;
            string[] specs = s.Pattern ? (_pk < s.Tokens.Length ? new[] { s.Tokens[_pk] } : null) : s.Any ?? s.All;
            if (specs == null) return token;
            foreach (var spec in specs) if (Matches(spec, token)) return token;
            string top = "strum:" + token.Substring(token.LastIndexOf('+') + 1);
            foreach (var spec in specs) if (Matches(spec, top)) return top;
            return token;
        }

        public static bool Pressed(string token, string name)
        {
            if (!Active) return false;
            var s = _index >= 0 && _index < _steps.Count ? _steps[_index] : null;
            if (s == null || !_armed) return true;
            token = Anchored(s, token);
            if (s.Roll) { RollPress(s, token, name); return true; }
            if (s.Pattern) { PatternPress(s, token, name); return true; }
            if (s.Sings) return true;
            if (token == "mic" && !Wants(s, token)) return true;
            if (s.All != null)
            {
                foreach (var a in s.All)
                {
                    if (!Matches(a, token)) continue;
                    // Every press of a chord is timed, not only the first: a late second pad is a miss.
                    if (OffBeat(s)) return true;
                    if (_got.Count == 0 || Time.unscaledTime - _gotSince > 1.5f) { _got.Clear(); _gotSince = Time.unscaledTime; }
                    _got.Add(a);
                    if (_got.Count >= s.All.Length) Right(s);
                    return true;
                }
            }
            else if (s.Any != null)
            {
                if (Wants(s, token)) { if (!OffBeat(s)) Right(s); return true; }
                if (Setup(s, token)) return true;
                if (token.StartsWith("fret:", StringComparison.Ordinal))
                {
                    bool strum = false;
                    foreach (var a in s.Any) if (a.StartsWith("strum:", StringComparison.Ordinal)) strum = true;
                    if (strum) return true;
                }
            }
            Wrong(s, name);
            return true;
        }

        public static void Sung(float midi)
        {
            if (!Active || !_armed || _index < 0 || _index >= _steps.Count) return;
            var s = _steps[_index];
            if (s.Sings) { MelodySample(s, midi); return; }
            if (s.Talk) { if (++_voiced >= 4) Right(s); return; }
            if (float.IsNaN(s.Sing)) return;
            float diff = (midi - s.Sing) % 12f;
            if (diff > 6f) diff -= 12f;
            if (diff < -6f) diff += 12f;
            if (Math.Abs(diff) <= 1.5f) { if (++_voiced >= 5) Right(s); }
            else if (_voiced > 0) _voiced--;
        }

        static void Right(Step s)
        {
            _armed = false;
            int serial = ++_serial;
            if (s.Hit != null) s.Hit();
            string text = Loc.T("Right.");
            Later(0.15f, () => { if (serial == _serial) Say(text); });
            AfterSpeech(0.25f, () => { if (serial == _serial) Ask(_index + 1); });
        }

        static void Wrong(Step s, string name)
        {
            _armed = false;
            int serial = ++_serial;
            CueBank.Play(_mode == "fivefret" ? "overstrum" : "miss", 0.7f);
            bool demo = ++_fails >= 2;
            Later(0.25f, () => { if (serial == _serial) Say(Loc.F("That was {0}.", name) + " " + s.Prompt + (string.IsNullOrEmpty(s.Hint) ? "" : " " + s.Hint) + Again(demo)); });
            AfterSpeech(0.35f, () => { if (serial == _serial) Retry(s, serial, demo); }, SoundGap);
        }

        // The right button but too far from the note: a miss, with how early or late it was.
        static bool OffBeat(Step s)
        {
            if (!s.Presses) return false;
            double off = Time.unscaledTime - _hitAt;
            if (Math.Abs(off) <= HitWindow) return false;
            Missed(s, off);
            return true;
        }

        static void Missed(Step s, double off)
        {
            _armed = false;
            int serial = ++_serial;
            CueBank.Play("miss", 0.7f);
            bool demo = ++_fails >= 2;
            bool part = double.IsNaN(off) && s.All != null && _got.Count > 0 && _got.Count < s.All.Length;
            string why = part ? Loc.T("only part of it was played. Play them all together.")
                : double.IsNaN(off) ? Loc.T("you didn't play it.") : Secs(off) + ". " + Advice(s, off < 0);
            Later(0.25f, () => { if (serial == _serial) Say(Loc.F("Missed: {0}", why) + Again(demo)); });
            AfterSpeech(0.35f, () => { if (serial == _serial) Retry(s, serial, demo); }, SoundGap);
        }

        // Rolls and swells

        static readonly Dictionary<string, int> _rollHits = new Dictionary<string, int>();

        static void RollTry(Step s, int serial)
        {
            _rollHits.Clear();
            float len = (float) s.RollLength;
            if (_lr)
            {
                // Listen and Repeat: the roll plays as a call, then is played back.
                float t0 = (float) Swell, answer = t0 + len + 0.3f + (float) Swell;
                CueBank.Play("listen", 0.7f);
                Later(t0, () => { if (serial == _serial) s.Sound(); });
                Later(answer - (float) Swell, () => { if (serial == _serial) CueBank.Play("yourturn", 0.7f); });
                Later(answer - 0.3f, () => { if (serial == _serial) _armed = true; });
                Later(answer + len + 0.3f, () => { if (serial == _serial && _armed) RollJudge(s); });
                return;
            }
            s.Sound();
            _armed = true;
            Later(len + 0.3f, () => { if (serial == _serial && _armed) RollJudge(s); });
        }

        static void RollPress(Step s, string token, string name)
        {
            if (!token.StartsWith("pad:", StringComparison.Ordinal)) return;
            foreach (int p in s.RollPads)
                if (token == "pad:" + p)
                {
                    int n;
                    _rollHits.TryGetValue(token, out n);
                    _rollHits[token] = n + 1;
                    return;
                }
            StopHolds();
            Wrong(s, name);
        }

        static void RollJudge(Step s)
        {
            _armed = false;
            int serial = ++_serial;
            StopHolds();
            int total = 0, least = int.MaxValue;
            foreach (int p in s.RollPads)
            {
                int n;
                _rollHits.TryGetValue("pad:" + p, out n);
                total += n;
                least = Math.Min(least, n);
            }
            // About four hits a second in all, and for a swell both pads used.
            int need = Math.Max(4, (int) Math.Round(s.RollLength * 4));
            bool ok = total >= need && least >= (s.RollPads.Length > 1 ? 2 : 1);
            if (ok)
            {
                if (s.Hit != null) s.Hit();
                Later(0.15f, () => { if (serial == _serial) Say(Loc.F("Right, {0} hits.", total)); });
                AfterSpeech(0.25f, () => { if (serial == _serial) Ask(_index + 1); });
                return;
            }
            CueBank.Play("miss", 0.7f);
            bool demo = ++_fails >= 2;
            string why = total == 0 ? Loc.T("you didn't play it.")
                : total < need ? (total == 1 ? Loc.F("{0} hit is too few. Keep hitting fast until it stops.", total) : Loc.F("{0} hits are too few. Keep hitting fast until it stops.", total))
                : Loc.T("use both pads, one after the other.");
            Later(0.25f, () => { if (serial == _serial) Say(Loc.F("Missed: {0}", why) + Again(demo)); });
            AfterSpeech(0.35f, () => { if (serial == _serial) Retry(s, serial, demo); }, SoundGap);
        }

        static Step RollStep(int[] pads, bool five, bool kb)
        {
            var names = five ? DrumButtonTest.FiveNames : DrumButtonTest.FourNames;
            bool swell = pads.Length > 1;
            const double len = 1.5;
            var s = new Step { RollPads = pads, RollLength = len, Length = len };
            s.Sound = () =>
            {
                StopHolds();
                for (int i = 0; i < pads.Length; i++)
                {
                    int ch = CueBank.StartRoll(DrumSound.SoundOf(pads[i], five), swell, i == 1, DrumSound.PanOf(pads[i], five), 0.7f);
                    if (ch != 0) _holds.Add(ch);
                }
                int serial = _serial;
                Later((float) len, () => { if (serial == _serial) StopHolds(); });
            };
            s.Hit = () => { foreach (var p in pads) CueBank.PlayDrumHit(DrumSound.SoundOf(p, five), DrumSound.PanOf(p, five), 0.9f); };
            string back = _lr ? " " + Loc.T("With Listen and Repeat on it plays first; then play it back as fast for as long.") : "";
            if (swell)
            {
                s.Prompt = Loc.F("Hit the {0} and the {1} in turn, fast, until it stops.", DrumButtonTest.Said(five, pads[0]), DrumButtonTest.Said(five, pads[1]));
                s.Text = Loc.T("A swell: two pads' sounds alternating fast. Hit the two pads one after the other, fast, for as long as it sounds; the exact timing does not matter.") + back + " " + s.Prompt;
                if (kb) s.Hint = Loc.F("On the keyboard, press {0} and {1} in turn.", DrumKeys[pads[0]], DrumKeys[pads[1]]);
            }
            else
            {
                s.Prompt = Loc.F("Hit the {0} fast until it stops.", DrumButtonTest.Said(five, pads[0]));
                s.Text = Loc.T("A roll: one pad's sound repeating fast. Keep hitting that pad, fast and steady, for as long as it sounds; the exact timing does not matter. "
                    + "Rolls and swells are on Expert, and on Hard in some songs.") + back + " " + s.Prompt;
                s.Hint = kb ? Loc.F("On the keyboard, press {0} again and again.", DrumKeys[pads[0]]) : DrumDeviceHint(pads, five);
            }
            return s;
        }

        static string Again(bool demo) => " " + (demo ? Loc.T("Listen to it played right, then try again.") : Loc.T("Again."));

        static void Retry(Step s, int serial, bool demo)
        {
            if (demo) { _fails = 0; Demo(s, serial); }
            else Cue(s, serial);
        }

        // "0.15 seconds late", "0.25 seconds early", to the nearest 0.05 s.
        static string Secs(double off)
        {
            double abs = Math.Abs(off), a = Math.Max(0.05, Math.Round(abs * 20) / 20);
            // Just past a pass limit, never read the limit itself ("0.1 seconds late" for a miss).
            foreach (double limit in new[] { HitWindow, PatternWindow, 0.3, 0.6 })
                if (Math.Abs(a - limit) < 1e-6 && abs > limit) a += 0.05;
            string n = a.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            if (off < 0) return a == 1 ? Loc.F("{0} second early", n) : Loc.F("{0} seconds early", n);
            return a == 1 ? Loc.F("{0} second late", n) : Loc.F("{0} seconds late", n);
        }

        static string Advice(Step s, bool early)
        {
            if (s.Call || s.Pattern) return Loc.T("Play as the your-turn sound ends.");
            if (early) return s.Riser ? Loc.T("Wait for the hit bar sound to end.") : Loc.T("Play right after the sound.");
            return s.Riser ? Loc.T("Play as the hit bar sound ends.") : Loc.T("Play as soon as you hear it.");
        }

        // The call: the listen swell ends as the pattern starts, the your-turn swell ends where the answer starts.
        // In the demo the hit sounds then play the answer; in a try the presses are judged against those times.
        static void PatternCall(Step s, int serial, bool demo)
        {
            float t0 = (float) Swell;
            double span = s.Offsets[s.Offsets.Length - 1];
            CueBank.Play("listen", 0.7f);
            for (int i = 0; i < s.Offsets.Length; i++)
            {
                int n = i;
                Later(t0 + (float) s.Offsets[i], () => { if (serial == _serial) s.NoteSound(n); });
            }
            float answer = t0 + (float) PatternShift;
            Later(answer - (float) Swell, () => { if (serial == _serial) CueBank.Play("yourturn", 0.7f); });
            if (s.Click)
                for (int i = 0; i < s.Offsets.Length; i++)
                {
                    int n = i;
                    Later(answer + (float) s.Offsets[n], () => { if (serial == _serial) CueBank.Play("click", 0.7f); });
                }
            if (demo)
            {
                for (int i = 0; i < s.Offsets.Length; i++)
                {
                    int n = i;
                    Later(answer + (float) s.Offsets[i], () => { if (serial == _serial) s.NoteHit(n); });
                }
                Later(answer + (float) span + 0.8f, () => { if (serial == _serial) Cue(s, serial); });
                return;
            }
            float start = Time.unscaledTime + answer;
            _expect = new double[s.Offsets.Length];
            for (int i = 0; i < _expect.Length; i++) _expect[i] = start + s.Offsets[i];
            _pk = 0;
            Later(answer - 0.3f, () => { if (serial == _serial) _armed = true; });
            Later(answer + (float) (span + PatternWindow) + 0.1f, () => { if (serial == _serial && _armed) PatternFail(s, null, double.NaN); });
        }

        // One note as a call: the listen swell ends as the note plays, the your-turn swell ends where the answer starts.
        static void NoteCall(Step s, int serial, bool demo)
        {
            float t0 = (float) Swell;
            float answer = t0 + (float) PatternShift;
            CueBank.Play("listen", 0.7f);
            Later(t0, () => { if (serial == _serial && s.Sound != null) s.Sound(); });
            Later(answer - (float) Swell, () => { if (serial == _serial) CueBank.Play("yourturn", 0.7f); });
            if (s.Click) Later(answer, () => { if (serial == _serial) CueBank.Play("click", 0.7f); });
            if (demo)
            {
                if (s.Hit != null) Later(answer, () => { if (serial == _serial) s.Hit(); });
                Later(answer + (float) s.Length + 0.8f, () => { if (serial == _serial) Cue(s, serial); });
                return;
            }
            _got.Clear();
            _voiced = 0;
            _hitAt = Time.unscaledTime + answer;
            Later(answer - 0.8f, () => { if (serial == _serial) _armed = true; });
            if (s.Presses) Later(answer + (float) HitWindow + 0.6f, () => { if (serial == _serial && _armed) Missed(s, double.NaN); });
            else Later(answer + (float) s.Length + 6f, () => { if (serial == _serial && _armed) Demo(s, serial); });
        }

        static void PatternPress(Step s, string token, string name)
        {
            if (_pk >= s.Tokens.Length) return;
            bool strum = s.Tokens[_pk].StartsWith("strum:", StringComparison.Ordinal);
            if (strum && (token.StartsWith("fret:", StringComparison.Ordinal) || token.StartsWith("lift:", StringComparison.Ordinal))) return;
            double off = Time.unscaledTime - _expect[_pk];
            if (!Matches(s.Tokens[_pk], token)) { PatternFail(s, name, double.NaN); return; }
            if (Math.Abs(off) > PatternWindow) { PatternFail(s, null, off); return; }
            s.NoteHit(_pk);
            if (++_pk < s.Tokens.Length) return;
            _hitAt = Time.unscaledTime;
            Right(s);
        }

        static void PatternFail(Step s, string wrong, double off)
        {
            _armed = false;
            int serial = ++_serial;
            CueBank.Play(wrong != null && _mode == "fivefret" ? "overstrum" : "miss", 0.7f);
            bool demo = ++_fails >= 2;
            int k = Math.Min(_pk, s.Tokens.Length - 1);
            string why = wrong != null ? Loc.F("Note {0} was {1}, not {2}.", k + 1, wrong, s.Names[k])
                : !double.IsNaN(off) ? Loc.F("Note {0}, {1}, was {2}.", k + 1, s.Names[k], Secs(off))
                : Loc.F("You didn't play note {0}, {1}.", k + 1, s.Names[k]);
            Later(0.25f, () => { if (serial == _serial) Say(Loc.T("Missed.") + " " + why + " " + (demo ? Loc.F("Listen to it played right: {0}", s.Prompt + (string.IsNullOrEmpty(s.Hint) ? "" : " " + s.Hint)) : Loc.T("Again."))); });
            AfterSpeech(0.35f, () => { if (serial == _serial) Retry(s, serial, demo); }, SoundGap);
        }

        static Step PatternStep(double[] beats, string[] tokens, string[] names, Action<int> sound, Action<int> hit, bool first)
        {
            var offsets = Array.ConvertAll(beats, b => b * 0.5);
            string prompt = string.Join(", ", names);
            return new Step
            {
                Offsets = offsets,
                Tokens = tokens,
                Names = names,
                NoteSound = sound,
                NoteHit = hit,
                Prompt = prompt + ".",
                Click = _mode == "fivefret" || _mode == "prokeys",
                Text = first
                    ? Loc.F("Now a whole pattern, as in a song. After the listen sound it plays: {0}"
                      + ". When the your-turn sound ends, play it back in the same rhythm. First it plays right, with the hit sounds.", prompt)
                    : Loc.F("Another pattern: {0}.", prompt)
            };
        }

        // Sung passages: a scale or a short tune, sung note by note.
        const double SingTolerance = 1.5;
        static float _melStart, _lastSweep;
        static readonly List<KeyValuePair<float, float>> _samples = new List<KeyValuePair<float, float>>();

        static readonly string[] NoteNames = { Loc.T("C"), Loc.T("C sharp"), Loc.T("D"), Loc.T("E flat"), Loc.T("E"), Loc.T("F"), Loc.T("F sharp"), Loc.T("G"), Loc.T("A flat"), Loc.T("A"), Loc.T("B flat"), Loc.T("B") };
        static string NoteName(int midi) => NoteNames[((midi % 12) + 12) % 12];

        static float PitchDiff(float sung, int target)
        {
            float d = (sung - target) % 12f;
            if (d > 6f) d -= 12f;
            if (d < -6f) d += 12f;
            return d;
        }

        static Step SungStep(int[] midis, double[] beats, double[] lens, string text, bool mic)
        {
            var names = Array.ConvertAll(midis, NoteName);
            return new Step
            {
                Melody = midis,
                Starts = Array.ConvertAll(beats, b => b * 0.6),
                Lens = Array.ConvertAll(lens, l => l * 0.6 - 0.05),
                Listen = !mic,
                Hit = () => CueBank.PlayHit(3, 0.8f),
                Prompt = Loc.F("Sing {0}.", string.Join(", ", names)),
                Text = text + " " + (mic ? Loc.T("First it plays as it should be sung, then you sing it with the Pitch Guide tones.") : Loc.T("Listen; with a microphone you would sing it."))
            };
        }

        // Played right: each note's tone held for its length, its Pitch Guide tone just before it, then the hit sound.
        static void MelodyDemo(Step s, int serial)
        {
            float t0 = 0.3f;
            for (int i = 0; i < s.Melody.Length; i++)
            {
                int m = s.Melody[i];
                double len = s.Lens[i];
                Later(t0 + (float) s.Starts[i] - 0.1f, () => { if (serial == _serial) CueBank.PlayGuide(m, 0.8f); });
                Later(t0 + (float) s.Starts[i], () =>
                {
                    if (serial != _serial) return;
                    CueBank.PlayKey(m, 0f, 0.6f);
                    Hold(CueBank.StartKeyHold(m, 0f), len);
                });
            }
            int last = s.Melody.Length - 1;
            float end = t0 + (float) (s.Starts[last] + s.Lens[last]);
            Later(end + 0.1f, () => { if (serial == _serial) s.Hit(); });
            if (s.Listen) Later(end + 0.8f, () => { if (serial == _serial) Ask(_index + 1); });
            else Later(end + 1.2f, () => { if (serial == _serial) Cue(s, serial); });
        }

        // Your turn: only the Pitch Guide tones, as in a song; the voice is sampled and judged at the end.
        static void MelodyTry(Step s, int serial)
        {
            float t0 = 0.5f;
            _samples.Clear();
            _melStart = Time.unscaledTime + t0;
            for (int i = 0; i < s.Melody.Length; i++)
            {
                int m = s.Melody[i];
                Later(t0 + (float) s.Starts[i] - 0.1f, () => { if (serial == _serial) CueBank.PlayGuide(m, 0.8f); });
            }
            _armed = true;
            int last = s.Melody.Length - 1;
            Later(t0 + (float) (s.Starts[last] + s.Lens[last]) + 0.4f, () => { if (serial == _serial && _armed) MelodyJudge(s); });
        }

        static void MelodySample(Step s, float midi)
        {
            if (float.IsNaN(midi) || float.IsInfinity(midi) || midi < 30f || midi > 100f) return;
            float now = Time.unscaledTime;
            _samples.Add(new KeyValuePair<float, float>(now - _melStart, midi));
            // The needle, as in a song: off pitch for a moment plays sing higher / sing lower.
            double t = now - _melStart;
            int cur = -1;
            for (int i = 0; i < s.Melody.Length; i++) if (t >= s.Starts[i] && t <= s.Starts[i] + s.Lens[i]) cur = i;
            if (cur < 0 || now - _lastSweep < 0.6f) return;
            int off = 0, seen = 0;
            for (int i = _samples.Count - 1; i >= 0 && _samples[i].Key >= t - 0.3; i--)
            {
                seen++;
                if (Math.Abs(PitchDiff(_samples[i].Value, s.Melody[cur])) > SingTolerance) off++;
            }
            if (seen >= 4 && off == seen && t - s.Starts[cur] > 0.25)
            {
                _lastSweep = now;
                CueBank.Play(PitchDiff(midi, s.Melody[cur]) > 0 ? "pitchdown" : "pitchup", 0.6f);
            }
        }

        static void MelodyJudge(Step s)
        {
            _armed = false;
            int serial = ++_serial;
            int n = s.Melody.Length, good = 0;
            string first = null;
            for (int i = 0; i < n; i++)
            {
                double a = s.Starts[i], b = a + s.Lens[i];
                int heard = 0, on = 0, before = 0;
                float sum = 0f, firstOn = float.NaN;
                foreach (var p in _samples)
                {
                    if (p.Key < a || p.Key > b) continue;
                    heard++;
                    float d = PitchDiff(p.Value, s.Melody[i]);
                    sum += d;
                    if (Math.Abs(d) <= SingTolerance) { on++; if (float.IsNaN(firstOn)) firstOn = p.Key; }
                    else if (float.IsNaN(firstOn)) before++;
                }
                // The first note has more leeway: the voice is still finding the pitch.
                bool ok = heard >= 3 && on >= heard * (i == 0 ? 0.25 : 0.4);
                double late = float.IsNaN(firstOn) ? 0 : firstOn - a;
                if (ok && late <= (i == 0 ? 0.6 : 0.3)) { good++; continue; }
                if (first != null) continue;
                string name = NoteName(s.Melody[i]);
                if (heard < 3) first = Loc.F("Note {0}, {1}, wasn't heard.", i + 1, name);
                // Silent until the pitch came: late. Singing the wrong pitch first is a pitch miss, said below.
                else if (!float.IsNaN(firstOn) && before < 3 && late > (i == 0 ? 0.6 : 0.3)) first = Loc.F("Note {0}, {1}, came in {2}.", i + 1, name, Secs(late));
                else first = sum / heard > 0 ? Loc.F("Note {0}, {1}, was too high.", i + 1, name) : Loc.F("Note {0}, {1}, was too low.", i + 1, name);
            }
            if (good == n)
            {
                s.Hit();
                Later(0.15f, () => { if (serial == _serial) Say(Loc.F("Right, all {0} notes on pitch.", n)); });
                AfterSpeech(0.25f, () => { if (serial == _serial) Ask(_index + 1); });
                return;
            }
            CueBank.Play("miss", 0.7f);
            bool demo = ++_fails >= 2;
            string text = (good == 0 && _samples.Count == 0 ? Loc.T("I didn't hear you sing.") + " " : Loc.F("You sang {0} of {1} on pitch.", good, n) + " " + first + " ")
                + (demo ? Loc.T("Listen to it sung right, then try again.") : Loc.T("Again."));
            Later(0.25f, () => { if (serial == _serial) Say(text); });
            AfterSpeech(0.35f, () => { if (serial == _serial) Retry(s, serial, demo); }, SoundGap);
        }

        static void Speeds()
        {
            int serial = _serial;
            Say(SpeedText() + " " + Loc.T("Songs can also change speed partway through, and harder difficulties add more notes. "
                + "Practice on the main menu can slow any song down while you learn it. That is the end of the practice."));
            AfterSpeech(0.1f, () =>
            {
                if (serial != _serial) return;
                _completed = true;
                Finish();
            });
        }

        static string SpeedText()
        {
            if (_lr && _mode != "vocals")
                return Loc.T("Songs move at different speeds, counted in beats a minute, from about 60 to over 200. A call keeps the song's rhythm and plays in the bars just before your turn, "
                    + "so in a slow song its notes are spread out and in a fast song they come close together. Call Length on the YargAccess tab sets how many bars a call lasts: 1, 2 or 4.")
                    + (_mode != "drums" ? " " + Loc.T("A click marks each note of your turn.") : "");
            string lead = _mode == "drums" ? Loc.T("Drums Look-ahead") : Loc.T("Look-ahead");
            switch (_mode)
            {
                case "vocals":
                    return Loc.T("Songs move at different speeds, counted in beats a minute. In a slow song each pitch has time to settle; "
                        + "in a fast one the pitches change quickly and the Pitch Guide tones come close together.");
                case "drums":
                    return Loc.F("Songs move at different speeds, counted in beats a minute, from about 60 to over 200. Each sound always comes the same time before its note, the "
                        + "{0}. In a slow song there is a gap after each note before the next sound; in a fast song the next sound can come before you have hit the last note, "
                        + "so keep a steady beat and play the notes in the order you heard them.", lead);
                default:
                    return Loc.F("Songs move at different speeds, counted in beats a minute, from about 60 to over 200. Each sound always comes the same time before its note, the "
                        + "{0}. In a slow song there is a gap after each note before the next sound; in a fast song the next sound can come before you have played the last note, "
                        + "so keep a steady beat and play the notes in the order you heard them.", lead)
                        + " " + Loc.T("When notes are far apart the hit bar sound rises to each one; when they are close, it becomes a click on each note.");
            }
        }

        static void Say(string text) { Captions.SayIn(_title, () => Active, text, true); }

        static void Later(float seconds, Action a) { _queue.Add(new Pending { At = Time.unscaledTime + seconds, Run = a }); }

        // Runs once `seconds` have passed and speech has then been quiet for `gap` seconds.
        static void AfterSpeech(float seconds, Action a, float gap = 0f) { _queue.Add(new Pending { At = Time.unscaledTime + seconds, AfterSpeech = true, Gap = gap, Run = a }); }

        static void Hold(int channel, double seconds)
        {
            if (channel == 0) return;
            _holds.Add(channel);
            Later((float) seconds, () => { CuePlayer.FadeOut(channel, 25); _holds.Remove(channel); });
        }

        static void StopHolds()
        {
            foreach (var c in _holds) CuePlayer.FadeOut(c, 25);
            _holds.Clear();
        }

        static string Join(List<string> names)
        {
            if (names.Count <= 1) return names.Count == 1 ? names[0] : "";
            return Loc.F("{0} and {1}", string.Join(", ", names.GetRange(0, names.Count - 1).ToArray()), names[names.Count - 1]);
        }

        // "A and B and C", one key per pair so the word order can change.
        static string And(IEnumerable<string> names)
        {
            string all = null;
            foreach (var n in names) all = all == null ? n : Loc.F("{0} and {1}", all, n);
            return all ?? "";
        }

        // Button names on the practice device, from the tutorial profile's binds.
        static string Btn(GameMode gm, int action) => PracticeDevices.ButtonName(gm, action);

        static string OnDevice(string text) => text == null || PracticeDevices.DeviceKind == null ? null : Loc.F("On your {0}, {1}", PracticeDevices.DeviceKind, text);

        static string AllOf(List<string> names) => names.Contains(null) ? null : Join(names);

        static string Strum5 => Btn(GameMode.FiveFretGuitar, 7) ?? Btn(GameMode.FiveFretGuitar, 6);

        static string Fret5(int f) => Btn(GameMode.FiveFretGuitar, f - 1);

        static string FretDeviceHint(int[] frets, CueBank.Kind kind)
        {
            if (PracticeDevices.Device == null) return null;
            string strum = Strum5;
            if (frets.Length == 0) return strum == null ? null : OnDevice(Loc.F("press {0} with no fret held.", strum));
            int top = frets[frets.Length - 1];
            if (frets.Length > 1)
            {
                var names = new List<string>();
                foreach (var f in frets) names.Add(Fret5(f));
                string all = AllOf(names);
                return all == null || strum == null ? null : OnDevice(Loc.F("hold {0} and press {1}.", all, strum));
            }
            string c = Fret5(top);
            if (c == null) return null;
            switch (kind)
            {
                case CueBank.Kind.HammerOn:
                    return Fret5(top - 1) == null ? null : OnDevice(Loc.F("hold {0} and press {1}.", Fret5(top - 1), c));
                case CueBank.Kind.PullOff:
                    return Fret5(top + 1) == null ? null : OnDevice(Loc.F("hold {0} and {1}, then let go of {1}.", c, Fret5(top + 1)));
                case CueBank.Kind.Tap:
                    return OnDevice(Loc.F("press {0}.", c));
                default:
                    return strum == null ? null : OnDevice(Loc.F("hold {0} and press {1}.", c, strum));
            }
        }

        static string StarHint(GameMode gm, int action)
        {
            var b = Btn(gm, action);
            return b == null ? null : OnDevice(Loc.F("press {0}.", b));
        }

        // A device with no button for it (a MIDI keyboard has no star power bind): the computer keyboard still works.
        static string KeyboardStill(string key) => PracticeDevices.Device == null ? null : Loc.F("On the computer keyboard, press {0}.", key);

        static readonly string[] Frets = { Loc.T("Open"), Loc.T("Green"), Loc.T("Red"), Loc.T("Yellow"), Loc.T("Blue"), Loc.T("Orange") };
        static readonly string[] Places = { Loc.T("in the centre"), Loc.T("far left"), Loc.T("on the left"), Loc.T("in the centre"), Loc.T("on the right"), Loc.T("far right") };

        static Step FretStep(int[] frets, CueBank.Kind kind, bool held, bool star, bool kb, double lead)
        {
            string dh = kb ? null : FretDeviceHint(frets, kind);
            bool open = frets.Length == 0;
            bool chord = frets.Length > 1;
            bool strum = kind == CueBank.Kind.Strum;
            int top = open ? 0 : frets[frets.Length - 1];
            var play = open ? new[] { 0 } : frets;
            if (_lr) lead = 0;
            var s = new Step { Lead = lead, Riser = !_lr, Length = held ? 1.2 : 0, Call = _lr, Click = _lr };
            s.Sound = () =>
            {
                foreach (var f in play)
                {
                    CueBank.PlayNote(f, kind);
                    if (held) Hold(CueBank.StartHold(f), lead + 1.2);
                }
                if (chord) CueBank.Play("chord", 0.8f);
                if (star) CueBank.Play("spnote", 0.8f);
                if (held) Later((float) (lead + 1.2), () => CueBank.Play("release", 0.5f));
            };
            s.Hit = () => { foreach (var f in play) CueBank.PlayHit(f, 0.8f); };
            var names = new List<string>();
            foreach (var f in frets) names.Add(Frets[f]);
            var digits = new List<string>();
            foreach (var f in frets) digits.Add(f.ToString());
            string token = "strum:" + string.Join("+", digits.ToArray());
            s.Any = !strum && !open ? new[] { "fret:" + top + "|" + token } : new[] { token };
            if (open)
            {
                s.Prompt = Loc.T("Strum with no fret held.");
                s.Text = Loc.T("An open note, the low sound in the centre.") + " " + s.Prompt;
                s.Hint = kb ? Loc.T("On the keyboard, press Down with no number held.") : dh;
                return s;
            }
            if (chord)
            {
                s.Prompt = Loc.F("Hold {0} and strum once.", Join(names));
                s.Text = Loc.F("A chord: {0} together, with the chord rake.", Join(names)) + " " + s.Prompt;
                s.Hint = kb ? Loc.F("On the keyboard, hold {0} and press Down.", And(digits)) : dh;
                return s;
            }
            string c = Frets[top];
            switch (kind)
            {
                case CueBank.Kind.HammerOn:
                    s.Prompt = Loc.F("Hold {0}, then press {1} without strumming.", Frets[top - 1], c);
                    s.Text = Loc.F("A hammer-on to {0}: the note slides up into place. It comes right after a lower note you hit: keep that fret down and press {0}, with no strum.", c) + " " + s.Prompt;
                    break;
                case CueBank.Kind.PullOff:
                    s.Any = new[] { "lift:" + top };
                    s.Prompt = Loc.F("Hold {0} and {1}, then lift {1} without strumming.", c, Frets[top + 1]);
                    s.Text = Loc.F("A pull-off to {0}: the note slides down. It comes right after a higher note you hit: with {0} already held, lift the higher fret, with no strum.", c) + " " + s.Prompt;
                    break;
                case CueBank.Kind.Tap:
                    s.Prompt = Loc.F("Press {0} with no strum.", c);
                    s.Text = Loc.F("A tap note on {0}: two wooden knocks.", c) + " " + s.Prompt;
                    break;
                default:
                    s.Prompt = Loc.F("Hold {0} and strum.", c);
                    s.Text = Loc.F("{0} note, {1}.", c, Places[top]) + " " + s.Prompt;
                    break;
            }
            if (held) s.Text = Loc.F("A held note on {0}: its tone lasts, then the Let go chimes play.", c) + " "
                + (_lr ? Loc.T("When you play it back, hold the fret as long as the tone lasted.") : Loc.T("In a song keep the fret down until the chimes.")) + " " + s.Prompt;
            if (star) s.Text = Loc.F("A star power note on {0}: the sparkle marks it. Hit every note of a star power phrase to fill the star power bar.", c) + " " + s.Prompt;
            if (kind == CueBank.Kind.HammerOn) s.Hint = kb ? Loc.F("On the keyboard, hold {0} and press {1}.", top - 1, top) : dh;
            else if (kind == CueBank.Kind.PullOff) s.Hint = kb ? Loc.F("On the keyboard, hold {0} and {1}, then let go of {1}.", top, top + 1) : dh;
            else s.Hint = kb ? (strum ? Loc.F("On the keyboard, hold {0} and press Down.", top) : Loc.F("On the keyboard, press {0}.", top)) : dh;
            return s;
        }

        static void FiveFret()
        {
            double lead = ModSettings.LookAheadSeconds;
            bool kb = PracticeDevices.Device == null && ButtonTest.KeyboardKeys;
            var strum = CueBank.Kind.Strum;
            for (int f = 1; f <= 5; f++) _steps.Add(FretStep(new[] { f }, strum, false, false, kb, lead));
            _steps.Add(FretStep(new int[0], strum, false, false, kb, lead));
            _steps.Add(FretStep(new[] { 1, 2 }, strum, false, false, kb, lead));
            _steps.Add(FretStep(new[] { 3 }, CueBank.Kind.HammerOn, false, false, kb, lead));
            _steps.Add(FretStep(new[] { 2 }, CueBank.Kind.PullOff, false, false, kb, lead));
            _steps.Add(FretStep(new[] { 4 }, CueBank.Kind.Tap, false, false, kb, lead));
            _steps.Add(FretStep(new[] { 1 }, strum, true, false, kb, lead));
            _steps.Add(FretStep(new[] { 3 }, strum, false, true, kb, lead));
            _steps.Add(StarStep(kb ? Loc.T("On the keyboard, press Backspace.") : StarHint(GameMode.FiveFretGuitar, 9) ?? KeyboardStill(Loc.T("Backspace"))));
            if (!ModSettings.ListenAndRepeat) return;
            _steps.Add(FretPattern(new[] { 0.0, 1, 2, 3 }, new[] { 1, 2, 3, 2 }, true, kb));
            _steps.Add(FretPattern(new[] { 0.0, 1, 1.5, 3 }, new[] { 1, 1, 3, 4 }, false, kb));
        }

        static Step FretPattern(double[] beats, int[] frets, bool first, bool kb)
        {
            var s = PatternStep(beats, Array.ConvertAll(frets, f => "strum:" + f), Array.ConvertAll(frets, f => Frets[f]),
                i => CueBank.PlayNote(frets[i], CueBank.Kind.Strum), i => CueBank.PlayHit(frets[i], 0.8f), first);
            if (kb) s.Hint = Loc.F("On the keyboard: {0}, each with Down.", string.Join(", ", Array.ConvertAll(frets, f => f.ToString())));
            else
            {
                var names = new List<string>(Array.ConvertAll(frets, f => Fret5(f)));
                string strum = Strum5;
                if (PracticeDevices.Device != null && !names.Contains(null) && strum != null)
                    s.Hint = OnDevice(Loc.F("{0}, each with {1}.", string.Join(", ", names.ToArray()), strum));
            }
            return s;
        }

        static Step StarStep(string hint)
        {
            return new Step
            {
                Lead = 0.3,
                Untimed = true,
                Sound = () => CueBank.Play("spready", 0.8f),
                Hit = () => CueBank.Play("yarg_spdeploy", 0.8f),
                Any = new[] { "sp" },
                Prompt = Loc.T("Press your star power button."),
                Text = Loc.T("This sparkle means star power is ready: the bar is half full or more. Press your star power button to turn it on and double your points."),
                Hint = hint
            };
        }

        static readonly string[] DrumKeys = { Loc.T("Space"), "Z", "X", "C", "V", "S", "D", "F" };
        static readonly string[] FourPlaces = { Loc.T("the centre"), Loc.T("left"), Loc.T("centre left"), Loc.T("centre right"), Loc.T("right"), Loc.T("centre left"), Loc.T("centre right"), Loc.T("right") };
        static readonly string[] FivePlaces = { Loc.T("the centre"), Loc.T("far left"), Loc.T("left"), Loc.T("the centre"), Loc.T("right"), Loc.T("far right") };

        static Step DrumStep(int[] pads, float vol, bool five, bool kb, string text)
        {
            var names = five ? DrumButtonTest.FiveNames : DrumButtonTest.FourNames;
            var places = five ? FivePlaces : FourPlaces;
            double lead = _lr ? 0 : ModSettings.DrumLookAheadSeconds;
            var s = new Step { Lead = lead, Call = _lr };
            s.Sound = () => { foreach (var p in pads) CueBank.PlayDrum(DrumSound.SoundOf(p, five), DrumSound.PanOf(p, five), vol); };
            s.Hit = () => { foreach (var p in pads) CueBank.PlayDrumHit(DrumSound.SoundOf(p, five), DrumSound.PanOf(p, five), 0.9f); };
            var padNames = new List<string>();
            var keys = new List<string>();
            foreach (var p in pads)
            {
                padNames.Add(p == 0 ? Loc.T("the kick") : Loc.F("the {0}", DrumButtonTest.Said(five, p)));
                keys.Add(DrumKeys[Math.Min(p, DrumKeys.Length - 1)]);
            }
            if (pads.Length == 1)
            {
                int p = pads[0];
                s.Any = new[] { "pad:" + p };
                s.Prompt = p == 0 ? Loc.T("Press the kick pedal.") : Loc.F("Hit the {0}.", DrumButtonTest.Said(five, p));
                // names[] stays English for the cymbal check; it is translated where spoken.
                s.Text = text ?? ((p == 0 ? Loc.T("The kick, the low sound in the centre.")
                    : names[p].EndsWith("cymbal") ? Loc.F("The {0}, {1}: its lane's note an octave up, with a shimmer.", DrumButtonTest.Said(five, p), places[p])
                    : Loc.F("The {0}, {1}.", DrumButtonTest.Said(five, p), places[p])) + " ") + s.Prompt;
            }
            else
            {
                s.All = Array.ConvertAll(pads, p => "pad:" + p);
                s.Prompt = Loc.F("Hit {0} together.", Join(padNames));
                s.Text = (text ?? Loc.T("Two at once sound together.") + " ") + s.Prompt;
            }
            s.Hint = kb ? Loc.F("On the keyboard, press {0}.", And(keys)) : DrumDeviceHint(pads, five);
            return s;
        }

        static bool _pro;

        static string PadButton(int pad, bool five)
        {
            var gm = five ? GameMode.FiveLaneDrums : GameMode.FourLaneDrums;
            var names = new List<string>();
            for (int a = 0; a <= 7; a++)
            {
                if (DrumSound.PadOfAction((DrumsAction) a, five, _pro) != pad) continue;
                var b = Btn(gm, a);
                if (b != null && !names.Contains(b)) names.Add(b);
            }
            if (names.Count == 0) return null;
            string all = names[0];
            for (int i = 1; i < names.Count; i++) all = Loc.F("{0} or {1}", all, names[i]);
            return all;
        }

        static string DrumDeviceHint(int[] pads, bool five)
        {
            if (PracticeDevices.Device == null) return null;
            var names = new List<string>();
            foreach (var p in pads) names.Add(PadButton(p, five));
            string all = AllOf(names);
            if (all == null) return null;
            bool press = PracticeDevices.Device is Gamepad;
            if (pads.Length > 1) return OnDevice(press ? Loc.F("press {0} together.", all) : Loc.F("hit {0} together.", all));
            return OnDevice(press ? Loc.F("press {0}.", all) : Loc.F("hit {0}.", all));
        }

        // Set by the tutorials for a kit the player said has no cymbals: four-lane steps, no cymbals.
        public static bool NoCymbals;

        static void Drums()
        {
            bool five, pro;
            DrumButtonTest.PracticeKit(out five, out pro);
            if (NoCymbals && !five) pro = false;
            _pro = pro;
            bool kb = PracticeDevices.Device == null && DrumButtonTest.KeyboardKeys && !five;
            int count = five ? 6 : pro ? 8 : 5;
            _steps.Add(DrumStep(new[] { 0 }, 0.8f, five, kb, null));
            for (int p = 1; p < count; p++) _steps.Add(DrumStep(new[] { p }, 0.8f, five, kb, null));
            _steps.Add(DrumStep(new[] { 0, 1 }, 0.8f, five, kb, null));
            var ghost = DrumStep(new[] { 1 }, 0.35f, five, kb, Loc.T("A ghost note on the Red pad: it plays softly, with a whisper. On a kit, hit it gently. Hit the Red pad."));
            var ghostPad = ghost.Sound;
            ghost.Sound = () => { ghostPad(); CueBank.PlayDrumMark(true, DrumSound.PanOf(1, five), 0.9f); };
            _steps.Add(ghost);
            var accent = DrumStep(new[] { 1 }, 1f, five, kb, Loc.T("An accent on the Red pad: it plays loudly, with a sharp crack. On a kit, hit it hard. Hit the Red pad."));
            var accentPad = accent.Sound;
            accent.Sound = () => { accentPad(); CueBank.PlayDrumMark(false, DrumSound.PanOf(1, five), 0.9f); };
            _steps.Add(accent);
            // A flam: two pads a split second apart (under 44 ms in a chart); both play, one just after the other.
            var flam = DrumStep(new[] { 1, 2 }, 0.75f, five, kb, Loc.T("A flam: two pads a split second apart, the second right behind the first. Both pads play.") + " ");
            flam.Sound = () =>
            {
                int serial = _serial;
                CueBank.PlayDrum(DrumSound.SoundOf(1, five), DrumSound.PanOf(1, five), 0.75f);
                Later(0.035f, () => { if (serial == _serial) CueBank.PlayDrum(DrumSound.SoundOf(2, five), DrumSound.PanOf(2, five), 0.75f); });
            };
            _steps.Add(flam);
            _steps.Add(RollStep(new[] { 1 }, five, kb));
            _steps.Add(RollStep(new[] { 2, 3 }, five, kb));
            var star = DrumStep(new[] { 2 }, 0.8f, five, kb, null);
            var plain = star.Sound;
            star.Sound = () => { plain(); CueBank.Play("spnote", 0.8f); };
            star.Text = Loc.T("A star power note: the sparkle marks it. Hit every note of a star power phrase to fill the star power bar.") + " " + star.Prompt;
            _steps.Add(star);
            _steps.Add(new Step
            {
                Lead = 0.3,
                Listen = true,
                Sound = () => CueBank.PlayFill(1.5, 0.8f),
                Text = Loc.T("On drums star power turns on in a fill, which comes only while star power is ready. A slow rise plays through the fill and ends on its last note: hit that note to turn star power on. Listen.")
            });
            if (!ModSettings.ListenAndRepeat) return;
            _steps.Add(DrumPattern(new[] { 0.0, 1, 2, 3 }, new[] { 0, 1, 0, 1 }, five, true, kb));
            _steps.Add(DrumPattern(new[] { 0.0, 0.5, 1, 2 }, new[] { 0, 0, 1, 2 }, five, false, kb));
        }

        static Step DrumPattern(double[] beats, int[] pads, bool five, bool first, bool kb)
        {
            var s = DrumPatternStep(beats, pads, five, first);
            if (kb) s.Hint = Loc.F("On the keyboard: {0}.", string.Join(", ", Array.ConvertAll(pads, p => DrumKeys[Math.Min(p, DrumKeys.Length - 1)])));
            else if (PracticeDevices.Device != null)
            {
                var names = new List<string>(Array.ConvertAll(pads, p => PadButton(p, five)));
                if (!names.Contains(null)) s.Hint = OnDevice(string.Join(", ", names.ToArray()) + ".");
            }
            return s;
        }

        static Step DrumPatternStep(double[] beats, int[] pads, bool five, bool first)
        {
            var names = five ? DrumButtonTest.FiveNames : DrumButtonTest.FourNames;
            return PatternStep(beats, Array.ConvertAll(pads, p => "pad:" + p), Array.ConvertAll(pads, p => p == 0 ? Loc.T("kick") : DrumButtonTest.Said(five, p)),
                i => CueBank.PlayDrum(DrumSound.SoundOf(pads[i], five), DrumSound.PanOf(pads[i], five), 0.8f),
                i => CueBank.PlayDrumHit(DrumSound.SoundOf(pads[i], five), DrumSound.PanOf(pads[i], five), 0.9f), first);
        }

        static string KeyName(Key k)
        {
            var s = k.ToString();
            if (s.StartsWith("Digit", StringComparison.Ordinal)) return s.Substring(5);
            if (k == Key.RightBracket) return Loc.T("right bracket");
            if (k == Key.LeftBracket) return Loc.T("left bracket");
            return s;
        }

        static Step KeyStep(int[] keys, bool held, bool star, bool kb, string text)
        {
            double lead = _lr ? 0 : ModSettings.LookAheadSeconds;
            var s = new Step { Lead = lead, Riser = !_lr, Length = held ? 1.2 : 0, Call = _lr, Click = _lr };
            s.Sound = () =>
            {
                foreach (var k in keys)
                {
                    CueBank.PlayKey(ProKeysSound.MidiOf(k), ProKeysSound.PanOf(k), 0.9f);
                    if (held)
                    {
                        Hold(CueBank.StartKeyHold(ProKeysSound.MidiOf(k), ProKeysSound.PanOf(k)), lead + 1.2);
                        Later((float) (lead + 1.2), () => CueBank.Play("release", 0.5f));
                    }
                }
                if (star) CueBank.Play("spnote", 0.8f);
            };
            s.Hit = () => { foreach (var k in keys) CueBank.PlayKeyHit(ProKeysSound.MidiOf(k), ProKeysSound.PanOf(k), 0.8f); };
            var names = new List<string>();
            var hints = new List<string>();
            foreach (var k in keys)
            {
                names.Add(ProKeysSound.NameOf(k));
                hints.Add(k < 5 ? KeyName(new[] { Key.Z, Key.S, Key.X, Key.D, Key.C }[k]) : KeyName(KeysButtonTest.Upper[k - 5]));
            }
            var specs = Array.ConvertAll(keys, k => "key:" + k + "|pc:" + (k % 12));
            if (keys.Length == 1)
            {
                s.Any = specs;
                s.Prompt = Loc.F("Press {0}.", names[0]);
                s.Text = (text ?? char.ToUpperInvariant(names[0][0]) + names[0].Substring(1) + ". ") + s.Prompt;
            }
            else
            {
                s.All = specs;
                s.Prompt = Loc.F("Press {0} together.", Join(names));
                s.Text = (text ?? Loc.F("A chord: {0}, sounding together.", Join(names)) + " ") + s.Prompt;
            }
            if (kb) s.Hint = Loc.F("On the computer keyboard, press {0}.", And(hints));
            else
            {
                var bound = new List<string>(Array.ConvertAll(keys, k => Btn(GameMode.ProKeys, k)));
                string all = PracticeDevices.Device == null ? null : AllOf(bound);
                s.Hint = all == null ? null : OnDevice(keys.Length > 1 ? Loc.F("press {0} together.", all) : Loc.F("press {0}.", all));
            }
            return s;
        }

        static void ProKeys()
        {
            bool kb = PracticeDevices.Device == null && KeysButtonTest.KeyboardKeys;
            var midis = new HashSet<int>();
            for (int k = 0; k <= 24; k++) midis.Add(ProKeysSound.MidiOf(k));
            CueBank.PrepareKeys(midis);
            _steps.Add(KeyStep(new[] { 0 }, false, false, kb, Loc.T("Low C, the lowest key, on the left. Each key plays its own pitch.") + " "));
            _steps.Add(KeyStep(new[] { 4 }, false, false, kb, null));
            _steps.Add(KeyStep(new[] { 7 }, false, false, kb, null));
            _steps.Add(KeyStep(new[] { 1 }, false, false, kb, Loc.T("A black key, C sharp, between C and D.") + " "));
            _steps.Add(KeyStep(new[] { 12 }, false, false, kb, Loc.T("Middle C, in the centre, an octave above low C.") + " "));
            _steps.Add(KeyStep(new[] { 24 }, false, false, kb, Loc.T("High C, the highest key, on the right.") + " "));
            _steps.Add(KeyStep(new[] { 0, 4, 7 }, false, false, kb, null));
            _steps.Add(KeyStep(new[] { 12 }, true, false, kb, Loc.T("A held note: its tone lasts, then the Let go chimes play.") + " " + (_lr ? Loc.T("When you play it back, hold the key as long as the tone lasted.") : Loc.T("In a song keep the key down until the chimes.")) + " "));
            _steps.Add(KeyStep(new[] { 7 }, false, true, kb, Loc.T("A star power note: the sparkle marks it. Hit every note of a star power phrase to fill the star power bar.") + " "));
            _steps.Add(StarStep(kb ? Loc.T("On the keyboard, press Backspace.") : StarHint(GameMode.ProKeys, 25) ?? KeyboardStill(Loc.T("Backspace"))));
            if (!ModSettings.ListenAndRepeat) return;
            _steps.Add(KeyPattern(new[] { 0.0, 1, 2, 3 }, new[] { 0, 4, 7, 12 }, true));
            _steps.Add(KeyPattern(new[] { 0.0, 0.5, 1, 2 }, new[] { 12, 7, 4, 0 }, false));
        }

        static Step KeyPattern(double[] beats, int[] keys, bool first)
        {
            return PatternStep(beats, Array.ConvertAll(keys, k => "key:" + k + "|pc:" + (k % 12)), Array.ConvertAll(keys, k => ProKeysSound.NameOf(k)),
                i => CueBank.PlayKey(ProKeysSound.MidiOf(keys[i]), ProKeysSound.PanOf(keys[i]), 0.9f),
                i => CueBank.PlayKeyHit(ProKeysSound.MidiOf(keys[i]), ProKeysSound.PanOf(keys[i]), 0.8f), first);
        }

        static void Vocals()
        {
            bool kb = PracticeDevices.Device == null && VocalButtonTest.KeyboardKeys;
            bool mic = VocalButtonTest.HasMic;
            CueBank.PrepareGuides(new[] { 57, 60, 62, 64, 65, 67 });
            CueBank.PrepareKeys(new HashSet<int> { 60, 62, 64, 65, 67 });
            _steps.Add(new Step
            {
                Lead = 0.1,
                Sing = 60,
                Listen = !mic,
                Sound = () => CueBank.PlayGuide(60, 0.8f),
                Hit = () => CueBank.PlayHit(3, 0.8f),
                Prompt = Loc.T("Sing the pitch of the tone."),
                Text = Loc.T("The Pitch Guide: a soft tone at the pitch to sing, just before the word starts. Any octave counts.") + " "
                    + (mic ? Loc.T("Sing the pitch of the tone and hold it.") : Loc.T("Listen; with a microphone you would sing it."))
            });
            _steps.Add(SungStep(new[] { 60, 62, 64, 65, 67 }, new[] { 0.0, 1, 2, 3, 4 }, new[] { 1.0, 1, 1, 1, 1.5 },
                Loc.T("A scale: five notes climbing, C, D, E, F, G. Each new pitch has its own Pitch Guide tone: sing each note and hold it until the next. "
                + "While you sing too far off, a rising sweep means sing higher and a falling sweep means sing lower."), mic));
            _steps.Add(SungStep(new[] { 64, 62, 60, 62, 64, 64, 64 }, new[] { 0.0, 1, 2, 3, 4, 5, 6 }, new[] { 1.0, 1, 1, 1, 1, 1, 2 },
                Loc.T("A short tune, the way a song moves: E, D, C, D, then E three times, the last one held."), mic));
            _steps.Add(new Step
            {
                Lead = 0.1,
                Talk = true,
                Listen = !mic,
                Sound = () => CueBank.Play("vocaltalk"),
                Hit = () => CueBank.PlayHit(3, 0.8f),
                Prompt = Loc.T("Say a word."),
                Text = Loc.T("Two soft breaths: these words are spoken, not sung, and score at any pitch.") + " " + (mic ? Loc.T("Say a word.") : Loc.T("Listen."))
            });
            _steps.Add(new Step
            {
                Lead = _lr ? 0 : ModSettings.DrumLookAheadSeconds,
                Call = _lr,
                Sound = () => CueBank.Play("vocalperc"),
                Hit = () => CueBank.PlayHit(3, 0.8f),
                Any = new[] { "hit", "mic" },
                Prompt = mic ? Loc.T("Press your Hit button or tap the microphone.") : Loc.T("Press your Hit button."),
                Text = _lr
                    ? (mic ? Loc.T("A percussion note, such as a tambourine: a woodblock knock. Play it back with your Hit button or tap the microphone.")
                        : Loc.T("A percussion note, such as a tambourine: a woodblock knock. Play it back with your Hit button."))
                    : (mic ? Loc.T("A percussion note, such as a tambourine: a woodblock knock plays just before it. Press your Hit button or tap the microphone.")
                        : Loc.T("A percussion note, such as a tambourine: a woodblock knock plays just before it. Press your Hit button.")),
                Hint = kb ? Loc.T("On the keyboard, press Backspace.") : StarHint(GameMode.Vocals, 1) ?? KeyboardStill(Loc.T("Backspace"))
            });
            _steps.Add(new Step
            {
                Lead = 0.1,
                Listen = true,
                Sound = () => { CueBank.PlayGuide(62, 0.8f); CueBank.Play("spnote", 0.8f); },
                Text = Loc.T("The sparkle with a tone means a star power phrase: sing it well to fill the star power bar. Listen.")
            });
            _steps.Add(StarStep(kb ? Loc.T("On the keyboard, press Space.") : StarHint(GameMode.Vocals, 2) ?? KeyboardStill(Loc.T("Space"))));
            if (!ModSettings.ListenAndRepeat) return;
            var hit = mic ? Loc.T("Hit or a tap on the microphone") : Loc.T("Hit");
            var h = Loc.T("Hit");
            var perc = PatternStep(new[] { 0.0, 1, 1.5, 2 }, new[] { "hit|mic", "hit|mic", "hit|mic", "hit|mic" }, new[] { h, h, h, h },
                i => CueBank.Play("vocalperc"), i => CueBank.PlayHit(3, 0.8f), true);
            perc.Text = Loc.F("Now a whole pattern, as in a song: Listen and Repeat works on percussion notes, and you sing the rest of the song as usual. "
                + "After the listen sound, four knocks play. When the your-turn sound ends, play them back with {0}, in the same rhythm. First they play right, with the hit sounds.", hit);
            perc.Prompt = Loc.T("four knocks.");
            var hitName = kb ? Loc.T("Backspace") : Btn(GameMode.Vocals, 1);
            if (hitName != null) perc.Hint = kb ? Loc.F("On the keyboard, press {0} for each knock.", hitName) : Loc.F("On your {0}, press {1} for each knock.", PracticeDevices.DeviceKind, hitName);
            _steps.Add(perc);
        }
    }
}
