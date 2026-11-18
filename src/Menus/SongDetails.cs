using System;
using System.Collections.Generic;
using System.Globalization;
using MelonLoader;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Menu.MusicLibrary;
using YARG.Player;
using YARG.Playlists;
using YARG.Scores;
using YARG.Song;

namespace YargAccess
{
    public static class SongDetails
    {
        public static string Head(SongViewType view)
        {
            if (view == null || view.SongEntry == null) return "";
            try { return Stars(view) + ", " + (PlaylistContainer.FavoritesPlaylist.ContainsSong(view.SongEntry) ? "favorite" : "not favorite"); }
            catch (Exception ex) { ModLog.Warn("SongDetails.Head", ex); return ""; }
        }

        public static string For(SongViewType view)
        {
            if (view == null || view.SongEntry == null) return "";
            var parts = new List<string>();
            try
            {
                parts.Add(Stars(view));
                var repeat = ListenRepeatScores.ForSong(view.SongEntry);
                if (repeat != null) parts.Add(repeat);
                parts.Add(PlaylistContainer.FavoritesPlaylist.ContainsSong(view.SongEntry) ? "favorite" : "not favorite");
                var e = view.SongEntry;
                string inst = Instruments(e);
                if (inst.Length > 0) parts.Add(inst);
                Add(parts, "album", Str(e.Album));
                string year = e.ParsedYear;
                if (!string.IsNullOrEmpty(e.YearSecondary)) year += " (" + e.YearSecondary + ")";
                Add(parts, "year", year);
                string genre = Str(e.Genre);
                if (genre.Length > 0) genre = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(genre);
                string sub = Str(e.Subgenre);
                if (sub.Length > 0) genre = genre.Length > 0 ? genre + ", " + sub : sub;
                Add(parts, "genre", genre);
                parts.Add("length " + Length(e.SongLengthMilliseconds));
                Add(parts, "charter", Str(e.Charter));
                string source = "";
                try { source = SongSources.SourceToGameName(e.Source); } catch { }
                Add(parts, "source", TextUtil.Clean(source));
                parts.Add(Rating(e.SongRating));
            }
            catch (Exception ex) { ModLog.Warn("SongDetails", ex); }
            parts.RemoveAll(string.IsNullOrEmpty);
            var sb = new System.Text.StringBuilder();
            foreach (var p in parts)
            {
                if (sb.Length > 0)
                    sb.Append(sb[sb.Length - 1] == '.' ? " " : p.StartsWith(InstrumentsLead) ? ". " : ", ");
                sb.Append(p);
            }
            return sb.ToString();
        }

        const string InstrumentsLead = "Instruments.";

        static void Add(List<string> parts, string name, string value)
        {
            if (!string.IsNullOrEmpty(value)) parts.Add(name + " " + value);
        }

        static string Str(object sortString)
        {
            return sortString == null ? "" : TextUtil.Clean(sortString.ToString());
        }

        static string Stars(SongViewType view)
        {
            StarAmount? stars = view.GetStarAmount();
            if (stars == null) return OtherInstrument(view.SongEntry);
            var info = view.GetScoreInfo();
            return info == null ? StarText(stars.Value)
                : Describe(stars.Value, info.Value.Score, info.Value.Difficulty, info.Value.Percent, info.Value.IsFc);
        }

        static string OtherInstrument(SongEntry e)
        {
            string title, body;
            return Elsewhere(e, out title, out body) ? title + "; " + body : "not played";
        }

        public static bool Elsewhere(SongViewType view, out string title, out string body)
        {
            title = body = null;
            try { return view != null && view.SongEntry != null && view.GetStarAmount() == null && Elsewhere(view.SongEntry, out title, out body); }
            catch (Exception ex) { ModLog.Warn("SongDetails.Elsewhere", ex); return false; }
        }

        static bool Elsewhere(SongEntry e, out string title, out string body)
        {
            title = body = null;
            YargPlayer human = null;
            int humans = 0;
            foreach (var p in PlayerContainer.Players)
                if (p != null && p.Profile != null && !p.Profile.IsBot) { human = p; humans++; }
            if (humans != 1) return false;
            var current = human.Profile.CurrentInstrument;
            PlayerScoreRecord best = null;
            foreach (Instrument inst in Enum.GetValues(typeof(Instrument)))
            {
                if (inst == current || inst == Instrument.Band || !e.HasInstrument(inst)) continue;
                var r = ScoreContainer.GetPreferredHighScore(e.Hash, human.Profile.Id, inst, false);
                if (r != null && (best == null || r.Score > best.Score)) best = r;
            }
            if (best == null) return false;
            title = "not played on " + Playability.InstrumentName(current);
            body = Playability.InstrumentName(best.Instrument) + ", " + Describe(best.Stars, best.Score, best.Difficulty, best.GetPercent(), best.IsFc);
            return true;
        }

        static string Describe(StarAmount stars, int score, Difficulty difficulty, float pct, bool fc)
        {
            string s = StarText(stars) + ", best score " + score.ToString("N0", CultureInfo.CurrentCulture) + " on " + difficulty;
            if (pct <= 1.0001f) pct *= 100f;
            s += ", " + Math.Floor(pct) + "%";
            if (fc) s += ", full combo";
            return s;
        }

        public static string StarText(StarAmount stars)
        {
            string s;
            switch (stars)
            {
                case StarAmount.Star1: s = "1 star"; break;
                case StarAmount.Star2: s = "2 stars"; break;
                case StarAmount.Star3: s = "3 stars"; break;
                case StarAmount.Star4: s = "4 stars"; break;
                case StarAmount.Star5: s = "5 stars"; break;
                case StarAmount.StarGold: s = "gold stars"; break;
                case StarAmount.StarSilver: s = "silver stars"; break;
                case StarAmount.StarBrutal: s = "brutal stars"; break;
                default: s = "no stars"; break;
            }
            return s;
        }

        static string Instruments(SongEntry e)
        {
            var list = new List<string>();
            Part(list, e, Instrument.FiveFretGuitar, "guitar");
            Part(list, e, Instrument.FiveFretBass, "bass");
            if (e.HasInstrument(Instrument.FiveLaneDrums)) Part(list, e, Instrument.FiveLaneDrums, "five-lane drums");
            else if (e.HasInstrument(Instrument.ProDrums)) Part(list, e, Instrument.ProDrums, "pro drums");
            else Part(list, e, Instrument.FourLaneDrums, "drums");
            Part(list, e, Instrument.Keys, "keys");
            bool harmonyOnly = !e.HasInstrument(Instrument.Vocals) && e.HasInstrument(Instrument.Harmony);
            int voices = e.VocalsCount;
            string vocals = voices >= 3 ? voices + "-part harmonies" : voices == 2 ? "2-part vocals" : "vocals";
            Part(list, e, harmonyOnly ? Instrument.Harmony : Instrument.Vocals, vocals);
            if (e.HasInstrument(Instrument.ProGuitar_17Fret) || e.HasInstrument(Instrument.ProGuitar_22Fret))
                Part(list, e, e[Instrument.ProGuitar_17Fret].Intensity == -1 && e.HasInstrument(Instrument.ProGuitar_22Fret) ? Instrument.ProGuitar_22Fret : Instrument.ProGuitar_17Fret, "pro guitar");
            else Part(list, e, Instrument.FiveFretCoopGuitar, "co-op guitar");
            if (e.HasInstrument(Instrument.ProBass_17Fret) || e.HasInstrument(Instrument.ProBass_22Fret))
                Part(list, e, e[Instrument.ProBass_17Fret].Intensity == -1 && e.HasInstrument(Instrument.ProBass_22Fret) ? Instrument.ProBass_22Fret : Instrument.ProBass_17Fret, "pro bass");
            else Part(list, e, Instrument.FiveFretRhythm, "rhythm");
            Part(list, e, Instrument.EliteDrums, "elite drums");
            Part(list, e, Instrument.ProKeys, "pro keys");
            Part(list, e, Instrument.Band, "band");
            return list.Count == 0 ? "" : InstrumentsLead + " " + string.Join(". ", list.ToArray()) + ".";
        }

        static void Part(List<string> list, SongEntry e, Instrument inst, string name)
        {
            var v = e[inst];
            if (v.SubTracks == 0) return;
            list.Add(v.Intensity < 0 ? name + " unrated" : name + " " + v.Intensity);
        }

        static string Length(long ms)
        {
            var t = TimeSpan.FromMilliseconds(ms);
            return t.Hours > 0 ? t.ToString("h\\:mm\\:ss") : t.ToString("m\\:ss");
        }

        static string Rating(SongRating r)
        {
            switch (r)
            {
                case SongRating.Family_Friendly: return "rated family friendly";
                case SongRating.Supervision_Recommended: return "rated supervision recommended";
                case SongRating.Mature: return "rated mature";
                case SongRating.Sensitive_Content: return "rated sensitive content";
                default: return "not rated";
            }
        }
    }
}
