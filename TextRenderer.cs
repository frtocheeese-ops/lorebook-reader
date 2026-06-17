using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Vykreslí text přes GDI+ (System.Drawing) do XNA textury.
    /// Důvod: vestavěné GW2 bitmap fonty nemají diakritiku ani znaky
    /// jiných jazyků. GDI umí jakýkoli nainstalovaný Windows font s plnou
    /// Unicode sadou, takže titulky i UI fungují česky, německy, japonsky...
    ///
    /// Výsledky se cachují (stejný text+font+barva = stejná textura),
    /// takže se nerenderuje každý snímek. Textury se uvolňují přes Dispose.
    /// </summary>
    public sealed class TextRenderer : IDisposable {

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, Texture2D> _cache =
            new Dictionary<string, Texture2D>();
        private readonly Queue<string> _cacheOrder = new Queue<string>();
        private const int MaxCache = 60;

        // písmo blízké GW2 estetice; fallback řetězec, kdyby chybělo
        private static readonly string[] FontCandidates = {
            "Cantarell", "Segoe UI", "Tahoma", "Arial"
        };
        private static readonly string ResolvedFamily = ResolveFontFamily();

        public TextRenderer(GraphicsDevice graphicsDevice) {
            _graphicsDevice = graphicsDevice;
        }

        /// <summary>
        /// Vykreslí (případně vrátí z cache) jeden řádek textu jako texturu.
        /// </summary>
        public Texture2D RenderLine(string text, float fontSize, XnaColor color,
                                    bool bold = false) {
            if (string.IsNullOrEmpty(text)) return null;
            string key = $"{fontSize}|{(bold ? 1 : 0)}|{color.PackedValue}|{text}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            Texture2D tex = Render(text, fontSize, color, bold);
            _cache[key] = tex;
            _cacheOrder.Enqueue(key);
            if (_cacheOrder.Count > MaxCache) {
                string old = _cacheOrder.Dequeue();
                if (_cache.TryGetValue(old, out var oldTex)) {
                    _cache.Remove(old);
                    oldTex.Dispose();
                }
            }
            return tex;
        }

        /// <summary>Změří šířku textu bez vykreslení (pro zalamování).</summary>
        public float MeasureWidth(string text, float fontSize, bool bold = false) {
            if (string.IsNullOrEmpty(text)) return 0;
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            using (var font = MakeFont(fontSize, bold)) {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                return g.MeasureString(text, font,
                    int.MaxValue, StringFormat.GenericTypographic).Width;
            }
        }

        /// <summary>Zalomí text na řádky podle skutečné GDI šířky.</summary>
        public List<string> WrapText(string text, float fontSize, int maxWidth,
                                     bool bold = false) {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            foreach (string paragraph in text.Replace("\r", "").Split('\n')) {
                if (paragraph.Length == 0) { lines.Add(""); continue; }
                string[] words = paragraph.Split(' ');
                string current = "";
                foreach (string word in words) {
                    string candidate = current.Length == 0
                        ? word : current + " " + word;
                    if (MeasureWidth(candidate, fontSize, bold) <= maxWidth
                        || current.Length == 0) {
                        current = candidate;
                    } else {
                        lines.Add(current);
                        current = word;
                    }
                }
                if (current.Length > 0) lines.Add(current);
            }
            return lines;
        }

        public float LineHeight(float fontSize, bool bold = false) {
            using (var font = MakeFont(fontSize, bold))
                return font.GetHeight();
        }

        // ---------------------------------------------------------------------

        private Texture2D Render(string text, float fontSize, XnaColor color,
                                 bool bold) {
            using (var font = MakeFont(fontSize, bold)) {
                int w, h;
                using (var measureBmp = new Bitmap(1, 1))
                using (var mg = Graphics.FromImage(measureBmp)) {
                    var size = mg.MeasureString(text, font,
                        int.MaxValue, StringFormat.GenericTypographic);
                    w = Math.Max(1, (int)Math.Ceiling(size.Width) + 4);
                    h = Math.Max(1, (int)Math.Ceiling(size.Height) + 4);
                }

                using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(bmp)) {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);

                    var brush = new SolidBrush(
                        Color.FromArgb(color.A, color.R, color.G, color.B));
                    g.DrawString(text, font, brush, 2, 2,
                        StringFormat.GenericTypographic);
                    brush.Dispose();

                    return BitmapToTexture(bmp);
                }
            }
        }

        private Texture2D BitmapToTexture(Bitmap bmp) {
            var data = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try {
                int byteCount = data.Stride * bmp.Height;
                byte[] bytes = new byte[byteCount];
                System.Runtime.InteropServices.Marshal.Copy(
                    data.Scan0, bytes, 0, byteCount);

                // GDI je BGRA, XNA chce RGBA + premultiplied alpha
                for (int i = 0; i < byteCount; i += 4) {
                    byte b = bytes[i];
                    byte gg = bytes[i + 1];
                    byte r = bytes[i + 2];
                    byte a = bytes[i + 3];
                    bytes[i]     = (byte)(r * a / 255);
                    bytes[i + 1] = (byte)(gg * a / 255);
                    bytes[i + 2] = (byte)(b * a / 255);
                    bytes[i + 3] = a;
                }

                var tex = new Texture2D(_graphicsDevice, bmp.Width, bmp.Height,
                    false, SurfaceFormat.Color);
                tex.SetData(bytes);
                return tex;
            } finally {
                bmp.UnlockBits(data);
            }
        }

        private static Font MakeFont(float size, bool bold) =>
            new Font(ResolvedFamily, size,
                bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);

        private static string ResolveFontFamily() {
            try {
                using (var installed = new InstalledFontCollection()) {
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var fam in installed.Families)
                        names.Add(fam.Name);
                    foreach (string candidate in FontCandidates)
                        if (names.Contains(candidate))
                            return candidate;
                }
            } catch { /* fallback níže */ }
            return "Arial";
        }

        public void Dispose() {
            foreach (var tex in _cache.Values)
                tex.Dispose();
            _cache.Clear();
            _cacheOrder.Clear();
        }
    }
}
