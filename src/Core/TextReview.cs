using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace YargAccess
{
    public static class TextReview
    {
        static string _text = "";
        static readonly List<string> _pieces = new List<string>();
        static int _idx = -1;
        static bool _speaking;
        static string _short, _full;

        // Japanese full-width punctuation (、。！？) is followed by no space, so it splits on its own.
        static readonly Regex Split = new Regex(@"(?<=[,.!?;:])\s+|(?<=[、。，．！？；：])\s*");

        public static void Spoken(string text)
        {
            if (_speaking || string.IsNullOrWhiteSpace(text)) return;
            if (_short != null && text.Contains(_short)) text = _full;
            _short = _full = null;
            Set(text);
        }

        public static void SetExtended(string shortText, string fullText)
        {
            if (string.IsNullOrEmpty(shortText) || shortText == fullText) { _short = _full = null; return; }
            _short = shortText;
            _full = fullText;
        }

        static void Set(string text)
        {
            _text = text;
            _idx = -1;
            _pieces.Clear();
            foreach (var raw in Split.Split(text))
            {
                var p = raw.Trim().TrimEnd(',', ';', '、', '，', '；');
                if (p.EndsWith(".") && !p.EndsWith("..")) p = p.Substring(0, p.Length - 1);
                if (p.Length > 0) _pieces.Add(p);
            }
        }

        public static void Next() { Step(+1); }
        public static void Previous() { Step(-1); }

        static void Step(int dir)
        {
            if (_pieces.Count == 0) { Say(Loc.T("Nothing to review")); return; }
            if (_idx < 0) _idx = dir > 0 ? 0 : _pieces.Count - 1;
            else _idx = Math.Max(0, Math.Min(_pieces.Count - 1, _idx + dir));
            Say(_pieces[_idx]);
        }

        public static void All()
        {
            if (_text.Length == 0) { Say(Loc.T("Nothing to review")); return; }
            _idx = -1;
            Say(_text);
        }

        static void Say(string s)
        {
            _speaking = true;
            try { Speech.Say(s, true); }
            finally { _speaking = false; }
        }
    }
}
