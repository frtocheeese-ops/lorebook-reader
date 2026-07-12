using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Najde pergamen lorebooků na snímku obrazovky.
    /// Pergamen = velký souvislý blok světlých, BEZBARVÝCH pixelů
    /// (R≈G≈B) s knižním poměrem stran a vysokou vyplněností (solidity).
    /// 1:1 port ověřené Python detekce z prototypu.
    /// </summary>
    public static class ParchmentDetector {

        public const int LumThresh    = 185; // min. jas pixelu (0-255)
        public const int ChromaThresh = 60;  // max. barevnost (max-min RGB)
        public const int Cell         = 8;   // velikost buňky downsamplu

        /// <returns>Obdélník pergamenu v souřadnicích bitmapy, nebo null.</returns>
        public static Rectangle? Find(Bitmap bmp, out double solidity) {
            solidity = 0;
            int w = bmp.Width, h = bmp.Height;
            int cw = w / Cell, ch = h / Cell;
            if (cw < 4 || ch < 4) return null;

            // 1) Spočítat na buňku počet pixelů splňujících masku
            var counts = new int[ch, cw];
            var data = bmp.LockBits(new Rectangle(0, 0, w, h),
                                    ImageLockMode.ReadOnly,
                                    PixelFormat.Format24bppRgb);
            try {
                unsafe {
                    byte* basePtr = (byte*)data.Scan0;
                    int usableH = ch * Cell, usableW = cw * Cell;
                    for (int y = 0; y < usableH; y++) {
                        byte* row = basePtr + y * data.Stride;
                        int cy = y / Cell;
                        for (int x = 0; x < usableW; x++) {
                            byte b = row[x * 3];
                            byte g = row[x * 3 + 1];
                            byte r = row[x * 3 + 2];
                            int lum = (299 * r + 587 * g + 114 * b) / 1000;
                            int max = r > g ? (r > b ? r : b) : (g > b ? g : b);
                            int min = r < g ? (r < b ? r : b) : (g < b ? g : b);
                            if (lum > LumThresh && (max - min) < ChromaThresh)
                                counts[cy, x / Cell]++;
                        }
                    }
                }
            } finally {
                bmp.UnlockBits(data);
            }

            // 2) Buňka je "pergamenová", pokud aspoň 45 % pixelů prošlo
            int cellPixels = Cell * Cell;
            var cells = new bool[ch, cw];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                    cells[y, x] = counts[y, x] >= 0.45 * cellPixels;

            // 3) Flood fill bloby, vybrat nejlepšího kandidáta
            var seen = new bool[ch, cw];
            var stack = new Stack<(int y, int x)>();
            Rectangle? best = null;
            int bestArea = 0;

            for (int sy = 0; sy < ch; sy++) {
                for (int sx = 0; sx < cw; sx++) {
                    if (!cells[sy, sx] || seen[sy, sx]) continue;

                    int minY = sy, maxY = sy, minX = sx, maxX = sx, area = 0;
                    seen[sy, sx] = true;
                    stack.Push((sy, sx));
                    while (stack.Count > 0) {
                        var (y, x) = stack.Pop();
                        area++;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        TryVisit(y - 1, x); TryVisit(y + 1, x);
                        TryVisit(y, x - 1); TryVisit(y, x + 1);

                        void TryVisit(int ny, int nx) {
                            if (ny >= 0 && ny < ch && nx >= 0 && nx < cw
                                && cells[ny, nx] && !seen[ny, nx]) {
                                seen[ny, nx] = true;
                                stack.Push((ny, nx));
                            }
                        }
                    }

                    int bh = maxY - minY + 1, bw = maxX - minX + 1;
                    double sol   = (double)area / (bh * bw);
                    double ratio = (double)bh / bw;
                    double wFrac = (double)bw / cw;
                    double hFrac = (double)bh / ch;
                    bool ok = sol > 0.6
                              && ratio > 0.9 && ratio < 3.0
                              && wFrac > 0.05 && wFrac < 0.85
                              && hFrac > 0.12 && hFrac < 0.98;
                    if (ok && area > bestArea) {
                        bestArea = area;
                        solidity = sol;
                        best = new Rectangle(minX * Cell, minY * Cell,
                                             bw * Cell, bh * Cell);
                    }
                }
            }
            return best;
        }

        /// <summary>Vnitřní výřez pergamenu s výchozími okraji
        /// (3 % strany, 4 % nahoře, 2 % dole — ořízne ozdobný rámeček).</summary>
        public static Rectangle InnerCrop(Rectangle box) =>
            InnerCrop(box, 4.0, 2.0, 3.0);

        /// <summary>Vnitřní výřez pergamenu s uživatelsky nastavitelnými okraji
        /// (v % rozměru boxu). Kladné = ořez dovnitř (schová rámeček), ZÁPORNÉ =
        /// přesah ven — když detekce spodek/okraj knihy podměří a text se jinak
        /// uřízne. Volající musí výsledek oříznout na rozměr snímku.</summary>
        public static Rectangle InnerCrop(Rectangle box,
                double topPct, double bottomPct, double sidePct) {
            int dxs = (int)(box.Width  * sidePct   / 100.0);
            int dyT = (int)(box.Height * topPct    / 100.0);
            int dyB = (int)(box.Height * bottomPct / 100.0);
            return new Rectangle(box.X + dxs, box.Y + dyT,
                                 box.Width - 2 * dxs, box.Height - dyT - dyB);
        }
    }
}
