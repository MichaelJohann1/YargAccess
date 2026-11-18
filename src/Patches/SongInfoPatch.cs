using System;
using HarmonyLib;
using MelonLoader;
using TMPro;
using YARG.Gameplay.HUD;

namespace YargAccess.Patches
{
    [HarmonyPatch(typeof(SongInfoText), "Start")]
    static class SongInfoPatch
    {
        static bool Prepare() => PatchGuard.Has(typeof(SongInfoText), "Start");

        static void Postfix(SongInfoText __instance)
        {
            try
            {
                if (__instance == null || ModSettings.InSongSpeech == null || ModSettings.InSongSpeech.Value != "all") return;
                var f = AccessTools.Field(typeof(SongInfoText), "_text");
                var t = f != null ? f.GetValue(__instance) as TMP_Text : null;
                if (t == null) return;
                var lines = t.text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var parts = new System.Collections.Generic.List<string>();
                foreach (var l in lines)
                {
                    var s = TextUtil.Clean(l);
                    if (!string.IsNullOrEmpty(s)) parts.Add(s);
                }
                if (parts.Count > 0) Speech.Say(string.Join(". ", parts.ToArray()), false);
            }
            catch (Exception ex) { ModLog.Warn("SongInfoPatch", ex); }
        }
    }
}
