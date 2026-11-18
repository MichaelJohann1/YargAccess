using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine.InputSystem;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Player;

namespace YargAccess.Patches
{
    [HarmonyPatch]
    static class NavigatorKeyboardDoublePatch
    {
        static bool Prepare() => AccessTools.Method(typeof(Navigator), "ProcessInput") != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(Navigator), "ProcessInput");

        static bool Prefix(YargPlayer player, ref GameInput input)
        {
            try
            {
                if (VirtualKeyboard.IsOpen) return false;
                if (HelpList.IsOpen) return false;
                if (ButtonTest.IsOpen || DrumButtonTest.IsOpen || KeysButtonTest.IsOpen || VocalButtonTest.IsOpen) return false;
                if (QuickBindAnnouncer.IsWaiting) return false;
                // The mod drives the file browser; no menu action may reach the menu behind it.
                if (FileBrowserNavigator.IsActive) return false;
                if (ReviewBinds.Capturing) return false;

                if (player == null) return false;

                var bindings = player.Bindings;
                if (bindings == null || bindings.InputDevices == null) return true;

                bool keyboard = false, gamepad = false, guitar = false;
                foreach (var d in bindings.InputDevices)
                {
                    if (d is Keyboard) keyboard = true;
                    else if (d is Gamepad) gamepad = true;
                    else if (d != null && d.GetType().Name.IndexOf("Guitar", StringComparison.OrdinalIgnoreCase) >= 0) guitar = true;
                }

                if (keyboard) return false;

                var act = (MenuAction) input.Action;
                // The Play A Show picker is voted through YARG with the colour buttons (the mod only reads it).
                if ((gamepad || guitar) && ShowPickerNavigator.IsActive
                    && (act == MenuAction.Green || act == MenuAction.Red || act == MenuAction.Yellow || act == MenuAction.Blue || act == MenuAction.Orange))
                    return true;
                if ((gamepad || guitar) && act != MenuAction.Start) return false;
                if (act == MenuAction.Select && ShowPickerNavigator.IsActive) return false;
                if (InputDriver.PadOwnedByMod() && (act == MenuAction.Up || act == MenuAction.Down || act == MenuAction.Left
                    || act == MenuAction.Right || act == MenuAction.Green || act == MenuAction.Red))
                    return false;
                if ((act == MenuAction.Left || act == MenuAction.Right) && InputDriver.SettingsOpen())
                    return false;
            }
            catch (Exception ex) { ModLog.Warn("NavigatorKeyboardDoublePatch", ex); }
            return true;
        }
    }
}
