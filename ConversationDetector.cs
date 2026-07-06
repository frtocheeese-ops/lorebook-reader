using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Detekce konverzačního dialogu v GW2 přes hnědý header bar (v6).
    ///
    /// Strategie: najít PŘECHOD z chladného herního světa do teplého
    /// headeru dialogu. Header je první horizontální warm strip, kde
    /// řádky 4-6 NAD ním jsou chladné (= herní svět, ne textura).
    /// Pod headerem musí být jasné pixely (= text dialogu).
    ///
    /// Toto eliminuje falešné pozitivy z herních textur, protože
    /// textury mají teplé řádky i nad sebou (jsou uprostřed warm plochy),
    /// zatímco dialog header je na HRANICI teplé a chladné oblasti.
    ///
    /// v6: textová oblast pro OCR se MĚŘÍ z reálných jasných buněk
    /// (ConversationHit.TextArea) místo frakcí šířky panelu — frakce
    /// usekávaly slova na okrajích řádků ("scared", "It's", "probably",
    /// "was"). Svislé mantinely zóny zůstávají kalibrované frakce
    /// (nahoře NPC titulek/echo odpovědi, dole response options).
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

        // --- v6: měření TextArea ---
        // Svislá zóna textu uvnitř panelu (kalibrováno ve v5; horní
        // frakce kryje NPC titulek + echo odpovědi, spodní hranice
        // nepustí response options — "Read on." nesmí do OCR)
        private const double ZoneTopFrac    = 0.18;
        private const double ZoneHeightFrac = 0.40;
        // Buňka je "textová" od 3 jasných px (jeden flíček nestačí)
        private const int TextCellBrightMin = 3;
        // Mezera v běhu sloupců ≤ 4 buňky (~32 px) = mezislovní mezera;
        // větší díra odděluje vzdálené jasné UI (quest tracker apod.)
        private const int MaxColumnGapCells = 4;
        // Vyhledávací okno širší než panel (× šířka headeru) — právě
        // přesahy za panelRight usekávaly poslední slova řádků
        private const double SearchLeftFrac  = 0.10;
        private const double SearchRightFrac = 0.20;
        // Padding výřezu v buňkách (WinRT OCR zahazuje slova na hraně)
        private const int PadCellsX = 2;
        private const int PadCellsY = 1;

        /// <summary>Výsledek detekce: panel pro UI + změřená textová
        /// oblast pro OCR.</summary>
        public sealed class ConversationHit {
            public Rectangle Panel;    // celý dialog (tlačítka, NPC jméno)
            public Rectangle TextArea; // změřený text pro OCR výřez
            public double Solidity;    // podíl jasných pixelů pod headerem
        }

        /// <summary>Zpětně kompatibilní obal — vrací jen panel
        /// (detekční smyčka tlačítek nic víc nepotřebuje).</summary>
        public static Rectangle? Find(Bitmap bmp, out double solidity) {
            var hit = FindHit(bmp);
            solidity = hit?.Solidity ?? 0;
            return hit?.Panel;
        }

        /// <returns>Panel + změřená textová oblast, nebo null.</returns>
        public static ConversationHit FindHit(Bitmap bmp) {
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

                var panel = new Rectangle(panelLeft, hy,
                                          panelRight - panelLeft,
                                          panelBot - hy);
                // v6: textovou oblast změřit z jasných buněk — frakce
                // panelu usekávaly slova na okrajích (důkaz: "was" v
                // dump_20260706_093527; dřív "scared"/"It's"/"probably")
                var textArea = MeasureTextArea(brightCounts, cw, ch,
                                               panel, hw, w);
                return new ConversationHit {
                    Panel    = panel,
                    TextArea = textArea,
                    Solidity = textFrac
                };
            }

            return null; // žádný platný header nenalezen
        }

        /// <summary>Frakční výřez (v5) — dnes už jen FALLBACK, když
        /// měření jasných buněk nic nenajde. Primárně se používá
        /// ConversationHit.TextArea.</summary>
        public static Rectangle TextCrop(Rectangle box) {
            int skipTop = (int)(box.Height * ZoneTopFrac);
            int textH   = (int)(box.Height * ZoneHeightFrac);
            return new Rectangle(box.X, box.Y + skipTop,
                                 box.Width, textH);
        }

        /// <summary>v6: změří skutečný rozsah textu z jasných buněk
        /// uvnitř kalibrované svislé zóny. Vodorovně hledá v okně
        /// širším než panel a bere nejdelší běh textových sloupců
        /// (s tolerancí mezislovních mezer) — vzdálené jasné UI tak
        /// nepřilepí, ale useknutá slova za okrajem panelu zachytí.</summary>
        private static Rectangle MeasureTextArea(int[,] brightCounts,
                                                 int cw, int ch,
                                                 Rectangle panel,
                                                 int headerW, int frameW) {
            int zoneTop = panel.Y + (int)(panel.Height * ZoneTopFrac);
            int zoneBot = zoneTop + (int)(panel.Height * ZoneHeightFrac);
            var fallback = new Rectangle(panel.X, zoneTop,
                                         panel.Width, zoneBot - zoneTop);

            int rowStart = zoneTop / Cell;
            int rowEnd   = Math.Min(ch - 1, (zoneBot - 1) / Cell);
            if (rowEnd < rowStart) return fallback;

            int colStart = Math.Max(0,
                panel.X - (int)(headerW * SearchLeftFrac)) / Cell;
            int colEnd = Math.Min(frameW - 1,
                panel.Right + (int)(headerW * SearchRightFrac)) / Cell;
            colEnd = Math.Min(colEnd, cw - 1);
            if (colEnd < colStart) return fallback;

            // 1) sloupce obsahující text
            var colHasText = new bool[cw];
            for (int cx = colStart; cx <= colEnd; cx++) {
                for (int r = rowStart; r <= rowEnd; r++) {
                    if (brightCounts[r, cx] >= TextCellBrightMin) {
                        colHasText[cx] = true;
                        break;
                    }
                }
            }

            // 2) nejdelší běh textových sloupců s tolerancí mezer
            int bestS = -1, bestE = -1, bestLen = -1;
            int runS = -1, lastHit = -1;
            for (int cx = colStart; cx <= colEnd; cx++) {
                if (!colHasText[cx]) continue;
                if (runS < 0) {
                    runS = cx;
                } else if (cx - lastHit > MaxColumnGapCells) {
                    if (lastHit - runS > bestLen) {
                        bestLen = lastHit - runS;
                        bestS = runS; bestE = lastHit;
                    }
                    runS = cx;
                }
                lastHit = cx;
            }
            if (runS >= 0 && lastHit - runS >= bestLen) {
                bestS = runS; bestE = lastHit;
            }
            if (bestS < 0) return fallback; // žádný text -> v5 chování

            // 3) svislé doměření na vítězném běhu (řádek se počítá
            //    od 2 textových buněk — jeden jasný flíček nestačí)
            int minRow = int.MaxValue, maxRow = -1;
            for (int r = rowStart; r <= rowEnd; r++) {
                int cells = 0;
                for (int cx = bestS; cx <= bestE; cx++)
                    if (brightCounts[r, cx] >= TextCellBrightMin) cells++;
                if (cells >= 2) {
                    if (r < minRow) minRow = r;
                    if (r > maxRow) maxRow = r;
                }
            }
            if (maxRow < 0) { minRow = rowStart; maxRow = rowEnd; }

            int left   = Math.Max(0, (bestS - PadCellsX) * Cell);
            int right  = Math.Min(frameW, (bestE + 1 + PadCellsX) * Cell);
            int top    = Math.Max(zoneTop, (minRow - PadCellsY) * Cell);
            int bottom = Math.Min(zoneBot, (maxRow + 1 + PadCellsY) * Cell);
            if (right <= left || bottom <= top) return fallback;

            return new Rectangle(left, top, right - left, bottom - top);
        }
    }
}
