using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Neural hlasy Microsoft Edge (online, neoficiální Read Aloud endpoint).
    /// Vlastní WebSocket klient kvůli hlavičkám, které ClientWebSocket
    /// na .NET Frameworku zakazuje. Protokol dle edge-tts (Chromium 143):
    /// Sec-MS-GEC podpis + MUID cookie. Čte po dávkách s generováním
    /// dopředu; onChunk hlásí právě čtený text (pro titulky).
    /// </summary>
    public sealed class EdgeTtsService : IDisposable {

        private const string TrustedToken   = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
        private const string Host           = "speech.platform.bing.com";
        private const string BasePath       =
            "/consumer/speech/synthesize/readaloud/edge/v1";
        private const string ChromiumFull   = "143.0.3650.75";
        private const string ChromiumMajor  = "143";

        /// <summary>Osvědčené hlasy pro jazyky klienta GW2.</summary>
        public static readonly string[] CuratedVoices = {
            "en-GB-RyanNeural",
            "en-GB-SoniaNeural",
            "en-US-AndrewMultilingualNeural",
            "en-US-AriaNeural",
            "en-US-GuyNeural",
            "en-US-JennyNeural",
            "en-AU-WilliamNeural",
            "de-DE-ConradNeural",
            "de-DE-KatjaNeural",
            "fr-FR-HenriNeural",
            "fr-FR-DeniseNeural",
            "es-ES-AlvaroNeural",
            "es-ES-ElviraNeural"
        };

        /// <summary>Výchozí neural hlas pro cílový jazyk překladu.</summary>
        public static string VoiceForLanguage(string lang) {
            switch ((lang ?? "").ToLowerInvariant()) {
                case "cs":    return "cs-CZ-AntoninNeural";
                case "de":    return "de-DE-ConradNeural";
                case "es":    return "es-ES-AlvaroNeural";
                case "fr":    return "fr-FR-HenriNeural";
                case "it":    return "it-IT-DiegoNeural";
                case "pl":    return "pl-PL-MarekNeural";
                case "pt":    return "pt-PT-DuarteNeural";
                case "ru":    return "ru-RU-DmitryNeural";
                case "ja":    return "ja-JP-KeitaNeural";
                case "ko":    return "ko-KR-InJoonNeural";
                case "zh-cn": return "zh-CN-YunxiNeural";
                default:      return null;
            }
        }

        private CancellationTokenSource _cts;
        private WaveOutEvent _currentOut;

        static EdgeTtsService() {
            System.Net.ServicePointManager.SecurityProtocol |=
                System.Net.SecurityProtocolType.Tls12;
        }

        /// <summary>Přečte text; onChunk dostává právě čtenou dávku
        /// (null po skončení). Při problému se sítí vyhodí výjimku.</summary>
        public async Task SpeakAsync(string text, string voice, double rate,
                                     Action<string> onChunk = null) {
            Stop();
            var cts = new CancellationTokenSource();
            _cts = cts;
            CancellationToken ct = cts.Token;

            List<string> chunks = TextCleaner.SplitChunks(text);
            if (chunks.Count == 0) return;
            string prosodyRate = RateToProsody(rate);

            try {
                Task<byte[]> nextTask = SynthesizeChunkAsync(
                    chunks[0], voice, prosodyRate, ct);
                for (int i = 0; i < chunks.Count; i++) {
                    byte[] mp3 = await nextTask.ConfigureAwait(false);
                    if (ct.IsCancellationRequested) return;

                    nextTask = (i + 1 < chunks.Count)
                        ? SynthesizeChunkAsync(chunks[i + 1], voice,
                                               prosodyRate, ct)
                        : Task.FromResult<byte[]>(null);

                    onChunk?.Invoke(chunks[i]);
                    await PlayMp3Async(mp3, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested) return;
                }
            } finally {
                onChunk?.Invoke(null);
            }
        }

        public void Stop() {
            try { _cts?.Cancel(); } catch { /* ignore */ }
            try { _currentOut?.Stop(); } catch { /* ignore */ }
        }

        public void Dispose() {
            Stop();
            _currentOut?.Dispose();
        }

        // ------------------------ syntéza jedné dávky ------------------------

        private static async Task<byte[]> SynthesizeChunkAsync(
                string text, string voice, string prosodyRate,
                CancellationToken outerCt) {

            using (var timeout = new CancellationTokenSource(
                       TimeSpan.FromSeconds(15)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
                       outerCt, timeout.Token))
            using (var ws = new WebSocketLite()) {
                CancellationToken ct = linked.Token;

                string pathAndQuery = BasePath
                    + $"?TrustedClientToken={TrustedToken}"
                    + $"&ConnectionId={Guid.NewGuid():N}"
                    + $"&Sec-MS-GEC={GenerateSecMsGec()}"
                    + $"&Sec-MS-GEC-Version=1-{ChromiumFull}";

                var headers = new Dictionary<string, string> {
                    ["Pragma"]          = "no-cache",
                    ["Cache-Control"]   = "no-cache",
                    ["Origin"]          =
                        "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold",
                    ["User-Agent"]      =
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                        "AppleWebKit/537.36 (KHTML, like Gecko) " +
                        $"Chrome/{ChromiumMajor}.0.0.0 Safari/537.36 " +
                        $"Edg/{ChromiumMajor}.0.0.0",
                    ["Accept-Encoding"] = "gzip, deflate, br, zstd",
                    ["Accept-Language"] = "en-US,en;q=0.9",
                    ["Cookie"]          = $"muid={GenerateMuid()};"
                };

                await ws.ConnectAsync(Host, pathAndQuery, headers, ct)
                        .ConfigureAwait(false);

                string ts = DateTime.UtcNow.ToString(
                    "ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'");

                string config =
                    $"X-Timestamp:{ts}\r\n" +
                    "Content-Type:application/json; charset=utf-8\r\n" +
                    "Path:speech.config\r\n\r\n" +
                    "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":" +
                    "{\"sentenceBoundaryEnabled\":\"false\"," +
                    "\"wordBoundaryEnabled\":\"false\"}," +
                    "\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}\r\n";
                await ws.SendTextAsync(config, ct).ConfigureAwait(false);

                string ssml =
                    "<speak version='1.0' " +
                    "xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
                    $"<voice name='{voice}'>" +
                    $"<prosody pitch='+0Hz' rate='{prosodyRate}' volume='+0%'>" +
                    XmlEscape(text) +
                    "</prosody></voice></speak>";
                string ssmlMsg =
                    $"X-RequestId:{Guid.NewGuid():N}\r\n" +
                    "Content-Type:application/ssml+xml\r\n" +
                    $"X-Timestamp:{ts}Z\r\nPath:ssml\r\n\r\n" + ssml;
                await ws.SendTextAsync(ssmlMsg, ct).ConfigureAwait(false);

                var audio = new MemoryStream();
                while (true) {
                    var (type, data) = await ws.ReceiveAsync(ct)
                        .ConfigureAwait(false);
                    if (type == WebSocketLite.FrameType.Closed)
                        throw new IOException("Edge TTS closed the connection.");

                    if (type == WebSocketLite.FrameType.Text) {
                        string msg = Encoding.UTF8.GetString(data);
                        if (msg.Contains("Path:turn.end"))
                            break;
                    } else {
                        // [2 B délka hlavičky big-endian][hlavička][mp3 data]
                        if (data.Length < 2) continue;
                        int headerLen = (data[0] << 8) | data[1];
                        int offset = 2 + headerLen;
                        if (data.Length > offset)
                            audio.Write(data, offset, data.Length - offset);
                    }
                }

                if (audio.Length == 0)
                    throw new IOException("Edge TTS returned no audio.");
                return audio.ToArray();
            }
        }

        // --------------------------- přehrávání -----------------------------

        private async Task PlayMp3Async(byte[] mp3, CancellationToken ct) {
            if (mp3 == null || mp3.Length == 0) return;
            using (var ms = new MemoryStream(mp3))
            using (var reader = new Mp3FileReader(ms))
            using (var output = new WaveOutEvent()) {
                _currentOut = output;
                var done = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                output.PlaybackStopped += (s, e) => done.TrySetResult(true);
                output.Init(reader);
                output.Play();
                using (ct.Register(() => {
                           try { output.Stop(); } catch { /* ignore */ }
                           done.TrySetResult(true);
                       })) {
                    await done.Task.ConfigureAwait(false);
                }
                _currentOut = null;
            }
        }

        // ---------------------------- pomocné -------------------------------

        /// <summary>Sec-MS-GEC: SHA-256 z (Windows file time po 5 min + token).</summary>
        private static string GenerateSecMsGec() {
            long unixSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long winSec = unixSec + 11644473600L;
            winSec -= winSec % 300;
            ulong winTicks = (ulong)winSec * 10_000_000UL;
            string input = winTicks.ToString() + TrustedToken;
            using (var sha = SHA256.Create()) {
                byte[] hash = sha.ComputeHash(Encoding.ASCII.GetBytes(input));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }

        private static string GenerateMuid() {
            byte[] bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            var sb = new StringBuilder(32);
            foreach (byte b in bytes) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }

        private static string RateToProsody(double rate) {
            int pct = (int)Math.Round((rate - 1.0) * 100);
            pct = Math.Max(-50, Math.Min(100, pct));
            return (pct >= 0 ? "+" : "") + pct + "%";
        }

        private static string XmlEscape(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
