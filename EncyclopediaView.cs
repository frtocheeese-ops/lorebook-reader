using System;
using System.Collections.Generic;
using System.Linq;
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
        private Dropdown _expansionFilter;
        private FlowPanel _listPanel;
        private StandardButton _exportBtn, _importBtn;

        // pravý sloupec
        private Panel _detailPanel;
        private LorebookEntry _selected;
        private float _textFontSize = 18f;
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
            _root.ClearChildren();
            // ContentRegion = vnitřní plocha okna bez titulku a okrajů.
            // Použití .Width/.Height by zahrnulo rámeček a obsah by přetékal.
            int totalW = _root.ContentRegion.Width;
            int totalH = _root.ContentRegion.Height;
            int leftW = Math.Max(280, (int)(totalW * 0.40f));

            // ===================== LEVÝ PANEL =====================
            _searchBox = new TextBox {
                Parent = _root, Location = new Point(8, 6),
                Width = leftW - 16,
                PlaceholderText = "Search title, text, metadata…"
            };
            _searchBox.TextChanged += (s, e) => RefreshList();

            _sortDropdown = new Dropdown {
                Parent = _root, Location = new Point(8, 38), Width = leftW - 16
            };
            foreach (string item in SortItems) _sortDropdown.Items.Add(item);
            _sortDropdown.SelectedItem = SortItems[0];
            _sortDropdown.ValueChanged += (s, e) => RefreshList();

            int halfW = (leftW - 24) / 2;
            _colorFilter = new Dropdown {
                Parent = _root, Location = new Point(8, 70), Width = halfW
            };
            _colorFilter.Items.Add("All");
            foreach (var c in Palette.Colors)
                if (c.Name != "None") _colorFilter.Items.Add(c.Name);
            _colorFilter.Items.Add("None");
            _colorFilter.SelectedItem = "All";
            _colorFilter.ValueChanged += (s, e) => RefreshList();

            _expansionFilter = new Dropdown {
                Parent = _root, Location = new Point(8 + halfW + 8, 70), Width = halfW
            };
            RebuildExpansionFilter();
            _expansionFilter.ValueChanged += (s, e) => RefreshList();

            _listPanel = new FlowPanel {
                Parent = _root, Location = new Point(8, 102),
                Width = leftW - 16, Height = totalH - 150,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 3),
                CanScroll = true, ShowBorder = true
            };

            _exportBtn = new StandardButton {
                Parent = _root, Location = new Point(8, totalH - 42),
                Width = halfW, Text = "Export…"
            };
            _exportBtn.Click += (s, e) => _module.ExportCatalogDialog();
            _importBtn = new StandardButton {
                Parent = _root, Location = new Point(8 + halfW + 8, totalH - 42),
                Width = halfW, Text = "Import…"
            };
            _importBtn.Click += (s, e) => _module.ImportCatalogDialog();

            // ===================== PRAVÝ PANEL =====================
            _detailPanel = new Panel {
                Parent = _root, Location = new Point(leftW + 8, 6),
                Width = totalW - leftW - 16, Height = totalH - 14,
                ShowBorder = true
            };

            RefreshList();
            // znovu vykreslit detail podle stavu
            if (_selected != null && _mode == DetailMode.Edit) ShowEditor(_selected);
            else if (_selected != null)                        ShowPreview(_selected);
            else                                               ShowEmpty();
        }

        public void RebuildExpansionFilter() {
            if (_expansionFilter == null) return;
            string previous = _expansionFilter.SelectedItem;
            _expansionFilter.Items.Clear();
            _expansionFilter.Items.Add("All");
            foreach (string exp in _catalog.DistinctExpansions())
                _expansionFilter.Items.Add(exp);
            _expansionFilter.SelectedItem =
                _expansionFilter.Items.Contains(previous ?? "All") ? previous : "All";
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
            if (_listPanel == null) return;
            _listPanel.ClearChildren();
            var results = _catalog.Query(
                _searchBox?.Text, CurrentSort(),
                _colorFilter?.SelectedItem, _expansionFilter?.SelectedItem);

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
            RebuildExpansionFilter();
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
            var (cr, cg, cb) = Palette.Resolve(entry.ColorTag);
            new Panel {
                Parent = row, Location = new Point(0, 0),
                Width = 5, Height = 46, BackgroundColor = new Color(cr, cg, cb)
            };
            new Label {
                Parent = row, Location = new Point(12, 4),
                Width = row.Width - 20, Height = 22,
                Text = entry.DisplayTitle, Font = GameService.Content.DefaultFont16
            };
            string meta = entry.MetadataLine;
            new Label {
                Parent = row, Location = new Point(12, 26),
                Width = row.Width - 20, Height = 16,
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
        private void ShowPreview(LorebookEntry entry) {
            _mode = DetailMode.Preview;
            _detailPanel.ClearChildren();
            int w = _detailPanel.Width;
            int h = _detailPanel.Height;

            new Label {
                Parent = _detailPanel, Location = new Point(12, 10),
                Width = w - 24, Height = 28,
                Text = entry.DisplayTitle, Font = GameService.Content.DefaultFont18
            };

            var playBtn = new StandardButton {
                Parent = _detailPanel, Location = new Point(12, 44),
                Width = 80, Text = "▶ Play"
            };
            playBtn.Click += (s, e) => _module.PlayFromCatalog(entry);
            var stopBtn = new StandardButton {
                Parent = _detailPanel, Location = new Point(96, 44),
                Width = 72, Text = "■ Stop"
            };
            stopBtn.Click += (s, e) => _module.StopSpeaking();
            var editBtn = new StandardButton {
                Parent = _detailPanel, Location = new Point(172, 44),
                Width = 80, Text = "Edit"
            };
            editBtn.Click += (s, e) => ShowEditor(entry);
            var delBtn = new StandardButton {
                Parent = _detailPanel, Location = new Point(w - 92, 44),
                Width = 80, Text = "Delete"
            };
            delBtn.Click += (s, e) => {
                _catalog.Remove(entry.Id);
                _selected = null;
                RefreshList();
                ShowEmpty();
            };

            // ovládání velikosti písma
            var fontMinus = new StandardButton {
                Parent = _detailPanel, Location = new Point(w - 92, 78),
                Width = 36, Text = "A−"
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

            string body = entry.Text;
            if (!string.IsNullOrEmpty(entry.TranslatedText))
                body += "\n\n———  " + (entry.TranslatedLang ?? "translation")
                    + "  ———\n\n" + entry.TranslatedText;

            var parchment = new ParchmentTextPanel(_textRenderer, _parchment) {
                Parent = _detailPanel, Location = new Point(12, 106),
                Width = w - 24, Height = h - 118,
                FontSize = _textFontSize
            };
            parchment.Text = body;
            parchment.ApplyWrap();   // vynutit zalomení na aktuální šířku

            fontMinus.Click += (s, e) => {
                _textFontSize = Math.Max(12f, _textFontSize - 2f);
                parchment.FontSize = _textFontSize;
            };
            fontPlus.Click += (s, e) => {
                _textFontSize = Math.Min(40f, _textFontSize + 2f);
                parchment.FontSize = _textFontSize;
            };
        }

        // ===================== DETAIL: editor =====================
        private void ShowEditor(LorebookEntry entry) {
            _mode = DetailMode.Edit;
            _detailPanel.ClearChildren();
            int w = _detailPanel.Width;
            int h = _detailPanel.Height;

            var back = new StandardButton {
                Parent = _detailPanel, Location = new Point(12, 10),
                Width = 90, Text = "‹ Back"
            };
            back.Click += (s, e) => ShowPreview(entry);

            // levá část editoru: metadata
            int formW = Math.Min(280, w / 2 - 16);
            var form = new FlowPanel {
                Parent = _detailPanel, Location = new Point(12, 46),
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
            var expBox = AddBox(form, entry.Expansion, formW - 4);
            expBox.TextChanged += (s, e) => { entry.Expansion = expBox.Text; Save(entry); };

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
                        entry.TranslatedText = tr;
                        entry.TranslatedLang = target;
                        Save(entry);
                        status.Text = "Saved.";
                    } catch (Exception ex) {
                        status.Text = "Translation failed.";
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
                Parent = _detailPanel, Location = new Point(editX, 46),
                Width = w - editX - 12, Height = 20,
                Text = "Book text (fix OCR errors, add page 2…)",
                TextColor = new Color(200, 200, 200)
            };
            int editW = w - editX - 12;
            var textEdit = new MultilineTextBox {
                Parent = _detailPanel,
                Location = new Point(editX, 70),
                Width = editW, Height = h - 130,
                Text = WrapForEdit(entry.Text, editW)
            };
            textEdit.TextChanged += (s, e) => {
                entry.Text = UnwrapFromEdit(textEdit.Text);
                Save(entry);
            };

            new Label {
                Parent = _detailPanel, Location = new Point(editX, h - 56),
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
            var font = GameService.Content.DefaultFont18;
            int maxW = Math.Max(60, pixelWidth - 24);
            var sb = new System.Text.StringBuilder();

            string[] hardLines = text.Replace("\r", "").Split('\n');
            for (int li = 0; li < hardLines.Length; li++) {
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
            return text.Replace(SoftBreak, " ").Replace("\u200B", "");
        }
    }
}
