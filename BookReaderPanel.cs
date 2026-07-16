using System;
using System.Collections.Generic;
using Blish_HUD;
using Blish_HUD.Controls;
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
        private const int PadY = 20;
        private const double TurnMs = 420;

        private readonly TextRenderer _tr;
        private readonly Texture2D _parchment;

        private LorebookEntry _entry;
        private string _body = "";
        private Texture2D _xpIcon;
        private float _fontSize = 18f;

        // stránky: seznam řádků (text, nadpis?, mezera-před?)
        private struct Line { public string Text; public bool Head; public bool Gap; }
        private readonly List<List<Line>> _pages = new List<List<Line>>();
        private int _page;          // 0 = obálka, 1..N = _pages[i-1]
        private int _lastLayoutW = -1, _lastLayoutH = -1;

        // animace otočení
        private bool _turning;
        private double _turnT;      // 0..1
        private int _turnDir;       // +1 dopředu, -1 zpět
        private int _pendingPage;

        private readonly StandardButton _prevBtn;
        private readonly StandardButton _nextBtn;

        public BookReaderPanel(TextRenderer tr, Texture2D parchment) {
            _tr = tr;
            _parchment = parchment;
            _prevBtn = new StandardButton {
                Parent = this, Text = "‹", Width = 30, Visible = false
            };
            _nextBtn = new StandardButton {
                Parent = this, Text = "›", Width = 30, Visible = false
            };
            _prevBtn.Click += (s, e) => Turn(-1);
            _nextBtn.Click += (s, e) => Turn(+1);
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
            if (_entry == null || w <= PadX * 2 + 20 || h <= PadY * 2 + 20) return;

            int wrapW = w - PadX * 2;
            float lineH = _tr.LineHeight(_fontSize);
            float headH = _tr.LineHeight(_fontSize + 2, bold: true);
            float gapH  = lineH * 0.55f;
            float availH = h - PadY * 2 - 18; // rezerva na číslo stránky

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
            // než existuje druhé (crash 2026-07-16, NRE v UpdateButtons)
            if (_prevBtn == null || _nextBtn == null) return;
            _prevBtn.Visible = _entry != null && _page > 0;
            _nextBtn.Visible = _entry != null && _page < _pages.Count;
            _prevBtn.Location = new Point(4, this.Height / 2 - 13);
            _nextBtn.Location = new Point(this.Width - 34, this.Height / 2 - 13);
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
                if (_turnT < 0.5) {
                    f = 1f - (float)(_turnT * 2);         // 1 → 0
                    anchorLeft = _turnDir > 0;            // dopředu: sevři vlevo
                } else {
                    f = (float)((_turnT - 0.5) * 2);      // 0 → 1
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
            float y = bounds.Y + PadY;
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
            string title = _entry.DisplayTitle ?? "";
            float titleSize = Math.Max(20f, _fontSize * 1.7f);
            var titleLines = _tr.WrapText(title, titleSize,
                (int)(fullW * 0.72f), bold: true, serif: true);
            float th = _tr.LineHeight(titleSize, bold: true, serif: true);

            float blockH = titleLines.Count * th + 34
                           + (HasExpansion() ? (_xpIcon != null ? 160 : 92) : 0);
            float y = bounds.Y + Math.Max(PadY + 6,
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

            // razítko datadisku (jen když je datadisk vyplněný)
            if (HasExpansion())
                PaintStamp(sb, bounds, page, f, anchorLeft,
                    (int)y + (_xpIcon != null ? 62 : 26));

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
