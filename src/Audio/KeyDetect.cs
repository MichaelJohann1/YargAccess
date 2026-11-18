using System;
using YARG.Core;
using YARG.Core.Chart;

namespace YargAccess.Audio
{
    public static class KeyDetect
    {
        public class Result
        {
            public int Tonic;
            public bool Minor;
            public string Source;
            public string Name => Names[Tonic] + (Minor ? " minor" : " major");
        }

        public static readonly string[] Names = { "C", "C sharp", "D", "E flat", "E", "F", "F sharp", "G", "A flat", "A", "B flat", "B" };

        static readonly double[] MajorProfile = { 6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88 };
        static readonly double[] MinorProfile = { 6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17 };

        const int MinNotes = 16;

        public static Result Detect(SongChart chart)
        {
            if (chart == null) return null;
            var hist = new double[12];
            int vocals = 0, keys = 0;
            try
            {
                foreach (var track in new[] { chart.Vocals, chart.Harmony })
                {
                    if (track == null) continue;
                    foreach (var part in track.Parts)
                        foreach (var phrase in part.NotePhrases)
                            foreach (var note in phrase.PhraseParentNote.ChildNotes)
                            {
                                if (note.IsNonPitched || note.IsPercussion) continue;
                                int pc = ((int) Math.Round(note.Pitch) % 12 + 12) % 12;
                                hist[pc] += Math.Max(0.1, note.TotalTimeLength);
                                vocals++;
                            }
                }
            }
            catch { }
            try
            {
                InstrumentDifficulty<ProKeysNote> diff;
                if (chart.ProKeys != null && chart.ProKeys.TryGetDifficulty(Difficulty.Expert, out diff))
                    foreach (var n in diff.Notes)
                        foreach (var c in n.AllNotes)
                        {
                            hist[(c.Key % 12 + 12) % 12] += Math.Max(0.1, c.TimeLength);
                            keys++;
                        }
            }
            catch { }
            if (vocals + keys < MinNotes) return null;

            double best = double.NegativeInfinity;
            var result = new Result();
            for (int tonic = 0; tonic < 12; tonic++)
            {
                double maj = Correlate(hist, MajorProfile, tonic), min = Correlate(hist, MinorProfile, tonic);
                if (maj > best) { best = maj; result.Tonic = tonic; result.Minor = false; }
                if (min > best) { best = min; result.Tonic = tonic; result.Minor = true; }
            }
            result.Source = vocals >= MinNotes && keys >= MinNotes ? "vocals and pro keys" : (vocals >= keys ? "vocals" : "pro keys");
            return result;
        }

        static double Correlate(double[] hist, double[] profile, int tonic)
        {
            double mh = 0, mp = 0;
            for (int i = 0; i < 12; i++) { mh += hist[i]; mp += profile[i]; }
            mh /= 12; mp /= 12;
            double num = 0, dh = 0, dp = 0;
            for (int i = 0; i < 12; i++)
            {
                double h = hist[(i + tonic) % 12] - mh, p = profile[i] - mp;
                num += h * p; dh += h * h; dp += p * p;
            }
            return dh <= 0 || dp <= 0 ? 0 : num / Math.Sqrt(dh * dp);
        }
    }
}
