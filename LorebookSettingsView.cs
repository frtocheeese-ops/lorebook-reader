using System;
using System.Linq;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using Windows.Media.Ocr;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Nastavení modulu: keybindy, ikona reproduktoru, volba TTS enginu
    /// (offline Windows / online Edge neural), dropdowny hlasů,
    /// rychlost řeči a OCR jazyk.
    /// </summary>
    public class LorebookSettingsView : View {

        private const string AutoVoiceItem  = "(auto — match OCR language)";
        private const string EngineWindows  = "Windows voices (offline)";
        private const string EngineEdge     = "Edge neural voices (online)";

        private readonly LorebookReaderModule _module;

        private Label    _winVoiceLabel;
        private Dropdown _winVoiceDropdown;
        private Label    _edgeVoiceLabel;
        private Dropdown _edgeVoiceDropdown;

        public LorebookSettingsView(LorebookReaderModule module) {
            _module = module;
        }

        protected override void Build(Container buildPanel) {
            var panel = new FlowPanel {
                Parent              = buildPanel,
                WidthSizingMode     = SizingMode.Fill,
                HeightSizingMode    = SizingMode.AutoSize,
                FlowDirection       = ControlFlowDirection.SingleTopToBottom,
                ControlPadding      = new Vector2(0, 10),
                OuterControlPadding = new Vector2(12, 12)
            };

            // --- keybindy ---
            new KeybindingAssigner(_module.ReadKeybindSetting.Value) {
                KeyBindingName = "Read lorebook",
                Parent         = panel
            };
            new KeybindingAssigner(_module.StopKeybindSetting.Value) {
                KeyBindingName = "Stop reading",
                Parent         = panel
            };

            // --- ikona reproduktoru ---
            var speakerCheckbox = new Checkbox {
                Text    = "Show speaker icon on open books",
                Checked = _module.ShowSpeakerButtonSetting.Value,
                Parent  = panel
            };
            speakerCheckbox.CheckedChanged += (s, e) =>
                _module.ShowSpeakerButtonSetting.Value = e.Checked;

            // --- conversation capture ---
            var convCheckbox = new Checkbox {
                Text    = "Conversation capture mode (also detect NPC dialogues)",
                Checked = _module.ConversationCaptureSetting.Value,
                Parent  = panel
            };
            convCheckbox.CheckedChanged += (s, e) =>
                _module.ConversationCaptureSetting.Value = e.Checked;
            // sync: když se setting změní přes keybind, aktualizovat checkbox
            _module.ConversationCaptureSetting.SettingChanged += (s, e) =>
                convCheckbox.Checked = e.NewValue;

            new KeybindingAssigner(_module.ConvToggleKeybindSetting.Value) {
                KeyBindingName = "Toggle conversation capture",
                Parent         = panel
            };

            // P1.1: debug capture pro kalibraci a bug reporty
            new KeybindingAssigner(_module.DebugDumpKeybindSetting.Value) {
                KeyBindingName = "Save debug capture",
                Parent         = panel
            };

            // --- volba TTS enginu ---
            new Label {
                Text           = "Voice engine",
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };
            var engineDropdown = new Dropdown {
                Width  = 360,
                Parent = panel
            };
            engineDropdown.Items.Add(EngineWindows);
            engineDropdown.Items.Add(EngineEdge);
            engineDropdown.SelectedItem =
                _module.VoiceEngineSetting.Value == "edge"
                    ? EngineEdge : EngineWindows;
            engineDropdown.ValueChanged += (s, e) => {
                _module.VoiceEngineSetting.Value =
                    engineDropdown.SelectedItem == EngineEdge ? "edge" : "windows";
                UpdateEngineVisibility();
            };

            // --- offline hlas (Windows) ---
            _winVoiceLabel = new Label {
                Text           = "Windows voice",
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };
            _winVoiceDropdown = new Dropdown {
                Width  = 360,
                Parent = panel
            };
            _winVoiceDropdown.Items.Add(AutoVoiceItem);
            foreach (var (name, lang) in TtsService.InstalledVoices())
                _winVoiceDropdown.Items.Add($"{name}  [{lang}]");

            string savedVoice = _module.VoiceNameSetting.Value ?? "";
            _winVoiceDropdown.SelectedItem =
                _winVoiceDropdown.Items.FirstOrDefault(i =>
                    savedVoice.Length > 0
                    && i.StartsWith(savedVoice, StringComparison.OrdinalIgnoreCase))
                ?? AutoVoiceItem;
            _winVoiceDropdown.ValueChanged += (s, e) => {
                string v = _winVoiceDropdown.SelectedItem;
                _module.VoiceNameSetting.Value =
                    v == AutoVoiceItem ? "" : v.Split(new[] { "  [" },
                        StringSplitOptions.None)[0];
            };

            // --- online hlas (Edge neural) ---
            _edgeVoiceLabel = new Label {
                Text           = "Edge neural voice (requires internet)",
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };
            _edgeVoiceDropdown = new Dropdown {
                Width  = 360,
                Parent = panel
            };
            foreach (string v in EdgeTtsService.CuratedVoices)
                _edgeVoiceDropdown.Items.Add(v);
            _edgeVoiceDropdown.SelectedItem =
                EdgeTtsService.CuratedVoices.Contains(_module.EdgeVoiceSetting.Value)
                    ? _module.EdgeVoiceSetting.Value
                    : EdgeTtsService.CuratedVoices[0];
            _edgeVoiceDropdown.ValueChanged += (s, e) =>
                _module.EdgeVoiceSetting.Value = _edgeVoiceDropdown.SelectedItem;

            // --- rychlost ---
            var rateLabel = new Label {
                Text           = RateText(_module.SpeakingRateSetting.Value),
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };
            var rateBar = new TrackBar {
                MinValue = 50,
                MaxValue = 200,
                Value    = _module.SpeakingRateSetting.Value * 100f,
                Width    = 360,
                Parent   = panel
            };
            rateBar.ValueChanged += (s, e) => {
                float rate = (float)Math.Round(rateBar.Value) / 100f;
                _module.SpeakingRateSetting.Value = rate;
                rateLabel.Text = RateText(rate);
            };

            // --- OCR jazyk ---
            new Label {
                Text           = "OCR language (your GW2 client language)",
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };
            var ocrDropdown = new Dropdown {
                Width  = 360,
                Parent = panel
            };
            foreach (var lang in OcrEngine.AvailableRecognizerLanguages)
                ocrDropdown.Items.Add($"{lang.LanguageTag}  ({lang.DisplayName})");

            string savedLang = _module.OcrLanguageSetting.Value ?? "en-US";
            ocrDropdown.SelectedItem =
                ocrDropdown.Items.FirstOrDefault(i => i.StartsWith(
                    savedLang, StringComparison.OrdinalIgnoreCase))
                ?? ocrDropdown.Items.FirstOrDefault();
            ocrDropdown.ValueChanged += (s, e) =>
                _module.OcrLanguageSetting.Value =
                    ocrDropdown.SelectedItem.Split(' ')[0];

            // --- titulky ---
            var subsCheckbox = new Checkbox {
                Text    = "Show subtitles while reading",
                Checked = _module.ShowSubtitlesSetting.Value,
                Parent  = panel
            };
            subsCheckbox.CheckedChanged += (s, e) =>
                _module.ShowSubtitlesSetting.Value = e.Checked;

            var opacityLabel = new Label {
                Text           = OpacityText(_module.SubtitleOpacitySetting.Value),
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };
            var opacityBar = new TrackBar {
                MinValue = 20, MaxValue = 100,
                Value    = _module.SubtitleOpacitySetting.Value * 100f,
                Width    = 360, Parent = panel
            };
            opacityBar.ValueChanged += (s, e) => {
                float v2 = (float)Math.Round(opacityBar.Value) / 100f;
                _module.SubtitleOpacitySetting.Value = v2;
                opacityLabel.Text = OpacityText(v2);
            };

            var posXLabel = new Label {
                Text           = PosText("X", _module.SubtitleXSetting.Value),
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };
            var posXBar = new TrackBar {
                MinValue = 0, MaxValue = 100,
                Value    = _module.SubtitleXSetting.Value,
                Width    = 360, Parent = panel
            };
            posXBar.ValueChanged += (s, e) => {
                float v2 = (float)Math.Round(posXBar.Value);
                _module.SubtitleXSetting.Value = v2;
                posXLabel.Text = PosText("X", v2);
            };

            var posYLabel = new Label {
                Text           = PosText("Y", _module.SubtitleYSetting.Value),
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };
            var posYBar = new TrackBar {
                MinValue = 0, MaxValue = 100,
                Value    = _module.SubtitleYSetting.Value,
                Width    = 360, Parent = panel
            };
            posYBar.ValueChanged += (s, e) => {
                float v2 = (float)Math.Round(posYBar.Value);
                _module.SubtitleYSetting.Value = v2;
                posYLabel.Text = PosText("Y", v2);
            };

            // --- velikost titulků ---
            new Label {
                Text           = "Subtitle size",
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };
            var sizeDropdown = new Dropdown {
                Width  = 360,
                Parent = panel
            };
            foreach (string item in _sizeItems)
                sizeDropdown.Items.Add(item);
            sizeDropdown.SelectedItem =
                SizeToItem(_module.SubtitleFontSizeSetting.Value);
            sizeDropdown.ValueChanged += (s, e) =>
                _module.SubtitleFontSizeSetting.Value =
                    ItemToSize(sizeDropdown.SelectedItem);

            // --- editace pozice tažením ---
            var editButton = new StandardButton {
                Text   = EditButtonText(),
                Width  = 360,
                Parent = panel
            };
            editButton.Click += (s, e) => {
                _module.SubtitleEditMode = !_module.SubtitleEditMode;
                editButton.Text = EditButtonText();
            };

            // --- reset titulků ---
            var resetButton = new StandardButton {
                Text   = "Reset subtitles to defaults",
                Width  = 360,
                Parent = panel
            };
            resetButton.Click += (s, e) => {
                _module.SubtitleEditMode = false;
                editButton.Text = EditButtonText();
                _module.ShowSubtitlesSetting.Value     = true;
                _module.SubtitleOpacitySetting.Value   = 0.9f;
                _module.SubtitleXSetting.Value         = 50f;
                _module.SubtitleYSetting.Value         = 82f;
                _module.SubtitleFontSizeSetting.Value  = 24;
                subsCheckbox.Checked      = true;
                opacityBar.Value          = 90f;
                opacityLabel.Text         = OpacityText(0.9f);
                posXBar.Value             = 50f;
                posXLabel.Text            = PosText("X", 50f);
                posYBar.Value             = 82f;
                posYLabel.Text            = PosText("Y", 82f);
                sizeDropdown.SelectedItem = SizeToItem(24);
            };

            // sync sliderů po přetažení titulku myší
            _module.SubtitleXSetting.SettingChanged += (s, e) => {
                posXBar.Value  = e.NewValue;
                posXLabel.Text = PosText("X", e.NewValue);
            };
            _module.SubtitleYSetting.SettingChanged += (s, e) => {
                posYBar.Value  = e.NewValue;
                posYLabel.Text = PosText("Y", e.NewValue);
            };

            // --- překlad ---
            new Label {
                Text           = "Translation",
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };
            var modeDropdown = new Dropdown {
                Width  = 360,
                Parent = panel
            };
            foreach (string item in _translateModes)
                modeDropdown.Items.Add(item);
            modeDropdown.SelectedItem =
                ModeToItem(_module.TranslateModeSetting.Value);
            modeDropdown.ValueChanged += (s, e) =>
                _module.TranslateModeSetting.Value =
                    ItemToMode(modeDropdown.SelectedItem);

            new Label {
                Text           = "Translate to",
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };
            var langDropdown = new Dropdown {
                Width  = 360,
                Parent = panel
            };
            foreach (var (code, name) in TranslationService.TargetLanguages)
                langDropdown.Items.Add(name);
            langDropdown.SelectedItem =
                LangCodeToName(_module.TranslateTargetSetting.Value);
            langDropdown.ValueChanged += (s, e) =>
                _module.TranslateTargetSetting.Value =
                    LangNameToCode(langDropdown.SelectedItem);

            new Label {
                Text           = "Note: translation uses a free online service "
                                 + "and may occasionally be unavailable.",
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };

            new Label {
                Text           = "Reading history is available via the Lorebook "
                                 + "Reader icon in the top-left icon bar.",
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };

            var capLabel = new Label {
                Text           = "Catalog size: " + _module.HistoryCapacitySetting.Value,
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                Parent         = panel
            };
            var capBar = new TrackBar {
                MinValue = 5, MaxValue = 100,
                Value    = _module.HistoryCapacitySetting.Value,
                Width    = 360, Parent = panel
            };
            capBar.ValueChanged += (s, e) => {
                int v2 = (int)Math.Round(capBar.Value);
                _module.HistoryCapacitySetting.Value = v2;
                capLabel.Text = "Catalog size: " + v2;
                _module.Catalog?.SetCapacity(v2);
            };

            UpdateEngineVisibility();
        }

        private static readonly string[] _translateModes = {
            "Off", "Subtitles only", "Subtitles + speech"
        };

        private static string ModeToItem(string mode) {
            if (mode == "subtitles") return _translateModes[1];
            if (mode == "full")      return _translateModes[2];
            return _translateModes[0];
        }

        private static string ItemToMode(string item) {
            if (item == _translateModes[1]) return "subtitles";
            if (item == _translateModes[2]) return "full";
            return "off";
        }

        private static string LangCodeToName(string code) {
            foreach (var (c, name) in TranslationService.TargetLanguages)
                if (c == code) return name;
            return TranslationService.TargetLanguages[0].Name;
        }

        private static string LangNameToCode(string name) {
            foreach (var (c, n) in TranslationService.TargetLanguages)
                if (n == name) return c;
            return "cs";
        }

        protected override void Unload() {
            _module.SubtitleEditMode = false;   // bezpečné ukončení editace
            base.Unload();
        }

        private string EditButtonText() =>
            _module.SubtitleEditMode
                ? "Done editing position"
                : "Edit subtitle position (drag with mouse)";

        private static readonly string[] _sizeItems = {
            "Small (18)", "Medium (24)", "Large (32)", "Huge (36)"
        };

        private static string SizeToItem(int size) {
            switch (size) {
                case 18: return _sizeItems[0];
                case 32: return _sizeItems[2];
                case 36: return _sizeItems[3];
                default: return _sizeItems[1];
            }
        }

        private static int ItemToSize(string item) {
            if (item == _sizeItems[0]) return 18;
            if (item == _sizeItems[2]) return 32;
            if (item == _sizeItems[3]) return 36;
            return 24;
        }

        private static string OpacityText(float v) =>
            $"Subtitle opacity: {v * 100:0} %";

        private static string PosText(string axis, float v) =>
            $"Subtitle position {axis}: {v:0} % of screen";

        private void UpdateEngineVisibility() {
            bool edge = _module.VoiceEngineSetting.Value == "edge";
            _winVoiceLabel.Visible     = !edge;
            _winVoiceDropdown.Visible  = !edge;
            _edgeVoiceLabel.Visible    = edge;
            _edgeVoiceDropdown.Visible = edge;
            (_winVoiceLabel.Parent as FlowPanel)?.RecalculateLayout();
        }

        private static string RateText(float rate) =>
            $"Speaking rate: {rate:0.00}×";
    }
}
