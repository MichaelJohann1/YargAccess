using System;
using System.Reflection;
using HarmonyLib;
using YARG.Core.Input;
using YARG.Player;

namespace YargAccess.Patches
{
    // YARG's calibrator counts every menu button as a tap, Back included. The mod gives the calibrator a
    // Back (B, Red, Escape, Backspace) and help (View, Select), so those presses are not taps: without this
    // a Back while waiting for the first press would start the clicks for one frame before stopping them.
    [HarmonyPatch]
    static class CalibratorInputPatch
    {
        static Type Target => Type.GetType("YARG.Menu.Calibrator.Calibrator, Assembly-CSharp");

        static bool Prepare() => Target != null && PatchGuard.Has(Target, "OnMenuInput");
        static MethodBase TargetMethod() => AccessTools.Method(Target, "OnMenuInput");

        static bool Prefix(YargPlayer player, ref GameInput input)
        {
            try
            {
                var act = (MenuAction) input.Action;
                if (act == MenuAction.Red || act == MenuAction.Select) return false;
            }
            catch (Exception ex) { ModLog.Warn("CalibratorInputPatch", ex); }
            return true;
        }
    }
}
