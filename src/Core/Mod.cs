using System;
using MelonLoader;
using UnityEngine;
using YargAccess.Accessibility;

[assembly: MelonInfo(typeof(YargAccess.Mod), "YargAccess", YargAccess.Mod.Version, "Michael Johann")]
[assembly: MelonGame]

namespace YargAccess
{
    public class Mod : MelonMod
    {
        public const string Version = "0.1.4";

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("YargAccess v" + Version + " loaded" + (Speech.Quiet ? " (quiet test mode: no speech, audio muted)" : ""));

            try { Application.runInBackground = true; } catch { }

            ModSettings.Init();

            try { MenuAnnouncer.SuppressFor(5f); } catch { }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg("Scene initialized: " + sceneName);

            PracticeAssist.Reset();
            HelpList.Reset();
            HighwaySound.Reset();
            DrumSound.Reset();
            ProKeysSound.Reset();
            VocalSound.Reset();
            BeatTicks.Reset();
            PracticeSectionNavigator.Reset();
            ScoreScreenNavigator.Reset();
            SongLog.Reset();

            Speech.Initialize();
            MenuAnnouncer.SuppressFor(4f);

            DisableWrapAroundNavigation();

            NvdaFocusFix.Install();

            PracticeDevices.CleanLeftovers();

            if (sceneName == "MenuScene") SystemSpecs.LogOnce();
            SessionLog.Install();
        }

        float _muteAt;

        public override void OnUpdate()
        {
            if (Speech.Quiet && Time.unscaledTime >= _muteAt)
            {
                _muteAt = Time.unscaledTime + 0.25f;
                try
                {
                    ManagedBass.Bass.GlobalStreamVolume = 0;
                    ManagedBass.Bass.GlobalSampleVolume = 0;
                    ManagedBass.Bass.GlobalMusicVolume = 0;
                }
                catch { }
            }
            InputDriver.Tick();
            SongLog.Tick();
        }

        public override void OnGUI()
        {
            VirtualKeyboard.OnGUI();
            Tooltips.OnGUI();
        }

        public override void OnApplicationQuit()
        {
            NvdaFocusFix.Uninstall();
            Speech.Stop();
        }

        static bool _wrapDisabled;
        static void DisableWrapAroundNavigation()
        {
            if (_wrapDisabled) return;
            try
            {
                var settings = YARG.Settings.SettingsManager.Settings;
                var prop = settings?.GetType().GetProperty("WrapAroundNavigation");
                var setting = prop?.GetValue(settings);
                var valueProp = setting?.GetType().GetProperty("Value");
                if (valueProp != null) { valueProp.SetValue(setting, false); _wrapDisabled = true; }
            }
            catch (Exception ex) { ModLog.Warn("DisableWrapAroundNavigation", ex); }
        }

        public static string PrettifyScene(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var sb = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
