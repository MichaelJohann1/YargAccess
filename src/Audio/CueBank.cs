using System;
using System.Collections.Generic;
using MelonLoader;
using YARG.Core.Audio;

namespace YargAccess.Audio
{
    public static class CueBank
    {
        public class Cue
        {
            public string Id;
            public string Name;
            public string Meaning;
            public float Pan;
            public int Sample;
            public float Volume = 1f;
            public SfxSample? Yarg;
            public string Group = "fivefret";
        }

        public static float MasterVolume = 1f;

        public static bool Mirror;
        static float FretPan(int fret) => Mirror ? -Get(FretIds[fret]).Pan : Get(FretIds[fret]).Pan;

        public static readonly List<Cue> All = new List<Cue>
        {
            new Cue { Id = "green",  Name = Loc.T("Green note"),  Meaning = Loc.T("a note on the green fret, far left, the lowest"), Pan = -0.9f },
            new Cue { Id = "red",    Name = Loc.T("Red note"),    Meaning = Loc.T("a note on the red fret, left"),       Pan = -0.45f },
            new Cue { Id = "yellow", Name = Loc.T("Yellow note"), Meaning = Loc.T("a note on the yellow fret, centre"),  Pan = 0f },
            new Cue { Id = "blue",   Name = Loc.T("Blue note"),   Meaning = Loc.T("a note on the blue fret, right"),     Pan = 0.45f },
            new Cue { Id = "orange", Name = Loc.T("Orange note"), Meaning = Loc.T("a note on the orange fret, far right, the highest"), Pan = 0.9f },
            new Cue { Id = "open",   Name = Loc.T("Open note"),   Meaning = Loc.T("strum with no fret held, a low note in the centre"), Pan = 0f },
            new Cue { Id = "chord", Name = Loc.T("Chord"), Pan = 0f,
                      Meaning = Loc.T("plays with a chord's notes: hold all of them together and strum once") },
            new Cue { Id = "hammeron", Name = Loc.T("Hammer-on"), Pan = 0f,
                      Meaning = Loc.T("press the higher fret without strumming, right after a note you hit") },
            new Cue { Id = "pulloff", Name = Loc.T("Pull-off"), Pan = 0f,
                      Meaning = Loc.T("lift to the lower fret without strumming, right after a note you hit") },
            new Cue { Id = "tap", Name = Loc.T("Tap note"), Pan = 0f,
                      Meaning = Loc.T("press the fret, no strum, even after a miss") },
            new Cue { Id = "sweep", Name = Loc.T("Hit bar"), Pan = 0f,
                      Meaning = Loc.T("runs from a note's sound to the moment the note reaches the hit bar: hit it as this ends") },
            new Cue { Id = "click", Name = Loc.T("Hit bar, fast passages"), Pan = 0f,
                      Meaning = Loc.T("marks each moment a note reaches the hit bar when notes are too close for the full hit bar sound: strum on each one") },
            new Cue { Id = "beat", Name = Loc.T("Beat"), Pan = 0f,
                      Meaning = Loc.T("a beat of the song, like a beat line crossing the highway: keep time with it") },
            new Cue { Id = "measure", Name = Loc.T("Bar"), Pan = 0f,
                      Meaning = Loc.T("the first beat of a bar") },
            new Cue { Id = "release", Name = Loc.T("Let go"), Pan = 0f,
                      Meaning = Loc.T("the end of a held note: the hold is complete, let go now or any time after") },
            new Cue { Id = "spstart", Name = Loc.T("Star power phrase start"), Pan = 0f,
                      Meaning = Loc.T("the first note of the star power phrase that makes star power ready: hit every note of it and you can activate star power") },
            new Cue { Id = "spnote", Name = Loc.T("Star power note"), Pan = 0f,
                      Meaning = Loc.T("a note in a star power phrase: hit every note of the phrase to fill a quarter of the bar") },
            new Cue { Id = "splevel", Name = Loc.T("Star power level"), Pan = 0f,
                      Meaning = Loc.T("after a phrase is complete, one to four steps say how many quarters of the bar are full. Two or more: you can activate star power") },
            new Cue { Id = "spready", Name = Loc.T("Star power ready"), Pan = 0f,
                      Meaning = Loc.T("a bright sparkle over a shimmer: star power can be turned on now, and stays ready until you use it. Guitar, bass, keys and vocals: press your star power button. Drums: hit the last note of the next fill") },
            new Cue { Id = "splost", Name = Loc.T("Star power phrase lost"), Pan = 0f,
                      Meaning = Loc.T("you missed a note of a star power phrase, so this phrase gives nothing") },
            new Cue { Id = "hit", Name = Loc.T("[sound] Hit"), Pan = 0f, Meaning = Loc.T("you hit a note; it plays in that fret's or pad's place, an octave above its note") },
            new Cue { Id = "miss", Name = Loc.T("Miss"), Pan = 0f, Meaning = Loc.T("you missed a note") },
            new Cue { Id = "overstrum", Name = Loc.T("Overstrum"), Pan = 0f, Meaning = Loc.T("you strummed when there was no note to hit") },
            new Cue { Id = "dropped", Name = Loc.T("Hold dropped"), Pan = 0f, Meaning = Loc.T("you let go of a held note too early") },
            new Cue { Id = "listen", Name = Loc.T("Listen"), Pan = 0f,
                      Meaning = Loc.T("Listen and Repeat: a pattern of notes starts now. Listen, don't play") },
            new Cue { Id = "yourturn", Name = Loc.T("Your turn"), Pan = 0f,
                      Meaning = Loc.T("Listen and Repeat: ends where your turn starts. Play the pattern back in the same rhythm") },
            new Cue { Id = "kick", Name = Loc.T("Kick"), Group = "drums", Pan = 0f,
                      Meaning = Loc.T("the lowest note, in the centre: the kick pedal. Every drum mode") },
            new Cue { Id = "padred", Name = Loc.T("Red pad"), Group = "drums", Pan = -0.75f,
                      Meaning = Loc.T("the key's root, far left: the red pad, the snare. Every drum mode") },
            new Cue { Id = "padyellow", Name = Loc.T("Yellow pad"), Group = "drums", Pan = -0.25f,
                      Meaning = Loc.T("the second note up, left of centre: the yellow pad, the high tom. Four-lane and pro drums; on five-lane yellow is a cymbal") },
            new Cue { Id = "padblue", Name = Loc.T("Blue pad"), Group = "drums", Pan = 0.25f,
                      Meaning = Loc.T("the third note up, right of centre: the blue pad, the middle tom. Every drum mode") },
            new Cue { Id = "padgreen", Name = Loc.T("Green pad"), Group = "drums", Pan = 0.75f,
                      Meaning = Loc.T("the fourth note up, far right: the green pad, the low tom. Four-lane and pro drums") },
            new Cue { Id = "padgreen5", Name = Loc.T("Five-lane green pad"), Group = "drums", Pan = 0.8f,
                      Meaning = Loc.T("the fifth note up, far right: the green pad. Five-lane drums only") },
            new Cue { Id = "drumroll", Name = Loc.T("Roll"), Group = "drums", Pan = -0.75f,
                      Meaning = Loc.T("a fast stream of one pad's sound, in its lane: a drum roll. Keep hitting that pad, fast and steady, until it stops; the exact timing does not matter. Expert, and Hard on some charts") },
            new Cue { Id = "drumswell", Name = Loc.T("Swell"), Group = "drums", Pan = 0f,
                      Meaning = Loc.T("two pads' sounds alternating fast, each in its lane: a swell. Keep hitting the two pads in turn until it stops; the exact timing does not matter. Expert, and Hard on some charts") },
            new Cue { Id = "drumfill", Name = Loc.T("Fill"), Group = "drums", Pan = 0f,
                      Meaning = Loc.T("a slow rise while a fill runs, only when star power can be turned on: play anything, then hit the fill's last note to turn star power on") },
            new Cue { Id = "drumactivator", Name = Loc.T("Star power hit"), Group = "drums", Pan = 0f,
                      Meaning = Loc.T("with the last note of a fill, when star power can be turned on: hitting this note turns star power on") },
            new Cue { Id = "drumghost", Name = Loc.T("Ghost note"), Group = "drums", Pan = -0.75f,
                      Meaning = Loc.T("a whisper with the pad played softly, in its lane: a ghost note. On a kit, hit it gently") },
            new Cue { Id = "drumaccent", Name = Loc.T("Accent"), Group = "drums", Pan = -0.75f,
                      Meaning = Loc.T("a sharp crack with the pad played loudly, in its lane: an accent. On a kit, hit it hard") },
            new Cue { Id = "cymyellow", Name = Loc.T("Yellow cymbal"), Group = "prodrums", Pan = -0.25f,
                      Meaning = Loc.T("the yellow pad's note an octave up, with a shimmer: the yellow cymbal, the hi-hat. Pro drums and five-lane; plain four-lane drums have no cymbals") },
            new Cue { Id = "cymblue", Name = Loc.T("Blue cymbal"), Group = "prodrums", Pan = 0.25f,
                      Meaning = Loc.T("the blue pad's note an octave up, with a shimmer: the blue cymbal, the ride. Pro drums only") },
            new Cue { Id = "cymgreen", Name = Loc.T("Green cymbal"), Group = "prodrums", Pan = 0.75f,
                      Meaning = Loc.T("the green pad's note an octave up, with a shimmer: the green cymbal, the crash. Pro drums, and the orange cymbal on five-lane; plain four-lane drums have no cymbals") },
            new Cue { Id = "vocalguide", Name = Loc.T("Pitch guide"), Group = "vocals", Pan = 0f,
                      Meaning = Loc.T("a soft tone at the pitch of the next note, just before you sing it. Sing that note, in any octave") },
            new Cue { Id = "pitchup", Name = Loc.T("Sing higher"), Group = "vocals", Pan = 0f,
                      Meaning = Loc.T("a rising sweep while you sing: you are too low to score, go up until it stops") },
            new Cue { Id = "pitchdown", Name = Loc.T("Sing lower"), Group = "vocals", Pan = 0f,
                      Meaning = Loc.T("a falling sweep while you sing: you are too high to score, go down until it stops") },
            new Cue { Id = "vocalperc", Name = Loc.T("Percussion"), Group = "vocals", Pan = 0f,
                      Meaning = Loc.T("a hard double knock: a percussion note, such as a tambourine or cowbell hit. Press your Hit button as soon as you hear it") },
            new Cue { Id = "vocaltalk", Name = Loc.T("Spoken words"), Group = "vocals", Pan = 0f,
                      Meaning = Loc.T("two soft breaths: the next words are spoken, not sung. Any pitch scores, so say them in time with the track") },
            new Cue { Id = "keynote", Name = Loc.T("Key"), Group = "prokeys", Pan = 0f,
                      Meaning = Loc.T("a key to press: its own pitch, two octaves of the keyboard from low C on the left to high C on the right. Held notes keep sounding until the Let go chimes") },
            new Cue { Id = "keygliss", Name = Loc.T("Glissando"), Group = "prokeys", Pan = 0f,
                      Meaning = Loc.T("a quick run up or down the white keys: sweep your hand across the keys in that direction until it ends; any key counts") },
            new Cue { Id = "yarg_spaward",Name = Loc.T("YARG star power gained"), Pan = 0f, Yarg = SfxSample.StarPowerAward,
                      Meaning = Loc.T("YARG's own sound: a star power phrase is complete") },
            new Cue { Id = "yarg_spready", Name = Loc.T("YARG star power ready"), Pan = 0f, Yarg = SfxSample.StarPowerReady,
                      Meaning = Loc.T("YARG's own sound: the bar is half full, star power can be activated") },
            new Cue { Id = "yarg_spdeploy", Name = Loc.T("YARG star power on"), Pan = 0f, Yarg = SfxSample.StarPowerDeploy,
                      Meaning = Loc.T("YARG's own sound: star power is activated") },
            new Cue { Id = "yarg_sprelease", Name = Loc.T("YARG star power over"), Pan = 0f, Yarg = SfxSample.StarPowerRelease,
                      Meaning = Loc.T("YARG's own sound: star power has run out") },
            new Cue { Id = "yarg_miss", Name = Loc.T("YARG miss"), Pan = 0f, Yarg = SfxSample.NoteMiss,
                      Meaning = Loc.T("YARG's own sound: a missed note broke a streak of ten or more") },
            new Cue { Id = "yarg_star", Name = Loc.T("YARG star earned"), Pan = 0f, Yarg = SfxSample.StarGain,
                      Meaning = Loc.T("YARG's own sound: your score reached another star") },
        };

        public enum Kind { Strum, HammerOn, PullOff, Tap }

        static readonly string[] FretIds = { "open", "green", "red", "yellow", "blue", "orange" };
        static readonly int[] _strike = new int[6], _hammer = new int[6], _pull = new int[6], _tap = new int[6],
            _hold = new int[6], _hit = new int[6];
        static readonly int[] _level = new int[5];
        static readonly Dictionary<int, int> _risers = new Dictionary<int, int>();
        static readonly Dictionary<string, int> _drumHits = new Dictionary<string, int>();

        static readonly int[] MajorSteps = { 0, 2, 4, 7, 9 };
        static readonly int[] MinorSteps = { 0, 3, 5, 7, 10 };

        static bool _loaded;
        static int _tonic = 9;
        static bool _minor = true;
        public static int Generation { get; private set; }

        public static Cue Get(string id)
        {
            foreach (var c in All) if (c.Id == id) return c;
            return null;
        }

        // Note and pad sounds are always in the song key's pentatonic scale; with no key, A minor
        // (the unpitched Tone Style was removed, user 2026-11-14).
        public static void SetKey(int tonic, bool minor)
        {
            if (_loaded && tonic == _tonic && minor == _minor) return;
            _tonic = tonic; _minor = minor;
            FreeAll();
        }

        public static bool EnsureLoaded()
        {
            if (_loaded) return true;
            if (!CuePlayer.Ready) return false;
            try
            {
                var steps = _minor ? MinorSteps : MajorSteps;
                int green = 69 + ((_tonic - 9 + 12) % 12);
                double top = Synth.MidiToHz(green + 12);
                for (int f = 0; f <= 5; f++)
                {
                    float[] strike, hammer, pull, tap, hold;
                    int midi = f == 0 ? green - 12 : green + steps[f - 1];
                    double hz = Synth.MidiToHz(midi);
                    strike = Synth.Tone(hz, 0.30, 0.002, 0.10, 0.8, 0.5);
                    hammer = Synth.Slur(hz / 2, hz, 0.32, 0.10, 0.004, 0.10, 0.3, 0.5);
                    pull = Synth.Slur(hz * 2, hz, 0.32, 0.10, 0.004, 0.10, 0.3, 0.5);
                    tap = Synth.Overlay(TapKnock(f), Synth.Tone(hz, 0.28, 0.004, 0.10, 0.5, 0.42), 0.05);
                    hold = Synth.Loop(hz, 0.5, 0.2);
                    var hit = Synth.Overlay(Synth.Tone(hz * 2, 0.22, 0.001, 0.07, 0.7, 0.62),
                                            Synth.Noise(5000, 0.02, 0.0003, 0.004, 0.3, 45 + f), 0);
                    _strike[f] = CuePlayer.Load(strike);
                    _hammer[f] = CuePlayer.Load(hammer);
                    _pull[f] = CuePlayer.Load(pull);
                    _tap[f] = CuePlayer.Load(tap);
                    _hold[f] = CuePlayer.Load(hold, loop: true);
                    _hit[f] = CuePlayer.Load(hit);
                    Get(FretIds[f]).Sample = CuePlayer.Load(Synth.HeldPreview(strike, hold, 0.7));
                }
                double[] rake = { 1500, 2200, 3200, 4600, 6500 };
                float[] chord = new float[0];
                for (int k = 0; k < rake.Length; k++)
                    chord = Synth.Overlay(chord, Synth.Noise(rake[k], 0.04, 0.0005, 0.01, 1.0, 51 + k), 0.012 * k);
                Get("chord").Sample = CuePlayer.Load(Synth.Level(chord, 0.55));
                Get("click").Sample = CuePlayer.Load(Synth.Noise(1200, 0.04, 0.0005, 0.008, 0.5, 52));
                Get("beat").Sample = CuePlayer.Load(Synth.Noise(650, 0.05, 0.0005, 0.012, 0.3, 63));
                Get("measure").Sample = CuePlayer.Load(Synth.Overlay(Synth.Noise(650, 0.06, 0.0005, 0.015, 0.45, 64),
                                                                     Synth.Noise(1300, 0.03, 0.0005, 0.008, 0.2, 65), 0));
                Get("miss").Sample = CuePlayer.Load(Synth.Overlay(Synth.Glide(240, 70, 0.32, "thud", 0.85),
                                                                  Synth.Noise(400, 0.12, 0.001, 0.04, 0.35, 59), 0));
                Get("overstrum").Sample = CuePlayer.Load(Synth.Glide(260, 200, 0.07, "thud", 0.45));
                Get("hammeron").Sample = _hammer[3];
                Get("pulloff").Sample = _pull[3];
                Get("tap").Sample = _tap[3];
                Get("hit").Sample = _hit[3];
                Get("release").Sample = CuePlayer.Load(Synth.Sequence(new[] { top * 1.5, top }, 0.07, 0.16, 0.6, 0.5));
                Get("dropped").Sample = CuePlayer.Load(Synth.Sequence(new[] { top / 4 * 1.5, top / 4 }, 0.09, 0.14, 0.9, 0.5));
                Get("listen").Sample = CuePlayer.Load(Synth.NoiseSlide(2400, 500, 0.4, 0.4, 0.05, 0.06, 0.35, 171));
                Get("yourturn").Sample = CuePlayer.Load(Synth.NoiseRise(500, 3200, 0.4, 0.35, 172));
                Get("spnote").Sample = CuePlayer.Load(Synth.Sequence(new[] { top * 2, top * 3 }, 0.03, 0.10, 0, 0.22));
                Get("spstart").Sample = CuePlayer.Load(Synth.Sequence(new[] { top, top * 1.5, top * 2, top * 3 }, 0.045, 0.14, 0.3, 0.35));
                Get("splost").Sample = CuePlayer.Load(Synth.Glide(top * 2, top / 2, 0.35, "fall", 0.4));
                var sparkle = Synth.Sequence(new[] { top * 2, top * 3, top * 4, top * 6 }, 0.04, 0.12, 0, 0.3);
                var shimmer = Synth.Noise(8000, 0.6, 0.02, 0.3, 0.22, 147);
                Get("spready").Sample = CuePlayer.Load(Synth.Level(Synth.Overlay(shimmer, sparkle, 0.02), 0.5));
                double[] up = { top, top * Math.Pow(2, steps[2] / 12.0), top * Math.Pow(2, steps[3] / 12.0), top * 2 };
                for (int n = 1; n <= 4; n++)
                {
                    var hz = new double[n];
                    Array.Copy(up, hz, n);
                    _level[n] = CuePlayer.Load(Synth.Sequence(hz, 0.09, 0.12, 0.3, 0.4));
                }
                Get("splevel").Sample = _level[4];
                BuildDrums(green, steps);
                Get("keynote").Sample = CuePlayer.Load(Synth.HeldPreview(KeyStrike(60), KeyHold(60), 0.7));
                Get("keygliss").Sample = CuePlayer.Load(Gliss(60, 84, 0.4));
                Get("vocalguide").Sample = CuePlayer.Load(Guide(green - 12));
                Get("pitchup").Sample = CuePlayer.Load(Synth.NoiseSlide(500, 2600, 0.2, 0.16, 0.004, 0.03, 0.5, 141));
                Get("pitchdown").Sample = CuePlayer.Load(Synth.NoiseSlide(2600, 500, 0.2, 0.16, 0.004, 0.03, 0.5, 142));
                var knock = Synth.Overlay(Synth.Overlay(Synth.Noise(450, 0.09, 0.0005, 0.03, 0.7, 148),
                                                        Synth.Noise(1900, 0.08, 0.0005, 0.02, 0.9, 143), 0),
                                          Synth.Noise(5000, 0.04, 0.0005, 0.01, 0.45, 144), 0);
                Get("vocalperc").Sample = CuePlayer.Load(Synth.Level(Synth.Overlay(knock, Synth.Noise(2600, 0.07, 0.0005, 0.02, 0.8, 149), 0.06), 0.95));
                Get("vocaltalk").Sample = CuePlayer.Load(Synth.Overlay(Synth.Noise(800, 0.12, 0.03, 0.06, 0.35, 145),
                                                                       Synth.Noise(1300, 0.12, 0.03, 0.06, 0.35, 146), 0.15));
                // Ghost note: a breathy whisper. Accent: a short bright crack on top of the pad.
                Get("drumghost").Sample = CuePlayer.Load(Synth.Level(Synth.Overlay(Synth.Noise(3200, 0.2, 0.03, 0.07, 0.5, 151),
                                                                                  Synth.Noise(6500, 0.18, 0.03, 0.06, 0.35, 152), 0), 0.45));
                Get("drumaccent").Sample = CuePlayer.Load(Synth.Level(Synth.Overlay(Synth.Overlay(Synth.Noise(2400, 0.07, 0.0005, 0.018, 0.9, 153),
                                                                                                  Synth.Noise(7000, 0.05, 0.0005, 0.012, 0.7, 154), 0),
                                                                                    Synth.Tone(1760, 0.06, 0.0005, 0.015, 0.9, 0.5), 0), 0.9));
                _loaded = true;
                EnsureRiser(0.3);
                MelonLogger.Msg("CueBank: made the cue sounds, " + KeyDetect.Names[_tonic] + (_minor ? " minor" : " major") + " pentatonic");
            }
            catch (Exception ex) { ModLog.Warn("CueBank.EnsureLoaded", ex); _loaded = true; }
            return true;
        }

        static void BuildDrums(int root, int[] steps)
        {
            Get("kick").Sample = CuePlayer.Load(Pad(root - 12, 0.9));
            _drumHits["kick"] = CuePlayer.Load(Hit(root - 12, 120));
            _padHz["kick"] = Synth.MidiToHz(root - 12);
            string[] pads = { "padred", "padyellow", "padblue", "padgreen", "padgreen5" };
            for (int i = 0; i < pads.Length; i++)
            {
                Get(pads[i]).Sample = CuePlayer.Load(Pad(root + steps[i], 0.8));
                _drumHits[pads[i]] = CuePlayer.Load(Hit(root + steps[i], 121 + i));
                _padHz[pads[i]] = Synth.MidiToHz(root + steps[i]);
            }
            string[] cyms = { "cymyellow", "cymblue", "cymgreen" };
            for (int i = 0; i < cyms.Length; i++)
            {
                var shimmer = Synth.Noise(7000, 0.12, 0.001, 0.04, 0.12, 110 + i);
                var body = Synth.Tone(Synth.MidiToHz(root + steps[i + 1] + 12), 0.4, 0.002, 0.14, 0.4, 0.42);
                Get(cyms[i]).Sample = CuePlayer.Load(Synth.Overlay(body, shimmer, 0));
                _drumHits[cyms[i]] = CuePlayer.Load(Hit(root + steps[i + 1] + 12, 126 + i));
                _padHz[cyms[i]] = Synth.MidiToHz(root + steps[i + 1] + 12);
            }
            Get("drumroll").Sample = CuePlayer.Load(Repeat(RollLoop("padred", false), 2));
            Get("drumswell").Sample = CuePlayer.Load(Repeat(Mix(RollLoop("padred", false), RollLoop("padblue", true)), 2));
            double top = Synth.MidiToHz(root + 12);
            Get("drumfill").Sample = CuePlayer.Load(FillRise(1.2));
            Get("drumactivator").Sample = CuePlayer.Load(Synth.Overlay(Synth.Sequence(new[] { top * 1.5, top * 2, top * 3 }, 0.035, 0.12, 0.3, 0.35),
                                                                     Synth.Glide(top, top * 4, 0.18, "rise", 0.25), 0.1));
        }

        static float[] TapKnock(int f)
        {
            var low = Synth.Overlay(Synth.Glide(1100, 600, 0.05, "fall", 0.5),
                                    Synth.Noise(2600, 0.03, 0.0003, 0.006, 0.45, 71 + f), 0);
            var high = Synth.Overlay(Synth.Glide(1500, 850, 0.05, "fall", 0.5),
                                     Synth.Noise(3400, 0.03, 0.0003, 0.006, 0.45, 77 + f), 0);
            return Synth.Overlay(low, high, 0.05);
        }

        static float[] Hit(int midi, int seed)
        {
            return Synth.Overlay(Synth.Tone(Synth.MidiToHz(midi) * 2, 0.22, 0.001, 0.07, 0.7, 0.62),
                                 Synth.Noise(5000, 0.02, 0.0003, 0.004, 0.3, seed), 0);
        }

        static float[] Guide(int midi) => Synth.Tone(Synth.MidiToHz(midi), 0.4, 0.015, 0.18, 0.2, 0.5);

        static readonly Dictionary<int, int> _guides = new Dictionary<int, int>();

        public static void PrepareGuides(IEnumerable<int> midis)
        {
            if (!EnsureLoaded()) return;
            foreach (int m in midis)
                if (!_guides.ContainsKey(m)) _guides[m] = CuePlayer.Load(Guide(m));
        }

        public static int PlayGuide(int midi, float volume)
        {
            if (!EnsureLoaded()) return 0;
            int sample;
            if (!_guides.TryGetValue(midi, out sample)) _guides[midi] = sample = CuePlayer.Load(Guide(midi));
            return CuePlayer.Play(sample, 0f, volume * Get("vocalguide").Volume * MasterVolume);
        }

        public static int PlayDrumHit(string padId, float pan, float volume)
        {
            int sample;
            if (!EnsureLoaded() || !_drumHits.TryGetValue(padId, out sample)) return 0;
            return CuePlayer.Play(sample, pan, volume * Get("hit").Volume * MasterVolume);
        }

        static float[] KeyStrike(int midi) => Synth.Tone(Synth.MidiToHz(midi), 0.32, 0.002, 0.11, 0.7, 0.5);
        static float[] KeyHold(int midi) => Synth.Loop(Synth.MidiToHz(midi), 0.5, 0.18);

        public static float[] Gliss(int fromMidi, int toMidi, double seconds)
        {
            var hz = new List<double>();
            int step = toMidi >= fromMidi ? 1 : -1;
            for (int m = fromMidi; step > 0 ? m <= toMidi : m >= toMidi; m += step)
            {
                int pc = ((m % 12) + 12) % 12;
                if (pc == 1 || pc == 3 || pc == 6 || pc == 8 || pc == 10) continue;
                hz.Add(Synth.MidiToHz(m));
            }
            if (hz.Count == 0) hz.Add(Synth.MidiToHz(fromMidi));
            double spacing = hz.Count > 1 ? seconds / (hz.Count - 1) : 0.05;
            return Synth.Sequence(hz.ToArray(), spacing, 0.12, 0.6, 0.4);
        }

        class KeySamples { public int Strike, Hold, Hit; }
        static readonly Dictionary<int, KeySamples> _keys = new Dictionary<int, KeySamples>();
        static readonly List<int> _glisses = new List<int>();

        static KeySamples KeyOf(int midi)
        {
            KeySamples k;
            if (!_keys.TryGetValue(midi, out k))
                _keys[midi] = k = new KeySamples { Strike = CuePlayer.Load(KeyStrike(midi)), Hold = CuePlayer.Load(KeyHold(midi), loop: true), Hit = CuePlayer.Load(Hit(midi, 140 + midi % 20)) };
            return k;
        }

        public static void PrepareKeys(IEnumerable<int> midis)
        {
            if (!EnsureLoaded()) return;
            foreach (int m in midis) KeyOf(m);
        }

        public static int PlayKey(int midi, float pan, float volume)
        {
            if (!EnsureLoaded()) return 0;
            return CuePlayer.Play(KeyOf(midi).Strike, pan, volume * Get("keynote").Volume * MasterVolume);
        }

        public static int StartKeyHold(int midi, float pan)
        {
            if (!EnsureLoaded()) return 0;
            return CuePlayer.PlayFadeIn(KeyOf(midi).Hold, pan, Get("keynote").Volume * MasterVolume, 60);
        }

        public static int PlayKeyHit(int midi, float pan, float volume)
        {
            if (!EnsureLoaded()) return 0;
            return CuePlayer.Play(KeyOf(midi).Hit, pan, volume * Get("hit").Volume * MasterVolume);
        }

        public static void FreeGlisses()
        {
            foreach (var g in _glisses) CuePlayer.Free(g);
            _glisses.Clear();
        }

        public static int LoadGliss(int fromMidi, int toMidi, double seconds)
        {
            if (!EnsureLoaded()) return 0;
            int s = CuePlayer.Load(Gliss(fromMidi, toMidi, seconds));
            _glisses.Add(s);
            return s;
        }

        public static int PlayGliss(int sample, float volume)
        {
            if (sample == 0) return 0;
            return CuePlayer.Play(sample, 0f, volume * Get("keygliss").Volume * MasterVolume);
        }

        static readonly Dictionary<string, double> _padHz = new Dictionary<string, double>();
        static readonly Dictionary<string, int> _rolls = new Dictionary<string, int>();
        static readonly Dictionary<int, int> _fillRises = new Dictionary<int, int>();

        static float[] RollLoop(string padId, bool late)
        {
            const double Period = 1.0 / 12;
            int n = CuePlayer.SampleRate / 2;
            var d = new float[n];
            double hz;
            if (!_padHz.TryGetValue(padId, out hz)) hz = 440;
            var strike = Synth.Tone(hz, 0.07, 0.001, 0.025, 0.6, 0.45);
            for (int k = 0; k < 6; k++)
            {
                int at = (int) ((k + (late ? 0.5 : 0)) * Period * CuePlayer.SampleRate);
                for (int i = 0; i < strike.Length; i++) d[(at + i) % n] += strike[i];
            }
            return d;
        }

        static float[] Repeat(float[] d, int times)
        {
            var r = new float[d.Length * times];
            for (int i = 0; i < r.Length; i++) r[i] = d[i % d.Length];
            return r;
        }

        static float[] Mix(float[] a, float[] b)
        {
            var r = new float[Math.Max(a.Length, b.Length)];
            for (int i = 0; i < r.Length; i++) r[i] = (i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f);
            return r;
        }

        public static int StartRoll(string padId, bool swell, bool late, float pan, float volume)
        {
            if (!EnsureLoaded()) return 0;
            string key = padId + (late ? "+" : "");
            int sample;
            if (!_rolls.TryGetValue(key, out sample)) _rolls[key] = sample = CuePlayer.Load(RollLoop(padId, late), loop: true);
            return CuePlayer.PlayFadeIn(sample, pan, volume * Get(swell ? "drumswell" : "drumroll").Volume * MasterVolume, 20);
        }

        static float[] FillRise(double seconds)
        {
            double top = Synth.MidiToHz(69 + ((_tonic - 9 + 12) % 12) + 12);
            return Synth.Overlay(Synth.Glide(top / 2, top, seconds, "rise", 0.25), Synth.NoiseRise(800, 3000, seconds, 0.08, 109), 0);
        }

        public static int PlayFill(double seconds, float volume)
        {
            if (!EnsureLoaded()) return 0;
            // 50 ms steps: each length is made once, not once per fill.
            int ms = (int) Math.Round(Math.Max(0.5, Math.Min(6.0, seconds)) * 20) * 50;
            int sample;
            if (!_fillRises.TryGetValue(ms, out sample)) _fillRises[ms] = sample = CuePlayer.Load(FillRise(ms / 1000.0));
            return CuePlayer.Play(sample, 0f, volume * Get("drumfill").Volume * MasterVolume);
        }

        static float[] Pad(int midi, double bright)
        {
            return Synth.Tone(Synth.MidiToHz(midi), 0.22, 0.002, 0.07, bright, 0.5);
        }

        // The ghost whisper or accent crack, in the pad's place.
        public static int PlayDrumMark(bool ghost, float pan, float volume)
        {
            if (!EnsureLoaded()) return 0;
            var c = Get(ghost ? "drumghost" : "drumaccent");
            if (c == null || c.Sample == 0) return 0;
            return CuePlayer.Play(c.Sample, pan, volume * c.Volume * MasterVolume);
        }

        public static int PlayDrum(string id, float pan, float volume)
        {
            if (!EnsureLoaded()) return 0;
            var c = Get(id);
            if (c == null || c.Sample == 0) return 0;
            return CuePlayer.Play(c.Sample, pan, volume * c.Volume * MasterVolume);
        }

        public static void EnsureRiser(double seconds)
        {
            if (!EnsureLoaded()) return;
            int ms = (int) Math.Round(seconds * 1000);
            int sample;
            if (!_risers.TryGetValue(ms, out sample))
            {
                double top = Synth.MidiToHz(69 + ((_tonic - 9 + 12) % 12) + 12);
                sample = CuePlayer.Load(Synth.Glide(top / 4, top, seconds, "rise", 0.3));
                _risers[ms] = sample;
            }
            Get("sweep").Sample = sample;
        }

        public static int PlayRiser(double seconds, float volume)
        {
            EnsureRiser(seconds);
            int sample;
            if (!_risers.TryGetValue((int) Math.Round(seconds * 1000), out sample)) return 0;
            return CuePlayer.Play(sample, 0f, volume * Get("sweep").Volume * MasterVolume);
        }

        static readonly HashSet<string> Shared = new HashSet<string> { "hit", "sweep", "hammeron", "pulloff", "tap", "splevel" };

        static void FreeAll()
        {
            for (int f = 0; f <= 5; f++)
            {
                CuePlayer.Free(_strike[f]); CuePlayer.Free(_hammer[f]); CuePlayer.Free(_pull[f]); CuePlayer.Free(_tap[f]);
                CuePlayer.Free(_hold[f]); CuePlayer.Free(_hit[f]);
                _strike[f] = _hammer[f] = _pull[f] = _tap[f] = _hold[f] = _hit[f] = 0;
            }
            for (int n = 1; n <= 4; n++) { CuePlayer.Free(_level[n]); _level[n] = 0; }
            foreach (var c in All) { if (!Shared.Contains(c.Id)) CuePlayer.Free(c.Sample); c.Sample = 0; }
            foreach (var r in _risers.Values) CuePlayer.Free(r);
            _risers.Clear();
            foreach (var h in _drumHits.Values) CuePlayer.Free(h);
            _drumHits.Clear();
            foreach (var h in _rolls.Values) CuePlayer.Free(h);
            _rolls.Clear();
            foreach (var h in _fillRises.Values) CuePlayer.Free(h);
            _fillRises.Clear();
            foreach (var k in _keys.Values) { CuePlayer.Free(k.Strike); CuePlayer.Free(k.Hold); CuePlayer.Free(k.Hit); }
            _keys.Clear();
            foreach (var g in _glisses) CuePlayer.Free(g);
            _glisses.Clear();
            Generation++;
            foreach (var g in _guides.Values) CuePlayer.Free(g);
            _guides.Clear();
            _loaded = false;
        }

        public static int Play(string id, float volume = 1f)
        {
            var c = Get(id);
            if (c == null) return 0;
            if (c.Yarg.HasValue)
            {
                try { GlobalAudioHandler.PlaySoundEffect(c.Yarg.Value); }
                catch (Exception ex) { ModLog.Warn("CueBank.Play " + id, ex); }
                return 0;
            }
            if (!EnsureLoaded() || c.Sample == 0) return 0;
            return CuePlayer.Play(c.Sample, c.Pan, volume * c.Volume * MasterVolume);
        }

        public static int PlayNote(int fret, Kind kind)
        {
            if (!EnsureLoaded()) return 0;
            var c = Get(FretIds[fret]);
            int sample = _strike[fret];
            float kindVolume = 1f;
            switch (kind)
            {
                case Kind.HammerOn: sample = _hammer[fret]; kindVolume = Get("hammeron").Volume; break;
                case Kind.PullOff: sample = _pull[fret]; kindVolume = Get("pulloff").Volume; break;
                case Kind.Tap: sample = _tap[fret]; kindVolume = Get("tap").Volume; break;
            }
            return CuePlayer.Play(sample, FretPan(fret), c.Volume * kindVolume * MasterVolume);
        }

        public static int StartHold(int fret)
        {
            if (!EnsureLoaded()) return 0;
            var c = Get(FretIds[fret]);
            return CuePlayer.PlayFadeIn(_hold[fret], FretPan(fret), c.Volume * MasterVolume, 60);
        }

        public static int PlayHit(int fret, float volume)
        {
            if (!EnsureLoaded()) return 0;
            return CuePlayer.Play(_hit[fret], FretPan(fret), volume * Get("hit").Volume * MasterVolume);
        }

        public static int PlayLevel(int quarters, float volume)
        {
            if (!EnsureLoaded()) return 0;
            quarters = Math.Max(1, Math.Min(4, quarters));
            return CuePlayer.Play(_level[quarters], 0f, volume * Get("splevel").Volume * MasterVolume);
        }
    }
}
