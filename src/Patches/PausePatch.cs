using System;
using UnityEngine;
using HarmonyLib;
using MelonLoader;

namespace YargAccess.Patches
{

    [HarmonyPatch(typeof(YARG.Gameplay.HUD.PauseMenuManager), "PushMenu")]
    static class PausePushPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(YARG.Gameplay.HUD.PauseMenuManager), "PushMenu") != null;

        static void Prefix(object __0)
        {
            try
            {
                Menu.OnEnter(PauseAnnouncer.Label(__0 != null ? __0.ToString() : ""));
                MenuAnnouncer.BeginBatch();
            }
            catch (Exception ex) { ModLog.Warn("PausePushPatch.Prefix", ex); }
        }

        static void Postfix(object __0, object __result)
        {
            PauseAnnouncer.OnPushMenu(__0 != null ? __0.ToString() : "", __result as Component);
        }
    }

    [HarmonyPatch(typeof(YARG.Gameplay.HUD.PauseMenuManager), "PopMenu")]
    static class PausePopPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(YARG.Gameplay.HUD.PauseMenuManager), "PopMenu") != null;
        static void Prefix() { try { Menu.OnEnter("Pause"); MenuAnnouncer.BeginBatch(); } catch { } }
        static void Postfix() { PauseAnnouncer.OnPopMenu(); }
    }

    [HarmonyPatch(typeof(YARG.Gameplay.GameManager), "ResumeCore")]
    static class ResumeCorePatch
    {
        static bool Prepare() => AccessTools.Method(typeof(YARG.Gameplay.GameManager), "ResumeCore") != null;
        static void Postfix() { PauseAnnouncer.OnResume(); }
    }
}
