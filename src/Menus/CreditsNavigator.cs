using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using MelonLoader;

namespace YargAccess
{
    public static class CreditsNavigator
    {
        const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance;
        static FieldInfo _containerField;
        static readonly List<string> _lines = new List<string>();
        static int _idx;
        static bool _opened;

        public static bool IsActive
        {
            get { var c = UnityEngine.Object.FindObjectOfType<YARG.Menu.Credits.CreditsMenu>(); return c != null && c.isActiveAndEnabled; }
        }

        public static bool IsReading => _opened && IsActive;

        public static void Tick()
        {
            if (!IsActive) { _opened = false; return; }
            if (_opened) return;
            var items = Build();
            if (items.Count == 0) return;
            _lines.Clear();
            _lines.AddRange(items);
            _idx = 0;
            _opened = true;
            MenuAnnouncer.Focused(_lines.Count + " entries. " + Line());
        }

        public static void Move(int dir)
        {
            if (_lines.Count == 0) return;
            _idx = Mathf.Clamp(_idx + dir, 0, _lines.Count - 1);
            MenuAnnouncer.Focused(Line(), true);
        }

        public static void Jump(bool toEnd)
        {
            if (_lines.Count == 0) return;
            _idx = toEnd ? _lines.Count - 1 : 0;
            MenuAnnouncer.Focused(Line(), true);
        }

        public static void Repeat() { Move(0); }

        static string Line() { return _lines[_idx] + Keys.Pos(_idx, _lines.Count); }

        static List<string> Build()
        {
            var items = new List<string>();
            try
            {
                var cm = UnityEngine.Object.FindObjectOfType<YARG.Menu.Credits.CreditsMenu>();
                if (cm == null) return items;
                if (_containerField == null)
                    _containerField = typeof(YARG.Menu.Credits.CreditsMenu).GetField("_creditsContainer", BF);
                var container = _containerField != null ? _containerField.GetValue(cm) as Transform : null;
                if (container == null) return items;

                for (int i = 0; i < container.childCount; i++)
                {
                    var child = container.GetChild(i);
                    var parts = new List<string>();
                    foreach (var t in child.GetComponentsInChildren<TMPro.TMP_Text>(false))
                    {
                        if (t == null || string.IsNullOrWhiteSpace(t.text)) continue;
                        if (t.GetComponentInParent<Button>() != null) continue;
                        var s = TextUtil.Clean(t.text);
                        if (!string.IsNullOrWhiteSpace(s)) parts.Add(s);
                    }
                    if (parts.Count > 0) items.Add(string.Join(". ", parts));
                }
            }
            catch (Exception ex) { ModLog.Warn("CreditsNavigator.Build", ex); }
            return items;
        }
    }
}
