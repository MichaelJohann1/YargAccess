using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YargAccess
{
    public static class Tooltips
    {

        static readonly Dictionary<string, string> Profile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Instrument Select", Loc.T("The kind of instrument this profile plays, such as five-fret guitar, four-lane, pro or five-lane drums, pro keys or vocals. It decides which parts you can pick and which buttons you bind.") },
            { "Note Speed", Loc.T("How fast notes scroll down the highway. It only changes what is drawn; the mod's sounds play at the same times.") },
            { "Highway Length", Loc.T("How far ahead the highway shows notes. It only changes what is drawn.") },
            { "Lefty Flip", Loc.T("Mirrors the lanes left to right for left-handed players. The mod's note sounds are mirrored with them.") },
            { "Engine Preset", Loc.T("The rules for hitting notes: the size of the hit window and how forgiving strums and frets are.") },
            { "Theme Select", Loc.T("How the notes and frets look. Visual only.") },
            { "Color Profile Select", Loc.T("The colours of the notes, frets and effects. Visual only.") },
            { "Camera Preset", Loc.T("The camera angle and curve of the highway. Visual only.") },
            { "Highway Preset", Loc.T("The highway's colours and background. Visual only.") },
            { "Rock Meter Preset", Loc.T("How quickly you fail: how much misses and overhits cost and how fast hits win it back.") },
            { "Input Calibration", Loc.T("This profile's own timing correction in milliseconds, added to the game-wide calibration. Raise it if your hits count as late, lower it if they count as early.") },
            { "Range Disable", Loc.T("Shows markers where a part moves to a different set of five lanes, mostly on keys. Visual only.") },
        };

        static readonly KeyValuePair<string, string>[] ProfileByLabel =
        {
            new KeyValuePair<string, string>("Open", Loc.T("Whether the highway shows a lane for open notes: never, always, or only when the chart has open notes. Visual only.")),
            new KeyValuePair<string, string>("Cymbal", Loc.T("Draws cymbal notes as cymbals instead of flat notes. Visual only.")),
            new KeyValuePair<string, string>("Activation", Loc.T("On drums, which hit at the end of a fill turns star power on. Rightmost Note: the rightmost pad of the fill's last note. All Notes: any pad of that note.")),
            new KeyValuePair<string, string>("Hand Position", Loc.T("Shows hand position markers on the highway. Visual only.")),
        };

        public static string ForProfileSetting(string key, string label)
        {
            string tip;
            if (key != null && Profile.TryGetValue(key, out tip)) return tip;
            if (label != null)
                foreach (var kv in ProfileByLabel)
                    if (label.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0) return kv.Value;
            return null;
        }

        static readonly Dictionary<string, string> EnginePresets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Default", Loc.T("A fixed hit window, 70 milliseconds either side of each note.") },
            { "Casual", Loc.T("More forgiving than Default: strums and frets may be a little further apart, hammer-ons can be held before they arrive, the wrong fret doesn't break your streak, and rolls are easier.") },
            { "Precision", Loc.T("Stricter than Default: the hit window shrinks from 60 to as little as 20 milliseconds either side as notes get faster, early strums are forgiven less, and vocals need truer pitch.") },
            { "Solo Taps", Loc.T("Default, plus during solos the solo frets play notes without strumming.") },
        };

        static readonly Dictionary<string, string> RockMeterPresets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Normal", Loc.T("The standard rock meter.") },
            { "Easy", Loc.T("Misses and overhits cost less and hits win back more than Normal.") },
            { "Casual", Loc.T("The most forgiving: misses cost half as much and hits win back twice as much as Normal.") },
            { "Hard", Loc.T("Misses cost one and a half times as much and hits win back half as much as Normal,, and you start with the meter half full.") },
            { "Unfair", Loc.T("Misses cost three times as much and hits win back a quarter as much as Normal, starting half full.") },
        };

        // Built-in camera presets (YARG.Core CameraPreset.Defaults). The names copy other games' camera
        // angles: The Band is Rock Band, Hero is Guitar Hero, Clone is Clone Hero.
        static readonly Dictionary<string, string> CameraPresets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Default", Loc.T("YARG's own view: the camera fairly high behind the highway, tilted down, and the far end of the highway curves gently away.") },
            { "Circular", Loc.T("Made for the Circular theme: a slightly wider view from a little lower and further back, with a flat highway.") },
            { "High FOV", Loc.T("Like Default with a wider view, so more of the highway fits on the screen.") },
            { "The Band 1", Loc.T("Copies a Rock Band camera: a narrower view, a little lower and tilted down a bit more than Default, with the curved highway.") },
            { "The Band 2", Loc.T("Copies a Rock Band camera: the narrowest view, high and close to the highway, so the notes look bigger.") },
            { "The Band 3", Loc.T("Copies a Rock Band camera: a wider view from lower and further back.") },
            { "The Band 4", Loc.T("Copies a Rock Band camera: a wide view, tilted down less than Default.") },
            { "Hero 2", Loc.T("Copies the Guitar Hero 2 camera: low behind the highway and only a little tilted, so the highway runs far into the distance. Flat highway.") },
            { "Hero 3", Loc.T("Copies the Guitar Hero 3 camera: low and close, tilted a little, flat highway.") },
            { "Hero Traveling the World", Loc.T("Copies the camera of the later Guitar Hero games: low, a little tilted, flat highway.") },
            { "Hero Live", Loc.T("Copies the Guitar Hero Live camera: a wide view, tilted down a little less than Default, flat highway.") },
            { "Clone", Loc.T("Copies the Clone Hero camera: a medium view from low behind the highway, flat highway.") },
        };

        // Built-in themes (YARG.Themes.ThemePreset.Defaults): the 3D models of the notes and frets.
        static readonly Dictionary<string, string> Themes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Rectangular", Loc.T("YARG's own look: notes are flat rectangular bars across their lanes, and the frets at the strike line are flat buttons. Works for every instrument.") },
            { "Circular (Beta)", Loc.T("Notes are round gems and the frets are round, like the older guitar games. Only five-fret guitar and five-lane keys; other instruments keep the Rectangular look. It picks the Circular colours and camera.") },
            { "YARG on Fire", Loc.T("The April Fools theme: flaming notes in bright colours. Five-fret guitar, drums and five-lane keys.") },
        };

        static readonly Dictionary<string, string> ColorProfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Default", Loc.T("YARG's colours: Green, Red, Yellow, Blue and Orange frets and notes, purple open notes, white star power notes, grey missed notes.") },
            { "Circular", Loc.T("Colours to go with the Circular theme, for five-fret guitar; the other instruments keep the default colours.") },
            { "YARG on Fire", Loc.T("Colours to go with the YARG on Fire theme: brighter frets and notes, with a purple Orange fret.") },
        };

        static readonly Dictionary<string, string> HighwayPresets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Default", Loc.T("A dark grey highway with a pattern. It turns blue (the groove) while your multiplier is at its highest, and glows orange while star power runs.") },
            { "Basic", Loc.T("A plain dark highway: no pattern, no blue groove colour and no star power colour.") },
            { "No Groove", Loc.T("Like Default, but the highway stays grey at the highest multiplier instead of turning blue.") },
        };

        public static string ForBuiltInPreset(string type, string name)
        {
            if (type == null || name == null) return null;
            string tip;
            type = TypeWord(type);
            string shown = name;
            name = name.Replace(" Engine", "").Trim();
            if (type.IndexOf("Engine", StringComparison.OrdinalIgnoreCase) >= 0 && EnginePresets.TryGetValue(name, out tip)) return tip;
            if (type.IndexOf("Engine", StringComparison.OrdinalIgnoreCase) >= 0)
                foreach (var kv in EngineKeys)
                    if (SameText(shown, Yarg(kv.Value)) && EnginePresets.TryGetValue(kv.Key, out tip)) return tip;
            if (type.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0 && RockMeterPresets.TryGetValue(name, out tip)) return tip;
            if (type.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0 && CameraPresets.TryGetValue(name, out tip)) return tip;
            if (type.IndexOf("Theme", StringComparison.OrdinalIgnoreCase) >= 0 && Themes.TryGetValue(name, out tip)) return tip;
            if (type.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0 && ColorProfiles.TryGetValue(name, out tip)) return tip;
            if (type.IndexOf("Highway", StringComparison.OrdinalIgnoreCase) >= 0 && HighwayPresets.TryGetValue(name, out tip)) return tip;
            return null;
        }

        // YARG shows the built-in engine names and the Presets tab's type names in its own language: they
        // are matched by YARG's localization keys too, not only by the English names.
        static readonly KeyValuePair<string, string>[] EngineKeys =
        {
            new KeyValuePair<string, string>("Default", "Settings.PresetSetting.EnginePreset.DefaultEngines.DefaultEngine"),
            new KeyValuePair<string, string>("Casual", "Settings.PresetSetting.EnginePreset.DefaultEngines.CasualEngine"),
            new KeyValuePair<string, string>("Precision", "Settings.PresetSetting.EnginePreset.DefaultEngines.PrecisionEngine"),
            new KeyValuePair<string, string>("Solo Taps", "Settings.PresetSetting.EnginePreset.DefaultEngines.SoloTapsEngine"),
        };

        static readonly KeyValuePair<string, string>[] TypeKeys =
        {
            new KeyValuePair<string, string>("Engine", "Settings.PresetType.EnginePresetContainer"),
            new KeyValuePair<string, string>("Rock", "Settings.PresetType.RockMeterPresetContainer"),
            new KeyValuePair<string, string>("Highway", "Settings.PresetType.HighwayPresetContainer"),
        };

        static string TypeWord(string type)
        {
            foreach (var kv in TypeKeys)
                if (SameText(type, Yarg(kv.Value))) return kv.Key;
            return type;
        }

        static string Yarg(string key)
        {
            try { return TextUtil.Clean(YARG.Localization.Localize.Key(key)); } catch { return null; }
        }

        static bool SameText(string a, string b)
        {
            return !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        static readonly Dictionary<string, string> PresetSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "PercussionHitWindow", Loc.T("Vocals: the time window for hitting a percussion note.") },
            { "ChordStaggerWindow", Loc.T("Keys: how far apart in time the keys of a chord can be pressed and still count as one chord.") },
            { "FatFingerWindow", Loc.T("Keys: how long an extra key pressed next to the right one is forgiven.") },
            { "PerfectPitchPercent", Loc.T("Vocals: how close to the note your pitch must be to count as perfect, as a share of the pitch window.") },
            { "SustainDropLeniency", Loc.T("How long you can let go of a held note before the hold counts as dropped.") },
            { "StarPowerColor", Loc.T("The colour the highway glows while star power runs, unless Star Power Highway FX Mode in Settings turns it off.") },
            { "BackgroundBaseColor1", Loc.T("The highway's main colour.") },
            { "BackgroundBaseColor2", Loc.T("The second highway colour, blended over the first towards the far end.") },
            { "BackgroundBaseColor3", Loc.T("Not used by YARG.") },
            { "BackgroundPatternColor", Loc.T("The colour of the pattern drawn on the highway.") },
            { "BackgroundGrooveBaseColor1", Loc.T("The groove is the highway's look while your multiplier is at its highest. This is its main colour.") },
            { "BackgroundGrooveBaseColor2", Loc.T("The second groove colour, blended over the first.") },
            { "BackgroundGrooveBaseColor3", Loc.T("Not used by YARG.") },
            { "BackgroundGroovePatternColor", Loc.T("The colour of the pattern while the groove shows, at the highest multiplier.") },
            { "NoteHeight", Loc.T("How tall the notes are drawn, from 0.8 to 1.2 times their normal height.") },
            { "BackgroundImage", Loc.T("An image file drawn on the highway instead of the built-in pattern.") },
            { "SideImage", Loc.T("An image file drawn along the sides of the highway; it lights up during star power.") },
            { "BaseWaviness", Loc.T("How much the highway's image ripples as it scrolls.") },
            { "SideWaviness", Loc.T("How much the side image ripples as it scrolls.") },
        };

        public static string ForPresetSetting(string unlocalizedName)
        {
            string tip;
            if (unlocalizedName == null) return null;
            if (PresetSettings.TryGetValue(unlocalizedName, out tip)) return tip;
            // Preset settings are named "<PresetType>.<Field>" (PresetSubTab.CreateField).
            int dot = unlocalizedName.LastIndexOf('.');
            string field = dot >= 0 ? unlocalizedName.Substring(dot + 1) : unlocalizedName;
            if (PresetSettings.TryGetValue(field, out tip)) return tip;
            if (!unlocalizedName.StartsWith("ColorProfile", StringComparison.OrdinalIgnoreCase) && dot >= 0) return null;
            return ForColorSetting(field);
        }

        // Color profile settings are named lane plus part ("GreenFretInner", "RedCymbalParticles").
        // One line per part, matched on the end of the name.
        static readonly KeyValuePair<string, string>[] ColorParts =
        {
            new KeyValuePair<string, string>("MetalStarPower", Loc.T("The colour of the notes' metal rims during star power.")),
            new KeyValuePair<string, string>("Metal", Loc.T("The colour of the notes' metal rims.")),
            new KeyValuePair<string, string>("ActivationNote", Loc.T("The colour of a note that turns star power on at the end of a drum fill.")),
            new KeyValuePair<string, string>("FretInner", Loc.T("The colour of the inner part of this lane's fret, the button at the strike line.")),
            new KeyValuePair<string, string>("Fret", Loc.T("The colour of this lane's fret, the button drawn at the strike line.")),
            new KeyValuePair<string, string>("NoteStarPower", Loc.T("The colour of this lane's notes while they are part of a star power phrase or star power runs.")),
            new KeyValuePair<string, string>("Starpower", Loc.T("The colour of this lane's notes while they are part of a star power phrase or star power runs.")),
            new KeyValuePair<string, string>("Particles", Loc.T("The colour of the burst drawn at the strike line when you hit a note in this lane.")),
            new KeyValuePair<string, string>("Overlay", Loc.T("The colour laid over the keys of this group on the pro keys highway.")),
            new KeyValuePair<string, string>("Miss", Loc.T("The colour a note turns when you miss it.")),
            new KeyValuePair<string, string>("Key", Loc.T("The colour of these keys on the pro keys highway.")),
            new KeyValuePair<string, string>("Note", Loc.T("The colour of this lane's notes.")),
            new KeyValuePair<string, string>("Cymbal", Loc.T("The colour of this lane's cymbal notes.")),
            new KeyValuePair<string, string>("Drum", Loc.T("The colour of this lane's pad notes.")),
        };

        static string ForColorSetting(string name)
        {
            foreach (var kv in ColorParts)
                if (name.EndsWith(kv.Key, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return null;
        }

        static readonly Dictionary<string, string> Rows = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "All Strums", Loc.T("Every note needs a strum: hammer-ons, pull-offs and taps become strummed notes.") },
            { "All HOPOs", Loc.T("Every note that can be is a hammer-on or pull-off, played without strumming.") },
            { "All Taps", Loc.T("Every note is a tap: press the fret, no strum.") },
            { "HOPOs To Taps", Loc.T("Hammer-ons and pull-offs become taps, which still work after a miss.") },
            { "Taps to HOPOs", Loc.T("Taps become hammer-ons and pull-offs, which only work right after a note you hit.") },
            { "Note Shuffle", Loc.T("The notes move to random frets or pads.") },
            { "No Kicks", Loc.T("Drums with no kick notes.") },
            { "Unpitched Only", Loc.T("Vocals score your timing only; pitch doesn't matter.") },
            { "No Dynamics", Loc.T("Drum ghost and accent notes count as normal notes.") },
            { "No Percussion", Loc.T("Vocals without the percussion notes.") },
            { "No Range Shifts", Loc.T("Keys and guitar parts stay in the same five lanes instead of moving.") },
            { "Opens To Greens", Loc.T("Open notes become green notes.") },
            { "Beginner", Loc.T("Every note can be hit with any fret or pad; just play in time.") },
            { "Expert+", Loc.T("Expert drums with double kick notes.") },
        };

        // YARG's localization keys for the rows above: the difficulty screen shows them in YARG's language.
        static readonly KeyValuePair<string, string>[] RowKeys =
        {
            new KeyValuePair<string, string>("All Strums", "Enum.Modifier.AllStrums"),
            new KeyValuePair<string, string>("All HOPOs", "Enum.Modifier.AllHopos"),
            new KeyValuePair<string, string>("All Taps", "Enum.Modifier.AllTaps"),
            new KeyValuePair<string, string>("HOPOs To Taps", "Enum.Modifier.HoposToTaps"),
            new KeyValuePair<string, string>("Taps to HOPOs", "Enum.Modifier.TapsToHopos"),
            new KeyValuePair<string, string>("Note Shuffle", "Enum.Modifier.NoteShuffle"),
            new KeyValuePair<string, string>("No Kicks", "Enum.Modifier.NoKicks"),
            new KeyValuePair<string, string>("Unpitched Only", "Enum.Modifier.UnpitchedOnly"),
            new KeyValuePair<string, string>("No Dynamics", "Enum.Modifier.NoDynamics"),
            new KeyValuePair<string, string>("No Percussion", "Enum.Modifier.NoVocalPercussion"),
            new KeyValuePair<string, string>("No Range Shifts", "Enum.Modifier.RangeCompress"),
            new KeyValuePair<string, string>("Opens To Greens", "Enum.Modifier.OpensToGreens"),
            new KeyValuePair<string, string>("Beginner", "Enum.Difficulty.Beginner"),
            new KeyValuePair<string, string>("Expert+", "Enum.Difficulty.ExpertPlus"),
        };

        static bool RowStarts(string spoken, string name)
        {
            return !string.IsNullOrEmpty(name) && spoken.StartsWith(name, StringComparison.OrdinalIgnoreCase)
                && (spoken.Length == name.Length || spoken[name.Length] == ',' || spoken[name.Length] == '.');
        }

        public static string ForDifficultyRow(string spoken)
        {
            if (string.IsNullOrEmpty(spoken)) return null;
            foreach (var kv in Rows)
                if (RowStarts(spoken, kv.Key))
                    return kv.Value;
            string tip;
            foreach (var kv in RowKeys)
                if (RowStarts(spoken, Yarg(kv.Value)) && Rows.TryGetValue(kv.Key, out tip)) return tip;
            return null;
        }

        static GameObject _root;
        static TMP_Text _title, _body;
        static string _shown;
        static string _pendingTitle;
        static Func<bool> _stillHere;
        static bool _keyboard;

        public static void Show(string text, string title = null, Func<bool> stillHere = null, bool keyboard = false, bool intoSettings = true)
        {
            _shown = string.IsNullOrEmpty(text) ? null : text;
            _stillHere = stillHere;
            _keyboard = keyboard;
            _pendingTitle = title;
            if (_shown != null && intoSettings && IntoSettingsPanel(title, _shown)) _shown = null;
            Refresh();
        }

        static bool Visible => _keyboard || !VirtualKeyboard.IsOpen;

        public static void OnGUI()
        {
            if (_shown != null && _stillHere != null)
            {
                bool here = false;
                try { here = _stillHere(); } catch { }
                if (!here) { _shown = null; _stillHere = null; _keyboard = false; Refresh(); return; }
            }
            if (_root != null && _shown != null && _root.activeSelf != Visible) _root.SetActive(Visible);
        }

        static void Refresh()
        {
            try
            {
                if (_shown == null) { if (_root != null) _root.SetActive(false); return; }
                if (_root == null) Build();
                if (_root == null) return;
                _title.text = _pendingTitle ?? "";
                _title.gameObject.SetActive(!string.IsNullOrEmpty(_pendingTitle));
                _body.text = _shown;
                _root.SetActive(Visible);
            }
            catch (Exception ex) { ModLog.Warn("Tooltips.Refresh", ex); }
        }

        static bool IntoSettingsPanel(string title, string text)
        {
            try
            {
                var sm = YARG.Menu.Settings.SettingsMenu.Instance;
                if (sm == null || !sm.isActiveAndEnabled) return false;
                var d = PanelText(sm, "_settingDescription");
                if (d == null) return false;
                var n = PanelText(sm, "_settingName");
                if (n != null && !string.IsNullOrEmpty(title)) n.text = title;
                d.text = text;
                return true;
            }
            catch { return false; }
        }

        static TMP_Text PanelText(YARG.Menu.Settings.SettingsMenu sm, string field)
        {
            return typeof(YARG.Menu.Settings.SettingsMenu).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(sm) as TMP_Text;
        }

        static TMP_FontAsset Font(string name)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if (f.name == name) return f;
            return null;
        }

        static void Build()
        {
            _root = new GameObject("YargAccessTooltip");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            GameObject header = null;
            try
            {
                var n = PanelText(YARG.Menu.Settings.SettingsMenu.Instance, "_settingName");
                if (n != null) header = UnityEngine.Object.Instantiate(n.transform.parent.gameObject, _root.transform, false);
            }
            catch (Exception ex) { ModLog.Warn("Tooltips: YARG's panel not found", ex); }
            if (header != null)
            {
                header.SetActive(true);
                foreach (var t in header.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.gameObject.name == "Setting Name") _title = t;
                    else if (t.gameObject.name == "Setting Description") _body = t;
                }
            }
            if (_title == null || _body == null)
            {
                if (header != null) UnityEngine.Object.Destroy(header);
                header = new GameObject("Header", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                header.transform.SetParent(_root.transform, false);
                header.GetComponent<Image>().color = new Color(0.012f, 0.078f, 0.129f, 1f);
                var vg = header.GetComponent<VerticalLayoutGroup>();
                vg.padding = new RectOffset(25, 25, 15, 15);
                vg.childControlWidth = vg.childControlHeight = true;
                vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
                _title = Text(header.transform, "Setting Name", Font("Barlow-SemiBold"), 30f, Color.white, FontStyles.UpperCase);
                _body = Text(header.transform, "Setting Description", Font("Barlow"), 20.7f, new Color(0.388f, 0.471f, 0.529f, 1f), FontStyles.Normal);
            }
            header.name = "YargAccessTooltipPanel";
            var rt = (RectTransform) header.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(24f, 90f);
            rt.sizeDelta = new Vector2(860f, 125f);
            var le = header.GetComponent<LayoutElement>() ?? header.AddComponent<LayoutElement>();
            le.minHeight = 125f;
            var fit = header.GetComponent<ContentSizeFitter>() ?? header.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (var g in header.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            _root.SetActive(false);
        }

        static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.enableWordWrapping = true;
            return t;
        }
    }
}
