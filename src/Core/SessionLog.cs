using System;
using System.Linq;
using MelonLoader;
using UnityEngine.InputSystem;

namespace YargAccess
{
    // Logs controllers being plugged in or unplugged, and profiles being connected or disconnected,
    // while the game runs. Lost input is a common report, and this shows when a device went away.
    public static class SessionLog
    {
        static bool _installed;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            try
            {
                InputSystem.onDeviceChange += OnDeviceChange;
                YARG.Player.PlayerContainer.PlayerAdded += p => LogPlayer("Profile connected", p);
                YARG.Player.PlayerContainer.PlayerRemoved += p => LogPlayer("Profile disconnected", p);
            }
            catch (Exception ex) { ModLog.Warn("SessionLog.Install", ex); }
        }

        static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            try
            {
                if (device is Mouse) return;
                string what;
                switch (change)
                {
                    case InputDeviceChange.Added: what = "added"; break;
                    case InputDeviceChange.Removed: what = "removed"; break;
                    case InputDeviceChange.Disconnected: what = "disconnected"; break;
                    case InputDeviceChange.Reconnected: what = "reconnected"; break;
                    // Enabled / Disabled are left out: YARG disables every device whenever its window
                    // loses focus, which would fill the log on each Alt+Tab.
                    default: return;
                }
                MelonLogger.Msg("Input device " + what + ": " + DeviceName(device));
            }
            catch (Exception ex) { ModLog.Warn("SessionLog.OnDeviceChange", ex); }
        }

        public static string DeviceName(InputDevice d)
        {
            if (d == null) return "?";
            var product = d.description.product;
            return string.IsNullOrEmpty(product) ? d.displayName : product;
        }

        static void LogPlayer(string what, YARG.Player.YargPlayer p)
        {
            try
            {
                if (p == null) return;
                MelonLogger.Msg(what + ": " + p.Profile.Name + (p.Profile.IsBot ? " (bot)" : "")
                    + ", " + p.Profile.CurrentInstrument + ", devices: " + Devices(p));
            }
            catch (Exception ex) { ModLog.Warn("SessionLog.LogPlayer", ex); }
        }

        public static string Devices(YARG.Player.YargPlayer p)
        {
            var b = p.Bindings;
            var names = b.InputDevices.Select(DeviceName).ToList();
            if (b.Microphone != null) names.Add("microphone " + b.Microphone.DisplayName);
            return names.Count > 0 ? string.Join(", ", names) : "none";
        }
    }
}
