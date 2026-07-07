using System;
using System.Collections.Generic;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Titulkový overlay vykreslovaný přes GDI (TextRenderer), takže
    /// podporuje plnou diakritiku všech jazyků. Text se zalamuje podle
    /// přesného GDI měření (žádné ořezávání krajů) a kreslí řádek po řádku,
    /// každý centrovaný. Podporuje editační mód (přetažení myší).
    /// </summary>
    public sealed class SubtitleOverlay : Control {

        public const string SampleText =
            "Lorebook Reader — náhled titulků. Přetáhni mě myší.";

        private static readonly Logger Logger =
            Logger.GetLogger<SubtitleOverlay>();

        private readonly TextRenderer _textRenderer;

        private float _fontSize = 24f;
        private int _boxWidth = 600;
        private string _rawText = "";
        private readonly List<string> _lines = new List<string>();

        private bool _editMode;
        private bool _dragging;
        private Point _dragOffset;

        public event EventHandler<EventArgs> PositionEdited;

        public SubtitleOverlay(TextRenderer textRenderer) {
            _textRenderer = textRenderer;
            this.Visible = false;
        }

        public float FontSize {
            get => _fontSize;
            set {
                if (Math.Abs(_fontSize - value) < 0.1f) return;
                _fontSize = value;
                Reflow();
            }
        }

        public int BoxWidth {
            get => _boxWidth;
            set {
                int w = Math.Max(100, value);
                if (_boxWidth == w) return;
                _boxWidth = w;
                Reflow();
            }
        }

        public string SubtitleText {
            get => _rawText;
            set {
                value = value ?? "";
                if (_rawText == value) return;
                _rawText = value;
                Reflow();
            }
        }

        public bool EditMode {
            get => _editMode;
            set {
                if (_editMode == value) return;
                _editMode = value;
                if (_editMode) {
                    SubtitleText = SampleText;
                    this.Visible = true;
                } else {
                    StopDrag(false);
                    SubtitleText = "";
                    this.Visible = false;
                }
            }
        }

        protected override CaptureType CapturesInput() =>
            _editMode ? CaptureType.Mouse : CaptureType.None;

        private void Reflow() {
            try {
                _lines.Clear();
                if (string.IsNullOrEmpty(_rawText)) {
                    this.Size = new Point(_boxWidth, 1);
                    return;
                }
                // pixel-přesné zalamování — jediný wrapper v pipeline
                // (TextCleaner text nezalamuje, jen sanitizuje znaky)
                _lines.AddRange(
                    _textRenderer.WrapText(_rawText, _fontSize, _boxWidth - 16));
                float lh = _textRenderer.LineHeight(_fontSize);
                int height = (int)Math.Ceiling(lh * _lines.Count) + 10;
                this.Size = new Point(_boxWidth, Math.Max(1, height));
            } catch (Exception ex) {
                Logger.Warn(ex, "Subtitle layout failed.");
                _lines.Clear();
                this.Size = new Point(_boxWidth, 1);
            }
        }

        // ----------------------------- drag ------------------------------

        protected override void OnLeftMouseButtonPressed(MouseEventArgs e) {
            base.OnLeftMouseButtonPressed(e);
            if (!_editMode || _dragging) return;
            _dragging = true;
            _dragOffset = new Point(
                GameService.Input.Mouse.Position.X - this.Location.X,
                GameService.Input.Mouse.Position.Y - this.Location.Y);
            GameService.Input.Mouse.MouseMoved += OnGlobalMouseMoved;
            GameService.Input.Mouse.LeftMouseButtonReleased += OnGlobalMouseReleased;
        }

        private void OnGlobalMouseMoved(object sender, MouseEventArgs e) {
            if (!_dragging) return;
            var mouse = GameService.Input.Mouse.Position;
            var sprite = GameService.Graphics.SpriteScreen.Size;
            int x = mouse.X - _dragOffset.X;
            int y = mouse.Y - _dragOffset.Y;
            this.Location = new Point(
                Math.Max(0, Math.Min(x, sprite.X - this.Width)),
                Math.Max(0, Math.Min(y, sprite.Y - this.Height)));
        }

        private void OnGlobalMouseReleased(object sender, MouseEventArgs e) {
            StopDrag(true);
        }

        private void StopDrag(bool fireEvent) {
            if (!_dragging) return;
            _dragging = false;
            GameService.Input.Mouse.MouseMoved -= OnGlobalMouseMoved;
            GameService.Input.Mouse.LeftMouseButtonReleased -= OnGlobalMouseReleased;
            if (fireEvent)
                PositionEdited?.Invoke(this, EventArgs.Empty);
        }

        // ---------------------------- render -----------------------------

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds) {
            try {
                if (_editMode) {
                    spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel,
                        bounds, new Color(0, 0, 0, 120));
                }
                if (_lines.Count == 0) return;

                float lh = _textRenderer.LineHeight(_fontSize);
                float y = bounds.Y + 5;
                var white  = Color.White * this.Opacity;
                var shadow = new Color(0, 0, 0, (int)(210 * this.Opacity));

                foreach (string line in _lines) {
                    if (line.Length == 0) { y += lh; continue; }

                    var shadowTex = _textRenderer.RenderLine(line, _fontSize, shadow);
                    var textTex   = _textRenderer.RenderLine(line, _fontSize, white);
                    if (textTex == null) { y += lh; continue; }

                    int x = bounds.X + (bounds.Width - textTex.Width) / 2;
                    // stín (offset 2px) + text
                    if (shadowTex != null) {
                        spriteBatch.DrawOnCtrl(this, shadowTex,
                            new Rectangle(x + 2, (int)y + 2,
                                          shadowTex.Width, shadowTex.Height));
                    }
                    spriteBatch.DrawOnCtrl(this, textTex,
                        new Rectangle(x, (int)y, textTex.Width, textTex.Height));
                    y += lh;
                }
            } catch (Exception ex) {
                Logger.Warn(ex, "Subtitle paint failed; hiding overlay.");
                this.Visible = false;
            }
        }

        protected override void DisposeControl() {
            StopDrag(false);
            base.DisposeControl();
        }
    }
}
