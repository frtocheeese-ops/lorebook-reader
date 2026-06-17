using System;
using System.Collections.Generic;
using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Vnitřní vykreslovací prvek: nakreslí pergamen + text přes GDI.
    /// Jeho výška se nastavuje podle počtu zalomených řádků, takže
    /// rodičovský Panel s CanScroll kolem něj scrolluje automaticky.
    /// </summary>
    internal sealed class ParchmentContent : Control {

        private static readonly Logger Logger =
            Logger.GetLogger<ParchmentContent>();

        private readonly TextRenderer _textRenderer;
        private readonly Texture2D _parchment;
        private readonly List<string> _lines = new List<string>();

        private string _text = "";
        private float _fontSize = 18f;
        private int _wrapWidth = 400;

        private const int PadX = 18;
        private const int PadY = 14;
        private static readonly Color InkColor = new Color(48, 36, 20);

        public ParchmentContent(TextRenderer tr, Texture2D parchment) {
            _textRenderer = tr;
            _parchment = parchment;
        }

        public void SetContent(string text, float fontSize, int wrapWidth) {
            _text = text ?? "";
            _fontSize = fontSize;
            _wrapWidth = Math.Max(50, wrapWidth);
            Relayout();
        }

        public void SetWrapWidth(int wrapWidth) {
            int w = Math.Max(50, wrapWidth);
            if (w == _wrapWidth) return;
            _wrapWidth = w;
            Relayout();
        }

        public void SetFontSize(float fontSize) {
            if (Math.Abs(_fontSize - fontSize) < 0.1f) return;
            _fontSize = fontSize;
            Relayout();
        }

        private void Relayout() {
            try {
                _lines.Clear();
                _lines.AddRange(_textRenderer.WrapText(
                    _text, _fontSize, _wrapWidth - PadX * 2));
                float lh = _textRenderer.LineHeight(_fontSize);
                int height = (int)Math.Ceiling(lh * Math.Max(1, _lines.Count)) + PadY * 2;
                this.Size = new Point(_wrapWidth, height);
            } catch (Exception ex) {
                Logger.Warn(ex, "Parchment relayout failed.");
            }
        }

        protected override void Paint(SpriteBatch spriteBatch, XnaRectangle bounds) {
            try {
                if (_parchment != null) {
                    spriteBatch.DrawOnCtrl(this, _parchment, bounds,
                        new XnaRectangle(0, 0, bounds.Width, bounds.Height),
                        Color.White);
                }
                float lh = _textRenderer.LineHeight(_fontSize);
                float y = bounds.Y + PadY;
                foreach (string line in _lines) {
                    if (line.Length > 0) {
                        var tex = _textRenderer.RenderLine(line, _fontSize, InkColor);
                        if (tex != null) {
                            spriteBatch.DrawOnCtrl(this, tex,
                                new XnaRectangle(bounds.X + PadX, (int)y,
                                                 tex.Width, tex.Height));
                        }
                    }
                    y += lh;
                }
            } catch (Exception ex) {
                Logger.Warn(ex, "Parchment paint failed.");
            }
        }
    }

    /// <summary>
    /// Pergamenový textový panel: rolovatelný Panel obsahující jeden
    /// vykreslovací child (ParchmentContent). Reaguje na změnu velikosti
    /// rodiče i na nastavení velikosti písma.
    /// </summary>
    public sealed class ParchmentTextPanel : Panel {

        private readonly ParchmentContent _content;
        private float _fontSize = 18f;
        private string _text = "";

        public ParchmentTextPanel(TextRenderer tr, Texture2D parchment) {
            this.CanScroll = true;
            this.ShowBorder = true;
            _content = new ParchmentContent(tr, parchment) {
                Parent = this,
                Location = Point.Zero
            };
        }

        public float FontSize {
            get => _fontSize;
            set {
                if (Math.Abs(_fontSize - value) < 0.1f) return;
                _fontSize = value;
                _content.SetFontSize(_fontSize);
            }
        }

        public string Text {
            get => _text;
            set {
                _text = value ?? "";
                _content.SetContent(_text, _fontSize, EffectiveWidth());
            }
        }

        private int EffectiveWidth() =>
            Math.Max(50, this.Width - 20); // rezerva na scrollbar

        /// <summary>Vynutí zalomení textu na aktuální šířku (po buildu/resize).</summary>
        public void ApplyWrap() {
            _content?.SetContent(_text, _fontSize, EffectiveWidth());
        }

        public override void RecalculateLayout() {
            base.RecalculateLayout();
            _content?.SetWrapWidth(EffectiveWidth());
        }
    }
}
