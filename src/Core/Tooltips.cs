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
            { "Instrument Select", "The kind of instrument this profile plays, such as five-fret guitar, four-lane, pro or five-lane drums, pro keys or vocals. It decides which parts you can pick and which buttons you bind." },
            { "Note Speed", "How fast notes scroll down the highway. It only changes what is drawn; the mod's sounds play at the same times." },
            { "Highway Length", "How far ahead the highway shows notes. It only changes what is drawn." },
            { "Lefty Flip", "Mirrors the lanes left to right for left-handed players. The mod's note sounds are mirrored with them." },
            { "Engine Preset", "The rules for hitting notes: the size of the hit window and how forgiving strums and frets are." },
            { "Theme Select", "How the notes and frets look. Visual only." },
            { "Color Profile Select", "The colours of the notes, frets and effects. Visual only." },
            { "Camera Preset", "The camera angle and curve of the highway. Visual only." },
            { "Highway Preset", "The highway's colours and background. Visual only." },
            { "Rock Meter Preset", "How quickly you fail: how much misses and overhits cost and how fast hits win it back." },
            { "Input Calibration", "This profile's own timing correction in milliseconds, added to the game-wide calibration. Raise it if your hits count as late, lower it if they count as early." },
            { "Range Disable", "Shows markers where a part moves to a different set of five lanes, mostly on keys. Visual only." },
        };

        static readonly KeyValuePair<string, string>[] ProfileByLabel =
        {
            new KeyValuePair<string, string>("Open", "Whether the highway shows a lane for open notes: never, always, or only when the chart has open notes. Visual only."),
            new KeyValuePair<string, string>("Cymbal", "Draws cymbal notes as cymbals instead of flat notes. Visual only."),
            new KeyValuePair<string, string>("Activation", "On drums, which hit at the end of a fill turns star power on. Rightmost Note: the rightmost pad of the fill's last note. All Notes: any pad of that note."),
            new KeyValuePair<string, string>("Hand Position", "Shows hand position markers on the highway. Visual only."),
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
            { "Default", "A fixed hit window, 70 milliseconds either side of each note." },
            { "Casual", "More forgiving than Default: strums and frets may be a little further apart, hammer-ons can be held before they arrive, the wrong fret doesn't break your streak, and rolls are easier." },
            { "Precision", "Stricter than Default: the hit window shrinks from 60 to as little as 20 milliseconds either side as notes get faster, early strums are forgiven less, and vocals need truer pitch." },
            { "Solo Taps", "Default, plus during solos the solo frets play notes without strumming." },
        };

        static readonly Dictionary<string, string> RockMeterPresets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Normal", "The standard rock meter." },
            { "Easy", "Misses and overhits cost less and hits win back more than Normal." },
            { "Casual", "The most forgiving: misses cost half as much and hits win back twice as much as Normal." },
            { "Hard", "Misses cost one and a half times as much and hits win back half as much as Normal,, and you start with the meter half full." },
            { "Unfair", "Misses cost three times as much and hits win back a quarter as much as Normal, starting half full." },
        };

        // Built-in camera presets (YARG.Core CameraPreset.Defaults). The names copy other games' camera
        // angles: The Band is Rock Band, Hero is Guitar Hero, Clone is Clone Hero.
        static readonly Dictionary<string, string> CameraPresets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Default", "YARG's own view: the camera fairly high behind the highway, tilted down, and the far end of the highway curves gently away." },
            { "Circular", "Made for the Circular theme: a slightly wider view from a little lower and further back, with a flat highway." },
            { "High FOV", "Like Default with a wider view, so more of the highway fits on the screen." },
            { "The Band 1", "Copies a Rock Band camera: a narrower view, a little lower and tilted down a bit more than Default, with the curved highway." },
            { "The Band 2", "Copies a Rock Band camera: the narrowest view, high and close to the highway, so the notes look bigger." },
            { "The Band 3", "Copies a Rock Band camera: a wider view from lower and further back." },
            { "The Band 4", "Copies a Rock Band camera: a wide view, tilted down less than Default." },
            { "Hero 2", "Copies the Guitar Hero 2 camera: low behind the highway and only a little tilted, so the highway runs far into the distance. Flat highway." },
            { "Hero 3", "Copies the Guitar Hero 3 camera: low and close, tilted a little, flat highway." },
            { "Hero Traveling the World", "Copies the camera of the later Guitar Hero games: low, a little tilted, flat highway." },
            { "Hero Live", "Copies the Guitar Hero Live camera: a wide view, tilted down a little less than Default, flat highway." },
            { "Clone", "Copies the Clone Hero camera: a medium view from low behind the highway, flat highway." },
        };

        // Built-in themes (YARG.Themes.ThemePreset.Defaults): the 3D models of the notes and frets.
        static readonly Dictionary<string, string> Themes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Rectangular", "YARG's own look: notes are flat rectangular bars across their lanes, and the frets at the strike line are flat buttons. Works for every instrument." },
            { "Circular (Beta)", "Notes are round gems and the frets are round, like the older guitar games. Only five-fret guitar and five-lane keys; other instruments keep the Rectangular look. It picks the Circular colours and camera." },
            { "YARG on Fire", "The April Fools theme: flaming notes in bright colours. Five-fret guitar, drums and five-lane keys." },
        };

        static readonly Dictionary<string, string> ColorProfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Default", "YARG's colours: Green, Red, Yellow, Blue and Orange frets and notes, purple open notes, white star power notes, grey missed notes." },
            { "Circular", "Colours to go with the Circular theme, for five-fret guitar; the other instruments keep the default colours." },
            { "YARG on Fire", "Colours to go with the YARG on Fire theme: brighter frets and notes, with a purple Orange fret." },
        };

        static readonly Dictionary<string, string> HighwayPresets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Default", "A dark grey highway with a pattern. It turns blue (the groove) while your multiplier is at its highest, and glows orange while star power runs." },
            { "Basic", "A plain dark highway: no pattern, no blue groove colour and no star power colour." },
            { "No Groove", "Like Default, but the highway stays grey at the highest multiplier instead of turning blue." },
        };

        public static string ForBuiltInPreset(string type, string name)
        {
            if (type == null || name == null) return null;
            string tip;
            name = name.Replace(" Engine", "").Trim();
            if (type.IndexOf("Engine", StringComparison.OrdinalIgnoreCase) >= 0 && EnginePresets.TryGetValue(name, out tip)) return tip;
            if (type.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0 && RockMeterPresets.TryGetValue(name, out tip)) return tip;
            if (type.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0 && CameraPresets.TryGetValue(name, out tip)) return tip;
            if (type.IndexOf("Theme", StringComparison.OrdinalIgnoreCase) >= 0 && Themes.TryGetValue(name, out tip)) return tip;
            if (type.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0 && ColorProfiles.TryGetValue(name, out tip)) return tip;
            if (type.IndexOf("Highway", StringComparison.OrdinalIgnoreCase) >= 0 && HighwayPresets.TryGetValue(name, out tip)) return tip;
            return null;
        }

        static readonly Dictionary<string, string> PresetSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "PercussionHitWindow", "Vocals: the time window for hitting a percussion note." },
            { "ChordStaggerWindow", "Keys: how far apart in time the keys of a chord can be pressed and still count as one chord." },
            { "FatFingerWindow", "Keys: how long an extra key pressed next to the right one is forgiven." },
            { "PerfectPitchPercent", "Vocals: how close to the note your pitch must be to count as perfect, as a share of the pitch window." },
            { "SustainDropLeniency", "How long you can let go of a held note before the hold counts as dropped." },
            { "StarPowerColor", "The colour the highway glows while star power runs, unless Star Power Highway FX Mode in Settings turns it off." },
            { "BackgroundBaseColor1", "The highway's main colour." },
            { "BackgroundBaseColor2", "The second highway colour, blended over the first towards the far end." },
            { "BackgroundBaseColor3", "Not used by YARG." },
            { "BackgroundPatternColor", "The colour of the pattern drawn on the highway." },
            { "BackgroundGrooveBaseColor1", "The groove is the highway's look while your multiplier is at its highest. This is its main colour." },
            { "BackgroundGrooveBaseColor2", "The second groove colour, blended over the first." },
            { "BackgroundGrooveBaseColor3", "Not used by YARG." },
            { "BackgroundGroovePatternColor", "The colour of the pattern while the groove shows, at the highest multiplier." },
            { "NoteHeight", "How tall the notes are drawn, from 0.8 to 1.2 times their normal height." },
            { "BackgroundImage", "An image file drawn on the highway instead of the built-in pattern." },
            { "SideImage", "An image file drawn along the sides of the highway; it lights up during star power." },
            { "BaseWaviness", "How much the highway's image ripples as it scrolls." },
            { "SideWaviness", "How much the side image ripples as it scrolls." },
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
            new KeyValuePair<string, string>("MetalStarPower", "The colour of the notes' metal rims during star power."),
            new KeyValuePair<string, string>("Metal", "The colour of the notes' metal rims."),
            new KeyValuePair<string, string>("ActivationNote", "The colour of a note that turns star power on at the end of a drum fill."),
            new KeyValuePair<string, string>("FretInner", "The colour of the inner part of this lane's fret, the button at the strike line."),
            new KeyValuePair<string, string>("Fret", "The colour of this lane's fret, the button drawn at the strike line."),
            new KeyValuePair<string, string>("NoteStarPower", "The colour of this lane's notes while they are part of a star power phrase or star power runs."),
            new KeyValuePair<string, string>("Starpower", "The colour of this lane's notes while they are part of a star power phrase or star power runs."),
            new KeyValuePair<string, string>("Particles", "The colour of the burst drawn at the strike line when you hit a note in this lane."),
            new KeyValuePair<string, string>("Overlay", "The colour laid over the keys of this group on the pro keys highway."),
            new KeyValuePair<string, string>("Miss", "The colour a note turns when you miss it."),
            new KeyValuePair<string, string>("Key", "The colour of these keys on the pro keys highway."),
            new KeyValuePair<string, string>("Note", "The colour of this lane's notes."),
            new KeyValuePair<string, string>("Cymbal", "The colour of this lane's cymbal notes."),
            new KeyValuePair<string, string>("Drum", "The colour of this lane's pad notes."),
        };

        static string ForColorSetting(string name)
        {
            foreach (var kv in ColorParts)
                if (name.EndsWith(kv.Key, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return null;
        }

        static readonly Dictionary<string, string> Rows = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "All Strums", "Every note needs a strum: hammer-ons, pull-offs and taps become strummed notes." },
            { "All HOPOs", "Every note that can be is a hammer-on or pull-off, played without strumming." },
            { "All Taps", "Every note is a tap: press the fret, no strum." },
            { "HOPOs To Taps", "Hammer-ons and pull-offs become taps, which still work after a miss." },
            { "Taps to HOPOs", "Taps become hammer-ons and pull-offs, which only work right after a note you hit." },
            { "Note Shuffle", "The notes move to random frets or pads." },
            { "No Kicks", "Drums with no kick notes." },
            { "Unpitched Only", "Vocals score your timing only; pitch doesn't matter." },
            { "No Dynamics", "Drum ghost and accent notes count as normal notes." },
            { "No Percussion", "Vocals without the percussion notes." },
            { "No Range Shifts", "Keys and guitar parts stay in the same five lanes instead of moving." },
            { "Opens To Greens", "Open notes become green notes." },
            { "Beginner", "Every note can be hit with any fret or pad; just play in time." },
            { "Expert+", "Expert drums with double kick notes." },
        };

        public static string ForDifficultyRow(string spoken)
        {
            if (string.IsNullOrEmpty(spoken)) return null;
            foreach (var kv in Rows)
                if (spoken.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase)
                    && (spoken.Length == kv.Key.Length || spoken[kv.Key.Length] == ',' || spoken[kv.Key.Length] == '.'))
                    return kv.Value;
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
