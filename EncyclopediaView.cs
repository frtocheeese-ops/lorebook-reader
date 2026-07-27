using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Encyklopedie v master-detail rozložení, které se přizpůsobuje
    /// velikosti okna (okno je resizovatelné). Vlevo seznam + filtry,
    /// vpravo náhled na pergamenu s přehráním, editací textu i metadat,
    /// překladem a nastavitelnou velikostí písma.
    /// </summary>
    public class EncyclopediaView : View {

        private readonly LorebookReaderModule _module;
        private readonly LorebookCatalog _catalog;
        private readonly TextRenderer _textRenderer;
        private readonly Texture2D _parchment;

        private Container _root;

        // levý sloupec
        private TextBox _searchBox;
        private Dropdown _sortDropdown;
        private Dropdown _colorFilter;
        private Panel _railPanel;      // rail datadisků (ikony + počty)
        private string _filterXp;      // null = vše, "" = bez datadisku, jinak název
        private FlowPanel _listPanel;
        private StandardButton _exportBtn, _importBtn;

        // pravý sloupec
        private Panel _detailPanel;
        private LorebookEntry _selected;
        private float _textFontSize = 18f;
        private bool _readerFullscreen; // kniha přes celé okno (bez railu/seznamu)

        // debounce ukládání editoru: text se do disku nezapisuje při každém
        // stisku klávesy (to zamrzávalo Blish), ale ~1,2 s po poslední změně
        // a při odchodu z editoru. entry.Text se mění in-place, takže flush
        // jen persistuje aktuální stav katalogu.
        private System.Threading.Timer _editFlushTimer;
        private volatile bool _editDirty;
        private enum DetailMode { Empty, Preview, Edit }
        private DetailMode _mode = DetailMode.Empty;

        private static readonly string[] SortItems = {
            "Newest first", "Oldest first", "Title A–Z", "Title Z–A", "Color"
        };

        public EncyclopediaView(LorebookReaderModule module, Texture2D parchment) {
            _module = module;
            _catalog = module.Catalog;
            _textRenderer = module.SharedTextRenderer;
            _parchment = parchment;
        }

        protected override void Build(Container buildPanel) {
            _root = buildPanel;
            BuildLayout();
            // přebuduj layout při změně velikosti okna.
            // Resized se na okně volá spolehlivě (ContentResized ne vždy,
            // protože WindowBase2 nastavuje ContentRegion ručně).
            buildPanel.Resized += (s, e) => BuildLayout();
        }

        private void BuildLayout() {
            FlushEdits(); // rebuild (např. resize okna) → dozapiš rozeditované
            _root.ClearChildren();
            // ContentRegion = vnitřní plocha okna bez titulku a okrajů.
            // Použití .Width/.Height by zahrnulo rámeček a obsah by přetékal.
            int totalW = _root.ContentRegion.Width;
            int totalH = _root.ContentRegion.Height;

            // fullscreen čtení: jen kniha přes celé okno + A± (výstup
            // stejnou rohovou značkou na knize)
            if (_readerFullscreen && _selected != null) {
                BuildFullscreenReader(totalW, totalH);
                return;
            }
            _readerFullscreen = false;

            // editace přes celé okno (víc místa než úzký detail panel);
            // ukončí se tlačítkem Done → návrat na normální layout + náhled
            if (_mode == DetailMode.Edit && _selected != null) {
                ShowEditor(_selected, totalW, totalH);
                return;
            }
            bool railMini = _module.EncyclopediaRailCollapsedSetting.Value;
            int railW = railMini ? 54 : 236; // expanded: vejde se i „Secrets of the Obscure"
            int listX = 8 + railW + 8;
            int leftW = Math.Max(250, (int)((totalW - railW) * 0.40f));

            // ============ RAIL DATADISKŮ (sbalitelný, viz redesign doc) ============
            _railPanel = new Panel {
                Parent = _root, Location = new Point(8, 6),
                Width = railW, Height = totalH - 14, ShowBorder = true
            };
            FillRail();

            // ===================== LEVÝ PANEL =====================
            _searchBox = new TextBox {
                Parent = _root, Location = new Point(listX, 6),
                Width = leftW - 8,
                PlaceholderText = "Search title, text, metadata…"
            };
            _searchBox.TextChanged += (s, e) => RefreshList();

            _sortDropdown = new Dropdown {
                Parent = _root, Location = new Point(listX, 38), Width = leftW - 8
            };
            foreach (string item in SortItems) _sortDropdown.Items.Add(item);
            _sortDropdown.SelectedItem = SortItems[0];
            _sortDropdown.ValueChanged += (s, e) => RefreshList();

            _colorFilter = new Dropdown {
                Parent = _root, Location = new Point(listX, 70), Width = leftW - 8
            };
            _colorFilter.Items.Add("All");
            foreach (var c in Palette.Colors)
                if (c.Name != "None") _colorFilter.Items.Add(c.Name);
            _colorFilter.Items.Add("None");
            _colorFilter.SelectedItem = "All";
            _colorFilter.ValueChanged += (s, e) => RefreshList();

            _listPanel = new FlowPanel {
                Parent = _root, Location = new Point(listX, 102),
                Width = leftW - 8, Height = totalH - 150,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 3),
                CanScroll = true, ShowBorder = true
            };

            int halfW = (leftW - 16) / 2;
            _exportBtn = new StandardButton {
                Parent = _root, Location = new Point(listX, totalH - 42),
                Width = halfW, Text = "Export…"
            };
            _exportBtn.Click += (s, e) => _module.ExportCatalogDialog();
            _importBtn = new StandardButton {
                Parent = _root, Location = new Point(listX + halfW + 8, totalH - 42),
                Width = halfW, Text = "Import…"
            };
            _importBtn.Click += (s, e) => _module.ImportCatalogDialog();

            // ===================== PRAVÝ PANEL =====================
            int detailX = listX + leftW + 8;
            _detailPanel = new Panel {
                Parent = _root, Location = new Point(detailX, 6),
                Width = totalW - detailX - 8, Height = totalH - 14,
                ShowBorder = true
            };

            RefreshList();
            // znovu vykreslit detail podle stavu (edit mód řeší větev nahoře)
            if (_selected != null) ShowPreview(_selected);
            else                   ShowEmpty();
        }

        private static readonly (string Name, string Code)[] RailPresets = {
            ("Core", "GW2"), ("Heart of Thorns", "HoT"),
            ("Path of Fire", "PoF"), ("Icebrood Saga", "IBS"),
            ("End of Dragons", "EoD"), ("Secrets of the Obscure", "SotO"),
            ("Janthir Wilds", "JW"), ("Visions of Eternity", "VoE")
        };

        /// <summary>Naplní rail datadisků: toggle sbalení, „All books",
        /// předvolby s počty (ikona z ref/xp_*.png, jinak zkratka), vlastní
        /// hodnoty z katalogu a „No expansion". Sbalený = jen ikony.</summary>
        public void FillRail() {
            if (_railPanel == null) return;
            _railPanel.ClearChildren();
            bool mini = _module.EncyclopediaRailCollapsedSetting.Value;
            int rowW = _railPanel.Width - 8;
            int y = 4;

            var toggle = new StandardButton {
                Parent = _railPanel, Location = new Point(4, y),
                Width = rowW, Text = mini ? "»" : "« Minimize",
                BasicTooltipText = mini
                    ? "Expand expansion rail" : "Collapse to icons only"
            };
            toggle.Click += (s, e) => {
                _module.EncyclopediaRailCollapsedSetting.Value = !mini;
                BuildLayout();
            };
            y += 34;

            var counts = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
            int noXp = 0, total = 0;
            foreach (var e in _catalog.All) {
                total++;
                string xp = (e.Expansion ?? "").Trim();
                if (xp.Length == 0) noXp++;
                else counts[xp] = (counts.TryGetValue(xp, out int c) ? c : 0) + 1;
            }

            y = AddRailRow(y, rowW, mini, null, "All books", "ALL", total);
            foreach (var (name, code) in RailPresets) {
                counts.TryGetValue(name, out int c);
                counts.Remove(name);
                y = AddRailRow(y, rowW, mini, name, name, code, c);
            }
            // povídky mají vlastní řádek níž, mezi vlastní hodnoty nepatří
            counts.Remove(ShortStoryLibrary.Expansion);
            foreach (var kv in counts.OrderBy(k => k.Key))
                y = AddRailRow(y, rowW, mini, kv.Key, kv.Key,
                    kv.Key.Length <= 4 ? kv.Key
                        : kv.Key.Substring(0, 3) + "…", kv.Value);
            // oficiální povídky z wiki — vlastní sekce, vždy předposlední
            counts.TryGetValue(ShortStoryLibrary.Expansion, out int storyCount);
            counts.Remove(ShortStoryLibrary.Expansion);
            y = AddRailRow(y, rowW, mini, ShortStoryLibrary.Expansion,
                "Short Stories", "SS", storyCount);

            if (noXp > 0)
                AddRailRow(y, rowW, mini, "", "No expansion", "—", noXp);
        }

        private int AddRailRow(int y, int rowW, bool mini, string value,
                               string label, string code, int count) {
            bool active = _filterXp == value;
            var row = new Panel {
                Parent = _railPanel, Location = new Point(4, y),
                Width = rowW, Height = 38,
                BackgroundColor = active
                    ? new Color(60, 70, 90) : Color.Transparent,
                BasicTooltipText = $"{label} ({count})"
            };
            if (count == 0 && value != null) row.Opacity = 0.45f;

            var icon = value == null || value.Length == 0
                ? null : _module.GetExpansionIcon(value);
            int tx;
            if (icon != null) {
                new Blish_HUD.Controls.Image(icon) {
                    Parent = row,
                    Location = new Point(mini ? (rowW - 26) / 2 : 6, 6),
                    Size = new Point(26, 26)
                };
                tx = 38;
            } else {
                new Label {
                    Parent = row,
                    Location = new Point(mini ? 0 : 5, 10),
                    Width = mini ? rowW : 32, Height = 18,
                    Text = code,
                    HorizontalAlignment = mini
                        ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                    Font = GameService.Content.DefaultFont14
                };
                tx = 40;
            }
            if (!mini) {
                new Label {
                    Parent = row, Location = new Point(tx, 10),
                    Width = rowW - tx - 28, Height = 18, Text = label,
                    Font = GameService.Content.DefaultFont14
                };
                new Label {
                    Parent = row, Location = new Point(rowW - 28, 10),
                    Width = 26, Height = 18, Text = count.ToString(),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    TextColor = new Color(160, 160, 160),
                    Font = GameService.Content.DefaultFont14
                };
            }
            row.MouseEntered += (s, e) => {
                if (!active) row.BackgroundColor = new Color(50, 56, 68);
            };
            row.MouseLeft += (s, e) => {
                if (!active) row.BackgroundColor = Color.Transparent;
            };
            row.Click += (s, e) => {
                _filterXp = value;
                FillRail();
                RefreshList();
            };
            return y + 42;
        }

        private SortMode CurrentSort() {
            switch (_sortDropdown.SelectedItem) {
                case "Oldest first": return SortMode.OldestFirst;
                case "Title A–Z":    return SortMode.TitleAZ;
                case "Title Z–A":    return SortMode.TitleZA;
                case "Color":        return SortMode.ColorTag;
                default:             return SortMode.NewestFirst;
            }
        }

        public void RefreshList() {
            // v edit módu je layout přes celé okno — seznam neexistuje
            // (jeho reference je navíc už disposnutá). Změny se promítnou
            // při návratu, kdy BuildLayout seznam postaví znovu.
            if (_listPanel == null || _mode == DetailMode.Edit) return;
            _listPanel.ClearChildren();
            var results = _catalog.Query(
                _searchBox?.Text, CurrentSort(),
                _colorFilter?.SelectedItem, null);
            // filtr datadisku řídí rail (null = vše, "" = bez datadisku)
            if (_filterXp != null) {
                results = _filterXp.Length == 0
                    ? results.Where(e =>
                          string.IsNullOrWhiteSpace(e.Expansion)).ToList()
                    : results.Where(e => string.Equals(
                          e.Expansion?.Trim(), _filterXp,
                          StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // sekce povídek: pod staženými ukázat i tituly, které uživatel
            // ještě nemá — kliknutím se nabídne stažení
            if (_filterXp == ShortStoryLibrary.Expansion) {
                foreach (var entry in results) AddListRow(entry);
                foreach (var story in ShortStoryLibrary.All) {
                    bool have = results.Any(e => string.Equals(
                        e.Title, story.Title, StringComparison.OrdinalIgnoreCase));
                    if (!have) AddStoryRow(story);
                }
                return;
            }

            if (results.Count == 0) {
                new Label {
                    Parent = _listPanel,
                    Text = "No lorebooks match. Read a book in-game to add it.",
                    AutoSizeHeight = true, Width = _listPanel.Width - 20
                };
                return;
            }
            foreach (var entry in results) AddListRow(entry);
        }

        /// <summary>Obnoví seznam I otevřený náhled podle katalogu (po append
        /// další stránky, importu apod.). V editačním režimu detail nechá být,
        /// ať nepřepíše rozeditovaný text uživatele.</summary>
        public void RefreshFromCatalog() {
            if (_readerFullscreen) return; // rail/seznam teď neexistují
            FillRail();
            RefreshList();
            if (_mode == DetailMode.Edit || _selected == null) return;
            LorebookEntry fresh = null;
            foreach (var e in _catalog.All)
                if (e.Id == _selected.Id) { fresh = e; break; }
            if (fresh != null) { _selected = fresh; ShowPreview(fresh); }
            else { _selected = null; ShowEmpty(); }
        }

        private void AddListRow(LorebookEntry entry) {
            var row = new Panel {
                Parent = _listPanel, Width = _listPanel.Width - 20, Height = 46,
                ShowBorder = false,
                BackgroundColor = (_selected != null && _selected.Id == entry.Id)
                    ? new Color(60, 70, 90) : Color.Transparent
            };
            bool isSelected = _selected != null && _selected.Id == entry.Id;
            row.MouseEntered += (s, e) => {
                if (!isSelected) row.BackgroundColor = new Color(48, 54, 66);
            };
            row.MouseLeft += (s, e) => {
                if (!isSelected) row.BackgroundColor = Color.Transparent;
            };

            var (cr, cg, cb) = Palette.Resolve(entry.ColorTag);
            new Panel {
                Parent = row, Location = new Point(0, 0),
                Width = 5, Height = 46, BackgroundColor = new Color(cr, cg, cb)
            };
            if (!entry.Opened) {
                // „unread glow": zlatě pulzující hřbet, dokud knihu neotevřeš
                var glow = new Panel {
                    Parent = row, Location = new Point(0, 0),
                    Width = 5, Height = 46,
                    BackgroundColor = new Color(233, 201, 106)
                };
                var tween = GameService.Animation.Tweener
                    .Tween(glow, new { Opacity = 0.25f }, 0.9f)
                    .Repeat().Reflect();
                glow.Disposed += (s, e) => tween.Cancel();
            }
            int titleX = 12;
            var xpIcon = _module.GetExpansionIcon(entry.Expansion);
            if (xpIcon != null) {
                new Blish_HUD.Controls.Image(xpIcon) {
                    Parent = row, Location = new Point(10, 13),
                    Size = new Point(20, 20)
                };
                titleX = 34;
            }
            new Label {
                Parent = row, Location = new Point(titleX, 4),
                Width = row.Width - titleX - (entry.Opened ? 8 : 46), Height = 22,
                Text = entry.DisplayTitle, Font = GameService.Content.DefaultFont16
            };
            if (!entry.Opened) {
                new Label {
                    Parent = row, Location = new Point(row.Width - 42, 5),
                    Width = 36, Height = 16, Text = "NEW",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    TextColor = new Color(233, 201, 106),
                    Font = GameService.Content.DefaultFont12
                };
            }
            string meta = entry.MetadataLine;
            new Label {
                Parent = row, Location = new Point(titleX, 26),
                Width = row.Width - titleX - 8, Height = 16,
                Text = string.IsNullOrEmpty(meta)
                    ? entry.TimestampLocal.ToString("g") : meta,
                TextColor = new Color(160, 160, 160)
            };
            row.Click += (s, e) => {
                _selected = entry;
                _mode = DetailMode.Preview;
                RefreshList();
                ShowPreview(entry);
            };
        }

        // ===================== DETAIL: prázdný =====================
        /// <summary>Řádek dosud nestažené povídky v seznamu.</summary>
        private void AddStoryRow(ShortStory story) {
            var row = new Panel {
                Parent = _listPanel, Width = _listPanel.Width - 20, Height = 46,
                BackgroundColor = Color.Transparent
            };
            row.MouseEntered += (s, e) =>
                row.BackgroundColor = new Color(48, 54, 66);
            row.MouseLeft += (s, e) => row.BackgroundColor = Color.Transparent;

            new Label {
                Parent = row, Location = new Point(12, 4),
                Width = row.Width - 20, Height = 22, Text = story.Title,
                Font = GameService.Content.DefaultFont16,
                TextColor = new Color(190, 190, 190)
            };
            new Label {
                Parent = row, Location = new Point(12, 26),
                Width = row.Width - 20, Height = 16,
                Text = "Not downloaded · " + story.Timeline,
                TextColor = new Color(140, 140, 140),
                Font = GameService.Content.DefaultFont12
            };
            row.Click += (s, e) => ShowStoryOffer(story);
        }

        /// <summary>Nabídka stažení jedné povídky: proč to takhle je + tlačítko.
        /// Po stažení se rovnou otevře normální náhled knihy.</summary>
        private void ShowStoryOffer(ShortStory story) {
            _mode = DetailMode.Empty;
            _selected = null;
            _detailPanel.ClearChildren();
            int w = _detailPanel.Width;

            new Label {
                Parent = _detailPanel, Location = new Point(12, 10),
                Width = w - 24, Height = 28, Text = story.Title,
                Font = GameService.Content.DefaultFont18
            };
            new Label {
                Parent = _detailPanel, Location = new Point(12, 42),
                Width = w - 24, Height = 20,
                Text = story.Writer + " · " + story.Published
                     + " · " + story.Timeline,
                TextColor = new Color(170, 170, 170)
            };

            var getBtn = new StandardButton {
                Parent = _detailPanel, Location = new Point(12, 74),
                Width = 130, Text = "Download"
            };
            var status = new Label {
                Parent = _detailPanel, Location = new Point(150, 78),
                Width = w - 170, Height = 20, Text = "",
                TextColor = new Color(170, 200, 170)
            };
            var why = new Label {
                Parent = _detailPanel, Location = new Point(12, 112),
                Width = w - 24, Height = 130, WrapText = true,
                TextColor = new Color(160, 160, 160),
                Text = "Why isn't this already here?\n\n"
                     + "The short stories were written and published by "
                     + "ArenaNet, so the module does not ship copies of them. "
                     + "Instead it fetches this one from the Guild Wars 2 Wiki "
                     + "when you ask for it, and stores it in your own codex "
                     + "together with a link back to the source.\n\n"
                     + "You need an internet connection for this step only — "
                     + "once downloaded, the story is yours to read offline."
            };

            getBtn.Click += (s, e) => {
                getBtn.Enabled = false;
                status.Text = "Downloading…";
                System.Threading.Tasks.Task.Run(async () => {
                    try {
                        string text = await ShortStoryLibrary
                            .FetchAsync(story, _module.StoryImageDirectory)
                            .ConfigureAwait(false);
                        // katalog i UI výhradně z hlavního vlákna
                        _module.RunOnMainThread(() => {
                            var entry = _catalog.AddCaptured(story.Title, text);
                            if (entry != null) {
                                entry.Expansion = ShortStoryLibrary.Expansion;
                                entry.Theme     = story.Timeline;
                                entry.Location  = story.Writer;
                                entry.Notes     = "Source: " + story.Url;
                                entry.Opened    = false;
                                _catalog.Update(entry);
                                FillRail();
                                RefreshList();
                                _selected = entry;
                                ShowPreview(entry);   // rovnou normální kniha
                            }
                        });
                    } catch (Exception ex) {
                        Logger.GetLogger<EncyclopediaView>()
                              .Warn(ex, "Short story download failed.");
                        _module.RunOnMainThread(() => {
                            status.Text = "Download failed — check your "
                                        + "connection and try again.";
                            status.TextColor = new Color(210, 150, 150);
                            getBtn.Enabled = true;
                        });
                    }
                });
            };
        }

        private void ShowEmpty() {
            _mode = DetailMode.Empty;
            _detailPanel.ClearChildren();
            new Label {
                Parent = _detailPanel,
                Location = new Point(0, _detailPanel.Height / 2 - 20),
                Width = _detailPanel.Width, Height = 40,
                Text = "Select a lorebook from the list",
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = new Color(150, 150, 150)
            };
        }

        // ===================== DETAIL: náhled =====================
        private static Panel MakeConfirmButton(Container parent, Point loc,
                                               string text, Color bg) {
            var p = new Panel {
                Parent = parent, Location = loc, Width = 80, Height = 26,
                BackgroundColor = bg
            };
            new Label {
                Parent = p, Location = new Point(0, 3), Width = 80, Height = 20,
                Text = text, HorizontalAlignment = HorizontalAlignment.Center
            };
            return p;
        }

        private void ShowPreview(LorebookEntry entry) {
            FlushEdits(); // odchod z editoru → dozapiš text
            _mode = DetailMode.Preview;
            // NEW badge zhasíná prvním otevřením (Update vyvolá refresh,
            // fresh záznam už má Opened=true, takže se to nezacyklí)
            if (!entry.Opened) {
                entry.Opened = true;
                _catalog.Update(entry);
            }
            _detailPanel.ClearChildren();
            int w = _detailPanel.Width;
            int h = _detailPanel.Height;

            int titleX = 12;
            var pvIcon = _module.GetExpansionIcon(entry.Expansion);
            if (pvIcon != null) {
                new Blish_HUD.Controls.Image(pvIcon) {
                    Parent = _detailPanel, Location = new Point(12, 10),
                    Size = new Point(26, 26)
                };
                titleX = 46;
            }
            new Label {
                Parent = _detailPanel, Location = new Point(titleX, 10),
                Width = w - titleX - 12, Height = 28,
                Text = entry.DisplayTitle, Font = GameService.Content.DefaultFont18
            };

            var playBtn = new StandardButton {
                Parent = _detailPanel, Location = new Point(12, 44),
                Width = 80, Text = "▶ Play"
            };
            playBtn.Click += (s, e) => _module.PlayFromCatalog(entry);
            var pauseBtn = new StandardButton {
                Parent = _detailPanel, Location = new Point(96, 44),
                Width = 80,
                Text = _module.SpeechPaused ? "Resume" : "Pause"
            };
            pauseBtn.Click += (s, e) =>
                pauseBtn.Text = _module.TogglePauseSpeaking() ? "Resume" : "Pause";
            var stopBtn = new StandardButton {
                Parent = _detailPanel, Location = new Point(180, 44),
                Width = 72, Text = "■ Stop"
            };
            stopBtn.Click += (s, e) => _module.StopSpeaking();
            var editBtn = new StandardButton {
                Parent = _detailPanel, Location = new Point(256, 44),
                Width = 80, Text = "Edit"
            };
            editBtn.Click += (s, e) => {
                _selected = entry;
                _mode = DetailMode.Edit;
                BuildLayout();   // editace se postaví přes celé okno
            };
            var delBtn = new StandardButton {
                Parent = _detailPanel, Location = new Point(w - 92, 44),
                Width = 80, Text = "Delete"
            };
            delBtn.Click += (s, e) => {
                // potvrzení proti smazání omylem: místo tlačítka se ukáže
                // zelené Confirm a červené Cancel (výběr jiné knihy to zruší)
                delBtn.Visible = false;
                Panel yes = null, no = null;
                yes = MakeConfirmButton(_detailPanel, new Point(w - 176, 44),
                    "Confirm", new Color(52, 122, 60));
                no = MakeConfirmButton(_detailPanel, new Point(w - 92, 44),
                    "Cancel", new Color(142, 48, 42));
                yes.Click += (s2, e2) => {
                    _catalog.Remove(entry.Id);
                    _selected = null;
                    RefreshList();
                    ShowEmpty();
                };
                no.Click += (s2, e2) => {
                    yes.Dispose();
                    no.Dispose();
                    delBtn.Visible = true;
                };
            };

            // ovládání velikosti písma
            var fontMinus = new StandardButton {
                Parent = _detailPanel, Location = new Point(w - 92, 78),
                // pozor: obyčejný spojovník, ne „−" (U+2212) — herní font
                // ten znak nemá a tlačítko pak ukazuje jen „A"
                Width = 36, Text = "A-"
            };
            var fontPlus = new StandardButton {
                Parent = _detailPanel, Location = new Point(w - 52, 78),
                Width = 36, Text = "A+"
            };

            string meta = entry.MetadataLine;
            if (!string.IsNullOrEmpty(meta)) {
                new Label {
                    Parent = _detailPanel, Location = new Point(12, 82),
                    Width = w - 120, Height = 18,
                    Text = meta, TextColor = new Color(190, 190, 190)
                };
            }

            string body = BuildBody(entry);

            // knižní čtečka: obálka s titulem a razítkem datadisku,
            // jedna stránka, listování s animací (redesign fáze B)
            var reader = new BookReaderPanel(_textRenderer, _parchment,
                _module.GetRefTexture("arrow_left.png"),
                _module.GetRefTexture("arrow_right.png"),
                _module.GetRefTexture("ornament_corner.png"),
                _module.GetRefTexture("seal.png"),
                _module.GetRefTexture("expand.png"),
                _module.GetRefTexture("collapse.png")) {
                Parent = _detailPanel, Location = new Point(12, 106),
                Width = w - 24, Height = h - 118,
                FontSize = _textFontSize
            };
            reader.SetEntry(entry, body,
                _module.GetExpansionStampIcon(entry.Expansion));
            reader.FullscreenToggled += (s, e) => {
                _readerFullscreen = true;
                BuildLayout();
            };
            // jemný fade-in místo skokového překreslení
            reader.Opacity = 0f;
            GameService.Animation.Tweener.Tween(
                reader, new { Opacity = 1f }, 0.2f);

            fontMinus.Click += (s, e) => {
                _textFontSize = Math.Max(12f, _textFontSize - 2f);
                reader.FontSize = _textFontSize;
            };
            fontPlus.Click += (s, e) => {
                _textFontSize = Math.Min(40f, _textFontSize + 2f);
                reader.FontSize = _textFontSize;
            };
        }

        // ===================== DETAIL: editor =====================
        private void ShowEditor(LorebookEntry entry, int w, int h) {
            _mode = DetailMode.Edit;

            // editace vyplní celé okno; ukončí ji „Done" (návrat na náhled)
            var done = new StandardButton {
                Parent = _root, Location = new Point(12, 10),
                Width = 150, Text = "✓ Done editing"
            };
            done.Click += (s, e) => {
                FlushEdits();
                _mode = DetailMode.Preview;
                BuildLayout();
            };
            new Label {
                Parent = _root, Location = new Point(172, 14),
                Width = Math.Max(20, w - 184), Height = 24,
                Text = "Editing: " + entry.DisplayTitle,
                Font = GameService.Content.DefaultFont18,
                TextColor = new Color(210, 200, 170)
            };

            // levá část editoru: metadata (širší, je víc místa)
            int formW = Math.Min(340, w / 3);
            var form = new FlowPanel {
                Parent = _root, Location = new Point(12, 46),
                Width = formW, Height = h - 58,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 5), CanScroll = true
            };

            AddLabel(form, "Title");
            var titleBox = AddBox(form, entry.Title, formW - 4);
            titleBox.TextChanged += (s, e) => { entry.Title = titleBox.Text; Save(entry); };

            AddLabel(form, "Color tag");
            var colorDd = new Dropdown { Parent = form, Width = formW - 4 };
            foreach (var c in Palette.Colors) colorDd.Items.Add(c.Name);
            colorDd.SelectedItem = string.IsNullOrEmpty(entry.ColorTag)
                ? "None" : entry.ColorTag;
            colorDd.ValueChanged += (s, e) => { entry.ColorTag = colorDd.SelectedItem; Save(entry); };

            AddLabel(form, "Expansion");
            var expDd = new Dropdown { Parent = form, Width = formW - 4 };
            foreach (string xp in ExpansionPresets) expDd.Items.Add(xp);
            string curXp = string.IsNullOrWhiteSpace(entry.Expansion)
                ? ExpansionPresets[0] : entry.Expansion.Trim();
            if (!expDd.Items.Contains(curXp))
                expDd.Items.Add(curXp); // starší vlastní hodnota — neztratit
            expDd.SelectedItem = curXp;
            expDd.ValueChanged += (s, e) => {
                entry.Expansion = expDd.SelectedItem == ExpansionPresets[0]
                    ? "" : expDd.SelectedItem;
                Save(entry);
            };

            AddLabel(form, "Theme");
            var themeBox = AddBox(form, entry.Theme, formW - 4);
            themeBox.TextChanged += (s, e) => { entry.Theme = themeBox.Text; Save(entry); };

            AddLabel(form, "Location acquired");
            var locBox = AddBox(form, entry.Location, formW - 4);
            locBox.TextChanged += (s, e) => { entry.Location = locBox.Text; Save(entry); };

            AddLabel(form, "Notes");
            var notesBox = AddBox(form, entry.Notes, formW - 4);
            notesBox.TextChanged += (s, e) => { entry.Notes = notesBox.Text; Save(entry); };

            AddLabel(form, "Translate & save");
            var langDd = new Dropdown { Parent = form, Width = formW - 4 };
            foreach (var (code, name) in TranslationService.TargetLanguages)
                langDd.Items.Add(name);
            langDd.SelectedItem = NameForCode(
                string.IsNullOrEmpty(entry.TranslatedLang)
                    ? _module.TranslateTargetSetting.Value : entry.TranslatedLang);
            var status = new Label {
                Parent = form, Width = formW - 4, Height = 22, Text = "",
                TextColor = new Color(190, 190, 190)
            };
            var trBtn = new StandardButton {
                Parent = form, Width = 150, Text = "Translate now"
            };
            trBtn.Click += (s, e) => {
                string target = CodeForName(langDd.SelectedItem);
                status.Text = "Translating…";
                System.Threading.Tasks.Task.Run(async () => {
                    try {
                        string tr = await TranslationService.TranslateAsync(
                            entry.Text, target);
                        // UI (Save→RefreshList, status) jen na hlavním vlákně —
                        // z pozadí by to náhodně shazovalo Blish
                        _module.RunOnMainThread(() => {
                            entry.TranslatedText = tr;
                            entry.TranslatedLang = target;
                            Save(entry);
                            status.Text = "Saved.";
                        });
                    } catch (Exception ex) {
                        _module.RunOnMainThread(() => status.Text = "Translation failed.");
                        Logger.GetLogger<EncyclopediaView>().Warn(ex, "Translate failed.");
                    }
                });
            };

            // pravá část editoru: editovatelný text knihy.
            // MultilineTextBox neumí word-wrap, takže text zalomíme jen
            // pro zobrazení (přes přesné GDI měření) a při uložení zalomení
            // zase odstraníme — uložená data zůstávají bez měkkých konců řádků.
            int editX = 12 + formW + 12;
            new Label {
                Parent = _root, Location = new Point(editX, 46),
                Width = w - editX - 12, Height = 20,
                Text = "Book text (fix OCR errors, add page 2…)",
                TextColor = new Color(200, 200, 200)
            };
            int editW = w - editX - 12;
            int editH = h - 130;
            // scrollovatelný rám: MultilineTextBox sám scrollbar nemá, takže
            // ho vložíme do panelu a necháme textbox růst do výšky obsahu.
            // KRITICKÉ: výška pole musí TĚSNĚ sedět na text. Blishí
            // MultilineTextBox.GetCursorIndexFromPosition spadne (NRE), když
            // klikneš pod poslední řádek — a přebytečná výška = velká prázdná
            // plocha ke kliknutí = pád celého Blish (crash 17.7.2026). Proto
            // font pole = font měření (18) a výška bez rezervy.
            var editScroll = new Panel {
                Parent = _root, Location = new Point(editX, 70),
                Width = editW, Height = editH, CanScroll = true
            };
            int boxW = editW - 18; // místo na scrollbar
            var editFont = GameService.Content.DefaultFont18;
            string wrapped = WrapForEdit(entry.Text, boxW);
            var textEdit = new MultilineTextBox {
                Parent = editScroll, Location = new Point(0, 0),
                Width = boxW, Font = editFont,
                Height = EditContentHeight(wrapped),
                Text = wrapped
            };
            textEdit.TextChanged += (s, e) => {
                entry.Text = UnwrapFromEdit(textEdit.Text);   // in-place, levné
                // KRITICKÉ: výšku NIKDY neměnit uvnitř TextChanged. Blishí
                // MultilineTextBox uprostřed zpracování změny přepočítá
                // rozvržení a sáhne na kurzor/řádky, které ještě odpovídají
                // starému textu -> NRE a pád celého Blish (reprodukce:
                // Enter a hned Backspace; hlášeno 23.7.2026). Změnu velikosti
                // proto odložíme na další snímek, mimo tento handler.
                int wanted = EditContentHeight(textEdit.Text);
                if (wanted != textEdit.Height) {
                    _module.RunOnMainThread(() => {
                        if (textEdit.Parent != null)         // ne po zavření
                            textEdit.Height = EditContentHeight(textEdit.Text);
                    });
                }
                ScheduleEditFlush();                          // disk až po pauze
            };

            new Label {
                Parent = _root, Location = new Point(editX, h - 56),
                Width = w - editX - 12, Height = 40,
                Text = "Tip: editor uses a basic font without accents, but the "
                     + "preview shows full diacritics.",
                TextColor = new Color(150, 150, 150)
            };
        }

        private void Save(LorebookEntry entry) {
            _catalog.Update(entry);
            RefreshList();
        }

        /// <summary>Naplánuje zápis editovaného textu na disk (~1,2 s po
        /// poslední změně). Časovač běží na pozadí; Flush() je zamčený.</summary>
        private void ScheduleEditFlush() {
            _editDirty = true;
            _editFlushTimer?.Dispose();
            _editFlushTimer = new System.Threading.Timer(_ => {
                if (_editDirty) { _editDirty = false; _catalog.Flush(); }
            }, null, 1200, System.Threading.Timeout.Infinite);
        }

        /// <summary>Okamžitě dozapíše rozeditovaný text (odchod z editoru,
        /// zavření okna, unload). Voláno i z modulu.</summary>
        public void FlushEdits() {
            _editFlushTimer?.Dispose();
            _editFlushTimer = null;
            if (_editDirty) { _editDirty = false; _catalog.Flush(); }
        }

        /// <summary>Těsná výška obsahu editoru podle počtu řádků. ŽÁDNÁ
        /// rezerva navíc — pole musí končit hned pod textem, jinak klik do
        /// prázdna pod textem shodí Blishí MultilineTextBox (NRE). Krátký
        /// text = krátké pole; prázdno pod ním je už panel (bezpečné).</summary>
        private static int EditContentHeight(string wrapped) {
            int lines = 1;
            if (wrapped != null)
                foreach (char ch in wrapped) if (ch == '\n') lines++;
            float lh = GameService.Content.DefaultFont18.LineHeight;
            return (int)(lines * lh) + 8;
        }

        /// <summary>Text knihy pro čtečku (včetně případného překladu).</summary>
        private static string BuildBody(LorebookEntry entry) {
            string body = entry.Text;
            if (!string.IsNullOrEmpty(entry.TranslatedText))
                body += "\n\n———  " + (entry.TranslatedLang ?? "translation")
                    + "  ———\n\n" + entry.TranslatedText;
            return body;
        }

        /// <summary>Kniha přes celé okno — čtecí režim. Zpět rohovou
        /// značkou na knize; A± zůstávají po ruce.</summary>
        private void BuildFullscreenReader(int totalW, int totalH) {
            var reader = new BookReaderPanel(_textRenderer, _parchment,
                _module.GetRefTexture("arrow_left.png"),
                _module.GetRefTexture("arrow_right.png"),
                _module.GetRefTexture("ornament_corner.png"),
                _module.GetRefTexture("seal.png"),
                _module.GetRefTexture("expand.png"),
                _module.GetRefTexture("collapse.png")) {
                Parent = _root, Location = new Point(8, 6),
                Width = totalW - 16, Height = totalH - 14,
                FontSize = _textFontSize
            };
            reader.SetEntry(_selected, BuildBody(_selected),
                _module.GetExpansionStampIcon(_selected.Expansion));
            reader.IsFullscreen = true;
            reader.FullscreenToggled += (s, e) => {
                _readerFullscreen = false;
                BuildLayout();
            };
            reader.Opacity = 0f;
            GameService.Animation.Tweener.Tween(
                reader, new { Opacity = 1f }, 0.2f);

            // předčítání i ve fullscreenu (přání uživatele 26.7.2026)
            var playBtn = new StandardButton {
                Parent = _root, Location = new Point(16, 12),
                Width = 70, Text = "▶ Play"
            };
            playBtn.Click += (s, e) => _module.PlayFromCatalog(_selected);
            var pauseBtn = new StandardButton {
                Parent = _root, Location = new Point(90, 12),
                Width = 78,
                Text = _module.SpeechPaused ? "Resume" : "Pause"
            };
            pauseBtn.Click += (s, e) =>
                pauseBtn.Text = _module.TogglePauseSpeaking() ? "Resume" : "Pause";
            var stopBtn = new StandardButton {
                Parent = _root, Location = new Point(172, 12),
                Width = 70, Text = "■ Stop"
            };
            stopBtn.Click += (s, e) => _module.StopSpeaking();

            var fontMinus = new StandardButton {
                Parent = _root, Location = new Point(250, 12),
                // pozor: obyčejný spojovník, ne „−" (U+2212) — herní font
                // ten znak nemá a tlačítko pak ukazuje jen „A"
                Width = 36, Text = "A-"
            };
            var fontPlus = new StandardButton {
                Parent = _root, Location = new Point(290, 12),
                Width = 36, Text = "A+"
            };
            fontMinus.Click += (s, e) => {
                _textFontSize = Math.Max(12f, _textFontSize - 2f);
                reader.FontSize = _textFontSize;
            };
            fontPlus.Click += (s, e) => {
                _textFontSize = Math.Min(40f, _textFontSize + 2f);
                reader.FontSize = _textFontSize;
            };
        }

        /// <summary>Předvolby datadisků (pořadí vydání). První položka =
        /// „bez datadisku". Ikony: ref/xp_*.png (GetExpansionIcon).</summary>
        internal static readonly string[] ExpansionPresets = {
            "(none)", "Core", "Heart of Thorns", "Path of Fire",
            "Icebrood Saga", "End of Dragons", "Secrets of the Obscure",
            "Janthir Wilds", "Visions of Eternity"
        };

        // ---------------------------- helpers ----------------------------
        private static void AddLabel(Container parent, string text) {
            new Label {
                Parent = parent, Text = text, AutoSizeHeight = true, Width = 280,
                TextColor = new Color(200, 200, 200)
            };
        }
        private static TextBox AddBox(Container parent, string value, int width) {
            return new TextBox { Parent = parent, Width = width, Text = value ?? "" };
        }
        private static string NameForCode(string code) {
            foreach (var (c, n) in TranslationService.TargetLanguages)
                if (c == code) return n;
            return TranslationService.TargetLanguages[0].Name;
        }
        private static string CodeForName(string name) {
            foreach (var (c, n) in TranslationService.TargetLanguages)
                if (n == name) return c;
            return "cs";
        }

        // Marker pro měkká zalomení vložená kvůli zobrazení v editoru;
        // při ukládání je odstraníme, aby uložený text zůstal čistý.
        private const string SoftBreak = "\u200B\n";  // zero-width space + LF

        /// <summary>Zalomí text na šířku editoru (MultilineTextBox sám neumí
        /// word-wrap). Tvrdé konce řádků z dat zachová, měkká zalomení
        /// označí markerem.</summary>
        private string WrapForEdit(string text, int pixelWidth) {
            if (string.IsNullOrEmpty(text)) return "";
            // značku ilustrace ukázat čitelně; UnwrapFromEdit ji vrátí zpět
            text = Regex.Replace(text, @"⟦IMG:([^⟧]*)⟧", "[image: $1]");
            var font = GameService.Content.DefaultFont18;
            int maxW = Math.Max(60, pixelWidth - 24);
            var sb = new System.Text.StringBuilder();

            string[] hardLines = text.Replace("\r", "").Split('\n');
            for (int li = 0; li < hardLines.Length; li++) {
                // PRÁZDNÝ ŘÁDEK (předěl odstavce) nahradíme mezerou. Blishí
                // MultilineTextBox spadne (NRE v GetCursorIndexFromPosition),
                // když se klikne na řádek bez jediného znaku — a předěl
                // odstavce má v editoru každá kniha (crash 26.7.2026).
                // Mezeru při ukládání zase odstraníme (UnwrapFromEdit).
                if (hardLines[li].Length == 0) {
                    sb.Append(' ');
                    if (li < hardLines.Length - 1) sb.Append('\n');
                    continue;
                }
                string[] words = hardLines[li].Split(' ');
                string current = "";
                foreach (string word in words) {
                    string candidate = current.Length == 0
                        ? word : current + " " + word;
                    if (font.MeasureString(candidate).Width <= maxW
                        || current.Length == 0) {
                        current = candidate;
                    } else {
                        sb.Append(current).Append(SoftBreak);
                        current = word;
                    }
                }
                sb.Append(current);
                if (li < hardLines.Length - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Odstraní měkká zalomení (slepí řádky odstavce zpět),
        /// tvrdé konce řádků zachová.</summary>
        private static string UnwrapFromEdit(string text) {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace(SoftBreak, " ").Replace("\u200B", "");
            // \u010Diteln\u00FD z\u00E1pis ilustrace zp\u011Bt na zna\u010Dku pro \u010Dte\u010Dku
            text = Regex.Replace(text, @"\[image:\s*([^\]]*)\]", "\u27E6IMG:$1\u27E7");
            // v\u00FDpl\u0148ov\u00E9 mezery z pr\u00E1zdn\u00FDch \u0159\u00E1dk\u016F (viz WrapForEdit) pry\u010D,
            // a\u0165 se do katalogu ulo\u017E\u00ED \u010Dist\u00FD text
            var lines = text.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].TrimEnd();
            return string.Join("\n", lines);
        }
    }
}
