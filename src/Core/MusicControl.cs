using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Audio;
using YARG.Core.Song;
using YARG.Menu.Persistent;
using YARG.Settings;

namespace YargAccess
{
    public static class MusicControl
    {
        static SongEntry _last;
        static bool _init;

        // YARG's menu music player only goes forward, to a random song, and keeps no history. The mod keeps
        // the songs it played so Previous can go back and Next can walk forward again (user, 2026-11-14).
        const int MaxHistory = 50;
        static readonly List<SongEntry> _history = new List<SongEntry>();
        static int _pos = -1;
        static SongEntry _requested;

        static void Record(SongEntry cur)
        {
            if (cur == null) return;
            if (ReferenceEquals(cur, _requested)) { _requested = null; return; }
            _requested = null;
            if (_pos >= 0 && _pos < _history.Count && ReferenceEquals(_history[_pos], cur)) return;
            if (_pos < _history.Count - 1) _history.RemoveRange(_pos + 1, _history.Count - _pos - 1);
            _history.Add(cur);
            if (_history.Count > MaxHistory) _history.RemoveAt(0);
            _pos = _history.Count - 1;
        }

        public static void Tick()
        {
            SongEntry cur = null;
            try { cur = MusicPlayer.NowPlaying; } catch { }
            if (!_init) { _last = cur; _init = true; Record(cur); return; }
            if (!ReferenceEquals(cur, _last))
            {
                Record(cur);
                bool first = _last == null;
                _last = cur;
                if (first) return;
                if (cur != null && Available() && !CreditsNavigator.IsReading && !GuidedPractice.Active && ModSettings.AnnounceMusic.Value) Speak(cur, false);
            }
        }

        public static void AnnounceNowPlaying()
        {
            var s = SafeNowPlaying();
            if (s == null || !Available()) { MenuAnnouncer.SayShown(Loc.T("No song playing")); return; }
            Speak(s, true);
        }

        public static void TogglePlay()
        {
            var p = Player();
            if (p == null || !Available()) { MenuAnnouncer.SayShown(Loc.T("No song playing")); return; }
            try { p.TogglePlay(); } catch (Exception ex) { ModLog.Warn("MusicControl.TogglePlay", ex); return; }
            bool? paused = MixerPaused(p);
            MenuAnnouncer.SayShown(paused == true ? Loc.T("Paused") : paused == false ? Loc.T("Playing") : Loc.T("Music toggled"));
        }

        public static void Next()
        {
            var p = Player();
            if (p == null || !Available()) { MenuAnnouncer.SayShown(Loc.T("No song playing")); return; }
            if (_pos >= 0 && _pos < _history.Count - 1 && ReferenceEquals(_history[_pos], SafeNowPlaying()))
            {
                _pos++;
                Play(p, _history[_pos]);
                return;
            }
            try { p.NextSong(); } catch (Exception ex) { ModLog.Warn("MusicControl.Next", ex); }
        }

        public static void Previous()
        {
            var p = Player();
            if (p == null || !Available()) { MenuAnnouncer.SayShown(Loc.T("No song playing")); return; }
            var cur = SafeNowPlaying();
            if (cur != null && (_pos < 0 || _pos >= _history.Count || !ReferenceEquals(_history[_pos], cur))) Record(cur);
            if (_pos <= 0) { MenuAnnouncer.SayShown(Loc.T("No previous song")); return; }
            _pos--;
            Play(p, _history[_pos]);
        }

        static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly BindingFlags NS = BindingFlags.NonPublic | BindingFlags.Static;

        // MusicPlayer.NextSong with a chosen song instead of a random one.
        static async void Play(MusicPlayer p, SongEntry entry)
        {
            try
            {
                var t = typeof(MusicPlayer);
                var lockObj = t.GetField("_lock", NP).GetValue(p);
                var mixerF = t.GetField("_mixer", NP);
                var currentF = t.GetField("_current", NS);
                _requested = entry;
                t.GetField("_nowPlaying", NS).SetValue(null, entry);
                Task<StemMixer> task;
                lock (lockObj)
                {
                    double vol = SettingsManager.Settings.MusicPlayerVolume.Value;
                    task = Task.Run(() => entry.LoadAudio(1f, vol, SongStem.Crowd));
                    currentF.SetValue(null, task);
                }
                var mixer = await task;
                if (mixer == null)
                {
                    MelonLogger.Warning("MusicControl.Play: could not load " + entry.Name);
                    _requested = null;
                    p.NextSong();
                    return;
                }
                lock (lockObj)
                {
                    if (!ReferenceEquals(currentF.GetValue(null), task) || !p.gameObject.activeSelf) { mixer.Dispose(); return; }
                    (mixerF.GetValue(p) as StemMixer)?.Dispose();
                    mixerF.SetValue(p, mixer);
                    mixer.SongEnd += () =>
                    {
                        mixer.Dispose();
                        if (ReferenceEquals(mixerF.GetValue(p), mixer)) mixerF.SetValue(p, null);
                        p.NextSong();
                    };
                    mixer.Play();
                    var song = t.GetField("_songText", NP).GetValue(p) as TextMeshProUGUI;
                    var artist = t.GetField("_artistText", NP).GetValue(p) as TextMeshProUGUI;
                    if (song != null) song.text = entry.Name;
                    if (artist != null) artist.text = entry.Artist;
                    var button = t.GetField("_playPauseButton", NP).GetValue(p) as Image;
                    var pause = t.GetField("_pauseSprite", NP).GetValue(p) as Sprite;
                    if (button != null && pause != null) button.sprite = pause;
                }
            }
            catch (Exception ex) { ModLog.Warn("MusicControl.Play", ex); }
        }

        static MusicPlayer Player()
        {
            try { var hb = HelpBar.Instance; return hb != null ? hb.MusicPlayer : null; } catch { return null; }
        }

        static bool Available()
        {
            var p = Player();
            return p != null && p.gameObject.activeInHierarchy;
        }

        static SongEntry SafeNowPlaying() { try { return MusicPlayer.NowPlaying; } catch { return null; } }

        static void Speak(SongEntry s, bool interrupt)
        {
            string name = s.Name;
            string artist = s.Artist;
            var msg = string.IsNullOrWhiteSpace(artist) ? Loc.F("Now playing, {0}", TextUtil.Clean(name))
                : Loc.F("Now playing, {0}, by {1}", TextUtil.Clean(name), TextUtil.Clean(artist));
            if (interrupt) MenuAnnouncer.Say(msg);
            else Speech.Say(msg, false);
        }

        static bool? MixerPaused(MusicPlayer p)
        {
            try
            {
                var f = typeof(MusicPlayer).GetField("_mixer", BindingFlags.NonPublic | BindingFlags.Instance);
                var mixer = f != null ? f.GetValue(p) : null;
                if (mixer == null) return null;
                var ip = mixer.GetType().GetProperty("IsPaused");
                return ip != null ? (bool?)(bool)ip.GetValue(mixer) : null;
            }
            catch { return null; }
        }
    }
}
