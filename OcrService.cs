using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
                    return string.Join("\n", result.Lines.Select(l => l.Text));
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
