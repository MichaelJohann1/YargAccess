using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MelonLoader;
using SimpleFileBrowser;

namespace YargAccess
{
    public static class FileBrowserNavigator
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        enum Kind { ChooseHere, SaveHere, FileName, Parent, Folder, File, Places, Place, Cancel }
        class Row { public Kind Kind; public string Label; public string Path; }

        static readonly List<Row> _rows = new List<Row>();
        static int _idx;
        static bool _inPlaces;
        static bool _wasOpen;
        static string _builtFor;
        static string _replaceAsked;

        // Set by the menu that opens the browser: run with the chosen paths after a choice, and once when
        // the browser closes (chosen or canceled), so the menu can say where the player is.
        public static Action<string[]> AfterChoose;
        public static Action OnClosed;

        public static bool IsActive
        {
            get { try { return FileBrowser.IsOpen; } catch { return false; } }
        }

        public static void Tick()
        {
            bool open = IsActive;
            if (open && !_wasOpen)
            {
                FileBrowser.CanDeleteFiles = false;
                FileBrowser.CanRenameFiles = false;
                // Replacing a file is confirmed by the mod (a second press), not by the browser's own panel.
                FileBrowser.ShowFileOverwriteDialog = false;
                _inPlaces = false;
                _idx = 0;
                _builtFor = null;
                _replaceAsked = null;
                Rebuild();
                string what = FoldersMode ? Loc.T("Choose a folder.") : SaveMode ? Loc.T("Save a file.") : Loc.T("Choose a file.");
                MenuAnnouncer.Say(what + " " + Where() + ". " + RowText(_idx)
                    + " " + Loc.F("{0} to move, {1} to open or choose, Backspace for the folder above, Escape to cancel.", Keys.UpDown, Keys.Ok));
            }
            if (!open && _wasOpen)
            {
                FileBrowser.ShowFileOverwriteDialog = true;
                var closed = OnClosed;
                OnClosed = null;
                AfterChoose = null;
                if (closed != null) { try { closed(); } catch (Exception ex) { ModLog.Warn("FileBrowserNavigator.OnClosed", ex); } }
            }
            _wasOpen = open;
        }

        static FileBrowser Browser => FileBrowser.Instance;

        static string CurrentPath
        {
            get { try { return (string) typeof(FileBrowser).GetProperty("CurrentPath", BF).GetValue(Browser, null); } catch { return ""; } }
            set { try { typeof(FileBrowser).GetProperty("CurrentPath", BF).SetValue(Browser, value, null); } catch (Exception ex) { ModLog.Warn("FileBrowserNavigator path", ex); } }
        }

        static bool FoldersMode
        {
            get
            {
                try { return typeof(FileBrowser).GetField("m_pickerMode", BF).GetValue(Browser).ToString() != "Files"; }
                catch { return true; }
            }
        }

        // A save dialog (it accepts a file name that doesn't exist yet).
        static bool SaveMode
        {
            get
            {
                try { return !FoldersMode && (bool) typeof(FileBrowser).GetField("m_acceptNonExistingFilename", BF).GetValue(Browser); }
                catch { return false; }
            }
        }

        static TMPro.TMP_InputField FilenameField =>
            typeof(FileBrowser).GetField("filenameInputField", BF).GetValue(Browser) as TMPro.TMP_InputField;

        static string Filename { get { try { var f = FilenameField; return f != null ? f.text : ""; } catch { return ""; } } }

        static void CallPrivate(string name)
        {
            typeof(FileBrowser).GetMethod(name, BF).Invoke(Browser, null);
        }

        static void SetFilename(string text)
        {
            var f = typeof(FileBrowser).GetField("filenameInputField", BF).GetValue(Browser) as TMPro.TMP_InputField;
            if (f != null) f.text = text;
        }

        static void Rebuild()
        {
            string path = CurrentPath;
            string key = (_inPlaces ? "places|" : "folder|") + path;
            if (key == _builtFor && _rows.Count > 0) return;
            _builtFor = key;
            _rows.Clear();
            if (_inPlaces)
            {
                try
                {
                    var links = typeof(FileBrowser).GetField("allQuickLinks", BF).GetValue(Browser) as IList;
                    if (links != null)
                        foreach (FileBrowserQuickLink l in links)
                            if (l != null)
                            {
                                string name = TextUtil.Clean(l.Name);
                                string label = string.Equals(name, l.TargetPath, StringComparison.OrdinalIgnoreCase) ? Loc.F("{0} drive", name) : name + ", " + l.TargetPath;
                                _rows.Add(new Row { Kind = Kind.Place, Label = label, Path = l.TargetPath });
                            }
                }
                catch (Exception ex) { ModLog.Warn("FileBrowserNavigator places", ex); }
                _rows.Add(new Row { Kind = Kind.Cancel, Label = Loc.T("Cancel") });
            }
            else
            {
                if (FoldersMode) _rows.Add(new Row { Kind = Kind.ChooseHere, Label = Loc.F("Choose this folder, {0}", path), Path = path });
                if (SaveMode)
                {
                    _rows.Add(new Row { Kind = Kind.SaveHere, Label = Loc.T("Save here"), Path = path });
                    _rows.Add(new Row { Kind = Kind.FileName, Label = Loc.T("File name") });
                }
                string parent = null;
                try { var p = Directory.GetParent(path); if (p != null) parent = p.FullName; } catch { }
                if (parent != null) _rows.Add(new Row { Kind = Kind.Parent, Label = Loc.F("Up to {0}", parent), Path = parent });
                try
                {
                    var entries = typeof(FileBrowser).GetField("validFileEntries", BF).GetValue(Browser) as IList;
                    if (entries != null)
                        foreach (FileSystemEntry e in entries)
                            _rows.Add(e.IsDirectory
                                ? new Row { Kind = Kind.Folder, Label = Loc.F("{0}, folder", e.Name), Path = e.Path }
                                : new Row { Kind = Kind.File, Label = Loc.F("{0}, file", e.Name), Path = e.Path });
                }
                catch (Exception ex) { ModLog.Warn("FileBrowserNavigator entries", ex); }
                _rows.Add(new Row { Kind = Kind.Places, Label = Loc.T("Drives and places") });
                _rows.Add(new Row { Kind = Kind.Cancel, Label = Loc.T("Cancel") });
            }
            if (_idx >= _rows.Count) _idx = _rows.Count - 1;
            if (_idx < 0) _idx = 0;
        }

        static string Where()
        {
            if (_inPlaces) return Loc.F("Drives and places, {0} items", Math.Max(0, _rows.Count - 1));
            int n = 0;
            foreach (var r in _rows) if (r.Kind == Kind.Folder || r.Kind == Kind.File) n++;
            return CurrentPath + ", " + (n == 0 ? Loc.T("empty") : n == 1 ? Loc.F("{0} item", n) : Loc.F("{0} items", n));
        }

        static string RowText(int i)
        {
            if (i < 0 || i >= _rows.Count) return "";
            var r = _rows[i];
            string hint = "";
            switch (r.Kind)
            {
                case Kind.ChooseHere: hint = Keys.Role("button") + Loc.F(", {0} to choose it", Keys.Ok); break;
                case Kind.SaveHere: return Loc.F("Save here as {0}", Filename) + Keys.Role("button") + Keys.Pos(i, _rows.Count);
                case Kind.FileName: return Loc.F("File name, {0}", Keys.RoleThen("edit box", Filename)) + Loc.F(", {0} to type", Keys.Ok) + Keys.Pos(i, _rows.Count);
                case Kind.Cancel: hint = Keys.Role("button"); break;
                case Kind.Folder: hint = Loc.F(", {0} to open", Keys.Ok); break;
                case Kind.File: hint = SaveMode ? Loc.F(", {0} to save over it", Keys.Ok) : Loc.F(", {0} to choose", Keys.Ok); break;
            }
            return r.Label + hint + Keys.Pos(i, _rows.Count);
        }

        public static void Move(int dir)
        {
            Rebuild();
            if (_rows.Count == 0) return;
            _idx = Math.Max(0, Math.Min(_rows.Count - 1, _idx + dir));
            MenuAnnouncer.Focused(RowText(_idx));
        }

        public static void Jump(bool end)
        {
            Rebuild();
            if (_rows.Count == 0) return;
            _idx = end ? _rows.Count - 1 : 0;
            MenuAnnouncer.Focused(RowText(_idx));
        }

        public static void Confirm()
        {
            Rebuild();
            if (_idx < 0 || _idx >= _rows.Count) return;
            var r = _rows[_idx];
            try
            {
                switch (r.Kind)
                {
                    case Kind.ChooseHere:
                        SetFilename("");
                        Choose(r.Path, true);
                        break;
                    case Kind.SaveHere:
                        if (string.IsNullOrWhiteSpace(Filename)) { MenuAnnouncer.SayShown(Loc.T("Type a file name first.")); break; }
                        if (File.Exists(Path.Combine(CurrentPath, Filename)) && _replaceAsked != Filename)
                        {
                            _replaceAsked = Filename;
                            MenuAnnouncer.SayShown(Loc.F("{0} already exists here. Press {1} again to replace it.", Filename, Keys.Ok));
                            break;
                        }
                        Choose(Path.Combine(CurrentPath, Filename), false);
                        break;
                    case Kind.FileName:
                        var field = FilenameField;
                        if (field != null) TextEdit.Begin(field, Loc.T("File name"), text => { _replaceAsked = null; MenuAnnouncer.Say(Loc.F("File name, {0}", text)); });
                        break;
                    case Kind.Parent:
                    case Kind.Folder:
                    case Kind.Place:
                        Open(r.Path);
                        break;
                    case Kind.File:
                        if (SaveMode && _replaceAsked != r.Path)
                        {
                            _replaceAsked = r.Path;
                            MenuAnnouncer.SayShown(Loc.F("Replace {0}? Press {1} again to replace it.", Path.GetFileName(r.Path), Keys.Ok));
                            break;
                        }
                        SetFilename(Path.GetFileName(r.Path));
                        Choose(r.Path, false);
                        break;
                    case Kind.Places:
                        _inPlaces = true; _idx = 0; _builtFor = null; Rebuild();
                        MenuAnnouncer.Say(Where() + ". " + RowText(0));
                        break;
                    case Kind.Cancel:
                        CallPrivate("OnCancelButtonClicked");
                        if (OnClosed == null) MenuAnnouncer.SayShown(Loc.T("Canceled"));
                        break;
                }
            }
            catch (Exception ex) { ModLog.Warn("FileBrowserNavigator.Confirm", ex); }
        }

        // Escape (and a controller's back button) cancels; Backspace goes to the folder above.
        public static void Back()
        {
            Rebuild();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            bool parentKey = kb != null && kb.backspaceKey.wasPressedThisFrame;
            if (!parentKey)
            {
                try { CallPrivate("OnCancelButtonClicked"); } catch { }
                if (OnClosed == null) MenuAnnouncer.SayShown(Loc.T("Canceled"));
                return;
            }
            if (_inPlaces)
            {
                try { CallPrivate("OnCancelButtonClicked"); } catch { }
                if (OnClosed == null) MenuAnnouncer.SayShown(Loc.T("Canceled"));
                return;
            }
            string parent = null;
            try { var p = Directory.GetParent(CurrentPath); if (p != null) parent = p.FullName; } catch { }
            if (parent != null) { Open(parent); return; }
            _inPlaces = true; _idx = 0; _builtFor = null; Rebuild();
            MenuAnnouncer.Say(Where() + ". " + RowText(0));
        }

        static void Open(string path)
        {
            string before = CurrentPath;
            _inPlaces = false;
            CurrentPath = path;
            if (CurrentPath == before && !string.Equals(path, before, StringComparison.OrdinalIgnoreCase))
            {
                MenuAnnouncer.SayShown(Loc.F("Can't open {0}", path));
                return;
            }
            _idx = 0;
            _builtFor = null;
            Rebuild();
            MenuAnnouncer.Say(Where() + ". " + RowText(0));
        }

        static void Choose(string path, bool folder)
        {
            var after = AfterChoose;
            CallPrivate("OnSubmitButtonClicked");
            if (IsActive) { MenuAnnouncer.SayShown(folder ? Loc.T("Can't choose that folder") : Loc.T("Can't choose that file")); return; }
            if (after != null)
            {
                AfterChoose = null;
                string[] result = null;
                try { result = FileBrowser.Result; } catch { }
                try { after(result ?? new[] { path }); } catch (Exception ex) { ModLog.Warn("FileBrowserNavigator.AfterChoose", ex); }
                return;
            }
            Captions.Say(folder ? Loc.F("Folder set to {0}. Scan Songs to add its songs.", path) : Loc.F("Chose {0}", Path.GetFileName(path)));
        }
    }
}
