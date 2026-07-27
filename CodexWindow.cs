using System;
using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Okno codexu. Jediný důvod pro vlastní třídu: <see cref="WindowBase2"/>
    /// natvrdo omezuje zvětšování na 1024×1024 px
    /// (WindowBase2.HandleWindowResize), takže se okno nedalo roztáhnout přes
    /// větší monitor — hlášeno uživateli 26.7.2026. Blish to řeší přesně
    /// tímto způsobem: podědit a přepsat HandleWindowResize
    /// (doporučil Freesnow, autor Blish HUD).
    /// </summary>
    public sealed class CodexWindow : StandardWindow {

        // pod tuhle velikost nemá smysl jít — rail, seznam i kniha vedle sebe
        private const int MinWidth  = 640;
        private const int MinHeight = 480;

        public CodexWindow(Texture2D background, Rectangle windowRegion,
                           Rectangle contentRegion, Point windowSize)
            : base(background, windowRegion, contentRegion, windowSize) { }

        /// <summary>Strop posouvá na velikost herní plochy, takže okno může
        /// vyplnit i 4K obrazovku. Minimum drží okno použitelné.</summary>
        protected override Point HandleWindowResize(Point newSize) {
            var screen = GameService.Graphics.SpriteScreen;
            int maxW = Math.Max(MinWidth,  screen?.Width  ?? MinWidth);
            int maxH = Math.Max(MinHeight, screen?.Height ?? MinHeight);

            return new Point(MathHelper.Clamp(newSize.X, MinWidth,  maxW),
                             MathHelper.Clamp(newSize.Y, MinHeight, maxH));
        }
    }
}
