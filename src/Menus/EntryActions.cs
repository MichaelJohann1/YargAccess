using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Menu.Data;
using YARG.Menu.History;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Persistent;
using YARG.Scores;

namespace YargAccess
{
    // Exporting and deleting History entries, and deleting songs from the library (user, 2026-11-17).
    // Keyboard: E exports, Delete deletes. Gamepad LT / guitar Select + strum down: in History a list
    // (Export Replay, Delete Entry); in the library the delete confirmation. Every delete asks first;
    // files go to the Recycle Bin, a History entry's score record is removed for good.
    public static class EntryActions
    {
        static string T(string label) => ModSettings.TextKey + label;

        // The song library itself, not Settings or a popup over it.
        static MusicLibraryMenu Library()
        {
            if (Menu.CurrentName != "Music Library") return null;
            var m = UnityEngine.Object.FindObjectOfType<MusicLibraryMenu>();
            return m != null && m.isActiveAndEnabled ? m : null;
        }

        static bool Blocked => DialogNavigator.IsActive || FileBrowserNavigator.IsActive || InputDriver.SettingsOpen()
            || ShowPickerNavigator.IsActive || HelpList.IsOpen || VirtualKeyboard.IsOpen;

        // LT / Select + strum down.
        public static bool Open()
        {
            if (Blocked) return false;
            if (HistoryNavigator.IsActive) { HistoryList(); return true; }
            var lib = Library();
            if (lib != null) { AskSongDelete(lib); return true; }
            return false;
        }

        // The Delete key.
        public static bool Delete()
        {
            if (Blocked) return false;
            if (HistoryNavigator.IsActive) { AskEntryDelete(); return true; }
            var lib = Library();
            if (lib != null) { AskSongDelete(lib); return true; }
            return false;
        }

        // ---- History ----

        static void HistoryList()
        {
            var row = HistoryNavigator.SelectedRow();
            var dm = DialogManager.Instance;
            if (dm == null) return;
            try
            {
                bool import = HistoryNavigator.IsImportTab();
                string tab = import ? "Switch To The History Tab" : "Switch To The Import Tab";
                var d = dm.ShowList("Entry actions");
                d.Title.text = row != null ? "Actions for " + HistoryNavigator.SongName(row) : "History actions";
                d.ClearButtons();
                d.AddDialogButton(T("Back"), MenuData.Colors.CancelButton, () => dm.ClearDialog());
                if (row != null)
                {
                    // On a pad LB switches tabs in History, so Play With Replay (LB, Orange, elsewhere) is here.
                    if (InputDriver.LastDevice == Keys.Device.Gamepad)
                        d.AddListButton("Play With Replay", () => { dm.ClearDialog(); InputDriver.PlayWithReplay(); }, false);
                    d.AddListButton("Export Replay", () => { dm.ClearDialog(); HistoryNavigator.ExportSelected(); }, false);
                    d.AddListButton(import ? "Remove From This List" : "Delete Entry", () => { dm.ClearDialog(); AskEntryDelete(); }, false);
                }
                // A guitar has no free tab buttons in History (its Yellow and Orange are Analyze and Play With Replay).
                d.AddListButton(tab, () => { dm.ClearDialog(); InputDriver.ToggleHistoryTab(); }, false);
                Tutorials.FocusNext(d, row == null ? tab : InputDriver.LastDevice == Keys.Device.Gamepad ? "Play With Replay" : "Export Replay");
            }
            catch (Exception ex) { ModLog.Warn("EntryActions.HistoryList", ex); }
        }

        static void AskEntryDelete()
        {
            var row = HistoryNavigator.SelectedRow();
            if (row == null) { MenuAnnouncer.SayShown("Choose an entry first."); return; }
            var record = HistoryNavigator.RecordOf(row);
            var entry = HistoryNavigator.EntryOf(row);
            string what = HistoryNavigator.SongName(row) + ", " + HistoryNavigator.WhenOf(row);
            string text, button;
            if (record != null)
            {
                text = "Delete " + what + "? Its score is removed from History and can't be brought back. Its replay goes to the Recycle Bin.";
                button = "Delete";
            }
            else if (entry != null)
            {
                text = "Remove " + what + " from the Import list? The replay file itself is kept.";
                button = "Remove";
            }
            else return;
            Confirm("Delete", text, button, () => DeleteEntry(record, entry, HistoryNavigator.ReplayPathOf(row)));
        }

        static void DeleteEntry(GameRecord record, ReplayInfo entry, string replay)
        {
            string result;
            try
            {
                if (record != null)
                {
                    DeleteScore(record.Id);
                    bool gone = true;
                    if (replay != null)
                    {
                        try { YARG.Replays.ReplayContainer.RemoveReplay(replay); } catch { }
                        gone = Recycle.Path(replay);
                        Recycle.Path(replay + ".yargaccess");
                    }
                    result = gone ? "Deleted" : "Deleted from History; the replay file could not be moved to the Recycle Bin";
                }
                else
                {
                    YARG.Replays.ReplayContainer.RemoveReplay(entry.FilePath);
                    try { YARG.Replays.ReplayContainer.WriteReplayCache(); } catch { }
                    result = "Removed from the list";
                }
            }
            catch (Exception ex) { ModLog.Warn("EntryActions.DeleteEntry", ex); result = "Could not delete it"; }
            HistoryNavigator.Refresh(result);
        }

        // Removes a GameRecord and its player scores from YARG's score database, then refreshes YARG's
        // cached high scores so the song library's stars and best scores follow.
        static void DeleteScore(int id)
        {
            var db = AccessTools.Field(typeof(ScoreContainer), "_db").GetValue(null);
            var conn = db.GetType().GetField("_db").GetValue(db);
            var exec = conn.GetType().GetMethod("Execute", new[] { typeof(string), typeof(object[]) });
            exec.Invoke(conn, new object[] { "DELETE FROM PlayerScores WHERE GameRecordId = ?", new object[] { id } });
            exec.Invoke(conn, new object[] { "DELETE FROM GameRecords WHERE Id = ?", new object[] { id } });
            try
            {
                (AccessTools.Field(typeof(ScoreContainer), "PlayerHighScores").GetValue(null) as IDictionary)?.Clear();
                (AccessTools.Field(typeof(ScoreContainer), "PlayerHighPercentages").GetValue(null) as IDictionary)?.Clear();
                AccessTools.Method(typeof(ScoreContainer), "FetchBandHighScores").Invoke(null, null);
                ScoreContainer.InvalidateScoreCache();
                YARG.Song.SongContainer.InvalidateStarsCache();
            }
            catch (Exception ex) { ModLog.Warn("EntryActions score cache", ex); }
        }

        // ---- Songs ----

        static void AskSongDelete(MusicLibraryMenu lib)
        {
            var view = lib.CurrentSelection as SongViewType;
            var song = view != null ? view.SongEntry : null;
            if (song == null) { MenuAnnouncer.SayShown("Choose a song first."); return; }
            if (song.SubType == EntryType.CON || song.SubType == EntryType.ExCON)
            {
                MenuAnnouncer.SayShown(song.Name + " is inside a pack file with other songs, so it can't be deleted on its own.");
                return;
            }
            Confirm("Delete Song", "Delete " + song.Name + " by " + song.Artist + "? Its files go to the Recycle Bin and it leaves your library. Your scores for it stay in History.",
                "Delete", () => DeleteSong(lib, song));
        }

        static void DeleteSong(MusicLibraryMenu lib, SongEntry song)
        {
            string loc = song.ActualLocation;
            bool ok = false;
            try
            {
                StopPreview(lib);
                if (IsSongRoot(loc)) { MenuAnnouncer.SayShown("That is a whole songs folder; it was not deleted."); return; }
                if (File.Exists(loc)) ok = Recycle.Path(loc);
                else if (Directory.Exists(loc))
                {
                    // Only the song's own files: a folder can also hold other songs in its subfolders.
                    ok = true;
                    foreach (var f in Directory.GetFiles(loc)) ok &= Recycle.Path(f);
                    if (ok && Directory.GetFileSystemEntries(loc).Length == 0) Recycle.Path(loc);
                }
                if (ok) RemoveFromLibrary(song);
            }
            catch (Exception ex) { ModLog.Warn("EntryActions.DeleteSong", ex); ok = false; }
            if (!ok) { MenuAnnouncer.SayShown("Could not delete " + song.Name + ": a file is in use. Try again after moving to another song."); return; }
            try { lib.RefreshAndReselect(false, true); } catch (Exception ex) { ModLog.Warn("EntryActions library refresh", ex); }
            MenuAnnouncer.SayShown("Deleted " + song.Name);
        }

        static bool IsSongRoot(string loc)
        {
            try
            {
                string full = Path.GetFullPath(loc).TrimEnd('\\', '/');
                if (Path.GetPathRoot(full).TrimEnd('\\', '/') == full) return true;
                foreach (var root in YARG.Settings.SettingsManager.Settings.SongFolders)
                    if (string.Equals(Path.GetFullPath(root).TrimEnd('\\', '/'), full, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            return false;
        }

        static void StopPreview(MusicLibraryMenu lib)
        {
            try
            {
                var c = AccessTools.Field(typeof(MusicLibraryMenu), "_previewCanceller").GetValue(lib) as System.Threading.CancellationTokenSource;
                if (c != null) c.Cancel();
                var ctxF = AccessTools.Field(typeof(MusicLibraryMenu), "_previewContext");
                var ctx = ctxF.GetValue(lib) as IDisposable;
                if (ctx != null) ctx.Dispose();
                ctxF.SetValue(lib, null);
            }
            catch (Exception ex) { ModLog.Warn("EntryActions.StopPreview", ex); }
        }

        // Takes the song out of YARG's song cache, then rebuilds the sorted lists from it.
        static void RemoveFromLibrary(SongEntry song)
        {
            var cache = AccessTools.Field(typeof(YARG.Song.SongContainer), "_songCache").GetValue(null);
            var entries = cache.GetType().GetField("Entries").GetValue(cache) as IDictionary;
            if (entries == null) return;
            object emptyKey = null;
            foreach (DictionaryEntry kv in entries)
            {
                var list = kv.Value as List<SongEntry>;
                if (list != null && list.Remove(song)) { if (list.Count == 0) emptyKey = kv.Key; break; }
            }
            if (emptyKey != null) entries.Remove(emptyKey);
            YARG.Song.SongContainer.RequestContainerRefresh();
        }

        // ---- Confirmation ----

        internal static void Confirm(string title, string text, string button, Action yes)
        {
            var dm = DialogManager.Instance;
            if (dm == null) return;
            try
            {
                var d = dm.ShowMessage(title, text);
                d.ClearButtons();
                d.AddDialogButton(T("Cancel"), MenuData.Colors.CancelButton, () => { dm.ClearDialog(); MenuAnnouncer.SayShown("Canceled"); });
                d.AddDialogButton(T(button), MenuData.Colors.ConfirmButton, () => { dm.ClearDialog(); yes(); });
                Tutorials.FocusNext(d, "Cancel");
            }
            catch (Exception ex) { ModLog.Warn("EntryActions.Confirm", ex); }
        }
    }
}
