using System;
using ManagedBass;
using MelonLoader;

namespace YargAccess.Audio
{
    public static class CuePlayer
    {
        public const int SampleRate = 44100;

        const int MaxPolyphony = 16;

        public static bool Ready
        {
            get
            {
                try { return Bass.CurrentDevice >= 0; }
                catch { return false; }
            }
        }

        public static int CreateTone(double frequencyHz, double seconds, double amplitude = 0.5)
        {
            try
            {
                if (!Ready) return 0;
                var wav = BuildSineWav(frequencyHz, seconds, amplitude);
                int handle = Bass.SampleLoad(wav, 0, wav.Length, MaxPolyphony, BassFlags.Default);
                if (handle == 0) MelonLogger.Warning("CuePlayer.CreateTone: SampleLoad failed - " + Bass.LastError);
                return handle;
            }
            catch (Exception ex)
            {
                ModLog.Warn("CuePlayer.CreateTone", ex);
                return 0;
            }
        }

        // Set by ReplayControls around its seek and speed calls (YARG's own replay bar is mouse-only).
        internal static bool ReplaySeeking;

        public static int Play(int sampleHandle, float pan = 0f, float volume = 1f)
        {
            try
            {
                if (sampleHandle == 0 || !Ready) return 0;
                // A replay seek (or speed change) re-runs every input up to the new time in one call, which
                // fires the hit, miss and other engine sounds for all of it at once.
                if (ReplaySeeking) return 0;

                int channel = Bass.SampleGetChannel(sampleHandle);
                if (channel == 0)
                {
                    ModLog.Warn("CuePlayer.Play", "SampleGetChannel failed - " + Bass.LastError);
                    return 0;
                }

                Bass.ChannelSetAttribute(channel, ChannelAttribute.Pan, Clamp(pan, -1f, 1f));
                Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, Clamp(volume, 0f, 1f));
                if (!Bass.ChannelPlay(channel, false))
                {
                    ModLog.Warn("CuePlayer.Play", "ChannelPlay failed - " + Bass.LastError);
                    return 0;
                }
                return channel;
            }
            catch (Exception ex)
            {
                ModLog.Warn("CuePlayer.Play", ex);
                return 0;
            }
        }

        public static bool Beep(double frequencyHz, double seconds, float pan = 0f, float volume = 0.5f)
        {
            int sample = CreateTone(frequencyHz, seconds);
            if (sample == 0) return false;
            bool ok = Play(sample, pan, volume) != 0;
            try { Bass.SampleFree(sample); } catch { }
            return ok;
        }

        public static void Free(int sampleHandle)
        {
            try { if (sampleHandle != 0) Bass.SampleFree(sampleHandle); } catch { }
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        static byte[] BuildSineWav(double frequencyHz, double seconds, double amplitude)
        {
            int frames = Math.Max(1, (int) (SampleRate * seconds));
            int dataBytes = frames * 2;
            var buf = new byte[44 + dataBytes];

            WriteAscii(buf, 0, "RIFF");
            WriteInt32(buf, 4, 36 + dataBytes);
            WriteAscii(buf, 8, "WAVE");
            WriteAscii(buf, 12, "fmt ");
            WriteInt32(buf, 16, 16);
            WriteInt16(buf, 20, 1);
            WriteInt16(buf, 22, 1);
            WriteInt32(buf, 24, SampleRate);
            WriteInt32(buf, 28, SampleRate * 2);
            WriteInt16(buf, 32, 2);
            WriteInt16(buf, 34, 16);
            WriteAscii(buf, 36, "data");
            WriteInt32(buf, 40, dataBytes);

            int fade = Math.Min(frames / 2, SampleRate / 200);
            for (int i = 0; i < frames; i++)
            {
                double env = 1.0;
                if (fade > 0)
                {
                    if (i < fade) env = (double) i / fade;
                    else if (i >= frames - fade) env = (double) (frames - i) / fade;
                }
                double v = Math.Sin(2.0 * Math.PI * frequencyHz * i / SampleRate) * amplitude * env;
                short s = (short) Math.Max(short.MinValue, Math.Min(short.MaxValue, v * short.MaxValue));
                buf[44 + i * 2] = (byte) (s & 0xFF);
                buf[44 + i * 2 + 1] = (byte) ((s >> 8) & 0xFF);
            }
            return buf;
        }

        public static int Load(float[] data, bool loop = false)
        {
            try
            {
                if (!Ready || data == null || data.Length == 0) return 0;
                int frames = data.Length, dataBytes = frames * 2;
                var buf = new byte[44 + dataBytes];
                WriteAscii(buf, 0, "RIFF"); WriteInt32(buf, 4, 36 + dataBytes); WriteAscii(buf, 8, "WAVE");
                WriteAscii(buf, 12, "fmt "); WriteInt32(buf, 16, 16); WriteInt16(buf, 20, 1); WriteInt16(buf, 22, 1);
                WriteInt32(buf, 24, SampleRate); WriteInt32(buf, 28, SampleRate * 2); WriteInt16(buf, 32, 2); WriteInt16(buf, 34, 16);
                WriteAscii(buf, 36, "data"); WriteInt32(buf, 40, dataBytes);
                for (int i = 0; i < frames; i++)
                {
                    float v = Math.Max(-1f, Math.Min(1f, data[i]));
                    short s = (short) (v * short.MaxValue);
                    buf[44 + i * 2] = (byte) (s & 0xFF);
                    buf[44 + i * 2 + 1] = (byte) ((s >> 8) & 0xFF);
                }
                int handle = Bass.SampleLoad(buf, 0, buf.Length, MaxPolyphony, loop ? BassFlags.Loop : BassFlags.Default);
                if (handle == 0) MelonLogger.Warning("CuePlayer.Load: SampleLoad failed - " + Bass.LastError);
                return handle;
            }
            catch (Exception ex)
            {
                ModLog.Warn("CuePlayer.Load", ex);
                return 0;
            }
        }

        public static int PlayFadeIn(int sampleHandle, float pan, float volume, int milliseconds)
        {
            int ch = Play(sampleHandle, pan, 0f);
            try { if (ch != 0) Bass.ChannelSlideAttribute(ch, ChannelAttribute.Volume, Clamp(volume, 0f, 1f), milliseconds); } catch { }
            return ch;
        }

        public static void Stop(int channel)
        {
            try { if (channel != 0) Bass.ChannelStop(channel); } catch { }
        }

        public static void FadeOut(int channel, int milliseconds = 5)
        {
            try { if (channel != 0) Bass.ChannelSlideAttribute(channel, ChannelAttribute.Volume, -1f, milliseconds); } catch { }
        }

        static void WriteAscii(byte[] b, int off, string s)
        {
            for (int i = 0; i < s.Length; i++) b[off + i] = (byte) s[i];
        }

        static void WriteInt32(byte[] b, int off, int v)
        {
            b[off] = (byte) v; b[off + 1] = (byte) (v >> 8); b[off + 2] = (byte) (v >> 16); b[off + 3] = (byte) (v >> 24);
        }

        static void WriteInt16(byte[] b, int off, int v)
        {
            b[off] = (byte) v; b[off + 1] = (byte) (v >> 8);
        }
    }
}
