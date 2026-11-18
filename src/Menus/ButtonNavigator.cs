using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MelonLoader;

namespace YargAccess
{
    public static class ButtonNavigator
    {
        static readonly List<Button> _buttons = new List<Button>();
        static Button _focused;

        public static void Move(int dir)
        {
            Rebuild();
            if (_buttons.Count == 0) { _focused = null; return; }
            int idx = _focused != null ? _buttons.IndexOf(_focused) : -1;
            if (idx < 0) idx = dir > 0 ? 0 : _buttons.Count - 1;
            else idx = Mathf.Clamp(idx + dir, 0, _buttons.Count - 1);
            _focused = _buttons[idx];
            MenuAnnouncer.Focused(Describer.GetLabel(_focused.gameObject) + Keys.Role("button") + Keys.Pos(idx, _buttons.Count), true);
        }

        public static void Confirm()
        {
            if (_focused == null || !_focused.isActiveAndEnabled) return;
            try { _focused.onClick.Invoke(); }
            catch (Exception ex) { ModLog.Warn("ButtonNavigator.Confirm", ex); }
        }

        public static void Reset() { _buttons.Clear(); _focused = null; }

        static void Rebuild()
        {
            _buttons.Clear();
            var seen = new HashSet<string>();
            foreach (var b in UnityEngine.Object.FindObjectsOfType<Button>())
            {
                if (b == null || !b.isActiveAndEnabled || !b.interactable) continue;
                if (IsExcluded(b)) continue;
                var label = Describer.GetLabel(b.gameObject);
                if (string.IsNullOrEmpty(label) || label.Length > 48) continue;
                if (CountChar(label, ',') >= 2) continue;
                if (!seen.Add(label.ToLowerInvariant())) continue;
                _buttons.Add(b);
            }
            _buttons.Sort((a, c) =>
            {
                Vector3 pa = a.transform.position, pc = c.transform.position;
                if (Mathf.Abs(pa.y - pc.y) > 1f) return pc.y.CompareTo(pa.y);
                return pa.x.CompareTo(pc.x);
            });
        }

        static bool IsExcluded(Button b)
        {
            for (var t = b.transform; t != null; t = t.parent)
            {
                var n = t.gameObject.name;
                if (n == "Music Player") return true;
                if (n.IndexOf("HelpBar", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        static int CountChar(string s, char ch) { int n = 0; foreach (var c in s) if (c == ch) n++; return n; }
    }
}
