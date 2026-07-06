using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Detekce konverzačního dialogu v GW2 přes hnědý header bar (v5).
    ///
    /// Strategie: najít PŘECHOD z chladného herního světa do teplého
    /// headeru dialogu. Header je první horizontální warm strip, kde
    /// řádky 4-6 NAD ním jsou chladné (= herní svět, ne textura).
    /// Pod headerem musí být jasné pixely (= text dialogu).
    ///
    /// Toto eliminuje falešné pozitivy z herních textur, protože
    /// textury mají teplé řádky i nad sebou (jsou uprostřed warm plochy),
    /// zatímco dialog header je na HRANICI teplé a chladné oblasti.
    /// </summary>
    public static class ConversationDetector {

        private const int Cell = 8;

        // Teplý pixel: R > G > B, R-B ≥ 8, lum 20-80, R ≥ 30
        private const int WarmDiffMin = 8;
        private const int LumMin      = 20;
        private const int LumMax      = 80;
        private const int RedMin      = 30;

        // Buňka je "warm" pokud ≥ 35% pixelů je teplých
        private const double MinWarmFrac = 0.35;
        // Jasný pixel (text): lum > 170
        private const int BrightThresh   = 170;
        // Header: min 25 buněk široký (~200px na 1920, ~330px na 2560)
        private const int MinHeaderCells = 25;

        // Přechod: řádky 4-6 NAD musí mít < 20% warm buněk
        private const double MaxAboveWarmFrac = 0.20;
        // Text: 4-12 řádků POD musí mít ≥ 0.3% jasných pixelů
        private const double MinTextBrightFrac = 0.003;

        /// <returns>Obdélník konverzačního panelu, nebo null.</returns>
        public static Rectangle? Find(Bitmap bmp, out double solidity) {
            solidity = 0;
            int w = bmp.Width, h = bmp.Height;
            int cw = w / Cell, ch = h / Cell;
            if (cw < 20 || ch < 20) return null;

            int maxCellY = ch / 2;

            // 1) Spočítat warm + bright pixely na buňku
            var warmCounts   = new int[ch, cw];
            var brightCounts = new int[ch, cw];
            int cellPixels = Cell * Cell;

            var data = bmp.LockBits(new Rectangle(0, 0, w, h),
                                    ImageLockMode.ReadOnly,
                                    PixelFormat.Format24bppRgb);
            try {
                unsafe {
                    byte* basePtr = (byte*)data.Scan0;
                    int stride = data.Stride;
                    int scanH = Math.Min(ch * Cell, h);
                    int usableW = cw * Cell;

                    for (int y = 0; y < scanH; y++) {
                        byte* row = basePtr + y * stride;
                        int cy = y / Cell;
                        for (int x = 0; x < usableW; x++) {
                            byte b = row[x * 3];
                            byte g = row[x * 3 + 1];
                            byte r = row[x * 3 + 2];
                            int lum = (299 * r + 587 * g + 114 * b) / 1000;
                            int cx = x / Cell;

                            if (r > g && g > b
                                && r - b >= WarmDiffMin
                                && lum >= LumMin && lum <= LumMax
                                && r >= RedMin) {
                                warmCounts[cy, cx]++;
                            }
                            if (lum > BrightThresh) {
                                brightCounts[cy, cx]++;
                            }
                        }
                    }
                }
            } finally {
                bmp.UnlockBits(data);
            }

            // 2) Pro každý řádek najít nejdelší warm strip
            //    Hledat PRVNÍ strip, kde řádky 4-6 NAD jsou chladné
            //    (= přechod z herního světa do dialogu)
            for (int cy = 4; cy < maxCellY; cy++) {
                int bestStart = -1, bestLen = 0;
                int runStart = -1, runLen = 0;

                for (int cx = 0; cx <= cw; cx++) {
                    bool isW = cx < cw
                        && (double)warmCounts[cy, cx] / cellPixels
                           >= MinWarmFrac;
                    if (isW) {
                        if (runStart < 0) runStart = cx;
                        runLen++;
                    } else {
                        if (runLen > bestLen) {
                            bestLen = runLen; bestStart = runStart;
                        }
                        runStart = -1; runLen = 0;
                    }
                }

                if (bestLen < MinHeaderCells) continue;

                // --- Kontrola přechodu: řádky 4-6 NAD ---
                int aboveWarm = 0, aboveTotal = 0;
                for (int dy = 4; dy <= 6; dy++) {
                    int ar = cy - dy;
                    if (ar < 0) continue;
                    for (int cx = bestStart;
                         cx < bestStart + bestLen && cx < cw; cx++) {
                        if ((double)warmCounts[ar, cx] / cellPixels
                            >= MinWarmFrac)
                            aboveWarm++;
                        aboveTotal++;
                    }
                }
                double aboveFrac = aboveTotal > 0
                    ? (double)aboveWarm / aboveTotal : 1;
                if (aboveFrac > MaxAboveWarmFrac) continue;

                // --- Kontrola textu: 4-12 řádků POD ---
                int totalBright = 0, totalPx = 0;
                for (int dy = 4; dy <= 12; dy++) {
                    int br = cy + dy;
                    if (br >= ch) break;
                    for (int cx = bestStart;
                         cx < bestStart + bestLen && cx < cw; cx++) {
                        totalBright += brightCounts[br, cx];
                        totalPx += cellPixels;
                    }
                }
                double textFrac = totalPx > 0
                    ? (double)totalBright / totalPx : 0;
                if (textFrac < MinTextBrightFrac) continue;

                // --- Nalezeno! Odvodit panel. ---
                int hx = bestStart * Cell;
                int hy = cy * Cell;
                int hw = bestLen * Cell;
                // Dialogový text v GW2 sahá výrazně doleva od headeru
                // a mírně doprava. Panel rozšířit štědře na obě strany.
                int panelH = (int)(hw * 0.55);
                int panelBot = Math.Min(h / 2, hy + Cell * 2 + panelH);
                int panelLeft = Math.Max(0, hx - (int)(hw * 0.70));
                int panelRight = Math.Min(w, hx + hw + (int)(hw * 0.25));

                solidity = textFrac;
                return new Rectangle(panelLeft, hy,
                                     panelRight - panelLeft,
                                     panelBot - hy);
            }

            return null; // žádný platný header nenalezen
        }

        /// <summary>Vnitřní výřez — skipne header, vezme NPC text.
        /// Štědřejší okraje než u pergamenu, protože konverzační
        /// text může být širší a vyšší.</summary>
        public static Rectangle TextCrop(Rectangle box) {
            // Header "Read on." / název dialogu zabírá cca 18 % výšky boxu.
            int skipTop = (int)(box.Height * 0.18);
            // Odpovědi ("Read on." / "Close the journal.") začínají
            // kolem 55 % výšky boxu — 0.40 zachytí text, ale ne odpovědi.
            int textH = (int)(box.Height * 0.40);
            return new Rectangle(box.X, box.Y + skipTop,
                                 box.Width, textH);
        }
    }
}
