using System;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Celoobrazovkový overlay pro kalibraci zóny dialogu. Uživatel přetáhne
    /// a roztáhne rámeček přesně přes plochu, kde se objevuje text vyprávění,
    /// a uloží. Zóna se pak používá jako pevný výřez pro OCR a jako kotva
    /// tlačítek — nahrazuje křehké hledání panelu heuristikou.
    ///
    /// Souřadnice jsou ve SpriteScreen prostoru (kde overlay kreslí); převod
    /// na pixely klientské oblasti řeší modul při uložení (callback dostane
    /// XNA Rectangle ve SpriteScreen prostoru).
    /// </summary>
    public sealed class DialogZoneCalibrator : Control {

        private enum Drag { None, Move, TL, TR, BL, BR }

        private const int HandleSize = 16;
        private const int MinW = 80;
        private const int MinH = 32;

        private static readonly Color DimColor    = new Color(0, 0, 0, 115);
        private static readonly Color FillColor   = new Color(90, 170, 255, 38);
        private static readonly Color BorderColor = new Color(120, 200, 255, 255);
        private static readonly Color HandleColor = new Color(255, 232, 150, 255);

        private Rectangle _zone;
        private readonly Action<Rectangle> _onSave;
        private readonly Action _onCancel;

        private Drag _mode = Drag.None;
        private bool _dragging;
        private Point _dragStart;
        private Rectangle _startZone;

        private readonly StandardButton _saveBtn;
        private readonly StandardButton _cancelBtn;
        private readonly Label _hint;

        public DialogZoneCalibrator(Rectangle initialZone,
                                    Action<Rectangle> onSave, Action onCancel) {
            _zone     = initialZone;
            _onSave   = onSave;
            _onCancel = onCancel;

            var screen = GameService.Graphics.SpriteScreen;
            this.Parent   = screen;
            this.Location = Point.Zero;
            this.Size     = screen.Size;
            this.ZIndex   = int.MaxValue - 10;

            _hint = new Label {
                Parent         = screen,
                Text           = "Drag the frame over the dialogue TEXT area "
                                 + "(corners = resize). Exclude 'Read on.'. Then Save.",
                AutoSizeWidth  = true,
                AutoSizeHeight = true,
                ZIndex         = int.MaxValue - 9
            };
            _saveBtn = new StandardButton {
                Parent = screen, Text = "Save zone", Width = 150,
                ZIndex = int.MaxValue - 9
            };
            _cancelBtn = new StandardButton {
                Parent = screen, Text = "Cancel", Width = 110,
                ZIndex = int.MaxValue - 9
            };
            _saveBtn.Click   += (s, e) => _onSave?.Invoke(_zone);
            _cancelBtn.Click += (s, e) => _onCancel?.Invoke();

            ClampZone();
            LayoutChrome();
        }

        protected override CaptureType CapturesInput() => CaptureType.Mouse;

        private static Point Mouse => GameService.Input.Mouse.Position;

        // Tažení přes globální Blish eventy (jako SubtitleOverlay) — NE přes
        // XNA Mouse.GetState(), ta v overlay hlásí Released a tah hned zabíjí.
        protected override void OnLeftMouseButtonPressed(MouseEventArgs e) {
            base.OnLeftMouseButtonPressed(e);
            if (_dragging) return;
            var m = Mouse;
            _mode = HitTest(m);
            if (_mode == Drag.None) return;
            _dragging  = true;
            _dragStart = m;
            _startZone = _zone;
            GameService.Input.Mouse.MouseMoved += OnGlobalMouseMoved;
            GameService.Input.Mouse.LeftMouseButtonReleased += OnGlobalMouseReleased;
        }

        private void OnGlobalMouseMoved(object sender, MouseEventArgs e) {
            if (!_dragging) return;
            var m = Mouse;
            ApplyDrag(m.X - _dragStart.X, m.Y - _dragStart.Y);
            ClampZone();
            LayoutChrome();
        }

        private void OnGlobalMouseReleased(object sender, MouseEventArgs e) => StopDrag();

        private void StopDrag() {
            if (!_dragging) return;
            _dragging = false;
            _mode = Drag.None;
            GameService.Input.Mouse.MouseMoved -= OnGlobalMouseMoved;
            GameService.Input.Mouse.LeftMouseButtonReleased -= OnGlobalMouseReleased;
        }

        public override void DoUpdate(GameTime gameTime) {
            var sz = GameService.Graphics.SpriteScreen.Size;
            if (this.Size != sz) this.Size = sz;
        }

        private Drag HitTest(Point m) {
            if (InHandle(m, _zone.Left,  _zone.Top))    return Drag.TL;
            if (InHandle(m, _zone.Right, _zone.Top))    return Drag.TR;
            if (InHandle(m, _zone.Left,  _zone.Bottom)) return Drag.BL;
            if (InHandle(m, _zone.Right, _zone.Bottom)) return Drag.BR;
            if (_zone.Contains(m))                      return Drag.Move;
            return Drag.None;
        }

        private static bool InHandle(Point m, int cx, int cy) =>
            Math.Abs(m.X - cx) <= HandleSize && Math.Abs(m.Y - cy) <= HandleSize;

        private void ApplyDrag(int dx, int dy) {
            var z = _startZone;
            switch (_mode) {
                case Drag.Move: z.X += dx; z.Y += dy; break;
                case Drag.BR:   z.Width += dx; z.Height += dy; break;
                case Drag.TL:   z.X += dx; z.Y += dy; z.Width -= dx; z.Height -= dy; break;
                case Drag.TR:   z.Y += dy; z.Width += dx; z.Height -= dy; break;
                case Drag.BL:   z.X += dx; z.Width -= dx; z.Height += dy; break;
            }
            _zone = z;
        }

        private void ClampZone() {
            var sz = GameService.Graphics.SpriteScreen.Size;
            if (_zone.Width  < MinW) _zone.Width  = MinW;
            if (_zone.Height < MinH) _zone.Height = MinH;
            if (_zone.Width  > sz.X) _zone.Width  = sz.X;
            if (_zone.Height > sz.Y) _zone.Height = sz.Y;
            if (_zone.X < 0) _zone.X = 0;
            if (_zone.Y < 0) _zone.Y = 0;
            if (_zone.Right  > sz.X) _zone.X = Math.Max(0, sz.X - _zone.Width);
            if (_zone.Bottom > sz.Y) _zone.Y = Math.Max(0, sz.Y - _zone.Height);
        }

        private void LayoutChrome() {
            var sz = GameService.Graphics.SpriteScreen.Size;
            _hint.Location = new Point(Math.Max(8, _zone.X),
                                       Math.Max(8, _zone.Y - 28));
            int by = Math.Min(sz.Y - 40, _zone.Bottom + 8);
            _cancelBtn.Location = new Point(
                Math.Max(8, _zone.Right - _cancelBtn.Width), by);
            _saveBtn.Location = new Point(
                Math.Max(8, _zone.Right - _cancelBtn.Width - _saveBtn.Width - 8), by);
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds) {
            var px = ContentService.Textures.Pixel;
            int L = _zone.Left, T = _zone.Top, R = _zone.Right, B = _zone.Bottom;

            // ztmavit okolí zóny (4 pruhy — zóna zůstane „vyříznutá")
            spriteBatch.DrawOnCtrl(this, px, new Rectangle(0, 0, bounds.Width, T), DimColor);
            spriteBatch.DrawOnCtrl(this, px, new Rectangle(0, B, bounds.Width, bounds.Height - B), DimColor);
            spriteBatch.DrawOnCtrl(this, px, new Rectangle(0, T, L, B - T), DimColor);
            spriteBatch.DrawOnCtrl(this, px, new Rectangle(R, T, bounds.Width - R, B - T), DimColor);

            // jemná výplň + rám
            spriteBatch.DrawOnCtrl(this, px, _zone, FillColor);
            spriteBatch.DrawOnCtrl(this, px, new Rectangle(L, T, _zone.Width, 2), BorderColor);
            spriteBatch.DrawOnCtrl(this, px, new Rectangle(L, B - 2, _zone.Width, 2), BorderColor);
            spriteBatch.DrawOnCtrl(this, px, new Rectangle(L, T, 2, _zone.Height), BorderColor);
            spriteBatch.DrawOnCtrl(this, px, new Rectangle(R - 2, T, 2, _zone.Height), BorderColor);

            // rohové úchyty
            DrawHandle(spriteBatch, px, L, T);
            DrawHandle(spriteBatch, px, R, T);
            DrawHandle(spriteBatch, px, L, B);
            DrawHandle(spriteBatch, px, R, B);
        }

        private void DrawHandle(SpriteBatch sb, Texture2D px, int cx, int cy) =>
            sb.DrawOnCtrl(this, px, new Rectangle(
                cx - HandleSize / 2, cy - HandleSize / 2, HandleSize, HandleSize),
                HandleColor);

        protected override void DisposeControl() {
            StopDrag();
            _hint?.Dispose();
            _saveBtn?.Dispose();
            _cancelBtn?.Dispose();
            base.DisposeControl();
        }
    }
}
