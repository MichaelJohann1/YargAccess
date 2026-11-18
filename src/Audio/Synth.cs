using System;

namespace YargAccess.Audio
{
    public static class Synth
    {
        const int Rate = CuePlayer.SampleRate;

        public static double MidiToHz(double midi) => 440.0 * Math.Pow(2.0, (midi - 69.0) / 12.0);

        public static float[] Tone(double f, double seconds, double attack, double decay, double bright, double peak)
        {
            int n = Frames(seconds);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = (double) i / Rate;
                double env = Math.Exp(-t / decay);
                if (attack > 0 && t < attack) env *= t / attack;
                d[i] = (float) (env * Wave(2 * Math.PI * f * t, bright));
            }
            return Finish(d, peak);
        }

        public static float[] Loop(double f, double bright, double peak)
        {
            int n = Rate;
            int k = Math.Max(1, (int) Math.Round(f));
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = (float) Wave(2 * Math.PI * k * i / n, bright);
            Normalize(d, peak);
            return d;
        }

        public static float[] HeldPreview(float[] strike, float[] loop, double seconds)
        {
            int n = Math.Max(strike.Length, Frames(seconds));
            var d = new float[n];
            int ramp = Rate * 40 / 1000, tail = Rate * 60 / 1000;
            for (int i = 0; i < n; i++)
            {
                double env = Math.Min(1.0, (double) i / ramp);
                if (i > n - tail) env *= (double) (n - i) / tail;
                d[i] = (float) (loop[i % loop.Length] * env + (i < strike.Length ? strike[i] : 0f));
            }
            return d;
        }

        public static float[] Noise(double centre, double seconds, double attack, double decay, double peak, int seed)
        {
            int n = Frames(seconds);
            var d = new float[n];
            var bp = new BandPass(centre, 3.0);
            var rnd = new Random(seed);
            for (int i = 0; i < n; i++)
            {
                double t = (double) i / Rate;
                double env = Math.Exp(-t / decay);
                if (attack > 0 && t < attack) env *= t / attack;
                d[i] = (float) (bp.Next(rnd.NextDouble() * 2 - 1) * env);
            }
            return Finish(d, peak);
        }

        public static float[] NoiseLoop(double centre, double peak, int seed)
        {
            int n = Rate, x = Rate / 10;
            var raw = new double[n + x];
            var bp = new BandPass(centre, 3.0);
            var rnd = new Random(seed);
            for (int i = 0; i < raw.Length; i++) raw[i] = bp.Next(rnd.NextDouble() * 2 - 1);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                double v = raw[i];
                if (i < x) { double w = (double) i / x; v = raw[i] * w + raw[n + i] * (1 - w); }
                d[i] = (float) v;
            }
            Normalize(d, peak);
            return d;
        }

        public static float[] Glide(double f0, double f1, double seconds, string shape, double peak)
        {
            int n = Frames(seconds);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double x = (double) i / n;
                phase += 2 * Math.PI * f0 * Math.Pow(f1 / f0, x) / Rate;
                double env = shape == "rise" ? 0.15 + 0.85 * x * x : Math.Exp(-4.0 * x);
                double v = Math.Sin(phase) * env;
                if (shape == "thud") v += Math.Sin(phase * 0.5) * 0.5 * env;
                d[i] = (float) v;
            }
            return Finish(d, peak);
        }

        public static float[] Slur(double f0, double f1, double seconds, double glide, double attack, double decay, double bright, double peak)
        {
            int n = Frames(seconds);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double) i / Rate;
                double f = t < glide ? f0 * Math.Pow(f1 / f0, t / glide) : f1;
                phase += 2 * Math.PI * f / Rate;
                double env = Math.Exp(-t / decay);
                if (attack > 0 && t < attack) env *= t / attack;
                d[i] = (float) (env * Wave(phase, bright));
            }
            return Finish(d, peak);
        }

        public static float[] Sequence(double[] hz, double spacing, double each, double bright, double peak)
        {
            float[] d = new float[0];
            for (int i = 0; i < hz.Length; i++)
                d = Overlay(d, Tone(hz[i], each, 0.002, each / 3, bright, 1.0), spacing * i);
            return Finish(d, peak);
        }

        public static float[] NoiseRise(double c0, double c1, double seconds, double peak, int seed)
        {
            int n = Frames(seconds);
            var d = new float[n];
            var rnd = new Random(seed);
            var bp = new BandPass(c0, 4.0);
            for (int i = 0; i < n; i++)
            {
                double x = (double) i / n;
                if (i % 64 == 0) bp.Tune(c0 * Math.Pow(c1 / c0, x), 4.0);
                d[i] = (float) (bp.Next(rnd.NextDouble() * 2 - 1) * (0.15 + 0.85 * x * x));
            }
            return Finish(d, peak);
        }

        public static float[] NoiseSlide(double c0, double c1, double seconds, double glide, double attack, double decay, double peak, int seed)
        {
            int n = Frames(seconds);
            var d = new float[n];
            var rnd = new Random(seed);
            var bp = new BandPass(c0, 6.0);
            for (int i = 0; i < n; i++)
            {
                double t = (double) i / Rate;
                if (i % 32 == 0) bp.Tune(t < glide ? c0 * Math.Pow(c1 / c0, t / glide) : c1, 6.0);
                double env = t < glide ? 1.0 : Math.Exp(-(t - glide) / decay);
                if (attack > 0 && t < attack) env *= t / attack;
                d[i] = (float) (bp.Next(rnd.NextDouble() * 2 - 1) * env);
            }
            return Finish(d, peak);
        }

        public static float[] Metal(double f0, double seconds, double decay, double noise, double peak, int seed)
        {
            double[] ratios = { 1.0, 1.47, 1.98, 2.53, 3.11, 3.87 };
            int n = Frames(seconds);
            var d = new float[n];
            var rnd = new Random(seed);
            var phase = new double[ratios.Length];
            for (int k = 0; k < phase.Length; k++) phase[k] = rnd.NextDouble() * 2 * Math.PI;
            var bp = new BandPass(Math.Min(f0 * 2.5, Rate * 0.4), 1.5);
            for (int i = 0; i < n; i++)
            {
                double t = (double) i / Rate;
                double env = Math.Exp(-t / decay);
                if (t < 0.001) env *= t / 0.001;
                double ring = 0;
                for (int k = 0; k < ratios.Length; k++) ring += Math.Sin(2 * Math.PI * f0 * ratios[k] * t + phase[k]) / (1 + k * 0.4);
                double hiss = bp.Next(rnd.NextDouble() * 2 - 1) * 3;
                d[i] = (float) (env * ((1 - noise) * ring / 3 + noise * hiss));
            }
            return Finish(d, peak);
        }

        public static float[] Level(float[] d, double peak) => Finish(d, peak);

        public static float[] Overlay(float[] a, float[] b, double offsetSeconds)
        {
            int off = (int) (offsetSeconds * Rate);
            var d = new float[Math.Max(a.Length, off + b.Length)];
            for (int i = 0; i < a.Length; i++) d[i] = a[i];
            for (int i = 0; i < b.Length; i++) d[off + i] += b[i];
            return d;
        }

        static double Wave(double phase, double bright) =>
            (Math.Sin(phase) + bright * 0.35 * Math.Sin(2 * phase) + bright * 0.12 * Math.Sin(3 * phase)) / (1 + bright * 0.47);

        static int Frames(double seconds) => Math.Max(1, (int) (Rate * seconds));

        static float[] Finish(float[] d, double peak)
        {
            Normalize(d, peak);
            int fade = Math.Min(d.Length / 2, Rate / 200);
            for (int i = 0; i < fade; i++) d[d.Length - 1 - i] *= (float) i / fade;
            return d;
        }

        static void Normalize(float[] d, double peak)
        {
            float max = 0;
            foreach (var v in d) max = Math.Max(max, Math.Abs(v));
            if (max <= 0) return;
            float k = (float) (peak / max);
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        class BandPass
        {
            double b0, b2, a1, a2, x1, x2, y1, y2;
            public BandPass(double f, double q) { Tune(f, q); }
            public void Tune(double f, double q)
            {
                double w = 2 * Math.PI * Math.Min(f, Rate * 0.45) / Rate, alpha = Math.Sin(w) / (2 * q), a0 = 1 + alpha;
                b0 = alpha / a0; b2 = -alpha / a0; a1 = -2 * Math.Cos(w) / a0; a2 = (1 - alpha) / a0;
            }
            public double Next(double x)
            {
                double y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }
    }
}
