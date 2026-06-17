using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Windows.Media.SpeechSynthesis;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Offline text-to-speech přes WinRT (OneCore hlasy).
    /// Stejné chunked schéma jako Edge engine: generuje dávku dopředu,
    /// zatímco předchozí hraje; onChunk hlásí právě čtený text (titulky).
    /// Přehrávání WAV streamu přes NAudio (spolehlivé eventy dokončení).
    /// </summary>
    public sealed class TtsService : IDisposable {

        private readonly SpeechSynthesizer _synth = new SpeechSynthesizer();
        private CancellationTokenSource _cts;
        private WaveOutEvent _currentOut;

        /// <summary>Nainstalované hlasy: (zobrazované jméno, jazykový tag).</summary>
        public static IEnumerable<(string Name, string Lang)> InstalledVoices() =>
            SpeechSynthesizer.AllVoices
                .Select(v => (v.DisplayName, v.Language))
                .OrderBy(v => v.Language)
                .ThenBy(v => v.DisplayName);

        /// <param name="voiceName">jméno hlasu; prázdné = auto dle jazyka</param>
        /// <param name="rate">rychlost řeči, 1.0 = normální</param>
        /// <param name="languageTag">jazyk textu (z OCR), např. "en-US"</param>
        /// <param name="onChunk">callback s právě čtenou dávkou (null = konec)</param>
        /// <returns>null = OK; jinak varovná zpráva pro uživatele</returns>
        public async Task<string> SpeakAsync(string text, string voiceName,
                                             double rate, string languageTag,
                                             Action<string> onChunk = null) {
            Stop();
            var cts = new CancellationTokenSource();
            _cts = cts;
            CancellationToken ct = cts.Token;
            string warning = SelectVoice(voiceName, languageTag);
            _synth.Options.SpeakingRate = Math.Max(0.5, Math.Min(3.0, rate));

            List<string> chunks = TextCleaner.SplitChunks(text);
            if (chunks.Count == 0) return warning;

            try {
                Task<byte[]> nextTask = SynthesizeChunkAsync(chunks[0]);
                for (int i = 0; i < chunks.Count; i++) {
                    byte[] wav = await nextTask.ConfigureAwait(false);
                    if (ct.IsCancellationRequested) return warning;

                    nextTask = (i + 1 < chunks.Count)
                        ? SynthesizeChunkAsync(chunks[i + 1])
                        : Task.FromResult<byte[]>(null);

                    onChunk?.Invoke(chunks[i]);
                    await PlayWavAsync(wav, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested) return warning;
                }
            } finally {
                onChunk?.Invoke(null);
            }
            return warning;
        }

        public void Stop() {
            try { _cts?.Cancel(); } catch { /* ignore */ }
            try { _currentOut?.Stop(); } catch { /* ignore */ }
        }

        public void Dispose() {
            Stop();
            _currentOut?.Dispose();
            _synth.Dispose();
        }

        // ---------------------------------------------------------------------

        private string SelectVoice(string voiceName, string languageTag) {
            var all = SpeechSynthesizer.AllVoices;
            VoiceInformation voice = null;
            string warning = null;

            if (!string.IsNullOrWhiteSpace(voiceName)) {
                voice = all.FirstOrDefault(v =>
                    v.DisplayName.IndexOf(voiceName.Trim(),
                        StringComparison.OrdinalIgnoreCase) >= 0);
                if (voice == null)
                    warning = $"Voice \"{voiceName}\" not found, choosing by language.";
            }
            if (voice == null) {
                string prefix = (languageTag ?? "en").Split('-')[0];
                voice = all.FirstOrDefault(v => v.Language.StartsWith(
                    prefix, StringComparison.OrdinalIgnoreCase));
                if (voice == null)
                    warning = $"No \"{prefix}\" voice is installed in Windows — "
                            + "using default. Install one via Settings > Time & "
                            + "Language > Speech > Add voices.";
            }
            if (voice != null) _synth.Voice = voice;
            return warning;
        }

        private async Task<byte[]> SynthesizeChunkAsync(string chunk) {
            using (SpeechSynthesisStream stream =
                       await _synth.SynthesizeTextToStreamAsync(chunk)) {
                var ms = new MemoryStream();
                await stream.AsStreamForRead().CopyToAsync(ms)
                            .ConfigureAwait(false);
                return ms.ToArray();
            }
        }

        private async Task PlayWavAsync(byte[] wav, CancellationToken ct) {
            if (wav == null || wav.Length == 0) return;
            using (var ms = new MemoryStream(wav))
            using (var reader = new WaveFileReader(ms))
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
    }
}
