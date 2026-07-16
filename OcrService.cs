using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Rozpoznání textu vestavěným Windows OCR (Windows.Media.Ocr).
    /// Stejný engine, který používal Python prototyp přes winocr.
    /// </summary>
    public static class OcrService {

        /// <param name="languageTag">např. "en-US", "de-DE", "fr-FR", "es-ES"</param>
        /// <param name="invert">true pro světlý text na tmavém pozadí (konverzace)</param>
        public static async Task<string> RecognizeAsync(Bitmap source, string languageTag,
                                                        bool invert = false) {
            using (Bitmap prepared = Preprocess(source, invert))
            using (var ms = new MemoryStream()) {
                prepared.Save(ms, ImageFormat.Bmp);
                ms.Position = 0;

                var decoder = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
                using (SoftwareBitmap sb = await decoder.GetSoftwareBitmapAsync(
                           BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)) {

                    OcrEngine engine =
                        OcrEngine.TryCreateFromLanguage(
                            new Windows.Globalization.Language(languageTag))
                        ?? OcrEngine.TryCreateFromUserProfileLanguages();

                    if (engine == null)
                        throw new InvalidOperationException(
                            $"Windows OCR language pack for '{languageTag}' is not installed.");

                    OcrResult result = await engine.RecognizeAsync(sb);
                    return AssembleWithParagraphs(result);
                }
            }
        }

        /// <summary>OCR krátkého jednořádkového textu (název knihy v hlavičce,
        /// NPC jméno na tmavém labelu — pak s invert=true).</summary>
        public static async Task<string> RecognizeLineAsync(
                Bitmap source, string languageTag, bool invert = false) {
            string text = await RecognizeAsync(source, languageTag, invert)
                .ConfigureAwait(false);
            // sloučit do jednoho řádku, ořezat nesmysly
            string line = (text ?? "").Replace("\r", " ").Replace("\n", " ");
            line = System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " ");
            return line.Trim();
        }

        /// <summary>Složí řádky OCR do textu a rekonstruuje strukturu:
        /// 1) předěly odstavců — svislá mezera výrazně větší než běžná
        ///    rozteč (medián) → prázdný řádek (\n\n);
        /// 2) NADPISY — výrazně kratší řádek, po kterém text pokračuje bez
        ///    mezery (kurzívní/tučné titulky sekcí, např. „The Flamebearer"),
        ///    se odělí jako vlastní odstavec, aby ho reflow nevléval do věty.
        /// Poměry mezera/rozteč i šířka/medián jsou bezrozměrné, takže prahy
        /// platí nezávisle na rozlišení.</summary>
        private static string AssembleWithParagraphs(OcrResult result) {
            var lines = result?.Lines;
            if (lines == null || lines.Count == 0) return "";

            int n = lines.Count;
            var tops   = new List<double>(n);
            var widths = new List<double>(n);
            foreach (var line in lines) {
                double top = double.MaxValue, l = double.MaxValue, r = 0;
                foreach (var word in line.Words) {
                    var b = word.BoundingRect;
                    if (b.Y < top) top = b.Y;
                    if (b.X < l) l = b.X;
                    if (b.X + b.Width > r) r = b.X + b.Width;
                }
                tops.Add(top == double.MaxValue ? 0 : top);
                widths.Add(r > l ? r - l : 0);
            }

            var deltas = new List<double>(Math.Max(0, n - 1));
            for (int i = 1; i < n; i++)
                deltas.Add(tops[i] - tops[i - 1]);
            double pitch = Median(deltas);    // běžná rozteč (0 při <2 řádcích)
            double medWidth = Median(widths); // typická šířka plného řádku

            // nadpis: řádek < 62 % typické šířky, po němž následuje řádek
            // v běžné rozteči (poslední krátký řádek odstavce nadpis není —
            // po něm přijde mezera nebo nic)
            bool IsHeading(int i) =>
                pitch > 0 && medWidth > 0 && n >= 3
                && widths[i] < medWidth * 0.62
                && i + 1 < n && (tops[i + 1] - tops[i]) <= pitch * 1.5;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++) {
                if (i > 0) {
                    double delta = tops[i] - tops[i - 1];
                    bool brk = (pitch > 0 && delta > pitch * 1.5)
                               || IsHeading(i - 1)   // za nadpisem
                               || IsHeading(i);      // před nadpisem
                    sb.Append(brk ? "\n\n" : "\n");
                }
                sb.Append(lines[i].Text);
            }
            return sb.ToString();
        }

        private static double Median(List<double> xs) {
            if (xs == null || xs.Count == 0) return 0;
            var s = xs.OrderBy(x => x).ToList();
            int n = s.Count;
            return (n % 2 == 1) ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2.0;
        }

        /// <summary>Grayscale + 2x upscale. Volitelná inverze pro
        /// konverzace (světlý text na tmavém pozadí → tmavý na světlém).</summary>
        private static Bitmap Preprocess(Bitmap src, bool invert = false) {
            var dst = new Bitmap(src.Width * 2, src.Height * 2,
                                 PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(dst)) {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                ColorMatrix matrix;
                if (invert) {
                    // grayscale + inverze v jednom kroku:
                    // výsledek = 1.0 - (0.299R + 0.587G + 0.114B)
                    matrix = new ColorMatrix(new[] {
                        new float[] { -.299f, -.299f, -.299f, 0, 0 },
                        new float[] { -.587f, -.587f, -.587f, 0, 0 },
                        new float[] { -.114f, -.114f, -.114f, 0, 0 },
                        new float[] { 0, 0, 0, 1, 0 },
                        new float[] { 1, 1, 1, 0, 1 }
                    });
                } else {
                    // standardní grayscale (pro pergamenové lorebooky)
                    matrix = new ColorMatrix(new[] {
                        new float[] { .299f, .299f, .299f, 0, 0 },
                        new float[] { .587f, .587f, .587f, 0, 0 },
                        new float[] { .114f, .114f, .114f, 0, 0 },
                        new float[] { 0, 0, 0, 1, 0 },
                        new float[] { 0, 0, 0, 0, 1 }
                    });
                }
                using (var attrs = new ImageAttributes()) {
                    attrs.SetColorMatrix(matrix);
                    g.DrawImage(src,
                        new Rectangle(0, 0, dst.Width, dst.Height),
                        0, 0, src.Width, src.Height,
                        GraphicsUnit.Pixel, attrs);
                }
            }
            return dst;
        }
    }
}
