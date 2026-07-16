using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Drawing;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rectangle = System.Drawing.Rectangle;
using Point = Microsoft.Xna.Framework.Point;
using Color = Microsoft.Xna.Framework.Color;

namespace Frtal.LorebookReader {

    [Export(typeof(Blish_HUD.Modules.Module))]
    public class LorebookReaderModule : Blish_HUD.Modules.Module {

        private static readonly Logger Logger =
            Logger.GetLogger<LorebookReaderModule>();

        // --- settings ---
        private SettingEntry<KeyBinding> _readKeybind;
        private SettingEntry<KeyBinding> _stopKeybind;
        private SettingEntry<bool>       _showSpeakerButton;
        private SettingEntry<string>     _voiceName;
        private SettingEntry<float>      _speakingRate;
        private SettingEntry<string>     _ocrLanguage;
        private SettingEntry<string>     _voiceEngine;   // "windows" / "edge"
        private SettingEntry<string>     _edgeVoice;
        private SettingEntry<bool>       _showSubtitles;
        private SettingEntry<float>      _subtitleOpacity;   // 0.2-1.0
        private SettingEntry<float>      _subtitleX;         // % šířky
        private SettingEntry<float>      _subtitleY;         // % výšky
        private SettingEntry<int>        _subtitleFontSize;  // 18/24/32/36
        private SettingEntry<string>     _translateMode;     // off/subtitles/full
        private SettingEntry<string>     _translateTarget;   // kód jazyka
        private SettingEntry<bool>       _conversationCapture; // conversation OCR toggle
        private SettingEntry<KeyBinding> _convToggleKeybind;   // keybind pro toggle
        private SettingEntry<KeyBinding> _debugDumpKeybind;    // P1.1: debug capture
        private SettingEntry<string>     _dialogZone;          // "x,y,w,h,resW,resH" px klienta
        private SettingEntry<KeyBinding> _calibrateKeybind;    // kalibrace zóny dialogu
        private SettingEntry<string>     _bookZone;            // OCR pole knížky (px + stamp)
        private SettingEntry<KeyBinding> _bookCalibrateKeybind;
        private SettingEntry<bool>       _encRailCollapsed;    // rail encyklopedie jen ikony

        // přístup pro LorebookSettingsView
        internal SettingEntry<KeyBinding> ReadKeybindSetting       => _readKeybind;
        internal SettingEntry<KeyBinding> StopKeybindSetting       => _stopKeybind;
        internal SettingEntry<bool>       ShowSpeakerButtonSetting => _showSpeakerButton;
        internal SettingEntry<string>     VoiceNameSetting         => _voiceName;
        internal SettingEntry<float>      SpeakingRateSetting      => _speakingRate;
        internal SettingEntry<string>     OcrLanguageSetting       => _ocrLanguage;
        internal SettingEntry<string>     VoiceEngineSetting       => _voiceEngine;
        internal SettingEntry<string>     EdgeVoiceSetting         => _edgeVoice;
        internal SettingEntry<bool>       ShowSubtitlesSetting     => _showSubtitles;
        internal SettingEntry<float>      SubtitleOpacitySetting   => _subtitleOpacity;
        internal SettingEntry<float>      SubtitleXSetting         => _subtitleX;
        internal SettingEntry<float>      SubtitleYSetting         => _subtitleY;
        internal SettingEntry<int>        SubtitleFontSizeSetting  => _subtitleFontSize;
        internal SettingEntry<string>     TranslateModeSetting     => _translateMode;
        internal SettingEntry<string>     TranslateTargetSetting   => _translateTarget;
        internal SettingEntry<bool>       ConversationCaptureSetting => _conversationCapture;
        internal SettingEntry<KeyBinding> ConvToggleKeybindSetting   => _convToggleKeybind;
        internal SettingEntry<KeyBinding> DebugDumpKeybindSetting    => _debugDumpKeybind;
        internal SettingEntry<string>     DialogZoneSetting          => _dialogZone;
        internal SettingEntry<KeyBinding> CalibrateKeybindSetting    => _calibrateKeybind;
        internal SettingEntry<string>     BookZoneSetting            => _bookZone;
        internal SettingEntry<KeyBinding> BookCalibrateKeybindSetting => _bookCalibrateKeybind;
        internal SettingEntry<bool>       EncyclopediaRailCollapsedSetting => _encRailCollapsed;

        // --- stav ---
        private TtsService _tts;
        private EdgeTtsService _edgeTts;
        private LorebookCatalog _catalog;
        private CornerIcon _cornerIcon;
        private StandardWindow _historyWindow;
        private volatile bool _catalogDirty;
        private TextRenderer _textRenderer;
        private EncyclopediaView _encyclopediaView;
        private Texture2D _parchmentTexture;

        internal TextRenderer SharedTextRenderer => _textRenderer;
        private int _speakSession;
        private volatile bool _chunkTranslate;
        private string _chunkTranslateTarget = "cs";
        private BookActionButton _speakerButton;
        private BookActionButton _saveButton;
        private BookActionButton _appendButton;
        private SubtitleOverlay _subtitleLabel;
        private volatile bool _subtitleDirty;
        private string _pendingSubtitle;
        // titulková cue: celý úsek (orig/překlad) → krátká cue posouvaná v čase
        private volatile string _cueSource;
        private volatile bool _cueSourceDirty;
        private System.Collections.Generic.List<string> _cues;
        private int _cueIndex;
        private double _cueElapsedMs;
        private int[] _cueWordStart;           // index prvního slova každého cue
        private volatile int _currentWordIndex; // právě čtené slovo (z TTS)
        private volatile bool _haveWordEvents;
        private volatile bool _wordSyncValid = true;
        private int _lastSubWidth = -1;
        private int _lastFontSize = -1;
        private int _subWidthCap = -1;   // strop šířky titulků (~42 znaků)
        private bool _readBusy;
        private bool _detectBusy;
        private double _detectTimerMs;

        // poslední výsledek detekce na pozadí (aplikuje se v Update)
        private volatile bool _bookVisible;
        private Rectangle _bookBox;          // v pixelech klientské oblasti GW2
        private Rectangle _gw2ClientRect;    // velikost klientské oblasti

        // conversation detection
        private volatile bool _convVisible;
        private Rectangle _convBox;          // konverzační panel

        // kalibrace zóny dialogu (uživatelsky označená plocha)
        private DialogZoneCalibrator _calibrator;
        private bool _calibNudgeShown;

        [ImportingConstructor]
        public LorebookReaderModule(
            [Import("ModuleParameters")] ModuleParameters moduleParameters)
            : base(moduleParameters) { }

        public override Blish_HUD.Graphics.UI.IView GetSettingsView() =>
            new LorebookSettingsView(this);

        protected override void DefineSettings(SettingCollection settings) {
            _readKeybind = settings.DefineSetting(
                "ReadKeybind",
                new KeyBinding(ModifierKeys.Ctrl | ModifierKeys.Alt, Keys.R),
                () => "Read lorebook",
                () => "Reads the currently open lorebook aloud.");

            _stopKeybind = settings.DefineSetting(
                "StopKeybind",
                new KeyBinding(ModifierKeys.Ctrl | ModifierKeys.Alt, Keys.S),
                () => "Stop reading",
                () => "Stops the current text-to-speech playback.");

            _showSpeakerButton = settings.DefineSetting(
                "ShowSpeakerButton", true,
                () => "Show speaker icon on open books",
                () => "Displays a clickable speaker icon next to a detected lorebook.");

            _voiceName = settings.DefineSetting(
                "VoiceName", "",
                () => "Voice (part of name)",
                () => "Leave empty for default. Available voices are listed in the log on module start.");

            _speakingRate = settings.DefineSetting(
                "SpeakingRate", 1.0f,
                () => "Speaking rate",
                () => "1.0 = normal speed.");
            _speakingRate.SetRange(0.5f, 2.0f);

            _ocrLanguage = settings.DefineSetting(
                "OcrLanguage", "en-US",
                () => "OCR language",
                () => "Language of your GW2 client: en-US, de-DE, fr-FR or es-ES.");

            _voiceEngine = settings.DefineSetting(
                "VoiceEngine", "windows",
                () => "Voice engine",
                () => "Windows = offline (private). Edge = online neural voices "
                    + "via a free Microsoft endpoint (sends text to a third party).");

            _edgeVoice = settings.DefineSetting(
                "EdgeVoice", "en-GB-RyanNeural",
                () => "Edge neural voice",
                () => "Used when the voice engine is set to Edge.");

            _showSubtitles = settings.DefineSetting(
                "ShowSubtitles", true,
                () => "Show subtitles",
                () => "Displays the text being read as an on-screen overlay.");

            _subtitleOpacity = settings.DefineSetting(
                "SubtitleOpacity", 0.9f,
                () => "Subtitle opacity", () => "");
            _subtitleOpacity.SetRange(0.2f, 1.0f);

            _subtitleX = settings.DefineSetting(
                "SubtitleX", 50f,
                () => "Subtitle horizontal position (%)", () => "");
            _subtitleX.SetRange(0f, 100f);

            _subtitleY = settings.DefineSetting(
                "SubtitleY", 82f,
                () => "Subtitle vertical position (%)", () => "");
            _subtitleY.SetRange(0f, 100f);

            _subtitleFontSize = settings.DefineSetting(
                "SubtitleFontSize", 24,
                () => "Subtitle size", () => "");

            _translateMode = settings.DefineSetting(
                "TranslateMode", "off",
                () => "Translation",
                () => "Off / subtitles only / subtitles + speech. Uses a free "
                    + "online translation endpoint (sends text to a third party).");

            _translateTarget = settings.DefineSetting(
                "TranslateTarget", "cs",
                () => "Translate to",
                () => "Target language for translation.");

            _conversationCapture = settings.DefineSetting(
                "ConversationCapture", false,
                () => "Conversation capture mode",
                () => "When enabled, also detects NPC dialogue windows "
                    + "(not just lorebooks). Useful for saving story "
                    + "conversations to the encyclopedia.");

            _convToggleKeybind = settings.DefineSetting(
                "ConvToggleKeybind",
                new KeyBinding(ModifierKeys.Ctrl | ModifierKeys.Alt, Keys.C),
                () => "Toggle conversation capture",
                () => "Quickly turn conversation capture on/off during gameplay.");

            // P1.1: debug dump — vrací se schopnost z Python prototypu.
            // Ukládá kompletní důkazní materiál pro kalibraci detektorů
            // a použitelné community bug reporty.
            _debugDumpKeybind = settings.DefineSetting(
                "DebugDumpKeybind",
                new KeyBinding(ModifierKeys.Ctrl | ModifierKeys.Alt, Keys.D),
                () => "Save debug capture",
                () => "Saves the current frame, detector results and OCR "
                    + "output to the lorebook_reader\\debug folder. "
                    + "Attach that folder to bug reports.");

            _dialogZone = settings.DefineSetting(
                "DialogZone", "",
                () => "Calibrated dialogue zone",
                () => "Internal store of the calibrated dialogue area "
                    + "(pixels + resolution stamp).");

            _calibrateKeybind = settings.DefineSetting(
                "CalibrateKeybind",
                new KeyBinding(ModifierKeys.Ctrl | ModifierKeys.Alt, Keys.Z),
                () => "Calibrate dialogue zone",
                () => "Opens a draggable frame to mark where dialogue text "
                    + "appears. Do this once per screen resolution.");

            _bookZone = settings.DefineSetting(
                "BookZone", "",
                () => "Lorebook OCR area (calibrated)",
                () => "Internal store of the calibrated lorebook OCR area.");

            _bookCalibrateKeybind = settings.DefineSetting(
                "BookCalibrateKeybind",
                new KeyBinding(ModifierKeys.Ctrl | ModifierKeys.Alt, Keys.B),
                () => "Calibrate lorebook OCR area",
                () => "Open a lorebook, then press this to drag a frame over the "
                    + "book text. Fixes text getting cut off. Once per resolution.");

            _encRailCollapsed = settings.DefineSetting(
                "EncyclopediaRailCollapsed", false,
                () => "Encyclopedia expansion rail collapsed",
                () => "Internal: the expansion rail shows icons only.");
        }

        protected override async Task LoadAsync() {
            _tts = new TtsService();
            _edgeTts = new EdgeTtsService();
            _textRenderer = new TextRenderer(GameService.Graphics.GraphicsDeviceManager.GraphicsDevice);

            string dir = ModuleParameters.DirectoriesManager
                .GetFullDirectoryPath("lorebook_reader");
            _catalog = new LorebookCatalog(dir);
            _catalog.Changed += () => _catalogDirty = true;
            Logger.Info("Available TTS voices: "
                        + string.Join(", ", TtsService.InstalledVoices()
                            .Select(v => $"{v.Name} [{v.Lang}]")));
            await Task.CompletedTask;
        }

        protected override void OnModuleLoaded(EventArgs e) {
            _readKeybind.Value.Enabled = true;
            _readKeybind.Value.Activated += OnReadActivated;
            _stopKeybind.Value.Enabled = true;
            _stopKeybind.Value.Activated += OnStopActivated;
            _convToggleKeybind.Value.Enabled = true;
            _convToggleKeybind.Value.Activated += OnConvToggleActivated;
            _debugDumpKeybind.Value.Enabled = true;
            _debugDumpKeybind.Value.Activated += OnDebugDumpActivated;
            _calibrateKeybind.Value.Enabled = true;
            _calibrateKeybind.Value.Activated += OnCalibrateActivated;
            _bookCalibrateKeybind.Value.Enabled = true;
            _bookCalibrateKeybind.Value.Activated += OnBookCalibrateActivated;

            // P0.3: pokud Load() narazil na poškozený katalog, říct to
            // uživateli — tiché selhání dřív umělo zahodit celou sbírku
            if (!string.IsNullOrEmpty(_catalog?.LoadWarning)) {
                Logger.Warn("Catalog load warning: " + _catalog.LoadWarning);
                ScreenNotification.ShowNotification(
                    "Lorebook Reader: " + _catalog.LoadWarning);
            }

            _speakerButton = MakeBookButton("speaker", "Read this book aloud",
                () => StartRead());
            _saveButton = MakeBookButton("save",
                "Save to encyclopedia (don't read)", () => StartSaveOnly());
            _appendButton = MakeBookButton("append",
                "Append this page to the last saved book", () => StartAppend());

            _subtitleLabel = new SubtitleOverlay(_textRenderer) {
                Parent   = GameService.Graphics.SpriteScreen,
                FontSize = _subtitleFontSize.Value,
                BoxWidth = 600,
                Visible  = false
            };
            _lastFontSize = _subtitleFontSize.Value;
            // Netflix strop šířky spočítat hned (ne až při změně fontu)
            _subWidthCap = (int)_textRenderer.MeasureWidth(
                new string('n', 42), _lastFontSize) + 16;
            _subtitleLabel.PositionEdited += (s, a) => {
                var spriteP = GameService.Graphics.SpriteScreen.Size;
                if (spriteP.X <= 0 || spriteP.Y <= 0) return;
                float xPct = (_subtitleLabel.Location.X
                              + _subtitleLabel.Width / 2f) / spriteP.X * 100f;
                float yPct = (float)_subtitleLabel.Location.Y / spriteP.Y * 100f;
                _subtitleX.Value = Math.Max(0f, Math.Min(100f, xPct));
                _subtitleY.Value = Math.Max(0f, Math.Min(100f, yPct));
            };

            _parchmentTexture =
                ModuleParameters.ContentsManager.GetTexture("parchment.png");

            _cornerIcon = new CornerIcon(
                ModuleParameters.ContentsManager.GetTexture("book.png"),
                ModuleParameters.ContentsManager.GetTexture("book_hover.png"),
                "Lorebook Encyclopedia");
            _cornerIcon.Click += (s, a) => {
                if (_historyWindow.Visible) {
                    _historyWindow.Hide();
                } else {
                    ShowEncyclopedia();
                }
            };

            _historyWindow = new StandardWindow(
                GameService.Content.DatAssetCache.GetTextureFromAssetId(155985),
                new Microsoft.Xna.Framework.Rectangle(40, 26, 913, 691),
                new Microsoft.Xna.Framework.Rectangle(70, 71, 839, 605),
                new Point(880, 640)) {
                Parent        = GameService.Graphics.SpriteScreen,
                Title         = "Lorebook Encyclopedia",
                SavesPosition = true,
                CanResize     = true,
                Id            = "frtal_lorebook_reader_encyclopedia"
            };

            base.OnModuleLoaded(e);
        }

        internal bool SubtitleEditMode {
            get => _subtitleLabel != null && _subtitleLabel.EditMode;
            set {
                if (_subtitleLabel == null) return;
                if (value) {
                    var sprite = GameService.Graphics.SpriteScreen.Size;
                    int width = Math.Max(100, (int)(sprite.X * 0.45f));
                    _subtitleLabel.BoxWidth = width;
                    _lastSubWidth = width;
                    _subtitleLabel.EditMode = true;
                    int sx = (int)(sprite.X * (_subtitleX.Value / 100f))
                             - width / 2;
                    int sy = (int)(sprite.Y * (_subtitleY.Value / 100f));
                    _subtitleLabel.Location = new Point(
                        Math.Max(0, Math.Min(sx, sprite.X - width)),
                        Math.Max(0, Math.Min(sy,
                            sprite.Y - Math.Max(1, _subtitleLabel.Size.Y))));
                } else {
                    _subtitleLabel.EditMode = false;
                }
            }
        }

        private BookActionButton MakeBookButton(
                string iconKey, string tooltip, Action onClick) {
            // textury načíst jednou — žádné GetTexture při každém hoveru
            var icon = ModuleParameters.ContentsManager
                .GetTexture(iconKey + ".png");
            var hover = ModuleParameters.ContentsManager
                .GetTexture(iconKey + "_hover.png");
            return new BookActionButton(icon, hover, tooltip, onClick) {
                Parent = GameService.Graphics.SpriteScreen
            };
        }

        private void OnReadActivated(object sender, EventArgs e) => StartRead();
        private void OnStopActivated(object sender, EventArgs e) {
            _tts.Stop();
            _edgeTts?.Stop();
        }

        // P1.1: debug capture — _dumpBusy přes Interlocked (nový kód už
        // nepíšeme přes obyčejné booly, viz revize P2.4)
        private int _dumpBusy;

        private void OnDebugDumpActivated(object sender, EventArgs e) {
            if (System.Threading.Interlocked.CompareExchange(
                    ref _dumpBusy, 1, 0) != 0) return;
            Task.Run(DebugDumpAsync);
        }

        /// <summary>Uloží frame.png + výsledky obou detektorů + výřez +
        /// raw/očištěný OCR text do lorebook_reader\debug\dump_&lt;čas&gt;.
        /// Běží oba detektory bez ohledu na conversation toggle — je to
        /// diagnostika; stav toggle se zapisuje do info.txt.</summary>
        private async Task DebugDumpAsync() {
            try {
                string root = ModuleParameters.DirectoriesManager
                    .GetFullDirectoryPath("lorebook_reader");
                string dir = System.IO.Path.Combine(root, "debug",
                    "dump_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                System.IO.Directory.CreateDirectory(dir);

                var info = new System.Text.StringBuilder();
                info.AppendLine("Lorebook Reader debug capture");
                info.AppendLine("Time: " + DateTime.Now.ToString("o"));
                // SemVer.Version je jen tranzitivní závislost BlishHUD —
                // přímé použití typu by chtělo referenci na SemVer assembly,
                // proto čtení přes reflexi (výsledek je stejně jen string)
                info.AppendLine("Module version: "
                    + (ModuleParameters.Manifest.GetType()
                        .GetProperty("Version")
                        ?.GetValue(ModuleParameters.Manifest)
                        ?.ToString() ?? "?"));
                info.AppendLine("OCR language: " + _ocrLanguage.Value);
                info.AppendLine("Conversation capture: "
                    + (_conversationCapture.Value ? "ON" : "OFF"));

                IntPtr hwnd = GameService.GameIntegration
                    .Gw2Instance.Gw2WindowHandle;
                using (Bitmap screen =
                           ScreenCapture.Grab(hwnd, out Rectangle _)) {
                    info.AppendLine(
                        $"Client area: {screen.Width}x{screen.Height}");
                    screen.Save(System.IO.Path.Combine(dir, "frame.png"),
                        System.Drawing.Imaging.ImageFormat.Png);

                    Rectangle? parch =
                        ParchmentDetector.Find(screen, out double pSol);
                    info.AppendLine(parch != null
                        ? $"ParchmentDetector: {parch} solidity {pSol:0.000}"
                        : "ParchmentDetector: no hit");

                    var convHit = ConversationDetector.FindHit(screen);
                    info.AppendLine(convHit != null
                        ? "ConversationDetector: panel " + convHit.Panel
                          + $" text {convHit.TextArea}"
                          + $" confidence {convHit.Confidence:0.000}"
                        : "ConversationDetector: no hit");
                    if (!string.IsNullOrEmpty(
                            ConversationDetector.LastDiagnostics)) {
                        info.AppendLine("Detector diagnostics:");
                        info.AppendLine(ConversationDetector.LastDiagnostics);
                    }

                    // kalibrovaná zóna: report + (když je) použij ji pro výřez,
                    // ať dump testuje přesně cestu, kterou jede čtení
                    var zoneDbg = GetCalibratedZone(screen.Width, screen.Height);
                    ConversationHit convUse = convHit;
                    if (zoneDbg != null) {
                        var zHit = ConversationDetector.MeasureInZone(
                            screen, zoneDbg.Value);
                        info.AppendLine($"Calibrated zone: {zoneDbg.Value}");
                        info.AppendLine(zHit != null
                            ? $"Zone measure: text {zHit.TextArea} "
                              + $"conf {zHit.Confidence:0.000}"
                            : "Zone measure: no bright text");
                        if (parch == null && zHit != null) convUse = zHit;
                    } else {
                        info.AppendLine("Calibrated zone: none");
                    }

                    // stejná priorita jako CaptureBookAsync: pergamen první
                    bool isConversation = parch == null && convUse != null;
                    Rectangle? box = parch ?? convUse?.Panel;

                    if (box != null) {
                        var bookCropDbg = parch != null
                            ? GetCalibratedBookCrop(parch.Value) : null;
                        Rectangle inner = isConversation
                            ? convUse.TextArea
                            : (bookCropDbg ?? ParchmentDetector.InnerCrop(box.Value));
                        inner = Rectangle.Intersect(inner,
                            new Rectangle(0, 0, screen.Width, screen.Height));
                        if (!isConversation)
                            info.AppendLine("Book crop: " + (bookCropDbg != null
                                ? "calibrated size (auto-positioned)" : "auto InnerCrop"));
                        info.AppendLine("Detector used: "
                            + (isConversation ? "conversation" : "parchment"));
                        info.AppendLine($"Text crop: {inner}");

                        using (Bitmap crop =
                                   screen.Clone(inner, screen.PixelFormat)) {
                            crop.Save(
                                System.IO.Path.Combine(dir, "crop.png"),
                                System.Drawing.Imaging.ImageFormat.Png);
                            string raw = await OcrService.RecognizeAsync(
                                crop, _ocrLanguage.Value,
                                invert: isConversation);
                            System.IO.File.WriteAllText(
                                System.IO.Path.Combine(dir, "ocr_raw.txt"),
                                raw ?? "");
                            System.IO.File.WriteAllText(
                                System.IO.Path.Combine(dir, "ocr_clean.txt"),
                                TextCleaner.CleanForEncyclopedia(raw ?? ""));
                        }
                    } else {
                        info.AppendLine("No panel detected — frame.png "
                            + "saved for calibration.");
                    }
                }

                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(dir, "info.txt"), info.ToString());
                ScreenNotification.ShowNotification(
                    "Debug capture saved: debug\\"
                    + System.IO.Path.GetFileName(dir));
                Logger.Info("Debug capture saved to " + dir);
            } catch (Exception ex) {
                Logger.Warn(ex, "Debug capture failed.");
                ScreenNotification.ShowNotification(
                    "Debug capture failed: " + ex.Message);
            } finally {
                System.Threading.Interlocked.Exchange(ref _dumpBusy, 0);
            }
        }

        private void OnConvToggleActivated(object sender, EventArgs e) {
            _conversationCapture.Value = !_conversationCapture.Value;
            string state = _conversationCapture.Value ? "ON" : "OFF";
            ScreenNotification.ShowNotification(
                $"Conversation capture: {state}");
            if (!_conversationCapture.Value) { _convVisible = false; return; }
            MaybeNudgeCalibration();
        }

        /// <summary>Při prvním zapnutí konverzace bez kalibrace jemně
        /// nabídne označení zóny — spolehlivost tím výrazně stoupne.</summary>
        private void MaybeNudgeCalibration() {
            if (_calibNudgeShown) return;
            if (!string.IsNullOrEmpty(_dialogZone.Value)) return;
            _calibNudgeShown = true;
            ScreenNotification.ShowNotification(
                "Tip: press Ctrl+Alt+Z to mark where dialogues appear — "
                + "detection then gets much more reliable.");
        }

        // --- kalibrace zóny dialogu ---

        internal void StartCalibration() =>
            OnCalibrateActivated(null, EventArgs.Empty);

        private void OnCalibrateActivated(object sender, EventArgs e) {
            if (_calibrator != null) return; // už běží
            var sp = GameService.Graphics.SpriteScreen.Size;
            Microsoft.Xna.Framework.Rectangle start;
            var zone = GetCalibratedZone(
                _gw2ClientRect.Width, _gw2ClientRect.Height);
            if (zone != null && _gw2ClientRect.Width > 0
                             && _gw2ClientRect.Height > 0) {
                float sx = (float)sp.X / _gw2ClientRect.Width;
                float sy = (float)sp.Y / _gw2ClientRect.Height;
                start = new Microsoft.Xna.Framework.Rectangle(
                    (int)(zone.Value.X * sx), (int)(zone.Value.Y * sy),
                    (int)(zone.Value.Width * sx), (int)(zone.Value.Height * sy));
            } else {
                // rozumný výchozí obdélník v horní části obrazovky
                // (užší — odpovídá ploše textu vyprávění, ne celému panelu)
                start = new Microsoft.Xna.Framework.Rectangle(
                    (int)(sp.X * 0.35f), (int)(sp.Y * 0.06f),
                    (int)(sp.X * 0.22f), (int)(sp.Y * 0.10f));
            }
            _calibrator = new DialogZoneCalibrator(
                start, OnCalibrationSaved, CloseCalibrator);
        }

        private void OnCalibrationSaved(
                Microsoft.Xna.Framework.Rectangle spriteRect) {
            try {
                IntPtr hwnd = GameService.GameIntegration
                    .Gw2Instance.Gw2WindowHandle;
                int cw, ch;
                using (Bitmap s = ScreenCapture.Grab(hwnd, out Rectangle _)) {
                    cw = s.Width; ch = s.Height;
                }
                var sp = GameService.Graphics.SpriteScreen.Size;
                float sx = (float)cw / Math.Max(1, sp.X);
                float sy = (float)ch / Math.Max(1, sp.Y);
                int x = Math.Max(0, (int)(spriteRect.X * sx));
                int y = Math.Max(0, (int)(spriteRect.Y * sy));
                int w = (int)(spriteRect.Width  * sx);
                int h = (int)(spriteRect.Height * sy);
                _dialogZone.Value = $"{x},{y},{w},{h},{cw},{ch}";
                ScreenNotification.ShowNotification(
                    $"Dialogue zone saved ({w}×{h} @ {cw}×{ch}).");
                Logger.Info($"Dialogue zone calibrated: {_dialogZone.Value}");
            } catch (Exception ex) {
                Logger.Warn(ex, "Calibration save failed.");
                ScreenNotification.ShowNotification(
                    "Calibration save failed: " + ex.Message);
            }
            CloseCalibrator();
        }

        private void CloseCalibrator() {
            _calibrator?.Dispose();
            _calibrator = null;
        }

        /// <summary>Naparsuje uloženou zónu, ale jen když stamp rozlišení
        /// sedí na aktuální klientskou oblast (jinak neplatná → null → padne
        /// se na heuristiku).</summary>
        private Rectangle? GetCalibratedZone(int clientW, int clientH) {
            var raw = _dialogZone.Value;
            if (string.IsNullOrEmpty(raw)) return null;
            var p = raw.Split(',');
            if (p.Length != 6) return null;
            if (!int.TryParse(p[0], out int x)  || !int.TryParse(p[1], out int y)
             || !int.TryParse(p[2], out int w)  || !int.TryParse(p[3], out int h)
             || !int.TryParse(p[4], out int rw) || !int.TryParse(p[5], out int rh))
                return null;
            if (w < 16 || h < 16) return null;
            if (clientW > 0 && clientH > 0 && (rw != clientW || rh != clientH))
                return null;
            return new Rectangle(x, y, w, h);
        }

        // --- kalibrace OCR pole knížky: ukládá JEN TVAR relativně k
        //     detekovanému boxu; pozice se pak vždy bere z auto-detekce ---

        private Rectangle _bookCalibBox; // detekovaná kniha v okamžiku kalibrace

        internal void StartBookCalibration() =>
            OnBookCalibrateActivated(null, EventArgs.Empty);

        private void OnBookCalibrateActivated(object sender, EventArgs e) {
            if (_calibrator != null) return;
            if (!_bookVisible || _gw2ClientRect.Width <= 0) {
                ScreenNotification.ShowNotification(
                    "Open a lorebook first (wait for the buttons), then calibrate.");
                return;
            }
            _bookCalibBox = _bookBox; // pozici neukládáme, jen tvar vůči ní
            var sp = GameService.Graphics.SpriteScreen.Size;
            float sx = (float)sp.X / _gw2ClientRect.Width;
            float sy = (float)sp.Y / _gw2ClientRect.Height;
            // seed = aktuální kalibrovaný výřez, jinak výchozí InnerCrop
            Rectangle seed = GetCalibratedBookCrop(_bookBox)
                             ?? ParchmentDetector.InnerCrop(_bookBox);
            var start = new Microsoft.Xna.Framework.Rectangle(
                (int)(seed.X * sx), (int)(seed.Y * sy),
                (int)(seed.Width * sx), (int)(seed.Height * sy));
            _calibrator = new DialogZoneCalibrator(
                start, OnBookZoneSaved, CloseCalibrator);
        }

        private void OnBookZoneSaved(
                Microsoft.Xna.Framework.Rectangle spriteRect) {
            try {
                var b = _bookCalibBox;
                if (b.Width <= 0 || b.Height <= 0) {
                    ScreenNotification.ShowNotification(
                        "Calibration lost the book — open it and try again.");
                    CloseCalibrator();
                    return;
                }
                IntPtr hwnd = GameService.GameIntegration
                    .Gw2Instance.Gw2WindowHandle;
                int cw, ch;
                using (Bitmap s = ScreenCapture.Grab(hwnd, out Rectangle _)) {
                    cw = s.Width; ch = s.Height;
                }
                var sp = GameService.Graphics.SpriteScreen.Size;
                float sx = (float)cw / Math.Max(1, sp.X);
                float sy = (float)ch / Math.Max(1, sp.Y);
                double fx = spriteRect.X * sx, fy = spriteRect.Y * sy;
                double fw = spriteRect.Width * sx, fh = spriteRect.Height * sy;
                // tvar RELATIVNĚ k boxu, v 1/10000 (základní body) → pozice
                // jde vždy z auto-detekce a funguje to napříč rozlišeními
                int xBp = (int)Math.Round((fx - b.X) * 10000.0 / b.Width);
                int yBp = (int)Math.Round((fy - b.Y) * 10000.0 / b.Height);
                int wBp = (int)Math.Round(fw * 10000.0 / b.Width);
                int hBp = (int)Math.Round(fh * 10000.0 / b.Height);
                _bookZone.Value = $"{xBp},{yBp},{wBp},{hBp}";
                ScreenNotification.ShowNotification(
                    "Lorebook OCR area saved (position stays auto-detected).");
                Logger.Info($"Lorebook OCR area (rel bp): {_bookZone.Value}");
            } catch (Exception ex) {
                Logger.Warn(ex, "Book calibration save failed.");
                ScreenNotification.ShowNotification(
                    "Book calibration save failed: " + ex.Message);
            }
            CloseCalibrator();
        }

        /// <summary>Výřez OCR pole = uložený tvar (relativně k boxu) aplikovaný
        /// na PRÁVĚ detekovaný box. Pozice tedy jde z detekce, jen velikost a
        /// odsazení je uživatelské. Null = nekalibrováno.</summary>
        private Rectangle? GetCalibratedBookCrop(Rectangle box) {
            var raw = _bookZone.Value;
            if (string.IsNullOrEmpty(raw)) return null;
            var p = raw.Split(',');
            if (p.Length != 4) return null;
            if (!int.TryParse(p[0], out int xBp) || !int.TryParse(p[1], out int yBp)
             || !int.TryParse(p[2], out int wBp) || !int.TryParse(p[3], out int hBp))
                return null;
            if (wBp < 100 || hBp < 100) return null; // < 1 % boxu = nesmysl
            int x = box.X + (int)Math.Round(xBp / 10000.0 * box.Width);
            int y = box.Y + (int)Math.Round(yBp / 10000.0 * box.Height);
            int w = (int)Math.Round(wBp / 10000.0 * box.Width);
            int h = (int)Math.Round(hBp / 10000.0 * box.Height);
            return new Rectangle(x, y, w, h);
        }

        private void StartRead() {
            if (_readBusy) return;
            _readBusy = true;
            Task.Run(ReadPipelineAsync);
        }

        // tlačítko „uložit bez přehrání": OCR -> jen do katalogu
        private void StartSaveOnly() {
            if (_readBusy) return;
            _readBusy = true;
            Task.Run(SaveOnlyPipelineAsync);
        }

        // tlačítko „připojit za poslední": OCR -> append k nejnovější knize
        private void StartAppend() {
            if (_readBusy) return;
            _readBusy = true;
            Task.Run(AppendPipelineAsync);
        }

        private void OnTtsChunk(string chunk) {
            // word-sync platí, jen když titulek = mluvený text (ne subtitles-only
            // překlad, kde hlas čte originál a titulky jsou přeložené)
            if (chunk != null) _wordSyncValid = !_chunkTranslate;
            if (chunk != null && _chunkTranslate) {
                int session = _speakSession;
                string src = chunk;
                string target = _chunkTranslateTarget;
                Task.Run(async () => {
                    string shown = src;
                    try {
                        shown = await TranslationService.TranslateAsync(
                            src, target);
                    } catch { /* nech originál */ }
                    if (session == _speakSession) {
                        _cueSource = shown;    // Update ho rozseká na cue
                        _cueSourceDirty = true;
                    }
                });
                return;
            }
            _cueSource = chunk;    // null = konec čtení
            _cueSourceDirty = true;
        }

        /// <summary>Engine hlásí index právě čteného slova (v rámci chunku).
        /// Jen uložit — mapování na cue dělá Update (jednovláknově).</summary>
        private void OnTtsWord(int wordIndex) {
            _currentWordIndex = wordIndex;
            _haveWordEvents = true;
        }

        /// <summary>Rozseká úsek textu na titulková cue: max 2 řádky, každý se
        /// vejde do lineWidthPx (měřeno reálnou šířkou fontu → sedí na jakémkoli
        /// rozlišení). Zarovnáno na věty, řádek se láme po slovech.</summary>
        private System.Collections.Generic.List<string> BuildSubtitleCues(
                string text, int lineWidthPx, int fontSize) {
            var cues = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(text) || _textRenderer == null)
                return cues;
            text = System.Text.RegularExpressions.Regex
                .Replace(text, @"\s+", " ").Trim();
            var sentences = System.Text.RegularExpressions.Regex
                .Split(text, @"(?<=[.!?])\s+");
            foreach (string sent in sentences) {
                // 1) zabalit větu na řádky (plná šířka)
                var lines = new System.Collections.Generic.List<string>();
                string line = "";
                foreach (string w in sent.Split(' ')) {
                    if (w.Length == 0) continue;
                    string cand = line.Length == 0 ? w : line + " " + w;
                    if (line.Length == 0
                        || _textRenderer.MeasureWidth(cand, fontSize) <= lineWidthPx)
                        line = cand;
                    else { lines.Add(line); line = w; }
                }
                if (line.Length > 0) lines.Add(line);
                // 2) párovat řádky OD KONCE → případný osiřelý (lichý) řádek je
                //    první PLNÝ řádek, ne poslední 1-slovo (to dělalo cue s
                //    jediným slovem)
                var sentCues = new System.Collections.Generic.List<string>();
                int idx = lines.Count;
                while (idx > 0) {
                    int start = Math.Max(0, idx - 2);
                    sentCues.Add(string.Join(" ",
                        lines.GetRange(start, idx - start)));
                    idx = start;
                }
                sentCues.Reverse();
                cues.AddRange(sentCues);
            }
            return cues;
        }

        /// <summary>Index prvního slova každého cue (kumulativně) — pro
        /// mapování word-boundary indexu z TTS na aktuální cue.</summary>
        private static int[] BuildCueWordStarts(
                System.Collections.Generic.List<string> cues) {
            if (cues == null || cues.Count == 0) return null;
            var starts = new int[cues.Count];
            int acc = 0;
            for (int k = 0; k < cues.Count; k++) {
                starts[k] = acc;
                acc += cues[k].Split(' ').Length;
            }
            return starts;
        }

        /// <summary>Společná OCR část: sejme obrazovku, najde pergamen,
        /// rozpozná text a hlavičku. Vrací false, když není co číst.</summary>
        private async Task<(bool ok, string title, string text)> CaptureBookAsync() {
            string title = null;
            string text;

            IntPtr hwnd = GameService.GameIntegration.Gw2Instance.Gw2WindowHandle;
            using (Bitmap screen = ScreenCapture.Grab(hwnd, out Rectangle screenRect)) {

                // 1) Zkusit pergamenový lorebook (priorita)
                Rectangle? box = ParchmentDetector.Find(screen, out double solidity);
                bool parchmentDetected = box != null;
                bool isConversation = false;
                Rectangle? convText = null; // v6: změřená textová oblast

                if (box != null) {
                    Logger.Info($"Parchment {box} solidity {solidity:0.00}");
                } else if (_conversationCapture.Value) {
                    // 2) Konverzace: s kalibrací pevná zóna (spolehlivá),
                    //    jinak heuristika. Manuální čtení věří uživateli —
                    //    když v zóně není měřitelný text, vezme celou zónu.
                    var zone = GetCalibratedZone(screen.Width, screen.Height);
                    var hit = zone != null
                        ? (ConversationDetector.MeasureInZone(screen, zone.Value)
                           ?? new ConversationHit {
                                  Panel = zone.Value, TextArea = zone.Value,
                                  Confidence = 0 })
                        : ConversationDetector.FindHit(screen);
                    if (hit != null) {
                        box = hit.Panel;
                        convText = hit.TextArea;
                        solidity = hit.Confidence;
                        isConversation = true;
                        Logger.Info($"Conversation {(zone != null ? "zone" : "auto")} "
                            + $"panel {hit.Panel} text {hit.TextArea} "
                            + $"conf {hit.Confidence:0.00}");
                    }
                }

                if (box == null) {
                    // fallback — střed obrazovky
                    box = new Rectangle(
                        (int)(screen.Width * 0.34), (int)(screen.Height * 0.12),
                        (int)(screen.Width * 0.32), (int)(screen.Height * 0.80));
                    Logger.Info("Neither parchment nor conversation detected, using center fallback.");
                }

                // Oříznout podle typu detekce (v6: konverzace používá
                // změřenou TextArea; frakční TextCrop je jen fallback)
                Rectangle? bookCrop = (parchmentDetected && !isConversation)
                    ? GetCalibratedBookCrop(box.Value) : null;
                Rectangle inner = isConversation
                    ? (convText ?? ConversationDetector.TextCrop(box.Value))
                    : (bookCrop ?? ParchmentDetector.InnerCrop(box.Value));
                inner = Rectangle.Intersect(inner,
                    new Rectangle(0, 0, screen.Width, screen.Height));

                using (Bitmap crop = screen.Clone(inner, screen.PixelFormat)) {
                    // Konverzace = světlý text na tmavém → invertovat pro OCR
                    string raw = await OcrService.RecognizeAsync(
                        crop, _ocrLanguage.Value, invert: isConversation);
                    text = TextCleaner.CleanForEncyclopedia(raw);
                }

                // Nadpis: u pergamenu čteme header nad knížkou,
                // u konverzace se pokusíme přečíst NPC jméno (vpravo od dialogu)
                title = isConversation
                    ? TryReadNpcName(screen, box.Value)
                    : TryReadHeader(screen, box.Value);
            }

            if (text.Length < 20) {
                ScreenNotification.ShowNotification(
                    "Lorebook Reader: no readable text found. Is a book open?");
                return (false, null, null);
            }
            Logger.Info($"OCR ok ({text.Length} chars)"
                + (title != null ? $", title \"{title}\"" : "") + ".");
            return (true, title, text);
        }

        private async Task ReadPipelineAsync() {
            try {
                var (ok, title, text) = await CaptureBookAsync();
                if (!ok) return;
                _catalog?.AddCaptured(title, text);   // uložit originál
                await SpeakTextAsync(text);            // a přečíst
            } catch (Exception ex) {
                Logger.Warn(ex, "Lorebook read failed.");
                ScreenNotification.ShowNotification("Lorebook Reader: " + ex.Message);
            } finally {
                _readBusy = false;
            }
        }

        private async Task SaveOnlyPipelineAsync() {
            try {
                var (ok, title, text) = await CaptureBookAsync();
                if (!ok) return;
                var entry = _catalog?.AddCaptured(title, text);
                ScreenNotification.ShowNotification(
                    "Saved to encyclopedia: " +
                    (entry?.DisplayTitle ?? "lorebook"));
            } catch (Exception ex) {
                Logger.Warn(ex, "Lorebook save failed.");
                ScreenNotification.ShowNotification("Lorebook Reader: " + ex.Message);
            } finally {
                _readBusy = false;
            }
        }

        private async Task AppendPipelineAsync() {
            try {
                var (ok, _, text) = await CaptureBookAsync();
                if (!ok) return;
                var entry = _catalog?.AppendToLatest(text);
                if (entry == null) {
                    ScreenNotification.ShowNotification(
                        "Nothing to append to yet — save a book first.");
                } else {
                    ScreenNotification.ShowNotification(
                        "Appended page to: " + entry.DisplayTitle);
                }
            } catch (Exception ex) {
                Logger.Warn(ex, "Lorebook append failed.");
                ScreenNotification.ShowNotification("Lorebook Reader: " + ex.Message);
            } finally {
                _readBusy = false;
            }
        }

        /// <summary>Přeloží (pokud zapnuto) a přečte text. Sdílené čtení
        /// pro živý lorebook i přehrání z historie.</summary>
        private async Task SpeakTextAsync(string text) {
            int session = System.Threading.Interlocked.Increment(ref _speakSession);
            string mode   = _translateMode.Value;
            string target = _translateTarget.Value;
            string speechLang = _ocrLanguage.Value;
            string edgeVoice  = _edgeVoice.Value;

            // full: přeložit celý text -> řeč i titulky v cílovém jazyce
            if (mode == "full") {
                try {
                    string translated = await TranslationService.TranslateAsync(
                        text, target);
                    if (!string.IsNullOrWhiteSpace(translated)) {
                        text = translated;
                        speechLang = target;
                        edgeVoice = EdgeTtsService.VoiceForLanguage(target)
                                    ?? edgeVoice;
                    }
                } catch (Exception tEx) {
                    Logger.Warn(tEx, "Translation failed, using original text.");
                    ScreenNotification.ShowNotification(
                        "Lorebook Reader: translation unavailable — using original.");
                }
            }

            // subtitles only: řeč zůstává originál, titulky se překládají
            // průběžně po dávkách v OnTtsChunk
            _chunkTranslate = (mode == "subtitles");
            _chunkTranslateTarget = target;

            if (_voiceEngine.Value == "edge") {
                try {
                    await _edgeTts.SpeakAsync(
                        text, edgeVoice, _speakingRate.Value, OnTtsChunk, OnTtsWord);
                    return;
                } catch (Exception edgeEx) {
                    Logger.Warn(edgeEx,
                        "Edge TTS failed, falling back to offline voice.");
                    ScreenNotification.ShowNotification(
                        "Lorebook Reader: online voice unavailable — using offline voice.");
                }
            }

            string warning = await _tts.SpeakAsync(
                text, _voiceName.Value, _speakingRate.Value,
                speechLang, OnTtsChunk, OnTtsWord);
            if (warning != null)
                ScreenNotification.ShowNotification("Lorebook Reader: " + warning);
        }

        // --- ikony datadisků pro encyklopedii (ref/xp_*.png) ---
        // cache; chybějící soubor = null → UI ikonu prostě nevykreslí
        private readonly System.Collections.Generic.Dictionary<string, Texture2D>
            _xpIconCache = new System.Collections.Generic.Dictionary<string, Texture2D>(
                StringComparer.OrdinalIgnoreCase);

        private static string ExpansionIconFile(string expansion) {
            if (string.IsNullOrWhiteSpace(expansion)) return null;
            switch (expansion.Trim().ToLowerInvariant()) {
                case "core":                   return "xp_core.png";
                case "heart of thorns":        return "xp_hot.png";
                case "path of fire":           return "xp_pof.png";
                case "icebrood saga":          return "xp_ibs.png";
                case "end of dragons":         return "xp_eod.png";
                case "secrets of the obscure": return "xp_soto.png";
                case "janthir wilds":          return "xp_jw.png";
                case "visions of eternity":    return "xp_voe.png";
                default: return null;
            }
        }

        private Texture2D LoadRefTexture(string file) {
            if (file == null) return null;
            if (_xpIconCache.TryGetValue(file, out var cached)) return cached;
            Texture2D tex = null;
            try {
                using (var s = ModuleParameters.ContentsManager.GetFileStream(file))
                    if (s != null)
                        tex = Blish_HUD.TextureUtil.FromStreamPremultiplied(s);
            } catch { tex = null; }
            _xpIconCache[file] = tex;
            return tex;
        }

        /// <summary>Malá ikona datadisku (rail, seznam, náhled).</summary>
        internal Texture2D GetExpansionIcon(string expansion) =>
            LoadRefTexture(ExpansionIconFile(expansion));

        /// <summary>Velké logo pro razítko na obálce: preferuje
        /// ref/xp_*_big.png (~256 px), jinak padne na malou ikonu.</summary>
        internal Texture2D GetExpansionStampIcon(string expansion) {
            string file = ExpansionIconFile(expansion);
            if (file == null) return null;
            return LoadRefTexture(file.Replace(".png", "_big.png"))
                   ?? LoadRefTexture(file);
        }

        /// <summary>OCR pruhu nad pergamenem (název knihy). Chyba => null.</summary>
        private string TryReadHeader(Bitmap screen, Rectangle parchment) {
            try {
                int hh = (int)(parchment.Height * 0.14);
                int hx = Math.Max(0, parchment.X - 10);
                int hy = Math.Max(0, parchment.Y - hh - 6);
                int hw = Math.Min(parchment.Width + 20, screen.Width - hx);
                int hAvail = parchment.Y - 2 - hy;
                if (hAvail < 10 || hw < 20) return null;

                var headerRect = new Rectangle(hx, hy, hw, hAvail);
                using (Bitmap headerCrop =
                           screen.Clone(headerRect, screen.PixelFormat)) {
                    string raw = OcrService.RecognizeLineAsync(
                        headerCrop, _ocrLanguage.Value).GetAwaiter().GetResult();
                    raw = (raw ?? "").Trim();
                    // rozumná délka názvu; jinak fallback později
                    if (raw.Length < 2 || raw.Length > 60) return null;
                    return raw;
                }
            } catch (Exception ex) {
                Logger.Debug(ex, "Header OCR failed.");
                return null;
            }
        }

        /// <summary>Pokusí se přečíst jméno NPC z tmavého labelu vpravo
        /// od konverzačního panelu (invertovaný OCR).</summary>
        private string TryReadNpcName(Bitmap screen, Rectangle convBox) {
            try {
                // Label sedí v pravé části union-panelu, pod headerem
                // (geometrie pro panel = union(header, text) z v6.1)
                int labelW = (int)(convBox.Width * 0.38);
                int labelH = (int)(convBox.Height * 0.45);
                int lx = convBox.Right - labelW;
                int ly = convBox.Y + (int)(convBox.Height * 0.15);
                // ověřit, že nevypadáváme z obrazovky
                lx = Math.Max(0, Math.Min(lx, screen.Width - labelW));
                ly = Math.Max(0, Math.Min(ly, screen.Height - labelH));
                labelW = Math.Min(labelW, screen.Width - lx);
                labelH = Math.Min(labelH, screen.Height - ly);
                if (labelW < 20 || labelH < 10) return null;

                var labelRect = new Rectangle(lx, ly, labelW, labelH);
                using (Bitmap labelCrop =
                           screen.Clone(labelRect, screen.PixelFormat)) {
                    // NPC jméno je bílý text na tmavém pozadí → invertovat
                    string raw = OcrService.RecognizeLineAsync(
                        labelCrop, _ocrLanguage.Value, invert: true)
                        .GetAwaiter().GetResult();
                    raw = (raw ?? "").Trim();
                    // NPC jméno bývá 2-40 znaků
                    if (raw.Length < 2 || raw.Length > 40) return null;
                    return raw;
                }
            } catch (Exception ex) {
                Logger.Debug(ex, "NPC name OCR failed.");
                return null;
            }
        }

        // přehrání uloženého lorebooku z historie (volá settings view)
        internal void PlayFromCatalog(LorebookEntry entry) {
            if (entry == null || _readBusy) return;
            _readBusy = true;
            Task.Run(async () => {
                try { await SpeakTextAsync(entry.Text); }
                catch (Exception ex) { Logger.Warn(ex, "History playback failed."); }
                finally { _readBusy = false; }
            });
        }

        internal LorebookCatalog Catalog => _catalog;

        private void ShowEncyclopedia() {
            _encyclopediaView = new EncyclopediaView(this, _parchmentTexture);
            _historyWindow.Show(_encyclopediaView);
        }


        internal void StopSpeaking() {
            _tts?.Stop();
            _edgeTts?.Stop();
        }

        internal void ExportCatalogDialog() {
            try {
                string dir = ModuleParameters.DirectoriesManager
                    .GetFullDirectoryPath("lorebook_reader");
                string path = System.IO.Path.Combine(dir,
                    "lorebook_export_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                    + ".json");
                _catalog.ExportToFile(path);
                ScreenNotification.ShowNotification(
                    "Exported to lorebook_reader\\" +
                    System.IO.Path.GetFileName(path));
                Logger.Info("Catalog exported to " + path);
            } catch (Exception ex) {
                Logger.Warn(ex, "Export failed.");
                ScreenNotification.ShowNotification("Export failed: " + ex.Message);
            }
        }

        internal void ImportCatalogDialog() {
            try {
                string dir = ModuleParameters.DirectoriesManager
                    .GetFullDirectoryPath("lorebook_reader");
                // importuje nejnovější *.json soubor začínající lorebook_export
                var files = System.IO.Directory.GetFiles(dir, "lorebook_export*.json");
                if (files.Length == 0) {
                    ScreenNotification.ShowNotification(
                        "No export file found in lorebook_reader folder.");
                    return;
                }
                string latest = files.OrderByDescending(f =>
                    System.IO.File.GetLastWriteTimeUtc(f)).First();
                int count = _catalog.ImportFromFile(latest, merge: true);
                ScreenNotification.ShowNotification(
                    $"Imported {count} lorebooks from " +
                    System.IO.Path.GetFileName(latest));
                if (_historyWindow.Visible) ShowEncyclopedia();
            } catch (Exception ex) {
                Logger.Warn(ex, "Import failed.");
                ScreenNotification.ShowNotification("Import failed: " + ex.Message);
            }
        }


        protected override void Update(GameTime gameTime) {
            // 1x za sekundu zkusit najít knihu kvůli ikonce reproduktoru
            _detectTimerMs += gameTime.ElapsedGameTime.TotalMilliseconds;
            if (_detectTimerMs >= 1000) {
                _detectTimerMs = 0;
                if (_showSpeakerButton.Value && !_detectBusy && !_readBusy) {
                    _detectBusy = true;
                    Task.Run(DetectForButton);
                } else if (!_showSpeakerButton.Value) {
                    _bookVisible = false;
                }
            }

            // katalog: obnovit otevřené okno encyklopedie po novém záznamu
            if (_catalogDirty && _historyWindow != null && _historyWindow.Visible) {
                _catalogDirty = false;
                _encyclopediaView?.RefreshFromCatalog();
            }

            // titulky: aplikace textu/pozice (jen v Update threadu)
            if (_subtitleLabel != null) {
                if (_subtitleFontSize.Value != _lastFontSize) {
                    _lastFontSize = _subtitleFontSize.Value;
                    _subtitleLabel.FontSize = _lastFontSize;
                    // Netflix ~42 znaků/řádek: strop šířky boxu podle
                    // skutečné šířky 42 průměrných znaků ('n') ve fontu.
                    // Přepočet jen při změně velikosti fontu.
                    _subWidthCap = (int)_textRenderer.MeasureWidth(
                        new string('n', 42), _lastFontSize) + 16;
                }
                if (_subtitleLabel.EditMode) {
                    _subtitleLabel.Opacity = _subtitleOpacity.Value;
                    // text, viditelnost a pozici v edit módu řídí overlay
                } else {
                // titulkové cue: TTS chunk → krátká cue (≤2 řádky), posun v čase
                int cueBoxW = Math.Max(100, Math.Min(
                    (int)(GameService.Graphics.SpriteScreen.Size.X * 0.45f),
                    _subWidthCap > 0 ? _subWidthCap : int.MaxValue));
                if (_cueSourceDirty) {
                    _cueSourceDirty = false;
                    _cues = string.IsNullOrEmpty(_cueSource)
                        ? null
                        : BuildSubtitleCues(_cueSource, (int)(cueBoxW * 0.9f),
                                            _lastFontSize);
                    _cueWordStart = BuildCueWordStarts(_cues);
                    _cueIndex = 0;
                    _cueElapsedMs = 0;
                    _haveWordEvents = false;
                    _currentWordIndex = 0;
                    _pendingSubtitle = (_cues != null && _cues.Count > 0)
                        ? TextCleaner.SanitizeForDisplay(_cues[0]) : null;
                    _subtitleDirty = true;
                } else if (_cues != null && _cues.Count > 0) {
                    if (_wordSyncValid && _haveWordEvents && _cueWordStart != null) {
                        // přesný sync: právě čtené slovo → jeho cue
                        int wi = _currentWordIndex;
                        int target = _cueIndex;
                        while (target + 1 < _cues.Count
                               && wi >= _cueWordStart[target + 1]) target++;
                        if (target > _cueIndex) {
                            _cueIndex = target;
                            _pendingSubtitle =
                                TextCleaner.SanitizeForDisplay(_cues[_cueIndex]);
                            _subtitleDirty = true;
                        }
                    } else if (_cueIndex < _cues.Count) {
                        // fallback: odhad rychlosti čtení (bez word eventů / překlad)
                        _cueElapsedMs += gameTime.ElapsedGameTime.TotalMilliseconds;
                        double cps = 17.0 * Math.Max(0.5, _speakingRate.Value);
                        double dur = Math.Max(700.0, Math.Min(7000.0,
                            _cues[_cueIndex].Length / cps * 1000.0));
                        if (_cueElapsedMs >= dur && _cueIndex + 1 < _cues.Count) {
                            _cueIndex++;
                            _cueElapsedMs = 0;
                            _pendingSubtitle =
                                TextCleaner.SanitizeForDisplay(_cues[_cueIndex]);
                            _subtitleDirty = true;
                        }
                    }
                }
                if (_subtitleDirty) {
                    _subtitleDirty = false;
                    _subtitleLabel.SubtitleText = _pendingSubtitle ?? "";
                }
                bool show = _showSubtitles.Value
                            && !string.IsNullOrEmpty(_subtitleLabel.SubtitleText);
                if (show) {
                    var sprite = GameService.Graphics.SpriteScreen.Size;
                    int width = Math.Max(100, Math.Min(
                        (int)(sprite.X * 0.45f),
                        _subWidthCap > 0 ? _subWidthCap : int.MaxValue));
                    if (width != _lastSubWidth) {
                        _subtitleLabel.BoxWidth = width;
                        _lastSubWidth = width;
                    }
                    _subtitleLabel.Opacity = _subtitleOpacity.Value;
                    int sx = (int)(sprite.X * (_subtitleX.Value / 100f)) - width / 2;
                    int sy = (int)(sprite.Y * (_subtitleY.Value / 100f));
                    _subtitleLabel.Location = new Point(
                        Math.Max(0, Math.Min(sx, sprite.X - width)),
                        Math.Max(0, Math.Min(sy,
                            sprite.Y - Math.Max(1, _subtitleLabel.Size.Y))));
                }
                _subtitleLabel.Visible = show;
                }
            }

            // aplikace výsledku detekce na UI (jen v Update threadu)
            if (_speakerButton != null) {
                bool showButtons = false;
                Rectangle activeBox = default;

                if (_bookVisible && _gw2ClientRect.Width > 0) {
                    activeBox = _bookBox;
                    showButtons = true;
                } else if (_convVisible && _gw2ClientRect.Width > 0) {
                    activeBox = _convBox;
                    showButtons = true;
                }

                if (showButtons) {
                    var sprite = GameService.Graphics.SpriteScreen.Size;
                    float scaleX = (float)sprite.X / _gw2ClientRect.Width;
                    float scaleY = (float)sprite.Y / _gw2ClientRect.Height;
                    // Konverzace: tlačítka za pravý okraj union-panelu
                    // (malá frakce + konstanta — velká frakce u širokých
                    // dialogů odstřelovala tlačítka daleko od textu)
                    int extraOffset = _convVisible
                        ? (int)(activeBox.Width * scaleX * 0.06f) + 12
                        : 6;
                    int x = (int)(activeBox.Right * scaleX) + extraOffset;
                    int y = (int)(activeBox.Top * scaleY);
                    int bx = Math.Min(x, sprite.X - _speakerButton.Width);
                    // tři ikony pod sebou: číst / uložit / připojit
                    _speakerButton.Location = new Point(bx, Math.Max(0, y));
                    _saveButton.Location    = new Point(bx, Math.Max(0, y + 44));
                    _appendButton.Location  = new Point(bx, Math.Max(0, y + 88));
                    _speakerButton.Visible = true;
                    _saveButton.Visible    = true;
                    _appendButton.Visible  = true;
                } else {
                    _speakerButton.Visible = false;
                    _saveButton.Visible    = false;
                    _appendButton.Visible  = false;
                }
            }
        }

        private void DetectForButton() {
            try {
                IntPtr hwnd = GameService.GameIntegration.Gw2Instance.Gw2WindowHandle;
                using (Bitmap screen = ScreenCapture.Grab(hwnd, out Rectangle screenRect)) {
                    _gw2ClientRect = new Rectangle(0, 0, screen.Width, screen.Height);

                    // 1) Vždy zkusit pergamen (priorita — lorebooky)
                    Rectangle? box = ParchmentDetector.Find(screen, out _);
                    if (box != null) {
                        _bookBox = box.Value;
                        _bookVisible = true;
                        _convVisible = false;
                        return;
                    }
                    _bookVisible = false;

                    // 2) Pokud conversation mode je ON a pergamen nenalezen,
                    //    zkusit konverzační dialog
                    if (_conversationCapture.Value) {
                        var zone = GetCalibratedZone(screen.Width, screen.Height);
                        if (zone != null) {
                            // text-like výplň zóny → dialog otevřený; prázdná
                            // (tma) i plná (obloha) zóna se odmítne přes conf
                            var zHit = ConversationDetector.MeasureInZone(
                                screen, zone.Value);
                            if (zHit != null && zHit.Confidence >= 0.03
                                             && zHit.Confidence <= 0.6) {
                                _convBox = zHit.Panel;   // = zóna → stabilní kotva
                                _convVisible = true;
                                return;
                            }
                            _convVisible = false;
                            return;
                        }
                        Rectangle? conv = ConversationDetector.Find(screen, out _);
                        if (conv != null) {
                            _convBox = conv.Value;
                            _convVisible = true;
                            return;
                        }
                    }
                    _convVisible = false;
                }
            } catch (Exception ex) {
                Logger.Debug(ex, "Button detection failed.");
                _bookVisible = false;
                _convVisible = false;
            } finally {
                _detectBusy = false;
            }
        }

        protected override void Unload() {
            _readKeybind.Value.Activated -= OnReadActivated;
            _stopKeybind.Value.Activated -= OnStopActivated;
            _convToggleKeybind.Value.Activated -= OnConvToggleActivated;
            _debugDumpKeybind.Value.Activated -= OnDebugDumpActivated;
            _calibrateKeybind.Value.Activated -= OnCalibrateActivated;
            _bookCalibrateKeybind.Value.Activated -= OnBookCalibrateActivated;
            _calibrator?.Dispose();
            _speakerButton?.Dispose();
            _saveButton?.Dispose();
            _appendButton?.Dispose();
            _subtitleLabel?.Dispose();
            _cornerIcon?.Dispose();
            _textRenderer?.Dispose();
            _historyWindow?.Dispose();
            _tts?.Dispose();
            _edgeTts?.Dispose();
            foreach (var t in _xpIconCache.Values) t?.Dispose();
            _xpIconCache.Clear();
        }
    }
}
