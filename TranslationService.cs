using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Strojový překlad přes neoficiální Google Translate endpoint
    /// (client=gtx — stejný, který používá knihovna googletrans).
    /// Bez API klíče. Vrací čisté JSON pole, parsuje se jednoduše.
    /// Při jakémkoli problému vyhodí výjimku — volající nechá originál.
    /// </summary>
    public static class TranslationService {

        /// <summary>Cílové jazyky nabízené v nastavení: (kód, popis).</summary>
        public static readonly (string Code, string Name)[] TargetLanguages = {
            ("cs", "Czech (Čeština)"),
            ("de", "German (Deutsch)"),
            ("es", "Spanish (Español)"),
            ("fr", "French (Français)"),
            ("it", "Italian (Italiano)"),
            ("pl", "Polish (Polski)"),
            ("pt", "Portuguese (Português)"),
            ("ru", "Russian (Русский)"),
            ("ja", "Japanese (日本語)"),
            ("ko", "Korean (한국어)"),
            ("zh-CN", "Chinese (中文)")
        };

        private const string Endpoint =
            "https://translate.googleapis.com/translate_a/single";

        static TranslationService() {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        /// <param name="text">zdrojový text</param>
        /// <param name="targetLang">cílový kód, např. "cs"</param>
        /// <param name="sourceLang">zdrojový kód, nebo "auto"</param>
        public static async Task<string> TranslateAsync(
                string text, string targetLang,
                string sourceLang = "auto",
                CancellationToken ct = default) {

            if (string.IsNullOrWhiteSpace(text))
                return text;

            string url = Endpoint
                + "?client=gtx"
                + "&sl=" + Uri.EscapeDataString(sourceLang)
                + "&tl=" + Uri.EscapeDataString(targetLang)
                + "&dt=t"
                + "&q=" + Uri.EscapeDataString(text);

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/130.0.0.0 Safari/537.36";
            request.Timeout = 15000;

            using (ct.Register(() => { try { request.Abort(); } catch { } }))
            using (var response = (HttpWebResponse)
                       await request.GetResponseAsync().ConfigureAwait(false))
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                string json = await reader.ReadToEndAsync().ConfigureAwait(false);
                return ParseTranslation(json);
            }
        }

        /// <summary>
        /// Odpověď je vnořené pole: [[["přeložený text","zdroj",...], ...], ...].
        /// Posbírá segmenty z prvního pole do výsledného řetězce.
        /// </summary>
        private static string ParseTranslation(string json) {
            var serializer = new JavaScriptSerializer();
            var root = serializer.DeserializeObject(json) as object[];
            if (root == null || root.Length == 0 || !(root[0] is object[] segments))
                throw new FormatException("Unexpected translation response.");

            var sb = new StringBuilder();
            foreach (var segObj in segments) {
                if (segObj is object[] seg && seg.Length > 0 && seg[0] != null)
                    sb.Append(seg[0].ToString());
            }
            string result = sb.ToString();
            if (result.Length == 0)
                throw new FormatException("Empty translation result.");
            return result;
        }
    }
}
