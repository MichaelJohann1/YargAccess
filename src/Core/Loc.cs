using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace YargAccess
{
    // The mod's own text in YARG's language (user, 2026-11-17). YARG takes its language only from the
    // -lang command line argument (CommandLineArgs, LocalizationManager.Initialize), which the YARG Access
    // launcher passes from its Settings tab; the mod reads the same argument, so its text matches YARG's.
    //
    // Every spoken or drawn English line goes through T (a whole line) or F (a line with {0}, {1} ... filled
    // in after translating). The English text is the key: Lang\<code>.json, built into the DLL, maps each
    // English line to its translation. A line with no translation is spoken in English (and logged once).
    // tools\extract-strings.ps1 lists every key; tools\check-lang.ps1 checks a language file against them.
    //
    // Rules for translations: keep {0}-style and {name}-style placeholders exactly; keep YARG's own words
    // as YARG's lang\<code>.json translates them (Star Power, Quickplay, menu and setting names).
    public static class Loc
    {
        public static readonly string[] Codes = { "en-US", "es-ES", "fr-FR", "it-IT", "ja-JP", "ko-KR", "pl-PL", "pt-BR", "pt-PT" };

        static Dictionary<string, string> _map;
        static readonly HashSet<string> _missing = new HashSet<string>();
        static string _code;

        // The culture code YARG was started with, en-US when none (or one YARG doesn't ship) was given.
        public static string Code
        {
            get
            {
                if (_code == null) _code = ReadCode();
                return _code;
            }
        }

        public static bool English => Code == "en-US";

        static string ReadCode()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++)
                    if (string.Equals(args[i], "-lang", StringComparison.OrdinalIgnoreCase))
                        foreach (var c in Codes)
                            if (string.Equals(c, args[i + 1], StringComparison.OrdinalIgnoreCase)) return c;
            }
            catch { }
            return "en-US";
        }

        static Dictionary<string, string> Map
        {
            get
            {
                if (_map != null) return _map;
                _map = new Dictionary<string, string>(StringComparer.Ordinal);
                if (English) return _map;
                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    using (var s = asm.GetManifestResourceStream("YargAccess.Lang." + Code + ".json"))
                    {
                        if (s == null) { MelonLoader.MelonLogger.Msg("Loc: no translation for " + Code + ", speaking English"); return _map; }
                        using (var r = new StreamReader(s, System.Text.Encoding.UTF8))
                        {
                            var d = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(r.ReadToEnd());
                            if (d != null)
                                foreach (var kv in d)
                                    if (!string.IsNullOrEmpty(kv.Value)) _map[kv.Key] = kv.Value;
                        }
                    }
                    MelonLoader.MelonLogger.Msg("Loc: " + Code + ", " + _map.Count + " lines");
                }
                catch (Exception ex) { ModLog.Warn("Loc.Load", ex); }
                return _map;
            }
        }

        // A whole line of the mod's text, in YARG's language.
        // A key may start with a context tag, "[key] Delete", when the same English word needs different
        // translations (the Delete key versus a Delete title). The tag is never spoken.
        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english)) return english;
            if (!English)
            {
                string t;
                if (Map.TryGetValue(english, out t)) return t;
                if (_missing.Add(english)) MelonLoader.MelonLogger.Msg("Loc missing (" + Code + "): " + english);
            }
            return Untagged(english);
        }

        static string Untagged(string s)
        {
            if (s.Length > 2 && s[0] == '[')
            {
                int end = s.IndexOf("] ", StringComparison.Ordinal);
                if (end > 0) return s.Substring(end + 2);
            }
            return s;
        }

        // A line with {0}, {1} ... : translated first, then filled in. A translation that breaks the
        // placeholders falls back to the English line.
        public static string F(string english, params object[] args)
        {
            var t = T(english);
            try { return string.Format(t, args); }
            catch (FormatException)
            {
                try { return string.Format(Untagged(english), args); } catch { return Untagged(english); }
            }
        }
    }
}
