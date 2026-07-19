using System;
using System.Collections.Generic;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Knižní čtečka encyklopedie (remake fáze B, viz
    /// docs/encyclopedia-redesign.md):
    ///  - JEDNA stránka pergamenu, stránkovaná z přesně měřených GDI řádků;
    ///  - stránka 0 = OBÁLKA: titul ve stylistickém (patkovém) fontu,
    ///    ozdobné linky, dole datum/místo — a RAZÍTKO datadisku (nakloněná
    ///    ikona + název, inkoustový vzhled). Bez datadisku razítko není;
    ///  - listování ‹ › s animací otočení stránky: odchozí list se vodorovně
    ///    „stiskne" k hraně a příchozí se rozvine z druhé (bez 3D, čistě
    ///    škálování textur v Paint — GW2 vibe, žádné závislosti).
    /// </summary>
    public sealed class BookReaderPanel : Container {

        private static readonly Logger Logger =
            Logger.GetLogger<BookReaderPanel>();

        private static readonly Color InkColor      = new Color(48, 36, 20);
        private static readonly Color HeadInk       = new Color(36, 26, 12);
        private static readonly Color FaintInk      = new Color(122, 104, 70);
        private static readonly Color StampInk      = new Color(126, 54, 34) * 0.85f;
        private const int PadX = 46; // volný pruh pro ‹ › tlačítka po stranách
        private const int PadY = 20; // spodní okraj
        // horní okraj textu: nechává místo pro tlačítko fullscreenu, které
        // sedí uprostřed horní hrany knihy (jinak by leželo na textu)
        private const int FsBtnSize = 46;
        private const int FsBtnTop  = 8;
        private const int PadTop = FsBtnTop + FsBtnSize + 10;
        private const double TurnMs = 420;

        private readonly TextRenderer _tr;
        private readonly Texture2D _parchment;
        private readonly Texture2D _ornament; // rohová ozdoba obálky (volitelné)
        private readonly Texture2D _seal;     // pečeť pro knihy bez datadisku

        private LorebookEntry _entry;
        private string _body = "";
        private Texture2D _xpIcon;
        private float _fontSize = 18f;

        // stránky: seznam řádků (text, nadpis?, mezera-před?)
        // (iniciála/drop cap vyzkoušena v 0.7.1 a na přání odstraněna)
        private struct Line {
            public string Text; public bool Head; public bool Gap;
        }
        private readonly List<List<Line>> _pages = new List<List<Line>>();
        private int _page;          // 0 = obálka, 1..N = _pages[i-1]
        private int _lastLayoutW = -1, _lastLayoutH = -1;

        // animace otočení
        private bool _turning;
        private double _turnT;      // 0..1
        private int _turnDir;       // +1 dopředu, -1 zpět
        private int _pendingPage;

        private readonly Control _prevBtn;
        private readonly Control _nextBtn;
        private readonly FullscreenButton _fsBtn;

        /// <summary>Klik na rohovou značku knihy (fullscreen ⇄ zpět).
        /// Přepnutí layoutu dělá vlastník (EncyclopediaView).</summary>
        public event EventHandler FullscreenToggled;

        private bool _isFullscreen;
        public bool IsFullscreen {
            get => _isFullscreen;
            set {
                _isFullscreen = value;
                if (_fsBtn != null) {
                    _fsBtn.Collapse = value;
                    _fsBtn.BasicTooltipText = value
                        ? "Exit full-window reading"
                        : "Read across the whole window";
                }
            }
        }

        public BookReaderPanel(TextRenderer tr, Texture2D parchment,
                               Texture2D arrowLeft = null,
                               Texture2D arrowRight = null,
                               Texture2D ornament = null,
                               Texture2D seal = null,
                               Texture2D expandIcon = null,
                               Texture2D collapseIcon = null) {
            _tr = tr;
            _parchment = parchment;
            _ornament = ornament;
            _seal = seal;
            _prevBtn = MakeArrow(arrowLeft, "‹");
            _nextBtn = MakeArrow(arrowRight, "›");
            _prevBtn.Click += (s, e) => Turn(-1);
            _nextBtn.Click += (s, e) => Turn(+1);
            // vlastní ikony z ref/ (expand.png / collapse.png); bez nich
            // se kreslí zabudované značky
            _fsBtn = new FullscreenButton {
                Parent = this, Visible = false,
                Size = new Point(FsBtnSize, FsBtnSize),
                ExpandIcon = expandIcon, CollapseIcon = collapseIcon,
                BasicTooltipText = "Read across the whole window"
            };
            _fsBtn.Click += (s, e) =>
                FullscreenToggled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Rohová značka fullscreenu — kreslená čistě z pixelů
        /// (závorky „roztáhnout" / čtvereček „obnovit"), žádný font/asset.</summary>
        private sealed class FullscreenButton : Control {
            public bool Collapse;
            public Texture2D ExpandIcon, CollapseIcon;
            private bool _hover;
            protected override CaptureType CapturesInput() => CaptureType.Mouse;
            protected override void OnMouseEntered(MouseEventArgs e) {
                _hover = true; base.OnMouseEntered(e);
            }
            protected override void OnMouseLeft(MouseEventArgs e) {
                _hover = false; base.OnMouseLeft(e);
            }
            protected override void Paint(SpriteBatch sb, Rectangle b) {
                // vlastní ikona z ref/ má přednost (hover = plný jas)
                var icon = Collapse ? CollapseIcon : ExpandIcon;
                if (icon != null) {
                    sb.DrawOnCtrl(this, icon, b, null,
                        _hover ? Color.White : Color.White * 0.85f);
                    return;
                }

                var px = ContentService.Textures.Pixel;
                // tmavé průsvitné pozadí + světlé linky → čitelné na
                // pergamenu i na tmavém okně (feedback 17.7.2026)
                sb.DrawOnCtrl(this, px, b,
                    new Color(0, 0, 0, _hover ? 165 : 120));
                var edge = new Color(150, 122, 60) * (_hover ? 1f : 0.85f);
                sb.DrawOnCtrl(this, px, new Rectangle(b.X, b.Y, b.Width, 1), edge);
                sb.DrawOnCtrl(this, px, new Rectangle(b.X, b.Bottom - 1, b.Width, 1), edge);
                sb.DrawOnCtrl(this, px, new Rectangle(b.X, b.Y, 1, b.Height), edge);
                sb.DrawOnCtrl(this, px, new Rectangle(b.Right - 1, b.Y, 1, b.Height), edge);
                var c = new Color(245, 233, 202) * (_hover ? 1f : 0.9f);
                const int t = 2, a = 9, m = 6; // m = odsazení od okraje rámečku
                if (!Collapse) {
                    // rohové závorky ven = „roztáhnout"
                    int L = b.X + m, R = b.Right - m, T = b.Y + m, B2 = b.Bottom - m;
                    sb.DrawOnCtrl(this, px, new Rectangle(L, T, a, t), c);
                    sb.DrawOnCtrl(this, px, new Rectangle(L, T, t, a), c);
                    sb.DrawOnCtrl(this, px, new Rectangle(R - a, T, a, t), c);
                    sb.DrawOnCtrl(this, px, new Rectangle(R - t, T, t, a), c);
                    sb.DrawOnCtrl(this, px, new Rectangle(L, B2 - t, a, t), c);
                    sb.DrawOnCtrl(this, px, new Rectangle(L, B2 - a, t, a), c);
                    sb.DrawOnCtrl(this, px, new Rectangle(R - a, B2 - t, a, t), c);
                    sb.DrawOnCtrl(this, px, new Rectangle(R - t, B2 - a, t, a), c);
                } else {
                    // čtvereček uprostřed = „obnovit okno"
                    int s2 = 14;
                    int x = b.X + (b.Width - s2) / 2, y = b.Y + (b.Height - s2) / 2;
                    sb.DrawOnCtrl(this, px, new Rectangle(x, y, s2, t), c);
                    sb.DrawOnCtrl(this, px, new Rectangle(x, y + s2 - t, s2, t), c);
                    sb.DrawOnCtrl(this, px, new Rectangle(x, y, t, s2), c);
                    sb.DrawOnCtrl(this, px, new Rectangle(x + s2 - t, y, t, s2), c);
                }
            }
        }

        /// <summary>Kamenný disk z ref/ (hover = plný jas); bez textury
        /// padne na obyčejné tlačítko se znakem.</summary>
        private Control MakeArrow(Texture2D tex, string fallback) {
            if (tex != null)
                return new ArrowButton(tex) {
                    Parent = this, Size = new Point(36, 36), Visible = false
                };
            return new StandardButton {
                Parent = this, Text = fallback, Width = 30, Visible = false
            };
        }

        private sealed class ArrowButton : Control {
            private readonly Texture2D _tex;
            private bool _hover;
            public ArrowButton(Texture2D tex) { _tex = tex; }
            protected override CaptureType CapturesInput() => CaptureType.Mouse;
            protected override void OnMouseEntered(MouseEventArgs e) {
                _hover = true; base.OnMouseEntered(e);
            }
            protected override void OnMouseLeft(MouseEventArgs e) {
                _hover = false; base.OnMouseLeft(e);
            }
            protected override void Paint(SpriteBatch sb, Rectangle bounds) {
                sb.DrawOnCtrl(this, _tex, bounds, null,
                    _hover ? Color.White : Color.White * 0.82f);
            }
        }

        public float FontSize {
            get => _fontSize;
            set {
                if (Math.Abs(_fontSize - value) < 0.1f) return;
                _fontSize = value;
                Repaginate();
            }
        }

        /// <summary>Nastaví zobrazenou knihu. Body = text (případně včetně
        /// připojeného překladu); ikona datadisku smí být null.</summary>
        public void SetEntry(LorebookEntry entry, string body, Texture2D xpIcon) {
            _entry = entry;
            _body = body ?? "";
            _xpIcon = xpIcon;
            _page = 0;
            _turning = false;
            Repaginate();
        }

        // ------------------------- stránkování --------------------------

        private void Repaginate() {
            try {
                RepaginateCore();
            } catch (Exception ex) {
                Logger.Warn(ex, "Book pagination failed.");
                _pages.Clear();
                _page = 0;
            }
        }

        private void RepaginateCore() {
            _pages.Clear();
            int w = this.Width, h = this.Height;
            _lastLayoutW = w; _lastLayoutH = h;
            if (_entry == null || w <= PadX * 2 + 20
                || h <= PadTop + PadY + 20) return;

            int wrapW = w - PadX * 2;
            float lineH = _tr.LineHeight(_fontSize);
            float headH = _tr.LineHeight(_fontSize + 2, bold: true);
            float gapH  = lineH * 0.55f;
            float availH = h - PadTop - PadY - 18; // rezerva na číslo stránky

            var cur = new List<Line>();
            float used = 0;

            foreach (string rawPara in _body.Replace("\r", "")
                         .Split(new[] { "\n\n" }, StringSplitOptions.None)) {
                string para = rawPara.Trim();
                if (para.Length == 0) continue;
                bool head = IsHeading(para);
                var lines = _tr.WrapText(para.Replace('\n', ' '),
                    head ? _fontSize + 2 : _fontSize, wrapW, bold: head);
                bool first = true;
                foreach (string ln in lines) {
                    float lh = head ? headH : lineH;
                    float need = lh + (first && cur.Count > 0 ? gapH : 0);
                    if (used + need > availH && cur.Count > 0) {
                        _pages.Add(cur);
                        cur = new List<Line>();
                        used = 0;
                        need = lh;
                        first = false; // na nové stránce bez mezery
                    }
                    cur.Add(new Line {
                        Text = ln, Head = head,
                        Gap = first && cur.Count > 0
                    });
                    used += need;
                    first = false;
                }
            }
            if (cur.Count > 0) _pages.Add(cur);
            if (_page > _pages.Count) _page = _pages.Count;
            UpdateButtons();
            Invalidate();
        }

        /// <summary>Nadpis sekce: krátký odstavec bez koncové interpunkce
        /// (OCR ho odděluje jako vlastní blok, viz AssembleWithParagraphs).</summary>
        private static bool IsHeading(string para) {
            if (para.Length > 48 || para.Contains("\n")) return false;
            char last = para[para.Length - 1];
            return last != '.' && last != '!' && last != '?' && last != ':'
                   && last != '"' && last != '\'';
        }

        // --------------------------- listování ---------------------------

        private void Turn(int dir) {
            int np = _page + dir;
            if (_turning || np < 0 || np > _pages.Count) return;
            _turning = true;
            _turnT = 0;
            _turnDir = dir;
            _pendingPage = np;
            UpdateButtons();
        }

        public override void UpdateContainer(GameTime gameTime) {
            if (this.Width != _lastLayoutW || this.Height != _lastLayoutH)
                Repaginate();
            if (_turning) {
                _turnT += gameTime.ElapsedGameTime.TotalMilliseconds / TurnMs;
                if (_turnT >= 0.5 && _page != _pendingPage)
                    _page = _pendingPage;
                if (_turnT >= 1.0) {
                    _turning = false;
                    _turnT = 0;
                    UpdateButtons();
                }
            }
        }

        private void UpdateButtons() {
            // ctor: Parent= prvního tlačítka spustí RecalculateLayout dřív,
            // než existují ostatní (crash 2026-07-16, NRE v UpdateButtons)
            if (_prevBtn == null || _nextBtn == null || _fsBtn == null) return;
            _prevBtn.Visible = _entry != null && _page > 0;
            _nextBtn.Visible = _entry != null && _page < _pages.Count;
            _fsBtn.Visible   = _entry != null;
            _prevBtn.Location = new Point(4, this.Height / 2 - _prevBtn.Height / 2);
            _nextBtn.Location = new Point(this.Width - _nextBtn.Width - 4,
                                          this.Height / 2 - _nextBtn.Height / 2);
            // uprostřed horní hrany knihy (text začíná až pod ním, viz PadTop)
            _fsBtn.Location = new Point(
                (this.Width - _fsBtn.Width) / 2, FsBtnTop);
        }

        public override void RecalculateLayout() {
            base.RecalculateLayout();
            UpdateButtons();
        }

        // --------------------------- kreslení ----------------------------

        public override void PaintBeforeChildren(SpriteBatch sb, Rectangle bounds) {
            try {
                PaintCore(sb, bounds);
            } catch (Exception ex) {
                Logger.Warn(ex, "Book reader paint failed.");
            }
        }

        private void PaintCore(SpriteBatch sb, Rectangle bounds) {
            if (_entry == null) return;

            // animace: 1. půlka = starý list se svírá k hraně,
            // 2. půlka = nový se rozvírá od druhé hrany
            float f = 1f;
            bool anchorLeft = true;
            if (_turning) {
                // juice pravidlo: UI animace nikdy lineárně — zavírání
                // kvadraticky zrychluje (ease-in), otvírání zpomaluje (ease-out)
                if (_turnT < 0.5) {
                    double t2 = _turnT * 2;
                    f = 1f - (float)(t2 * t2);            // 1 → 0, ease-in
                    anchorLeft = _turnDir > 0;            // dopředu: sevři vlevo
                } else {
                    double t2 = (_turnT - 0.5) * 2;
                    f = (float)(1 - (1 - t2) * (1 - t2)); // 0 → 1, ease-out
                    anchorLeft = _turnDir < 0;            // dopředu: rozviň zprava
                }
                f = Math.Max(0.02f, Math.Min(1f, f));
            }

            int fullW = bounds.Width;
            int pageW = (int)(fullW * f);
            int pageX = bounds.X + (anchorLeft ? 0 : fullW - pageW);
            var pageRect = new Rectangle(pageX, bounds.Y, pageW, bounds.Height);

            // pergamen (stín pod otáčeným listem dodá hloubku)
            if (_turning)
                sb.DrawOnCtrl(this, ContentService.Textures.Pixel, bounds,
                    new Color(0, 0, 0, 70));
            if (_parchment != null)
                sb.DrawOnCtrl(this, _parchment, pageRect, null, Color.White);
            else
                sb.DrawOnCtrl(this, ContentService.Textures.Pixel, pageRect,
                    new Color(231, 217, 182));

            // patina okrajů — stránka nepůsobí jako plochá textura
            DrawEdge(sb, pageRect, 6, FaintInk * 0.08f);
            DrawEdge(sb, pageRect, 3, FaintInk * 0.10f);
            DrawEdge(sb, pageRect, 1, FaintInk * 0.30f);

            if (_page == 0) PaintCover(sb, bounds, pageRect, f, anchorLeft);
            else            PaintPage(sb, bounds, pageRect, f, anchorLeft);

            // číslo stránky dole (jen obsahové stránky, bez animace)
            if (!_turning && _page > 0 && _pages.Count > 0) {
                var t = _tr.RenderLine($"{_page} / {_pages.Count}",
                    _fontSize * 0.68f, FaintInk);
                if (t != null)
                    sb.DrawOnCtrl(this, t, new Rectangle(
                        bounds.X + (fullW - t.Width) / 2,
                        bounds.Bottom - t.Height - 4, t.Width, t.Height));
            }
        }

        /// <summary>Rámeček z pruhů dané tloušťky po obvodu (patina).</summary>
        private void DrawEdge(SpriteBatch sb, Rectangle r, int t, Color c) {
            var px = ContentService.Textures.Pixel;
            sb.DrawOnCtrl(this, px, new Rectangle(r.X, r.Y, r.Width, t), c);
            sb.DrawOnCtrl(this, px, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
            sb.DrawOnCtrl(this, px, new Rectangle(r.X, r.Y, t, r.Height), c);
            sb.DrawOnCtrl(this, px, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
        }

        /// <summary>Přepočet X souřadnice do „stisknutého" listu.</summary>
        private static int SqueezeX(Rectangle bounds, Rectangle page,
                                    float f, bool anchorLeft, float x) {
            return anchorLeft
                ? page.X + (int)((x - bounds.X) * f)
                : page.Right - (int)((bounds.Right - x) * f);
        }

        private void PaintPage(SpriteBatch sb, Rectangle bounds,
                               Rectangle page, float f, bool anchorLeft) {
            var lines = _pages[_page - 1];
            float y = bounds.Y + PadTop;
            foreach (var ln in lines) {
                float lh = ln.Head
                    ? _tr.LineHeight(_fontSize + 2, bold: true)
                    : _tr.LineHeight(_fontSize);
                if (ln.Gap) y += _tr.LineHeight(_fontSize) * 0.55f;
                if (ln.Text.Length > 0) {
                    var tex = ln.Head
                        ? _tr.RenderLine(ln.Text, _fontSize + 2, HeadInk, bold: true)
                        : _tr.RenderLine(ln.Text, _fontSize, InkColor);
                    if (tex != null) {
                        int dx = SqueezeX(bounds, page, f, anchorLeft,
                                          bounds.X + PadX);
                        sb.DrawOnCtrl(this, tex, new Rectangle(
                            dx, (int)y, (int)(tex.Width * f), tex.Height));
                    }
                }
                y += lh;
            }
        }

        private void PaintCover(SpriteBatch sb, Rectangle bounds,
                                Rectangle page, float f, bool anchorLeft) {
            int fullW = bounds.Width;

            // zlaté ornamenty v rozích obálky (jedna textura, zrcadlení)
            if (_ornament != null) {
                int os = Math.Min(56, fullW / 6);
                const int inset = 8;
                void Corner(float cx, int cy2, SpriteEffects fx) {
                    int dx = SqueezeX(bounds, page, f, anchorLeft, cx);
                    sb.DrawOnCtrl(this, _ornament,
                        new Rectangle(dx, cy2, (int)(os * f), os),
                        null, Color.White * 0.9f, 0f, Vector2.Zero, fx);
                }
                Corner(bounds.X + inset, bounds.Y + inset,
                       SpriteEffects.None);
                Corner(bounds.Right - inset - os, bounds.Y + inset,
                       SpriteEffects.FlipHorizontally);
                Corner(bounds.X + inset, bounds.Bottom - inset - os,
                       SpriteEffects.FlipVertically);
                Corner(bounds.Right - inset - os, bounds.Bottom - inset - os,
                       SpriteEffects.FlipHorizontally
                       | SpriteEffects.FlipVertically);
            }
            string title = _entry.DisplayTitle ?? "";
            float titleSize = Math.Max(20f, _fontSize * 1.7f);
            var titleLines = _tr.WrapText(title, titleSize,
                (int)(fullW * 0.72f), bold: true, serif: true);
            float th = _tr.LineHeight(titleSize, bold: true, serif: true);

            float blockH = titleLines.Count * th + 34
                           + (HasExpansion()
                                  ? (_xpIcon != null ? 160 : 92)
                                  : (_seal != null ? 130 : 0));
            float y = bounds.Y + Math.Max(PadTop + 6,
                (bounds.Height - blockH) * 0.34f);

            foreach (string ln in titleLines) {
                var tex = _tr.RenderLine(ln, titleSize, HeadInk,
                                         bold: true, serif: true);
                if (tex != null) {
                    float cx = bounds.X + (fullW - tex.Width) / 2f;
                    int dx = SqueezeX(bounds, page, f, anchorLeft, cx);
                    sb.DrawOnCtrl(this, tex, new Rectangle(
                        dx, (int)y, (int)(tex.Width * f), tex.Height));
                }
                y += th;
            }

            // ozdobné linky pod titulem
            y += 10;
            DrawRule(sb, bounds, page, f, anchorLeft, (int)y, 0.52f, 2);
            y += 7;
            DrawRule(sb, bounds, page, f, anchorLeft, (int)y, 0.30f, 1);
            y += 26;

            // razítko datadisku; bez datadisku dostane obálka voskovou pečeť
            if (HasExpansion()) {
                PaintStamp(sb, bounds, page, f, anchorLeft,
                    (int)y + (_xpIcon != null ? 62 : 26));
            } else if (_seal != null) {
                int ss = Math.Min((int)(fullW * 0.28f), 104);
                int cx = SqueezeX(bounds, page, f, anchorLeft,
                                  bounds.X + fullW / 2f);
                sb.DrawOnCtrl(this, _seal,
                    new Rectangle(cx, (int)y + 56, (int)(ss * f), ss),
                    null, Color.White * 0.95f, -0.08f,
                    new Vector2(_seal.Width / 2f, _seal.Height / 2f),
                    SpriteEffects.None);
            }

            // dole datum + místo
            string meta = _entry.TimestampLocal == DateTime.MinValue
                ? "" : _entry.TimestampLocal.ToString("d MMMM yyyy");
            if (!string.IsNullOrWhiteSpace(_entry.Location))
                meta += (meta.Length > 0 ? "  ·  " : "") + _entry.Location;
            if (meta.Length > 0) {
                var mt = _tr.RenderLine(meta, _fontSize * 0.72f, FaintInk,
                                        serif: true);
                if (mt != null) {
                    float cx = bounds.X + (fullW - mt.Width) / 2f;
                    int dx = SqueezeX(bounds, page, f, anchorLeft, cx);
                    sb.DrawOnCtrl(this, mt, new Rectangle(
                        dx, bounds.Bottom - mt.Height - 14,
                        (int)(mt.Width * f), mt.Height));
                }
            }
        }

        private bool HasExpansion() =>
            !string.IsNullOrWhiteSpace(_entry?.Expansion);

        private void DrawRule(SpriteBatch sb, Rectangle bounds, Rectangle page,
                              float f, bool anchorLeft, int y,
                              float widthFrac, int thick) {
            int w = (int)(bounds.Width * widthFrac);
            float cx = bounds.X + (bounds.Width - w) / 2f;
            int dx = SqueezeX(bounds, page, f, anchorLeft, cx);
            sb.DrawOnCtrl(this, ContentService.Textures.Pixel,
                new Rectangle(dx, y, (int)(w * f), thick),
                FaintInk * 0.9f);
        }

        /// <summary>Razítko datadisku na obálce: OTISK samotného loga
        /// (ref/xp_*_big.png, jinak malá ikona) — velký, lehce nakloněný,
        /// s mírnou průhledností jako otisknuté razítko. Když logo chybí,
        /// fallback je inkoustový rámeček s názvem datadisku.</summary>
        private void PaintStamp(SpriteBatch sb, Rectangle bounds, Rectangle page,
                                float f, bool anchorLeft, int centerY) {
            const float rot = -0.14f; // ~ -8°
            float cxF = bounds.X + bounds.Width / 2f;
            var center = new Vector2(
                SqueezeX(bounds, page, f, anchorLeft, cxF), centerY);

            if (_xpIcon != null) {
                // otisk loga: zachovat poměr stran, strop ~40 % šířky stránky
                int maxS = Math.Min((int)(bounds.Width * 0.40f), 150);
                float scale = Math.Min(
                    (float)maxS / _xpIcon.Width, (float)maxS / _xpIcon.Height);
                int w = (int)(_xpIcon.Width * scale);
                int h = (int)(_xpIcon.Height * scale);
                sb.DrawOnCtrl(this, _xpIcon,
                    new Rectangle((int)center.X, (int)center.Y,
                                  (int)(w * f), h),
                    null, Color.White * 0.85f, rot,
                    new Vector2(_xpIcon.Width / 2f, _xpIcon.Height / 2f),
                    SpriteEffects.None);
                return;
            }

            // fallback bez loga: rámeček s názvem (inkoust)
            string name = _entry.Expansion.Trim();
            float nameSize = Math.Max(12f, _fontSize * 0.78f);
            var nameTex = _tr.RenderLine(name.ToUpperInvariant(), nameSize,
                                         StampInk, bold: true, serif: true);
            if (nameTex == null) return;
            int stampW = nameTex.Width + 30;
            int stampH = nameTex.Height + 18;
            var px = ContentService.Textures.Pixel;

            void Rot(Texture2D tex, Vector2 offset, int w, int h, Color c) {
                var o = Vector2.Transform(offset, Matrix.CreateRotationZ(rot));
                var pos = center + o;
                sb.DrawOnCtrl(this, tex,
                    new Rectangle((int)pos.X, (int)pos.Y, (int)(w * f), h),
                    null, c, rot,
                    new Vector2(tex.Width / 2f, tex.Height / 2f),
                    SpriteEffects.None);
            }

            Rot(px, new Vector2(0, -stampH / 2f), stampW, 2, StampInk);
            Rot(px, new Vector2(0,  stampH / 2f), stampW, 2, StampInk);
            Rot(px, new Vector2(-stampW / 2f, 0), 2, stampH, StampInk);
            Rot(px, new Vector2( stampW / 2f, 0), 2, stampH, StampInk);
            Rot(nameTex, Vector2.Zero, nameTex.Width, nameTex.Height,
                Color.White);
        }
    }
}
