using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Frtal.LorebookReader {

    /// <summary>Výsledek detekce konverzačního dialogu.</summary>
    public sealed class ConversationHit {
        /// <summary>Celý dialog (header + text) — pro umístění tlačítek
        /// a jako oblast, kde hledat NPC label.</summary>
        public Rectangle Panel;
        /// <summary>Přesný výřez NPC textu — přímo pro OCR.
        /// Odvozený z jasných textových pixelů, ne z odhadů.</summary>
        public Rectangle TextArea;
        /// <summary>Podíl jasných pixelů v textové oblasti (0–1).</summary>
        public double Confidence;
    }

    /// <summary>
    /// Detekce konverzačního dialogu v GW2 (v6.3).
    ///
    /// Kotva: hnědý header bar — teplý horizontální pruh (R>G>B) na vrchu
    /// dialogu, s chladným světem NAD ním a jasným textem POD ním.
    ///
    /// Header je jen VERTIKÁLNÍ kotva; hranice OCR výřezu se měří z jasných
    /// textových buněk (top/bottom v levé polovině — NPC label vpravo je
    /// také jasný; dno dynamicky přes mezeru 5+ řádků; left/right přes
    /// celou šířku — nejpravější jasná buňka = konec nejdelšího řádku).
    ///
    /// v6.2 (důkaz dump_20260706_230019 — „no hit" na denní scéně):
    ///  1. DVA PRŮCHODY: bar je průsvitný, nad světlým pozadím jeho jas
    ///     přeleze strict okno 20–80 a warm run se nesloží. Druhý průchod
    ///     povolí jas do 150; strukturální brány (transition, text pod,
    ///     startX) zůstávají — před texturami chrání ony, ne práh jasu.
    ///  2. RETRY: když kandidát headeru neprojde měřením textu, zkouší se
    ///     další řádky (v6.1 vracela null po prvním neúspěchu — jediná
    ///     teplá větev/lampa nad dialogem zabila celou detekci).
    ///  3. Okno hledání textTop rozšířeno na 20 řádků pod kotvou —
    ///     průsvitný pás bývá vysoký a text začíná až pod ním.
    ///  4. LastDiagnostics: čitelný záznam, kolik kandidátů které brány
    ///     odmítly — debug dump ho ukládá do info.txt, takže každý další
    ///     „no hit" rovnou říká proč.
    ///
    /// v6.3 (důkaz dumpy 20260707_111837/_111854 — „scared. It's" /
    /// „probably" uříznuté vpravo): horizontální rozsah textu se měří
    /// souvislým run-em přes textové řádky (tolerance mezer mezi slovy),
    /// ne po hraně headeru — průsvitný header bývá užší než text, proto
    /// se dlouhé řádky ořezávaly.
    /// </summary>
    public static class ConversationDetector {

        private const int Cell = 8;

        // Teplý pixel: R > G > B, R-B ≥ 8, R ≥ 30, jas v okně
        private const int WarmDiffMin = 8;
        private const int LumMin      = 20;
        private const int LumMax      = 80;   // strict průchod
        private const int LumMaxRelaxed = 150; // 2. průchod (průsvitný bar
                                               // nad světlým pozadím)
        private const int RedMin      = 30;

        // Buňka je "warm" pokud ≥ 35 % pixelů je teplých;
        // pro expanzi nalezeného headeru stačí 15 % (poloprůhlednost)
        private const double MinWarmFrac   = 0.35;
        private const double RelaxWarmFrac = 0.15;

        // Jasný pixel (text): lum > 170
        private const int BrightThresh = 170;
        // Buňka je "textová", má-li ≥ 3 jasné pixely
        private const int TextyCellMin = 3;

        // Header: min 25 buněk (~200 px), musí začínat v levých 65 %
        // obrazovky (guard proti minimapě/quest trackeru vpravo)
        private const int    MinHeaderCells  = 25;
        private const double MaxHeaderStartX = 0.65;

        // Transition: řádky 4–6 NAD headerem < 20 % warm buněk
        private const double MaxAboveWarmFrac = 0.20;
        // Potvrzení textu: řádky 4–12 POD ≥ 0.3 % jasných pixelů
        private const double MinTextBrightFrac = 0.003;

        // Text začíná do 20 řádků buněk pod kotvou (průsvitný pás je
        // vysoký; morning bar byl 1 řádek, denní pás až ~6 řádků)
        private const int TextTopWindowRows = 20;

        // Konec textu: 5 po sobě jdoucích prázdných řádků buněk (~40 px)
        // — mezera mezi odstavci je menší, mezera před odpověďmi větší
        private const int TextGapRows = 5;
        // Maximální výška textového pásma v řádcích buněk (~280 px)
        private const int MaxTextRows = 35;

        // Souvislý text: mezera ≤ 4 buňky (~32 px) = mezera mezi slovy;
        // větší mezera odděluje text od ikony knihy / NPC cedulky vpravo,
        // takže se do výřezu nepřitáhnou
        private const int HGapCols = 4;

        /// <summary>Diagnostika posledního běhu FindHit — debug dump ji
        /// zapisuje do info.txt. Čistý string, žádná Blish závislost.</summary>
        public static string LastDiagnostics { get; private set; } = "";

        /// <summary>Zpětně kompatibilní obal — vrací jen panel
        /// (detekční smyčka tlačítek nic víc nepotřebuje).</summary>
        public static Rectangle? Find(Bitmap bmp, out double solidity) {
            var hit = FindHit(bmp);
            solidity = hit?.Confidence ?? 0;
            return hit?.Panel;
        }

        /// <returns>ConversationHit s panelem a přesným text výřezem,
        /// nebo null pokud dialog nebyl nalezen.</returns>
        public static ConversationHit FindHit(Bitmap bmp) {
            int w = bmp.Width, h = bmp.Height;
            int cw = w / Cell, ch = h / Cell;
            if (cw < 20 || ch < 20) { LastDiagnostics = "frame too small"; return null; }

            // 1) Spočítat warm (strict + relaxed) a bright pixely na buňku
            //    v jednom průchodu (celý obraz — text může sahat pod půlku)
            var warmS  = new int[ch, cw];
            var warmR  = new int[ch, cw];
            var bright = new int[ch, cw];
            int cp = Cell * Cell;

            var data = bmp.LockBits(new Rectangle(0, 0, w, h),
                                    ImageLockMode.ReadOnly,
                                    PixelFormat.Format24bppRgb);
            try {
                unsafe {
                    byte* basePtr = (byte*)data.Scan0;
                    int stride = data.Stride;
                    int usableH = ch * Cell, usableW = cw * Cell;

                    for (int y = 0; y < usableH; y++) {
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
                                && r >= RedMin
                                && lum >= LumMin) {
                                if (lum <= LumMax)        warmS[cy, cx]++;
                                if (lum <= LumMaxRelaxed) warmR[cy, cx]++;
                            }
                            if (lum > BrightThresh)
                                bright[cy, cx]++;
                        }
                    }
                }
            } finally {
                bmp.UnlockBits(data);
            }

            // 2) Strict průchod; když nic, relaxed průchod
            var diag = new System.Text.StringBuilder();
            var hit = Scan(warmS, bright, cw, ch, w, cp, diag, "strict")
                   ?? Scan(warmR, bright, cw, ch, w, cp, diag, "relaxed");
            LastDiagnostics = diag.ToString();
            return hit;
        }

        /// <summary>Jeden průchod: najdi kandidáty headeru a u prvního,
        /// který projde i měřením textu, vrať hit. Kandidát, kterému se
        /// nepodaří změřit text, detekci NEukončí (retry na dalších
        /// řádcích).</summary>
        private static ConversationHit Scan(int[,] warm, int[,] bright,
                                            int cw, int ch, int w, int cp,
                                            System.Text.StringBuilder diag,
                                            string pass) {
            int maxCellY = ch / 2; // header hledáme v horní polovině
            int cand = 0, rejStart = 0, rejAbove = 0, rejText = 0,
                rejTop = 0, rejLR = 0;

            for (int cy = 4; cy < maxCellY; cy++) {
                // nejdelší warm run v řádku
                int bestS = -1, bestL = 0, rs = -1, rl = 0;
                for (int cx = 0; cx <= cw; cx++) {
                    bool isW = cx < cw
                        && (double)warm[cy, cx] / cp >= MinWarmFrac;
                    if (isW) {
                        if (rs < 0) rs = cx;
                        rl++;
                    } else {
                        if (rl > bestL) { bestS = rs; bestL = rl; }
                        rs = -1; rl = 0;
                    }
                }
                if (bestL < MinHeaderCells) continue;
                cand++;

                if (bestS * Cell > w * MaxHeaderStartX) { rejStart++; continue; }

                // transition — řádky 4–6 nad musí být chladné
                int aWarm = 0, aTotal = 0;
                for (int dy = 4; dy <= 6; dy++) {
                    int ar = cy - dy;
                    if (ar < 0) continue;
                    for (int cx = bestS; cx < bestS + bestL && cx < cw; cx++) {
                        if ((double)warm[ar, cx] / cp >= MinWarmFrac) aWarm++;
                        aTotal++;
                    }
                }
                if (aTotal > 0 && (double)aWarm / aTotal > MaxAboveWarmFrac) {
                    rejAbove++; continue;
                }

                // potvrzení textu pod headerem
                long tBright = 0, tTotal = 0;
                for (int dy = 4; dy <= 12; dy++) {
                    int br = cy + dy;
                    if (br >= ch) break;
                    for (int cx = bestS; cx < bestS + bestL && cx < cw; cx++) {
                        tBright += bright[br, cx];
                        tTotal += cp;
                    }
                }
                if (tTotal == 0
                    || (double)tBright / tTotal < MinTextBrightFrac) {
                    rejText++; continue;
                }

                // ---- kandidát prošel branami: expanze + měření textu ----

                // expanze headeru relaxed thresholdem — plná šířka baru
                // i tam, kde poloprůhlednost detekci oslabila
                int hL = bestS;
                while (hL > 0
                       && (double)warm[cy, hL - 1] / cp >= RelaxWarmFrac) hL--;
                int hR = bestS + bestL - 1;
                while (hR < cw - 1
                       && (double)warm[cy, hR + 1] / cp >= RelaxWarmFrac) hR++;

                // top/bottom textu — jen LEVÁ polovina (NPC label vpravo)
                int halfR = hL + (hR - hL) / 2;

                int textTop = -1;
                int topLimit = Math.Min(cy + TextTopWindowRows, ch);
                for (int r = cy + 2; r < topLimit; r++) {
                    int cnt = 0;
                    for (int c = hL; c <= halfR && c < cw; c++)
                        if (bright[r, c] >= TextyCellMin) cnt++;
                    if (cnt >= 3) { textTop = r; break; }
                }
                if (textTop < 0) { rejTop++; continue; } // retry níže

                int textBot = textTop, empty = 0;
                for (int r = textTop;
                     r < Math.Min(textTop + MaxTextRows, ch); r++) {
                    int cnt = 0;
                    for (int c = hL; c <= halfR && c < cw; c++)
                        if (bright[r, c] >= TextyCellMin) cnt++;
                    if (cnt < 2) {
                        if (++empty >= TextGapRows) break;
                    } else {
                        empty = 0;
                        textBot = r;
                    }
                }
                textBot++;

                // left/right: text běžně přesahuje (průsvitný) header, tak
                // ho neměř jen po jeho hraně. Vyjdi z headeru a rozšiřuj
                // přes textové řádky s tolerancí mezer mezi slovy; velká
                // mezera run ukončí, takže ikona knihy / NPC cedulka /
                // quest tracker vpravo se do výřezu nepřitáhnou.
                // (důkaz: dumpy 20260707_111837 a _111854 — „scared. It's"
                // a „probably" spadly, protože tR bylo vázané na hR+2.)
                int tL = hL, tR = hR;
                int wideR = Math.Min(cw - 1, hR + (hR - hL));
                int wideL = Math.Max(0, hL - (hR - hL) / 4);
                int gap = 0;
                for (int c = hR; c <= wideR; c++) {
                    bool texty = false;
                    for (int r = textTop; r < textBot && !texty; r++)
                        if (bright[r, c] >= TextyCellMin) texty = true;
                    if (texty) { tR = c; gap = 0; }
                    else if (++gap > HGapCols) break;
                }
                gap = 0;
                for (int c = hL; c >= wideL; c--) {
                    bool texty = false;
                    for (int r = textTop; r < textBot && !texty; r++)
                        if (bright[r, c] >= TextyCellMin) texty = true;
                    if (texty) { tL = c; gap = 0; }
                    else if (++gap > HGapCols) break;
                }
                if (tR <= tL) { rejLR++; continue; } // retry níže
                tL = Math.Max(0, tL - 2);
                tR = Math.Min(cw - 1, tR + 2);

                long areaBright = 0;
                for (int r = textTop; r < textBot; r++)
                    for (int c = tL; c <= tR; c++)
                        areaBright += bright[r, c];

                var textArea = new Rectangle(
                    tL * Cell,
                    Math.Max(0, (textTop - 1) * Cell),
                    (tR - tL + 1) * Cell,
                    (textBot - textTop + 2) * Cell);

                // panel = union(header extent, text area)
                int pL = Math.Min(hL * Cell, textArea.Left);
                int pR = Math.Max((hR + 1) * Cell, textArea.Right);
                int pT = cy * Cell;
                int pB = textArea.Bottom;
                var panel = new Rectangle(pL, pT, pR - pL, pB - pT);

                long areaPx = (long)(textBot - textTop)
                              * (tR - tL + 1) * cp;
                diag.AppendLine($"[{pass}] HIT anchor row {cy}; "
                    + $"candidates={cand} rejected: startX={rejStart} "
                    + $"above={rejAbove} textBelow={rejText} "
                    + $"textTop={rejTop} leftRight={rejLR}");
                return new ConversationHit {
                    Panel      = panel,
                    TextArea   = textArea,
                    Confidence = areaPx > 0 ? (double)areaBright / areaPx : 0
                };
            }

            diag.AppendLine($"[{pass}] no hit; candidates={cand} rejected: "
                + $"startX={rejStart} above={rejAbove} textBelow={rejText} "
                + $"textTop={rejTop} leftRight={rejLR}");
            return null;
        }

        /// <summary>Změří jasný text uvnitř předané zóny (v pixelech
        /// klientské oblasti) — BEZ hledání headeru. Používá se s uživatelskou
        /// kalibrací: zóna říká KDE dialog je, tohle změří přesný výřez textu
        /// (kvůli proměnné výšce odstavce) a vrátí ho i jako panel pro kotvení
        /// tlačítek. Vrací null, když v zóně není žádný jasný text (dialog
        /// nejspíš není otevřený). Confidence = podíl jasných pixelů: text je
        /// řídký (~0,05–0,3), plná obloha ~1,0 → volající si podle toho pozná
        /// „otevřený dialog" od prázdné plochy.</summary>
        public static ConversationHit MeasureInZone(Bitmap bmp, Rectangle zone) {
            int zx = Math.Max(0, zone.X);
            int zy = Math.Max(0, zone.Y);
            int zr = Math.Min(bmp.Width,  zone.Right);
            int zb = Math.Min(bmp.Height, zone.Bottom);
            int zw = zr - zx, zh = zb - zy;
            if (zw < Cell * 4 || zh < Cell) { LastDiagnostics = "zone too small"; return null; }

            int cw = zw / Cell, ch = zh / Cell;
            var bright = new int[ch, cw];

            var data = bmp.LockBits(new Rectangle(zx, zy, cw * Cell, ch * Cell),
                                    ImageLockMode.ReadOnly,
                                    PixelFormat.Format24bppRgb);
            try {
                unsafe {
                    byte* basePtr = (byte*)data.Scan0;
                    int stride = data.Stride;
                    int uH = ch * Cell, uW = cw * Cell;
                    for (int y = 0; y < uH; y++) {
                        byte* row = basePtr + y * stride;
                        int cy = y / Cell;
                        for (int x = 0; x < uW; x++) {
                            byte b = row[x * 3];
                            byte g = row[x * 3 + 1];
                            byte r = row[x * 3 + 2];
                            int lum = (299 * r + 587 * g + 114 * b) / 1000;
                            if (lum > BrightThresh) bright[cy, x / Cell]++;
                        }
                    }
                }
            } finally {
                bmp.UnlockBits(data);
            }

            // svislý rozsah: řádky s ≥ 2 textovými buňkami
            int top = -1, bot = -1;
            for (int r = 0; r < ch; r++) {
                int cnt = 0;
                for (int c = 0; c < cw; c++)
                    if (bright[r, c] >= TextyCellMin) cnt++;
                if (cnt >= 2) { if (top < 0) top = r; bot = r; }
            }
            if (top < 0) {
                LastDiagnostics = "[zone] no bright text — dialog closed?";
                return null;
            }

            // vodorovný rozsah přes textové řádky
            int left = cw, right = 0;
            long areaBright = 0;
            for (int r = top; r <= bot; r++)
                for (int c = 0; c < cw; c++) {
                    if (bright[r, c] >= TextyCellMin) {
                        if (c < left) left = c;
                        if (c > right) right = c;
                    }
                    areaBright += bright[r, c];
                }
            if (right < left) { LastDiagnostics = "[zone] no text columns"; return null; }

            top   = Math.Max(0, top - 1);
            left  = Math.Max(0, left - 1);
            bot   = Math.Min(ch - 1, bot + 1);
            right = Math.Min(cw - 1, right + 1);

            var textArea = new Rectangle(
                zx + left * Cell, zy + top * Cell,
                (right - left + 1) * Cell, (bot - top + 1) * Cell);
            var panel = new Rectangle(zx, zy, zw, zh); // celá zóna → stabilní kotva
            long areaPx = (long)(bot - top + 1) * (right - left + 1) * Cell * Cell;
            LastDiagnostics = $"[zone] text {textArea} in {panel}";
            return new ConversationHit {
                Panel      = panel,
                TextArea   = textArea,
                Confidence = areaPx > 0 ? (double)areaBright / areaPx : 0
            };
        }

        /// <summary>Frakční nouzový výřez — použije se, jen kdyby volající
        /// neměl změřenou TextArea (legacy fallback).</summary>
        public static Rectangle TextCrop(Rectangle box) {
            int skipTop = (int)(box.Height * 0.18);
            int textH   = (int)(box.Height * 0.40);
            return new Rectangle(box.X, box.Y + skipTop,
                                 box.Width, textH);
        }
    }
}
