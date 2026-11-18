using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using TMPro;
using UnityEngine;
using YARG.Menu.ScoreScreen;

namespace YargAccess
{
    public static class ScoreScreenNavigator
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        static ScoreScreenMenu _menu;
        static readonly List<string> _lines = new List<string>();
        static int _card;
        static int _idx;
        static bool _built;

        static ScoreScreenMenu Menu
        {
            get
            {
                if (_menu == null) { try { _menu = UnityEngine.Object.FindObjectOfType<ScoreScreenMenu>(); } catch { } }
                return _menu;
            }
        }

        public static bool IsActive
        {
            get
            {
                try { var m = Menu; return m != null && m.gameObject.activeInHierarchy; }
                catch { return false; }
            }
        }

        public static void Reset()
        {
            _menu = null; _lines.Clear(); _card = 0; _idx = 0; _built = false;
        }

        public static void Move(int dir)
        {
            if (!Build()) return;
            if (_lines.Count == 0) return;
            _idx = Mathf.Clamp(_idx + dir, 0, _lines.Count - 1);
            Announce();
        }

        public static void SwitchCard(int dir)
        {
            if (!Build()) return;
            int n = Cards().Count;
            if (n <= 1) { MenuAnnouncer.SayShown(Loc.T("Only one player")); return; }
            _card = Mathf.Clamp(_card + dir, 0, n - 1);
            _idx = 0;
            Rebuild();
            ScrollTo(_card);
            Announce();
        }

        public static void Confirm()
        {
            YargInput.Invoke("Green");
        }

        public static string OpenLine()
        {
            try
            {
                Reset();
                if (!Build()) return "";
                var head = new List<string>(4);
                var song = Tmp(Menu, "_songTitle");
                var artist = Tmp(Menu, "_artistName");
                if (!string.IsNullOrEmpty(song))
                    head.Add(string.IsNullOrEmpty(artist) ? song : Loc.F("{0}, by {1}", song, artist));
                var band = Tmp(Menu, "_bandScore");
                if (!string.IsNullOrEmpty(band)) head.Add(Loc.F("band score {0}", band));
                var repeat = ListenRepeatScores.ScoreLine();
                if (!string.IsNullOrEmpty(repeat)) head.Add(repeat);
                if (_lines.Count > 0) head.Add(_lines[0]);
                head.Add(Loc.F("{0} for the stats, {1} to continue.", Keys.UpDown, Keys.Ok));
                return string.Join(". ", head.ToArray());
            }
            catch (Exception ex) { ModLog.Warn("ScoreScreenNavigator.OpenLine", ex); return ""; }
        }

        static void Announce()
        {
            if (_idx < 0 || _idx >= _lines.Count) return;
            var cards = Cards();
            var pos = Keys.Pos(_idx, _lines.Count);
            if (cards.Count > 1) pos += Loc.F(", player {0} of {1}", _card + 1, cards.Count);
            MenuAnnouncer.Focused(_lines[_idx] + pos);
        }

        static bool Build()
        {
            if (_built && _lines.Count > 0) return true;
            return Rebuild();
        }

        static bool Rebuild()
        {
            _lines.Clear();
            var cards = Cards();
            if (cards.Count == 0) return false;
            _card = Mathf.Clamp(_card, 0, cards.Count - 1);
            var card = cards[_card];

            var who = new List<string>(4);
            Add(who, Tmp(card, "_playerName"));
            Add(who, Tmp(card, "_instrument"));
            Add(who, Tmp(card, "_difficulty"));
            Add(who, Tmp(card, "_tagText"));
            // Stars on the first line, every play style (user, 2026-11-14).
            var stars = Stars(card);
            if (!string.IsNullOrEmpty(stars)) who.Add(stars);
            if (who.Count > 0) _lines.Add(string.Join(", ", who.ToArray()));

            Row(Loc.T("Score"), Tmp(card, "_score"));
            Row(Loc.T("Accuracy"), Tmp(card, "_accuracyPercent"));
            Row(Loc.T("Notes hit"), Slashed(Tmp(card, "_notesHit")));
            var missed = Tmp(card, "_notesMissed");
            Row(Loc.T("Notes missed"), missed != null ? missed.TrimStart('-', '−') : null);
            Row(Loc.T("Best streak"), Tmp(card, "_maxStreak"));
            Row(Loc.T("Star power phrases"), Slashed(Tmp(card, "_starpowerPhrases")));
            Row(Loc.T("Star power activations"), Tmp(card, "_starPowerActivations"));
            Row(Loc.T("Time in star power"), Tmp(card, "_timeInStarPower"));
            Row(Loc.T("Average multiplier"), Tmp(card, "_averageMultiplier"));
            Row(Loc.T("Band bonus"), Tmp(card, "_bandBonusScore"));
            Row(Loc.T("Average offset"), Tmp(card, "_averageOffset"));

            foreach (var f in card.GetType().GetFields(BF | BindingFlags.DeclaredOnly))
            {
                if (!typeof(TMP_Text).IsAssignableFrom(f.FieldType)) continue;
                var val = Clean(f.GetValue(card) as TMP_Text);
                if (string.IsNullOrEmpty(val)) continue;
                Row(FieldLabel(f.Name), val);
            }

            _idx = Mathf.Clamp(_idx, 0, Mathf.Max(0, _lines.Count - 1));
            _built = true;
            return _lines.Count > 0;
        }

        // The instrument cards' own rows, named from their field.
        static string FieldLabel(string field)
        {
            switch (field)
            {
                case "_overstrums": return Loc.T("Overstrums");
                case "_hoposStrummed": return Loc.T("Hopos strummed");
                case "_ghostInputs": return Loc.T("Ghost inputs");
                case "_overhits": return Loc.T("Overhits");
            }
            return Sentence(Describer.PrettifyName(field.TrimStart('_')));
        }

        static string Sentence(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1).ToLower();
        }

        static void Row(string label, string value)
        {
            if (!string.IsNullOrEmpty(value)) _lines.Add(Loc.F("{0}, {1}", label, value));
        }

        static void Add(List<string> list, string s)
        {
            if (!string.IsNullOrEmpty(s)) list.Add(s);
        }

        static string Slashed(string s)
        {
            return string.IsNullOrEmpty(s) ? s : s.Replace(" / ", Loc.T(" of ")).Replace("/", Loc.T(" of "));
        }

        static string Stars(Component card)
        {
            try
            {
                var f = card.GetType().GetField("_starView", BF)
                        ?? card.GetType().BaseType?.GetField("_starView", BF);
                var view = f?.GetValue(card) as Component;
                if (view == null) return "";
                // YARG's StarView swaps each image's sprite: empty, standard, gold or brutal.
                var t = view.GetType();
                var arr = t.GetField("_starImages", BF)?.GetValue(view) as UnityEngine.UI.Image[];
                var empty = t.GetField("_emptyStar", BF)?.GetValue(view) as Sprite;
                var gold = t.GetField("_goldStar", BF)?.GetValue(view) as Sprite;
                var brutal = t.GetField("_brutalStar", BF)?.GetValue(view) as Sprite;
                if (arr == null) return "";
                int lit = 0;
                Sprite kind = null;
                foreach (var img in arr)
                    if (img != null && img.sprite != null && img.sprite != empty) { lit++; kind = img.sprite; }
                if (lit == 0) return Loc.T("0 stars");
                if (kind != null && kind == brutal) return Loc.F("{0} brutal stars", lit);
                if (kind != null && kind == gold) return Loc.F("{0} gold stars", lit);
                return lit == 1 ? Loc.T("1 star") : Loc.F("{0} stars", lit);
            }
            catch { return ""; }
        }

        static List<Component> Cards()
        {
            var list = new List<Component>();
            try
            {
                var m = Menu;
                if (m == null) return list;
                var f = typeof(ScoreScreenMenu).GetField("_cardContainer", BF);
                var container = f?.GetValue(m) as Transform;
                if (container == null) return list;
                for (int i = 0; i < container.childCount; i++)
                {
                    var child = container.GetChild(i);
                    if (!child.gameObject.activeSelf) continue;
                    foreach (var mb in child.GetComponents<MonoBehaviour>())
                        if (mb != null && mb.GetType().Name.EndsWith("ScoreCard")) { list.Add(mb); break; }
                }
            }
            catch (Exception ex) { ModLog.Warn("ScoreScreenNavigator.Cards", ex); }
            return list;
        }

        static void ScrollTo(int index)
        {
            try
            {
                var m = Menu;
                var f = typeof(ScoreScreenMenu).GetField("_cardScrollRect", BF);
                var rect = f?.GetValue(m) as UnityEngine.UI.ScrollRect;
                int n = Cards().Count;
                if (rect != null && n > 1) rect.horizontalNormalizedPosition = (float) index / (n - 1);
            }
            catch { }
        }

        static string Tmp(Component owner, string field)
        {
            if (owner == null) return "";
            try
            {
                for (var t = owner.GetType(); t != null; t = t.BaseType)
                {
                    var f = t.GetField(field, BF);
                    if (f != null) return Clean(f.GetValue(owner) as TMP_Text);
                }
            }
            catch { }
            return "";
        }

        static string Clean(TMP_Text t)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return "";
            return TextUtil.Clean(t.text);
        }
    }
}
