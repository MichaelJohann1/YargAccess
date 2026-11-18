using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using MelonLoader;
using YARG.Menu.Dialogs;

namespace YargAccess
{
    public static class DialogAnnouncer
    {
        static int _lastId;
        static Dialog _pending;
        static int _framesLeft;
        static int _pollCounter;

        public static void Schedule(Dialog d)
        {
            if (d == null) return;
            if (_pending != null && _pending.gameObject.activeInHierarchy) return;
            if (d.GetInstanceID() == _lastId) return;
            _pending = d;
            _framesLeft = 2;
        }

        public static void Tick()
        {
            if (_pending != null)
            {
                if (--_framesLeft <= 0)
                {
                    var d = _pending;
                    _pending = null;
                    AnnounceNow(d);
                }
                return;
            }

            if (++_pollCounter < 10) return;
            _pollCounter = 0;

            Dialog cur = null;
            try { cur = UnityEngine.Object.FindObjectOfType<Dialog>(); } catch { }
            if (cur == null) { _lastId = 0; return; }
            if (cur.GetInstanceID() != _lastId) Schedule(cur);
        }

        static void AnnounceNow(Dialog d)
        {
            if (d == null) return;
            _lastId = d.GetInstanceID();
            if (d is SongPickerListDialog) { ShowPickerNavigator.Opened(); return; }
            DialogNavigator.Reset();
            try
            {
                var sb = new StringBuilder();
                var title = d.Title;
                if (title != null && !string.IsNullOrWhiteSpace(title.text))
                    sb.Append(TextUtil.Clean(title.text));
                List<string> parts = null;
                foreach (var t in d.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (t == null || !t.isActiveAndEnabled) continue;
                    if (t.gameObject.name != "Message") continue;
                    var m = TextUtil.Clean(t.text);
                    parts = DialogNavigator.Sentences(m);
                    if (parts != null) m = parts[0];
                    if (!string.IsNullOrEmpty(m)) { if (sb.Length > 0) sb.Append(". "); sb.Append(m); }
                    break;
                }
                var focus = Tutorials.FocusOnOpen(d);
                if (!string.IsNullOrEmpty(focus)) MenuAnnouncer.MenuAlreadySpoken();
                if (parts != null)
                {
                    // A long message: the title and first sentence now, then each sentence as its own call.
                    // Up and Down step through the sentences; a tutorial page starts on the first one.
                    if (!string.IsNullOrEmpty(focus)) DialogNavigator.FocusPiece(0);
                    Speech.Then(parts[0], parts.GetRange(1, parts.Count - 1));
                    MenuAnnouncer.Focused(sb.ToString());
                    return;
                }
                if (!string.IsNullOrEmpty(focus)) { if (sb.Length > 0) sb.Append(sb[sb.Length - 1] == '.' ? " " : ". "); sb.Append(focus); }
                if (sb.Length > 0) MenuAnnouncer.Focused(sb.ToString());
            }
            catch (Exception ex) { ModLog.Warn("DialogAnnouncer.AnnounceNow", ex); }
        }
    }
}
