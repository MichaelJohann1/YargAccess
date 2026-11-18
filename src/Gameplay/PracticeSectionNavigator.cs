using System;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using YARG.Assets.Script.Helpers;
using YARG.Gameplay.HUD;

namespace YargAccess
{
    public static class PracticeSectionNavigator
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        static PracticeSectionMenu _menu;
        static int _lastIndex = -1;
        static int? _lastFirst;
        static bool _tracking;

        static PracticeSectionMenu Menu
        {
            get
            {
                if (_menu == null)
                {
                    try { _menu = UnityEngine.Object.FindObjectOfType<PracticeSectionMenu>(); } catch { }
                }
                return _menu;
            }
        }

        public static bool IsActive
        {
            get
            {
                try
                {
                    var m = Menu;
                    return m != null && m.gameObject.activeInHierarchy && m.Sections != null && m.Sections.Count > 0;
                }
                catch { return false; }
            }
        }

        public static void Move(int dir)
        {
            var m = Menu;
            if (m == null) return;
            YargInput.Invoke(dir < 0 ? "Up" : "Down");
            Announce(false);
        }

        public static void Confirm()
        {
            var m = Menu;
            if (m == null) return;
            try
            {
                bool hadStart = m.FirstSelectedIndex.HasValue;
                string hovered = SectionName(m, m.HoveredIndex);
                string start = hadStart ? SectionName(m, m.FirstSelectedIndex.Value) : null;

                if (hadStart) PauseAnnouncer.SuppressNextResume();

                YargInput.Invoke("Green");
                _lastFirst = m.FirstSelectedIndex;

                if (!hadStart)
                {
                    MenuAnnouncer.SayShown(Loc.F("Start at {0}. Now choose the last section, then press {1}.", hovered, Keys.Ok));
                }
                else
                {
                    int a = m.FirstSelectedIndex ?? 0, b = m.HoveredIndex;
                    string first = a <= b ? start : hovered;
                    string last = a <= b ? hovered : start;
                    int count = Mathf.Abs(b - a) + 1;
                    string what = (first == last)
                        ? Loc.F("Practicing {0}", first)
                        : Loc.F("Practicing {0} to {1}, {2} sections", first, last, count);
                    MenuAnnouncer.SayShown(what + ". " + PracticeAssist.SpeedPhrase());
                    _lastIndex = -1;
                }
            }
            catch (Exception ex) { ModLog.Warn("PracticeSectionNavigator.Confirm", ex); }
        }

        public static bool Back()
        {
            var m = Menu;
            if (m == null) return false;
            try
            {
                bool hadStart = m.FirstSelectedIndex.HasValue;
                bool everSelected = HasSelectedSections(m);

                YargInput.Invoke("Red");
                _lastFirst = m.FirstSelectedIndex;

                if (hadStart) MenuAnnouncer.SayShown(Loc.T("Start section cleared."));
                else if (!everSelected) MenuAnnouncer.SayShown(Loc.T("Leaving practice."));
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Warn("PracticeSectionNavigator.Back", ex);
                return false;
            }
        }

        public static void Announce(bool force)
        {
            var m = Menu;
            if (m == null) return;
            try
            {
                int i = m.HoveredIndex;
                if (!force && i == _lastIndex) return;
                _lastIndex = i;
                MenuAnnouncer.Focused(Line(m));
            }
            catch (Exception ex) { ModLog.Warn("PracticeSectionNavigator.Announce", ex); }
        }

        public static string Line(PracticeSectionMenu m)
        {
            if (m == null) return "";
            int i = m.HoveredIndex;
            int n = m.Sections.Count;
            var s = SectionName(m, i) + Keys.Pos(i, n);
            if (m.FirstSelectedIndex.HasValue)
            {
                int count = Mathf.Abs(i - m.FirstSelectedIndex.Value) + 1;
                s += ", " + (count == 1 ? Loc.F("{0} section selected", count) : Loc.F("{0} sections selected", count));
            }
            return s;
        }

        public static string OpenLine()
        {
            var m = Menu;
            if (m == null) return "";
            _lastIndex = m.HoveredIndex;
            return Line(m) + ". " + Loc.F("{0} to choose a section, {1} to set the start.", Keys.UpDown, Keys.Ok);
        }

        public static void Reset() { _lastIndex = -1; _menu = null; _tracking = false; }

        public static void Tick()
        {
            if (!IsActive) { _tracking = false; return; }
            var m = Menu;
            try
            {
                if (!_tracking)
                {
                    _tracking = true;
                    _lastFirst = m.FirstSelectedIndex;
                    if (_lastIndex < 0) _lastIndex = m.HoveredIndex;
                    return;
                }
                var first = m.FirstSelectedIndex;
                if (first != _lastFirst)
                {
                    _lastFirst = first;
                    _lastIndex = m.HoveredIndex;
                    MenuAnnouncer.SayShown(first.HasValue
                        ? Loc.F("Start at {0}. Now choose the last section, then press {1}.", SectionName(m, first.Value), Keys.Ok)
                        : Loc.T("Start section cleared."));
                    return;
                }
                Announce(false);
            }
            catch (Exception ex) { ModLog.Warn("PracticeSectionNavigator.Tick", ex); }
        }

        static string SectionName(PracticeSectionMenu m, int i)
        {
            if (m == null || m.Sections == null || i < 0 || i >= m.Sections.Count) return "";
            var raw = m.Sections[i].Name;
            try { return PracticeSectionHelper.ParseSectionName(raw); }
            catch { return Describer.PrettifyName(raw); }
        }

        static bool HasSelectedSections(PracticeSectionMenu m)
        {
            try
            {
                var f = typeof(PracticeSectionMenu).GetField("_hasSelectedSections", BF);
                return f != null && (bool) f.GetValue(m);
            }
            catch { return false; }
        }
    }
}
