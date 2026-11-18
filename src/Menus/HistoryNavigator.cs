using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using MelonLoader;
using YARG.Core.Game;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Menu.History;
using YARG.Scores;

namespace YargAccess
{
    // The History menu (user, 2026-11-14: more detail, better to move through, play in the chosen Play Style).
    // Each row says everything: stars, part, percent, score, hits and misses (every player and the band score
    // when there are several), play style, speed, bots, and the date and time. Nothing is left for the text review. Left / Right jump between the day groups, Home / End to the first and last
    // entry. Blue (4) plays the song yourself, in your Play Style. Enter still watches the replay as it was
    // played, and Orange plays along with it (your part in your Play Style, see ListenRepeat.RunCallLength).
    public static class HistoryNavigator
    {
        const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly FieldInfo _recordF = typeof(ReplayViewType).GetField("_record", BF);
        static readonly FieldInfo _entryF = typeof(ReplayViewType).GetField("_entry", BF);
        static readonly FieldInfo _songF = typeof(ReplayViewType).GetField("_songEntry", BF);
        static readonly FieldInfo _infoF = typeof(ReplayViewType).GetField("_gameInfo", BF);

        static HistoryMenu Menu()
        {
            var m = UnityEngine.Object.FindObjectOfType<HistoryMenu>();
            return m != null && m.isActiveAndEnabled ? m : null;
        }

        public static bool IsActive => Menu() != null && !DialogNavigator.IsActive;

        // ---- Row text ----

        // The spoken part after "song, artist" (full is no longer used: it is all spoken).
        public static bool Describe(object view, out string spoken, out string full)
        {
            spoken = full = null;
            var row = view as ReplayViewType;
            if (row == null) return false;
            try
            {
                var record = _recordF != null ? _recordF.GetValue(row) as GameRecord : null;
                var entry = _entryF != null ? _entryF.GetValue(row) as ReplayInfo : null;
                var song = _songF != null ? _songF.GetValue(row) as SongEntry : null;
                List<PlayerScoreRecord> players = null;
                if (_infoF != null)
                {
                    var info = (ViewType.GameInfo) _infoF.GetValue(row);
                    players = info.PlayerScoreRecords;
                }

                DateTime date = record != null ? record.Date : entry != null ? entry.Date : DateTime.MinValue;
                StarAmount band = record != null ? record.BandStars : entry != null ? entry.BandStars : StarAmount.None;
                int bandScore = record != null ? record.BandScore : entry != null ? entry.BandScore : 0;
                float speed = record != null ? record.SongSpeed : entry != null ? entry.SongSpeed : 1f;
                string style = Style(record, entry);

                // Everything is in the spoken row (user, 2026-11-14: not behind the text review).
                var said = new List<string>();
                var humans = new List<PlayerScoreRecord>();
                if (players != null) foreach (var p in players) if (p != null && !p.IsReplay) humans.Add(p);
                bool alone = humans.Count == 1 && players.Count == 1;
                if (alone)
                {
                    var p = humans[0];
                    said.Add(SongDetails.StarText(p.Stars));
                    said.Add(Part(p));
                    said.Add(Percent(p));
                    said.Add("score " + p.Score.ToString("N0", CultureInfo.CurrentCulture));
                    said.Add(p.NotesHit + " hit, " + p.NotesMissed + " missed");
                    if (p.IsFc) said.Add("full combo");
                }
                else
                {
                    said.Add(SongDetails.StarText(band));
                    said.Add("band score " + bandScore.ToString("N0", CultureInfo.CurrentCulture));
                    if (players != null)
                        foreach (var p in players)
                        {
                            if (p == null) continue;
                            string who = Name(p.PlayerId);
                            said.Add((who != null ? who + ": " : "") + Part(p) + ", " + SongDetails.StarText(p.Stars) + ", " + Percent(p)
                                + ", score " + p.Score.ToString("N0", CultureInfo.CurrentCulture) + ", " + p.NotesHit + " hit, " + p.NotesMissed + " missed"
                                + (p.IsFc ? ", full combo" : "") + (p.IsReplay ? ", from a replay" : ""));
                        }
                }
                if (players == null && entry != null && entry.Stats != null)
                    foreach (var st in entry.Stats)
                    {
                        if (st == null) continue;
                        string line = (string.IsNullOrEmpty(st.PlayerName) ? "" : st.PlayerName + ": ")
                            + SongDetails.StarText((StarAmount) (sbyte) Math.Max(0, Math.Min(6, (int) Math.Floor(st.Stars)))) + ", score " + st.Score.ToString("N0", CultureInfo.CurrentCulture);
                        int hit = IntField(st, "NumNotesHit"), total = IntField(st, "TotalNotes");
                        if (total > 0) line += ", " + hit + " hit, " + (total - hit) + " missed";
                        if (st.IsReplayPlayer) line += ", from a replay";
                        said.Add(line);
                    }
                if (style != null) said.Add(style);
                if (Math.Abs(speed - 1f) > 0.001f) said.Add(Math.Round(speed * 100) + "% speed");
                if (record != null && record.HasBots) said.Add("with bots");
                if (record != null && record.PlayedWithReplay) said.Add("played along with a replay");
                if (record != null && record.ReplayFileName == null) said.Add("no replay saved");
                if (song == null) said.Add("the song is not in your library");
                if (date != DateTime.MinValue) said.Add(When(date));

                spoken = string.Join(", ", said.ToArray());
                full = null;
                return true;
            }
            catch (Exception ex) { ModLog.Warn("HistoryNavigator.Describe", ex); return false; }
        }

        static int IntField(object o, string name)
        {
            try { var f = o.GetType().GetField(name, BF); return f != null ? Convert.ToInt32(f.GetValue(o)) : 0; } catch { return 0; }
        }

        static string ReplayPath(GameRecord record, ReplayInfo entry)
        {
            return entry != null ? entry.FilePath
                : record != null && record.ReplayFileName != null ? Path.Combine(ScoreContainer.ScoreReplayDirectory, record.ReplayFileName + ".replay") : null;
        }

        static string ReplayPath(ReplayViewType row)
        {
            if (row == null) return null;
            var record = _recordF != null ? _recordF.GetValue(row) as GameRecord : null;
            var entry = _entryF != null ? _entryF.GetValue(row) as ReplayInfo : null;
            return ReplayPath(record, entry);
        }

        static string Style(GameRecord record, ReplayInfo entry)
        {
            string path = ReplayPath(record, entry);
            if (path == null) return null;
            int size;
            return ListenRepeat.ReplayIsListenRepeat(path, out size) ? "Listen and Repeat " + size : "Play What You Hear";
        }

        static string Part(PlayerScoreRecord p) => Playability.InstrumentName(p.Instrument) + " " + p.Difficulty;

        static string Percent(PlayerScoreRecord p)
        {
            float pct = p.GetPercent();
            if (pct <= 1.0001f) pct *= 100f;
            return Math.Floor(pct) + "%";
        }

        static string Name(Guid id)
        {
            try
            {
                foreach (var prof in YARG.Player.PlayerContainer.Profiles)
                    if (prof != null && prof.Id == id) return prof.Name;
            }
            catch { }
            return null;
        }

        static string When(DateTime d)
        {
            var time = d.ToString("t", CultureInfo.CurrentCulture);
            if (d.Date == DateTime.Today) return "today at " + time;
            if (d.Date == DateTime.Today.AddDays(-1)) return "yesterday at " + time;
            return d.ToString(d.Year == DateTime.Today.Year ? "d MMMM" : "d MMMM yyyy", CultureInfo.CurrentCulture) + " at " + time;
        }

        // ---- Moving ----

        static IList Rows(HistoryMenu m)
        {
            var p = m.GetType().GetProperty("ViewList", BF);
            return p != null ? p.GetValue(m, null) as IList : null;
        }

        static void Select(HistoryMenu m, int index, string prefix)
        {
            _onButton = false;
            ListMenuAnnouncer.ResetLast();
            m.SelectedIndex = index;
            var line = ListMenuAnnouncer.TakeActiveLine();
            MenuAnnouncer.Focused(string.IsNullOrEmpty(prefix) ? line : prefix + ". " + line, true);
        }

        // Left / Right: the first entry of the previous / next day group.
        public static bool Jump(int dir)
        {
            var m = Menu();
            if (m == null || DialogNavigator.IsActive) return false;
            var rows = Rows(m);
            if (rows == null || rows.Count == 0) return true;
            int cur = m.SelectedIndex;
            // The header of the group the selection is in.
            int head = cur;
            while (head > 0 && !(rows[head] is CategoryViewType)) head--;
            int target = -1;
            if (dir > 0)
            {
                for (int i = cur + 1; i < rows.Count; i++) if (rows[i] is CategoryViewType) { target = i; break; }
            }
            else
            {
                // On a group's first entry (or its header), go to the group before; else to this group's start.
                bool atStart = cur <= head + 1;
                for (int i = (atStart ? head : head + 1) - 1; i >= 0; i--) if (rows[i] is CategoryViewType) { target = i; break; }
                if (!atStart && rows[head] is CategoryViewType) target = head;
            }
            if (target < 0) { MenuAnnouncer.SayShown(dir > 0 ? "Last group" : "First group"); return true; }
            string name = TextUtil.Clean(((ViewType) rows[target]).GetPrimaryText(false));
            if (!string.IsNullOrEmpty(name) && name.ToUpperInvariant() == name)
                name = char.ToUpperInvariant(name[0]) + name.Substring(1).ToLowerInvariant();
            int first = target + 1 < rows.Count && !(rows[target + 1] is CategoryViewType) ? target + 1 : target;
            Select(m, first, first == target ? null : name);
            return true;
        }

        public static bool Edge(bool end)
        {
            var m = Menu();
            if (m == null || DialogNavigator.IsActive) return false;
            var rows = Rows(m);
            if (rows == null || rows.Count == 0) return true;
            int i = -1;
            if (end) { for (int k = rows.Count - 1; k >= 0 && i < 0; k--) if (rows[k] is ReplayViewType) i = k; }
            else { for (int k = 0; k < rows.Count && i < 0; k++) if (rows[k] is ReplayViewType) i = k; }
            if (i < 0) { MenuAnnouncer.SayShown("No entries"); return true; }
            Select(m, i, null);
            return true;
        }

        // ---- Export Replay / Import Replay ----
        // YARG's header button (Export Replay on the History tab, Import Replay on the Import tab) has no
        // NavigatableBehaviour, so it was mouse-only. Up from the first entry steps onto it, Down steps back,
        // Enter presses it.
        static bool _onButton;

        public static void ResetZone() { _onButton = false; }

        static UnityEngine.GameObject HeaderButton(HistoryMenu m, out bool export)
        {
            export = false;
            var ex = typeof(HistoryMenu).GetField("_exportReplayButton", BF)?.GetValue(m) as UnityEngine.GameObject;
            var im = typeof(HistoryMenu).GetField("_importReplayButton", BF)?.GetValue(m) as UnityEngine.GameObject;
            if (ex != null && ex.activeInHierarchy) { export = true; return ex; }
            if (im != null && im.activeInHierarchy) return im;
            return null;
        }

        static string ButtonText(HistoryMenu m)
        {
            bool export;
            var b = HeaderButton(m, out export);
            if (b == null) return null;
            if (!export) return "Import Replay" + Keys.Role("button") + ". Opens a file browser to add a replay file to this list.";
            var row = m.CurrentSelection as ReplayViewType;
            var song = row != null && _songF != null ? _songF.GetValue(row) as SongEntry : null;
            string what = song != null ? "the replay of " + song.Name : "the chosen entry's replay";
            return "Export Replay" + Keys.Role("button") + ". Saves a copy of " + what + " as a file. To export another entry, select it and press E.";
        }

        public static bool MoveUp()
        {
            var m = Menu();
            if (m == null) { _onButton = false; return false; }
            bool export;
            if (HeaderButton(m, out export) == null || export) { _onButton = false; return false; }
            if (_onButton) { MenuAnnouncer.Focused(ButtonText(m), true); return true; }
            var rows = Rows(m);
            int first = rows != null && rows.Count > 1 && rows[0] is CategoryViewType ? 1 : 0;
            if (rows != null && rows.Count > 0 && m.SelectedIndex > first) return false;
            _onButton = true;
            MenuAnnouncer.Focused(ButtonText(m), true);
            return true;
        }

        public static bool MoveDown()
        {
            if (!_onButton) return false;
            _onButton = false;
            var m = Menu();
            if (m == null) return false;
            if (!HasEntries(m)) { MenuAnnouncer.SayShown("No entries"); return true; }
            Select(m, m.SelectedIndex, null);
            return true;
        }

        public static bool Confirm()
        {
            if (!_onButton) return false;
            var m = Menu();
            if (m == null) { _onButton = false; return false; }
            bool export;
            if (HeaderButton(m, out export) == null) { _onButton = false; return false; }
            try
            {
                if (export) Export(m);
                else
                {
                    int before = ReplayCount();
                    FileBrowserNavigator.AfterChoose = paths => _chosen = paths != null && paths.Length > 0 ? paths[0] : null;
                    FileBrowserNavigator.OnClosed = () => Closed(m, false, null, before);
                    _chosen = null;
                    m.ImportReplayButton();
                }
            }
            catch (Exception ex) { ModLog.Warn("HistoryNavigator.Confirm", ex); }
            return true;
        }

        static bool HasEntries(HistoryMenu m)
        {
            var rows = Rows(m);
            if (rows != null) foreach (var r in rows) if (r is ReplayViewType) return true;
            return false;
        }

        static int ReplayCount()
        {
            try { return YARG.Replays.ReplayContainer.Replays.Count; } catch { return -1; }
        }

        static string _chosen;

        // E on a row, or the Export Replay button: save the selected entry's replay as a file, with the
        // mod's Listen and Repeat note (.yargaccess) next to it so the copy plays back the same way.
        public static bool ExportSelected()
        {
            var m = Menu();
            if (m == null || FileBrowserNavigator.IsActive) return false;
            Export(m);
            return true;
        }

        static void Export(HistoryMenu m)
        {
            var row = m.CurrentSelection as ReplayViewType;
            if (row == null) { MenuAnnouncer.SayShown(HasEntries(m) ? "Choose an entry first." : "No entries"); return; }
            string source = ReplayPath(row);
            if (source == null || !File.Exists(source)) { MenuAnnouncer.SayShown("This entry has no replay to export."); return; }
            _chosen = null;
            FileBrowserNavigator.AfterChoose = paths => _chosen = paths != null && paths.Length > 0 ? paths[0] : null;
            FileBrowserNavigator.OnClosed = () => Closed(m, true, source, 0);
            m.ExportReplayButton();
        }

        // After the file browser closes: say what happened, then where the player is again.
        static void Closed(HistoryMenu m, bool export, string source, int before)
        {
            string result;
            if (_chosen == null) result = "Canceled";
            else if (export)
            {
                if (File.Exists(_chosen))
                {
                    result = "Saved " + Path.GetFileName(_chosen);
                    try
                    {
                        string note = source + ".yargaccess";
                        if (File.Exists(note)) { File.Copy(note, _chosen + ".yargaccess", true); result += ", with its Listen and Repeat file. Keep the two files together"; }
                    }
                    catch (Exception ex) { ModLog.Warn("HistoryNavigator export note", ex); }
                }
                else result = "Export failed: the file was not saved";
            }
            else
            {
                int after = ReplayCount();
                result = after > before ? "Imported " + Path.GetFileName(_chosen)
                    : "Not added: " + Path.GetFileName(_chosen) + " is already in the list, or isn't a replay YARG can read";
            }
            _chosen = null;
            if (Menu() == null) { MenuAnnouncer.SayShown(result); return; }
            string where = _onButton ? ButtonText(m) : ListMenuAnnouncer.TakeActiveLine();
            if (!_onButton && string.IsNullOrEmpty(where))
            {
                ListMenuAnnouncer.ResetLast();
                m.SelectedIndex = m.SelectedIndex;
                where = ListMenuAnnouncer.TakeActiveLine();
            }
            MenuAnnouncer.Focused(string.IsNullOrEmpty(where) ? result + "." : result + ". " + where, true);
        }

        // ---- For EntryActions ----

        internal static ReplayViewType SelectedRow()
        {
            var m = Menu();
            return m != null ? m.CurrentSelection as ReplayViewType : null;
        }

        internal static GameRecord RecordOf(ReplayViewType row) => row != null && _recordF != null ? _recordF.GetValue(row) as GameRecord : null;
        internal static ReplayInfo EntryOf(ReplayViewType row) => row != null && _entryF != null ? _entryF.GetValue(row) as ReplayInfo : null;
        internal static string ReplayPathOf(ReplayViewType row) => ReplayPath(row);

        internal static string SongName(ReplayViewType row)
        {
            var song = row != null && _songF != null ? _songF.GetValue(row) as SongEntry : null;
            if (song != null) return song.Name;
            var entry = EntryOf(row);
            if (entry != null) return entry.SongName;
            return "this entry";
        }

        internal static string WhenOf(ReplayViewType row)
        {
            var record = RecordOf(row);
            var entry = EntryOf(row);
            var d = record != null ? record.Date : entry != null ? entry.Date : DateTime.MinValue;
            return d == DateTime.MinValue ? "" : When(d);
        }

        internal static bool IsImportTab()
        {
            var m = Menu();
            bool export;
            return m != null && HeaderButton(m, out export) != null && !export;
        }

        // Rebuilds the list after a delete, keeps the place (or the entry before it) and reads the result.
        internal static void Refresh(string result)
        {
            var m = Menu();
            if (m == null) { MenuAnnouncer.SayShown(result); return; }
            try
            {
                int keep = m.SelectedIndex;
                HarmonyLib.AccessTools.Method(typeof(HistoryMenu), "RequestViewListUpdate").Invoke(m, null);
                var rows = Rows(m);
                if (rows == null || !HasEntries(m)) { _onButton = false; MenuAnnouncer.Focused(result + ". No entries", true); return; }
                int i = Math.Min(keep, rows.Count - 1);
                while (i > 0 && !(rows[i] is ReplayViewType)) i--;
                if (!(rows[i] is ReplayViewType)) for (i = 0; i < rows.Count && !(rows[i] is ReplayViewType); i++) { }
                Select(m, i, result);
            }
            catch (Exception ex) { ModLog.Warn("HistoryNavigator.Refresh", ex); MenuAnnouncer.SayShown(result); }
        }

        // ---- Playing ----

        // Blue: play the selected entry's song yourself, as from the library, in your Play Style.
        public static bool Colour(string action)
        {
            if (action != "Blue") return false;
            var m = Menu();
            if (m == null || DialogNavigator.IsActive) return false;
            var row = m.CurrentSelection as ReplayViewType;
            if (row == null) { MenuAnnouncer.SayShown("Choose a song first."); return true; }
            var song = _songF != null ? _songF.GetValue(row) as SongEntry : null;
            if (song == null) { MenuAnnouncer.SayShown("This song is not in your library."); return true; }
            if (YARG.Player.PlayerContainer.Players.Count == 0) { MenuAnnouncer.SayShown("Connect a profile first."); return true; }
            try
            {
                YARG.GlobalVariables.State = YARG.PersistentState.Default;
                var state = YARG.GlobalVariables.State;
                state.CurrentSong = song;
                if (state.ShowSongs == null) state.ShowSongs = new List<SongEntry>();
                state.ShowSongs.Clear();
                state.ShowSongs.Add(song);
                state.PlayingAShow = false;
                YARG.GlobalVariables.State = state;
                // Said after the push, so it joins the difficulty screen's line instead of being cut off by it.
                UnityEngine.MonoSingleton<YARG.Menu.MenuManager>.Instance.PushMenu(YARG.Menu.MenuManager.Menu.DifficultySelect);
                MenuAnnouncer.Say("Play " + song.Name + ", " + (ModSettings.ListenAndRepeat ? "Listen and Repeat" : "Play What You Hear"));
            }
            catch (Exception ex) { ModLog.Warn("HistoryNavigator.Colour", ex); }
            return true;
        }
    }
}
