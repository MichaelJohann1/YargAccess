using System;
using System.Reflection;
using MelonLoader;

namespace YargAccess
{
    // A patch whose target YARG has renamed or removed is skipped (with a line in the log) instead of
    // throwing inside PatchAll, which would stop every later patch from being applied.
    public static class PatchGuard
    {
        public static bool Has(Type type, string method)
        {
            try
            {
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                for (var t = type; t != null; t = t.BaseType)
                    foreach (var m in t.GetMethods(all))
                        if (m.Name == method) return true;
            }
            catch (Exception ex) { ModLog.Warn("PatchGuard", ex); }
            MelonLogger.Warning("Patch skipped: " + (type != null ? type.FullName : "?") + "." + method + " was not found in this YARG version.");
            return false;
        }
    }
}
