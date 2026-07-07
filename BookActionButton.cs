using System;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Akční tlačítko vedle detekované knihy/konverzace (číst / uložit /
    /// připojit). Oproti holé ikoně kreslí decentní tmavý podklad (bez
    /// rámu, průhlednější — na přání), aby ikona byla čitelná na
    /// pergamenu i světlé scéně, a dává odezvu na hover i kliknutí.
    /// Ikony se načítají jednou v konstruktoru — žádné opakované
    /// GetTexture při každém přejetí myší.
    /// </summary>
    public sealed class BookActionButton : Control {

        private readonly Texture2D _icon;
        private readonly Texture2D _iconHover;
        private readonly Action _onClick;

        private bool _hovered;
        private bool _pressed;

        // vzhled: jen decentní tmavý podklad, bez rámu; vyšší průhlednost
        // (na přání) — ikona nese hlavní čitelnost
        private static readonly Color BgNormal  = new Color(10, 8, 4, 110);
        private static readonly Color BgHover   = new Color(30, 23, 12, 160);
        private static readonly Color BgPressed = new Color(6, 4, 2, 180);
        private const int IconPad = 4; // ikona 32px v 40px tlačítku

        public BookActionButton(Texture2D icon, Texture2D iconHover,
                                string tooltip, Action onClick) {
            _icon = icon;
            _iconHover = iconHover ?? icon;
            _onClick = onClick;
            this.Size = new Point(40, 40);
            this.Visible = false;
            this.BasicTooltipText = tooltip;
        }

        protected override CaptureType CapturesInput() => CaptureType.Mouse;

        protected override void OnMouseEntered(MouseEventArgs e) {
            base.OnMouseEntered(e);
            _hovered = true;
        }

        protected override void OnMouseLeft(MouseEventArgs e) {
            base.OnMouseLeft(e);
            _hovered = false;
            _pressed = false;
        }

        protected override void OnLeftMouseButtonPressed(MouseEventArgs e) {
            base.OnLeftMouseButtonPressed(e);
            _pressed = true;
        }

        protected override void OnLeftMouseButtonReleased(MouseEventArgs e) {
            base.OnLeftMouseButtonReleased(e);
            if (_pressed) {
                _pressed = false;
                _onClick?.Invoke();
            }
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds) {
            // podklad
            Color bg = _pressed ? BgPressed : (_hovered ? BgHover : BgNormal);
            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel,
                bounds, bg);

            // ikona (při stisku o 1px níž — hmatový dojem)
            var tex = _hovered ? _iconHover : _icon;
            if (tex != null) {
                int off = _pressed ? 1 : 0;
                var iconRect = new Rectangle(
                    bounds.X + IconPad + off,
                    bounds.Y + IconPad + off,
                    bounds.Width - 2 * IconPad,
                    bounds.Height - 2 * IconPad);
                spriteBatch.DrawOnCtrl(this, tex, iconRect);
            }
        }
    }
}
