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
        private SettingEntry<int>        _historyCapacity;
        private SettingEntry<bool>       _conversationCapture; // conversation OCR toggle
        private SettingEntry<KeyBinding> _convToggleKeybind;   // keybind pro toggle
        private SettingEntry<KeyBinding> _debugDumpKeybind;    // P1.1: debug capture

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
        internal SettingEntry<int>        HistoryCapacitySetting   => _historyCapacity;
        internal SettingEntry<bool>       ConversationCaptureSetting => _conversationCapture;
        internal SettingEntry<KeyBinding> ConvToggleKeybindSetting   => _convToggleKeybind;
        internal SettingEntry<KeyBinding> DebugDumpKeybindSetting    => _debugDumpKeybind;

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
        private Blish_HUD.Controls.Image _speakerButton;
        private Blish_HUD.Controls.Image _saveButton;
        private Blish_HUD.Controls.Image _appendButton;
        private SubtitleOverlay _subtitleLabel;
        private volatile bool _subtitleDirty;
        private string _pendingSubtitle;
        private int _lastSubWidth = -1;
        private int _lastFontSize = -1;
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

            _historyCapacity = settings.DefineSetting(
                "HistoryCapacity", 10,
                () => "History size",
                () => "How many recently read lorebooks to keep.");

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
        }

        protected override async Task LoadAsync() {
            _tts = new TtsService();
            _edgeTts = new EdgeTtsService();
            _textRenderer = new TextRenderer(GameService.Graphics.GraphicsDeviceManager.GraphicsDevice);

            string dir = ModuleParameters.DirectoriesManager
                .GetFullDirectoryPath("lorebook_reader");
            _catalog = new LorebookCatalog(dir, _historyCapacity.Value);
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

        private Blish_HUD.Controls.Image MakeBookButton(
                string iconKey, string tooltip, Action onClick) {
            var btn = new Blish_HUD.Controls.Image(
                ModuleParameters.ContentsManager.GetTexture(iconKey + ".png")) {
                Parent  = GameService.Graphics.SpriteScreen,
                Size    = new Point(36, 36),
                Visible = false,
                BasicTooltipText = tooltip
            };
            btn.MouseEntered += (s, a) =>
                btn.Texture = ModuleParameters.ContentsManager
                    .GetTexture(iconKey + "_hover.png");
            btn.MouseLeft += (s, a) =>
                btn.Texture = ModuleParameters.ContentsManager
                    .GetTexture(iconKey + ".png");
            btn.Click += (s, a) => onClick();
            return btn;
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
                          + $" solidity {convHit.Solidity:0.000}"
                        : "ConversationDetector: no hit");

                    // stejná priorita jako CaptureBookAsync: pergamen první
                    bool isConversation = parch == null && convHit != null;
                    Rectangle? box = parch ?? convHit?.Panel;

                    if (box != null) {
                        Rectangle inner = isConversation
                            ? convHit.TextArea
                            : ParchmentDetector.InnerCrop(box.Value);
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
                                TextCleaner.CleanForTts(raw ?? ""));
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
            if (!_conversationCapture.Value) _convVisible = false;
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
                        _pendingSubtitle =
                            TextCleaner.SanitizeForDisplay(shown);
                        _subtitleDirty = true;
                    }
                });
                return;
            }
            _pendingSubtitle = chunk == null
                ? null : TextCleaner.SanitizeForDisplay(chunk);
            _subtitleDirty = true;
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
                bool isConversation = false;
                Rectangle? convText = null; // v6: změřená textová oblast

                if (box != null) {
                    Logger.Info($"Parchment {box} solidity {solidity:0.00}");
                } else if (_conversationCapture.Value) {
                    // 2) Zkusit konverzační dialog
                    var hit = ConversationDetector.FindHit(screen);
                    if (hit != null) {
                        box = hit.Panel;
                        convText = hit.TextArea;
                        solidity = hit.Solidity;
                        isConversation = true;
                        Logger.Info($"Conversation panel {hit.Panel} "
                            + $"text {hit.TextArea} solidity {hit.Solidity:0.00}");
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
                Rectangle inner = isConversation
                    ? (convText ?? ConversationDetector.TextCrop(box.Value))
                    : ParchmentDetector.InnerCrop(box.Value);

                using (Bitmap crop = screen.Clone(inner, screen.PixelFormat)) {
                    // Konverzace = světlý text na tmavém → invertovat pro OCR
                    string raw = await OcrService.RecognizeAsync(
                        crop, _ocrLanguage.Value, invert: isConversation);
                    text = TextCleaner.CleanForTts(raw);
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
                        text, edgeVoice, _speakingRate.Value, OnTtsChunk);
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
                speechLang, OnTtsChunk);
            if (warning != null)
                ScreenNotification.ShowNotification("Lorebook Reader: " + warning);
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
                // NPC label je v pravé části panelu, vertikálně uprostřed
                int labelW = (int)(convBox.Width * 0.30);
                int labelH = (int)(convBox.Height * 0.15);
                int lx = convBox.Right - labelW;
                int ly = convBox.Y + (int)(convBox.Height * 0.35);
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
                        labelCrop, _ocrLanguage.Value).GetAwaiter().GetResult();
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
                if (_encyclopediaView != null) {
                    _encyclopediaView.RebuildExpansionFilter();
                    _encyclopediaView.RefreshList();
                }
            }

            // titulky: aplikace textu/pozice (jen v Update threadu)
            if (_subtitleLabel != null) {
                if (_subtitleFontSize.Value != _lastFontSize) {
                    _lastFontSize = _subtitleFontSize.Value;
                    _subtitleLabel.FontSize = _lastFontSize;
                }
                if (_subtitleLabel.EditMode) {
                    _subtitleLabel.Opacity = _subtitleOpacity.Value;
                    // text, viditelnost a pozici v edit módu řídí overlay
                } else {
                if (_subtitleDirty) {
                    _subtitleDirty = false;
                    _subtitleLabel.SubtitleText = _pendingSubtitle ?? "";
                }
                bool show = _showSubtitles.Value
                            && !string.IsNullOrEmpty(_subtitleLabel.SubtitleText);
                if (show) {
                    var sprite = GameService.Graphics.SpriteScreen.Size;
                    int width = Math.Max(100, (int)(sprite.X * 0.45f));
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
                    // Konverzace: tlačítka dál doprava (za NPC jméno/portrét)
                    int extraOffset = _convVisible
                        ? (int)(activeBox.Width * scaleX * 0.18)
                        : 6;
                    int x = (int)(activeBox.Right * scaleX) + extraOffset;
                    int y = (int)(activeBox.Top * scaleY);
                    int bx = Math.Min(x, sprite.X - _speakerButton.Width);
                    // tři ikony pod sebou: číst / uložit / připojit
                    _speakerButton.Location = new Point(bx, Math.Max(0, y));
                    _saveButton.Location    = new Point(bx, Math.Max(0, y + 40));
                    _appendButton.Location  = new Point(bx, Math.Max(0, y + 80));
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
            _speakerButton?.Dispose();
            _saveButton?.Dispose();
            _appendButton?.Dispose();
            _subtitleLabel?.Dispose();
            _cornerIcon?.Dispose();
            _textRenderer?.Dispose();
            _historyWindow?.Dispose();
            _tts?.Dispose();
            _edgeTts?.Dispose();
        }
    }
}
