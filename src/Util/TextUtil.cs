using System.Text.RegularExpressions;

namespace YargAccess
{
    public static class TextUtil
    {
        static readonly Regex RichTag = new Regex("<[^>]+>", RegexOptions.Compiled);
        static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);
        static readonly Regex BidiControl = new Regex("[\\u200B-\\u200F\\u202A-\\u202E\\u2066-\\u2069\\uFEFF]", RegexOptions.Compiled);
        static readonly Regex LineBreak = new Regex(@"<br\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string Clean(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var stripped = BidiControl.Replace(RichTag.Replace(LineBreak.Replace(s, ", "), ""), "");
            return Whitespace.Replace(stripped, " ").Trim().TrimEnd(',');
        }

        public static string[] CleanLines(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new string[0];
            var parts = LineBreak.Split(s);
            var result = new System.Collections.Generic.List<string>(parts.Length);
            foreach (var p in parts)
            {
                var c = Clean(p);
                if (!string.IsNullOrEmpty(c)) result.Add(c);
            }
            return result.ToArray();
        }
    }
}
