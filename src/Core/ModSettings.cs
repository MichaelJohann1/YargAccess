using System;
using System.Collections.Generic;
using MelonLoader;
using YARG.Settings;
using YARG.Settings.Metadata;
using YARG.Settings.Types;

namespace YargAccess
{
    public static class ModSettings
    {
        public const string TabName = "YargAccess";
        public const string GlossaryTabName = "YargAccessGlossary";
        const string Prefix = "YargAccess";

        class Option
        {
            public string Name;
            public string Description;
            public ISettingType Setting;
        }

        static readonly Dictionary<string, Option> _options = new Dictionary<string, Option>();
        static readonly List<string> _order = new List<string>();
        public class Page
        {
            public string Row, TabName, Title;
            public readonly List<string> Keys = new List<string>();
            public MetadataTab Tab;
        }
        static readonly List<Page> _pages = new List<Page>();
        static Page _glossaryPage;
        static readonly Dictionary<string, NamedDropdown> _glossary = new Dictionary<string, NamedDropdown>();
        static MelonPreferences_Category _category;
        static bool _tabInstalled;

        public static ToggleSetting ReadDescriptions { get; private set; }
        public static ToggleSetting AnnounceMusic { get; private set; }
        public static ToggleSetting AnnounceSections { get; private set; }
        public static ToggleSetting BumpersSwitchTabs { get; private set; }
        public static ToggleSetting ReadPosition { get; private set; }
        public static ToggleSetting ReadRoles { get; private set; }
        public static NamedDropdown InSongSpeech { get; private set; }
        public static ToggleSetting NoteSounds { get; private set; }
        public static NamedDropdown SoundPreview { get; private set; }
        public static NamedDropdown PlayStyle { get; private set; }
        public static NamedDropdown CallLengthSetting { get; private set; }
        public static ToggleSetting DuckMusic { get; private set; }
        public static SliderSetting DuckAmount { get; private set; }
        public static NamedDropdown LookAhead { get; private set; }
        public static NamedDropdown DrumLookAhead { get; private set; }

        public static bool ListenAndRepeat => PlayStyle == null || PlayStyle.Value != "hear";
        public static int CallLength
        {
            get
            {
                int n;
                return CallLengthSetting != null && int.TryParse(CallLengthSetting.Value, out n) && n > 0 ? n : 2;
            }
        }
        public static ToggleSetting CueRepeats { get; private set; }
        public static ToggleSetting BeatTicks { get; private set; }
        public static ToggleSetting PitchGuide { get; private set; }
        public static ToggleSetting PitchFeedback { get; private set; }

        public static SliderSetting CueVolume { get; private set; }

        public static double LookAheadSeconds => Seconds(LookAhead, 0.3);
        public static double DrumLookAheadSeconds => Seconds(DrumLookAhead, 0.2);

        static double Seconds(NamedDropdown d, double fallback)
        {
            int ms;
            return d != null && int.TryParse(d.Value, out ms) && ms > 0 ? ms / 1000.0 : fallback;
        }

        public static Page GlossaryPage => _glossaryPage;
        public static MelonPreferences_Entry<bool> TutorialsOffered { get; private set; }
        public static MelonPreferences_Entry<string> ListenRepeatBest { get; private set; }

        public static Page PageForRow(string row) => row == null ? null : _pages.Find(p => p.Row == row);
        public static int RowIndex(string row) => _order.IndexOf(row);
        public static bool IsGlossaryDropdown(string name) => name != null && _glossary.ContainsKey(name);

        public class NamedDropdown : DropdownSetting<string>
        {
            readonly Dictionary<string, string> _names = new Dictionary<string, string>();
            readonly Dictionary<string, Func<string>> _live = new Dictionary<string, Func<string>>();
            public NamedDropdown(string value, Action<string> onChange) : base(value, onChange, localizable: false) { }
            public void Add(string id, string name) { _names[id] = name; Add(id); }
            public void Add(string id, Func<string> name) { _live[id] = name; Add(id); }
            public override string ValueToString(string value)
            {
                string n;
                Func<string> f;
                if (value != null && _live.TryGetValue(value, out f)) return f();
                return value != null && _names.TryGetValue(value, out n) ? n : value;
            }
        }

        public static void Init()
        {
            if (_category != null) return;
            _category = MelonPreferences.CreateCategory("YargAccess", "YargAccess");

            TutorialsOffered = _category.CreateEntry("TutorialsOffered", false);
            ListenRepeatBest = _category.CreateEntry("ListenRepeatBest", "");
            ReadDescriptions = new ToggleSetting(true, v => { });
            ReadPosition = Toggle("ReadPosition", true);
            ReadRoles = Toggle("ReadRoles", true);
            AnnounceMusic = Toggle("AnnounceMusic", true);
            AnnounceSections = Toggle("AnnounceSections", true);
            BumpersSwitchTabs = Toggle("BumpersSwitchTabs", true);

            var notes = _category.CreateEntry("NoteSounds", true, "Note Sounds",
                "Play the highway as sound during a song.");
            notes.Value = true;
            NoteSounds = new ToggleSetting(true, v => { notes.Value = v; Save(); });

            var playStyle = _category.CreateEntry("PlayMode", "repeat");
            if (playStyle.Value != "repeat" && playStyle.Value != "hear") playStyle.Value = "repeat";
            PlayStyle = new NamedDropdown(playStyle.Value, v => { playStyle.Value = v; Save(); });
            PlayStyle.Add("repeat", Loc.T("Listen and Repeat"));
            PlayStyle.Add("hear", Loc.T("Play What You Hear"));
            Register("PlayStyle", Loc.T("Play Style"),
                Loc.T("Listen and Repeat: a few bars play, then you play them back in the same rhythm. The notes in the bars where a pattern plays are taken out, so you play about half of a chart: "
                + "your score is lower than a full play and can't be compared with one, its best scores are kept apart, and there are no full combos. "
                + "Play What You Hear: every note of the chart plays its sound a Look-ahead before you hit it; harder, the song as charted, and full combos count. Starts with the next song."), PlayStyle);

            var callLength = _category.CreateEntry("CallLength", "2");
            // The value is 1, 2 or 4 bars. A value saved when the length counted notes is reset once to
            // the default, 2 bars.
            var inBars = _category.CreateEntry("CallLengthBars", false);
            if (!inBars.Value) { callLength.Value = "2"; inBars.Value = true; }
            if (callLength.Value != "1" && callLength.Value != "2" && callLength.Value != "4") callLength.Value = "2";
            CallLengthSetting = new NamedDropdown(callLength.Value, v => { callLength.Value = v; Save(); });
            CallLengthSetting.Add("1", Loc.T("1 bar"));
            CallLengthSetting.Add("2", Loc.T("2 bars, recommended"));
            CallLengthSetting.Add("4", Loc.T("4 bars"));
            Register("CallLength", Loc.T("Call Length"),
                Loc.T("Listen and Repeat: how many bars each pattern lasts. A pattern starts on a bar line, and your turn is the same number of bars right after it."), CallLengthSetting);

            var duck = _category.CreateEntry("DuckMusic", false);
            DuckMusic = new ToggleSetting(duck.Value, v => { duck.Value = v; Save(); });
            Register("DuckMusic", Loc.T("Duck Music During Listen"),
                Loc.T("Listen and Repeat: the song plays quieter while a pattern plays, so its notes are easier to hear, and comes back up for your turn."), DuckMusic);

            var duckAmount = _category.CreateEntry("DuckAmount", 50f);
            DuckAmount = new SliderSetting(Clamp100(duckAmount.Value), 0f, 100f, v => { duckAmount.Value = v; Save(); });
            Register("DuckAmount", Loc.T("Ducking Amount"),
                Loc.T("How much quieter the song gets while a pattern plays, when Duck Music During Listen is on. 100% silences it."), DuckAmount);

            var ahead = _category.CreateEntry("LookAheadMs", "300");
            string[] times = { "150", "200", "250", "300", "350", "400", "500" };
            if (Array.IndexOf(times, ahead.Value) < 0) ahead.Value = "300";
            LookAhead = new NamedDropdown(ahead.Value, v => { ahead.Value = v; Save(); });
            foreach (var t in times) LookAhead.Add(t, t == "300" ? Loc.F("{0} milliseconds, recommended", t) : Loc.F("{0} milliseconds", t));
            Register("LookAhead", Loc.T("Look-ahead"),
                Loc.T("How long before you must hit a note its sound plays, for guitar, bass and keys. Play what you hear as soon as you hear it. Raise it if you hit late, lower it if you hit early. 300 is recommended: time to hear the note, reach the fret and strum."), LookAhead);

            var drumAhead = _category.CreateEntry("DrumLookAheadMs", "200");
            if (Array.IndexOf(times, drumAhead.Value) < 0) drumAhead.Value = "200";
            DrumLookAhead = new NamedDropdown(drumAhead.Value, v => { drumAhead.Value = v; Save(); });
            foreach (var t in times) DrumLookAhead.Add(t, t == "200" ? Loc.F("{0} milliseconds, recommended", t) : Loc.F("{0} milliseconds", t));
            Register("DrumLookAhead", Loc.T("Drums Look-ahead"),
                Loc.T("How long before you must hit a drum note its sound plays. Hit what you hear as soon as you hear it. 200 is recommended: drums have no fret to reach, so the sound can come closer to the hit than on guitar."), DrumLookAhead);

            var guide = _category.CreateEntry("PitchGuide", true);
            PitchGuide = new ToggleSetting(guide.Value, v => { guide.Value = v; Save(); });
            Register("PitchGuide", Loc.T("Pitch Guide"),
                Loc.T("Vocals: a soft tone just before each new note, at the pitch to sing. Any octave scores, so sing it wherever it suits your voice. Spoken words, which score any pitch, get two soft breaths instead."), PitchGuide);

            var feedback = _category.CreateEntry("PitchFeedback", true);
            PitchFeedback = new ToggleSetting(feedback.Value, v => { feedback.Value = v; Save(); });
            Register("PitchFeedback", Loc.T("Pitch Feedback"),
                Loc.T("Vocals: while you sing a note too far off to score, a rising sweep means sing higher and a falling sweep means sing lower. Silence means you are on pitch. Needs a microphone on your profile."), PitchFeedback);

            var beats = _category.CreateEntry("BeatTicks", true);
            BeatTicks = new ToggleSetting(beats.Value, v => { beats.Value = v; Save(); });
            Register("BeatTicks", Loc.T("Beat Ticks"),
                Loc.T("A soft tick on every beat of the song, stronger on the first beat of each bar, like the beat lines on the highway. Play in time with them."), BeatTicks);

            var repeats = _category.CreateEntry("CueRepeats", false);
            CueRepeats = new ToggleSetting(repeats.Value, v => { repeats.Value = v; Save(); });
            Register("CueRepeats", Loc.T("Cue Repeated Notes"),
                Loc.T("On: every note plays its sound ahead. Off: a run of the same fret or chord plays only the first one and holds its tone through the run, so busy passages stay readable: keep the fret down and strum on the hit bar sounds. Drums always play every note."), CueRepeats);

            // Note sounds always match the song's key; A minor until a song sets one.
            Audio.CueBank.SetKey(9, true);

            _glossaryPage = new Page { TabName = GlossaryTabName, Title = Loc.T("Sound Glossary") };
            _pages.Add(_glossaryPage);

            SoundPreview = Glossary("FiveFret", Loc.T("Five-Fret"), Loc.T("Guitar, co-op guitar, rhythm, bass and keys."),
                Items(Audio.CueBank.All.FindAll(c => !c.Yarg.HasValue && c.Group == "fivefret")));
            var drums = Items(Audio.CueBank.All.FindAll(c => c.Group == "drums"));
            drums.Add(new KeyValuePair<string, string>("beat", Loc.T("Beat: a beat of the song, like a beat line crossing the highway")));
            drums.Add(new KeyValuePair<string, string>("measure", Loc.T("Bar: the first beat of a bar")));
            drums.Add(new KeyValuePair<string, string>("spstart", Loc.T("Star power phrase start: the first note of the star power phrase that makes star power ready. Hit every note of it, then the next fill turns star power on")));
            drums.Add(new KeyValuePair<string, string>("spnote", Loc.T("Star power note: a note in a star power phrase")));
            drums.Add(new KeyValuePair<string, string>("spready", Loc.T("Star power ready: a bright sparkle over a shimmer: star power is ready and stays ready until you use it. Hit the last note of the next fill to turn it on")));
            drums.Add(new KeyValuePair<string, string>("hit", Loc.T("Hit: you hit a note; it plays in that pad's place, an octave above the pad's note")));
            drums.Add(new KeyValuePair<string, string>("miss", Loc.T("Miss: you missed a note")));
            drums.Add(new KeyValuePair<string, string>("overstrum", Loc.T("Overhit: you hit a pad with no note to hit. The pad you hit plays quietly after it")));
            AddListenRepeat(drums, Loc.T("Listen: Listen and Repeat, a pattern of pads starts as this ends. Listen, don't play"));
            Glossary("Drums", Loc.T("Drums"), Loc.T("The kick and the pads, for four-lane, pro and five-lane drums. Each sound says which modes use it."), drums);
            Glossary("ProDrums", Loc.T("Pro Drums"), Loc.T("The cymbals. Pro drums has the same four lanes as plain four-lane drums, but yellow, blue and green can each be a tom or a cymbal; plain four-lane has no cymbals. Five-lane has two cymbal lanes, yellow and orange."),
                Items(Audio.CueBank.All.FindAll(c => c.Group == "prodrums")));
            var vocals = Items(Audio.CueBank.All.FindAll(c => c.Group == "vocals"));
            vocals.Add(new KeyValuePair<string, string>("hit", Loc.T("Hit: at the end of a phrase, you sang enough of it on pitch for it to count, the phrase YARG rates Awesome (lower ratings are silent); also a percussion note you hit")));
            vocals.Add(new KeyValuePair<string, string>("miss", Loc.T("Miss: a percussion note you missed")));
            vocals.Add(new KeyValuePair<string, string>("spready", Loc.T("Star power ready: a bright sparkle over a shimmer: star power is ready and stays ready until you use it. Press your star power button to turn it on")));
            AddListenRepeat(vocals, Loc.T("Listen: Listen and Repeat, a pattern of percussion notes starts as this ends. Listen, don't play"));
            Glossary("Vocals", Loc.T("Vocals"), Loc.T("Vocals and harmonies."), vocals);
            var keys = Items(Audio.CueBank.All.FindAll(c => c.Group == "prokeys"));
            keys.Add(new KeyValuePair<string, string>("sweep", Loc.T("Hit bar: runs from a note's sound to the moment it reaches the hit bar: press the key as this ends")));
            keys.Add(new KeyValuePair<string, string>("click", Loc.T("Hit bar, fast passages: marks each moment a note reaches the hit bar")));
            keys.Add(new KeyValuePair<string, string>("release", Loc.T("Let go: the hold is complete, let go of the key now or any time after")));
            keys.Add(new KeyValuePair<string, string>("hit", Loc.T("Hit: you hit a key; it plays in that key's place, an octave above it")));
            keys.Add(new KeyValuePair<string, string>("miss", Loc.T("Miss: you missed a note")));
            keys.Add(new KeyValuePair<string, string>("overstrum", Loc.T("Overhit: you pressed a key with no note for it, which breaks your combo. The key you pressed plays quietly after it")));
            keys.Add(new KeyValuePair<string, string>("spready", Loc.T("Star power ready: a bright sparkle over a shimmer: star power is ready and stays ready until you use it. Press your star power button to turn it on")));
            keys.Add(new KeyValuePair<string, string>("dropped", Loc.T("Hold dropped: you let go of a held key too early")));
            AddListenRepeat(keys, Loc.T("Listen: Listen and Repeat, a pattern of keys starts as this ends. Listen, don't play"));
            Glossary("ProKeys", Loc.T("Pro Keys"), Loc.T("Pro keys: 25 keys, low C to high C."), keys);
            Glossary("Yarg", Loc.T("YARG Sounds"), Loc.T("Star power and miss sounds YARG plays itself, at its sound effect volume."),
                Items(Audio.CueBank.All.FindAll(c => c.Yarg.HasValue)));

            var inSong = _category.CreateEntry("InSongSpeech", "important");
            if (inSong.Value != "all" && inSong.Value != "important" && inSong.Value != "off") inSong.Value = "important";
            InSongSpeech = new NamedDropdown(inSong.Value, v => { inSong.Value = v; Save(); });
            InSongSpeech.Add("all", Loc.T("All"));
            InSongSpeech.Add("important", Loc.T("Important only"));
            InSongSpeech.Add("off", Loc.T("Off"));
            Register("InSongSpeech", Loc.T("Speech During Songs"),
                Loc.T("YARG's pop-ups during a song. All: note streaks, Hot Start and the rest. Important only: Star Power Ready, Full Combo and New High Score, and when star power turns on and runs out; vocals still reads them all, since the phrase ratings are how a singer hears how a phrase went. Off: nothing."), InSongSpeech);

            Register("ReadPosition", Loc.T("Read Position In Lists"), Loc.T("Say where you are in a menu or list, such as 3 of 10."), ReadPosition);
            Register("ReadRoles", Loc.T("Read Control Types"), Loc.T("Say what kind of control each item is, such as button, slider, dropdown, checkbox or heading. Values such as checked or 75% are always read."), ReadRoles);
            Register("AnnounceMusic", Loc.T("Announce Menu Music"), Loc.T("Say the name of each song the menu music player starts."), AnnounceMusic);

            var all = _category.CreateEntry("CueVolume", 100f);
            CueVolume = new SliderSetting(Clamp100(all.Value), 0f, 100f, v =>
            {
                all.Value = v;
                Audio.CueBank.MasterVolume = v / 100f;
                Save();
                PlayPreview(SoundPreview.Value);
            });
            Audio.CueBank.MasterVolume = CueVolume.Value / 100f;
            Register("CueVolume", Loc.T("Accessibility Cues Master Volume"), Loc.T("Adjusts the volume of all accessibility cues together."), CueVolume);

            var fiveFret = AddPage("FiveFretVolumes", Prefix + "PageFiveFretVolumes", Loc.T("Five-Fret Volumes"), Loc.T("The volume of each guitar, bass, keys and pro keys sound."));
            var drumPage = AddPage("DrumVolumes", Prefix + "PageDrumVolumes", Loc.T("Drum Volumes"), Loc.T("The volume of each drum and cymbal sound."));
            var vocalPage = AddPage("VocalVolumes", Prefix + "PageVocalVolumes", Loc.T("Vocal Volumes"), Loc.T("The volume of the pitch guide, sing higher, sing lower, percussion and spoken words sounds."));
            var shared = AddPage("SharedVolumes", Prefix + "PageSharedVolumes", Loc.T("Shared Volumes"), Loc.T("The volume of the sounds every mode uses: hit bar, beats, star power, hit, miss, overstrum, and Listen and Repeat's listen and your turn."));
            foreach (var c in Audio.CueBank.All)
            {
                if (c.Yarg.HasValue) continue;
                var cue = c;
                var pref = _category.CreateEntry("Volume_" + cue.Id, 100f);
                var slider = new SliderSetting(Clamp100(pref.Value), 0f, 100f, v =>
                {
                    pref.Value = v;
                    cue.Volume = v / 100f;
                    Save();
                    PlayPreview(cue.Id);
                });
                cue.Volume = slider.Value / 100f;
                var page = cue.Group == "vocals" ? vocalPage : cue.Group == "prokeys" ? fiveFret : cue.Group != "fivefret" ? drumPage : (SharedCues.Contains(cue.Id) ? shared : fiveFret);
                RegisterIn(page, "Volume_" + cue.Id, Loc.F("{0} Volume", cue.Name), "", slider);
            }
        }

        static readonly HashSet<string> SharedCues = new HashSet<string> { "sweep", "click", "beat", "measure", "spstart", "spready", "spnote", "splevel", "splost", "hit", "miss", "overstrum", "listen", "yourturn" };

        // The Listen and Repeat start and stop sounds, in every mode's glossary.
        static void AddListenRepeat(List<KeyValuePair<string, string>> items, string listenLine)
        {
            items.Add(new KeyValuePair<string, string>("listen", listenLine));
            items.Add(new KeyValuePair<string, string>("yourturn", Loc.T("Your turn: Listen and Repeat, your turn starts as this ends. Play the pattern back in the same rhythm")));
        }

        static Page AddPage(string id, string tabName, string title, string description)
        {
            var open = new NamedDropdown("open", v => { });
            open.Add("open", () => Loc.F("{0} to open", Keys.Ok));
            Register(id, title, description, open);
            var page = new Page { Row = Prefix + id, TabName = tabName, Title = title };
            _pages.Add(page);
            return page;
        }

        static void RegisterIn(Page page, string id, string name, string description, ISettingType setting)
        {
            var key = Prefix + id;
            _options[key] = new Option { Name = name, Description = description, Setting = setting };
            page.Keys.Add(key);
        }

        static List<KeyValuePair<string, string>> Items(List<Audio.CueBank.Cue> cues)
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (var c in cues) list.Add(new KeyValuePair<string, string>(c.Id, Loc.F("{0}: {1}", c.Name, c.Meaning)));
            return list;
        }

        static NamedDropdown Glossary(string id, string name, string description, List<KeyValuePair<string, string>> items)
        {
            NamedDropdown d;
            if (items == null || items.Count == 0)
            {
                d = new NamedDropdown("none", v => { });
                d.Add("none", Loc.F("No sounds yet for {0}", name.ToLowerInvariant()));
            }
            else
            {
                d = new NamedDropdown(items[0].Key, PlayPreview);
                foreach (var it in items) d.Add(it.Key, it.Value);
            }
            var key = Prefix + "Glossary" + id;
            _options[key] = new Option { Name = name, Description = description, Setting = d };
            _glossaryPage.Keys.Add(key);
            _glossary[key] = d;
            return d;
        }

        public static void PlayGlossary(string name)
        {
            NamedDropdown d;
            if (name != null && _glossary.TryGetValue(name, out d) && d.Value != "none") PlayPreview(d.Value);
        }

        static float Clamp100(float v) { return v < 0f ? 0f : (v > 100f ? 100f : v); }

        static ToggleSetting Toggle(string id, bool defaultValue)
        {
            var pref = _category.CreateEntry(id, defaultValue);
            return new ToggleSetting(pref.Value, v =>
            {
                pref.Value = v;
                Save();
            });
        }

        internal static void Save()
        {
            try { _category.SaveToFile(false); }
            catch (Exception ex) { ModLog.Warn("ModSettings save", ex); }
        }

        static int _previewChannel;
        public static void PlayPreview() { PlayPreview(SoundPreview.Value); }
        static void PlayPreview(string id)
        {
            Audio.CuePlayer.Stop(_previewChannel);
            _previewChannel = Audio.CueBank.Play(id);
        }

        static void Register(string id, string name, string description, ISettingType setting)
        {
            var key = Prefix + id;
            _options[key] = new Option { Name = name, Description = description, Setting = setting };
            _order.Add(key);
        }

        public static void InstallTab()
        {
            if (_tabInstalled) return;
            Init();
            try
            {
                var tab = new MetadataTab(TabName, "Sound");
                foreach (var key in _order) tab.Add(new FieldMetadata(key));

                var shown = SettingsManager.DisplayedSettingsTabs;
                int at = shown.Count > 0 && shown[shown.Count - 1] is AllSettingsTab ? shown.Count - 1 : shown.Count;
                shown.Insert(at, tab);
                SettingsManager.AllSettingsTabs.Add(tab);

                foreach (var page in _pages)
                {
                    page.Tab = new MetadataTab(page.TabName, "Sound");
                    foreach (var key in page.Keys) page.Tab.Add(new FieldMetadata(key));
                    SettingsManager.AllSettingsTabs.Add(page.Tab);
                }
                _tabInstalled = true;
            }
            catch (Exception ex) { ModLog.Warn("ModSettings.InstallTab", ex); }
        }

        public static bool TryGetSetting(string name, out ISettingType setting)
        {
            setting = null;
            if (name == null || !name.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            Option o;
            if (!_options.TryGetValue(name, out o)) return false;
            setting = o.Setting;
            return true;
        }

        public const string TextKey = Prefix + ".Text.";

        public static bool TryLocalize(string key, out string value)
        {
            value = null;
            if (key == null || key.IndexOf(Prefix, StringComparison.Ordinal) < 0) return false;
            // The label after the prefix is the mod's English text (Tutorials.Text, EntryActions.T): translated here.
            if (key.StartsWith(TextKey, StringComparison.Ordinal)) { value = Loc.T(key.Substring(TextKey.Length)); return true; }
            if (key == "Settings.Tab." + TabName) { value = "YargAccess"; return true; }
            foreach (var page in _pages)
                if (key == "Settings.Tab." + page.TabName) { value = page.Title; return true; }
            const string head = "Settings.Setting.";
            if (!key.StartsWith(head, StringComparison.Ordinal)) return false;
            var rest = key.Substring(head.Length);
            int dot = rest.LastIndexOf('.');
            if (dot < 0) return false;
            Option o;
            if (!_options.TryGetValue(rest.Substring(0, dot), out o)) return false;
            var part = rest.Substring(dot + 1);
            if (part == "Name") value = o.Name;
            else if (part == "Description") value = o.Description;
            return value != null;
        }
    }
}
