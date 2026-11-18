using System;
using System.Collections.Generic;
using MelonLoader;

namespace YargAccess
{
    // One place for the mod's warnings. The first time an error happens it is written in full, with
    // the exception type and stack trace, so a log sent for help says where it came from. The same
    // error again (same place, type and message) is counted rather than written every frame: the log
    // notes it at 10, 100, 1000... repeats.
    public static class ModLog
    {
        static readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        static readonly object _lock = new object();

        public static void Warn(string where, Exception ex)
        {
            if (ex == null) { Warn(where, "unknown error"); return; }
            int n = Count(where + "|" + ex.GetType().FullName + "|" + ex.Message);
            if (n == 1) MelonLogger.Warning(where + ": " + ex);
            else if (IsMilestone(n)) MelonLogger.Warning(where + ": the same error " + n + " times so far: " + ex.GetType().Name + ": " + ex.Message);
        }

        public static void Warn(string where, string message)
        {
            int n = Count(where + "|" + message);
            if (n == 1) MelonLogger.Warning(where + ": " + message);
            else if (IsMilestone(n)) MelonLogger.Warning(where + ": the same warning " + n + " times so far: " + message);
        }

        static int Count(string key)
        {
            lock (_lock)
            {
                _counts.TryGetValue(key, out int n);
                _counts[key] = ++n;
                return n;
            }
        }

        static bool IsMilestone(int n)
        {
            while (n >= 10 && n % 10 == 0) n /= 10;
            return n == 1;
        }
    }
}
