---
name: tts-subtitles-translation
description: The speech and subtitle output stack of Lorebook Reader — offline OneCore TTS (TtsService), online Edge neural TTS (EdgeTtsService + WebSocketLite), the SubtitleOverlay, and TranslationService with its three modes. Use this whenever audio is silent, stops mid-text, stutters, or reads the wrong language; when Edge voices fail or error; when subtitles lag, wrap badly, desync from speech, or show untranslated text; when adding voices or a translation target; or before touching SpeakAsync, SplitChunks call sites, OnTtsChunk, or the Edge protocol constants.
---

# TTS, Subtitles & Translation

## The shared playback pattern (both engines)

Both `TtsService` (offline OneCore via WinRT) and `EdgeTtsService` (online neural)
implement the same pipelined loop, and any new engine must too:

```
chunks = TextCleaner.SplitChunks(text)          // whole sentences, packed to maxLen
nextTask = Synthesize(chunks[0])
for i in chunks:
    audio = await nextTask                      // synth of chunk i finished
    nextTask = Synthesize(chunks[i+1])          // PRE-SYNTH the next while playing
    onChunk(chunks[i])                          // subtitles get the text NOW
    await Play(audio)                           // NAudio WaveOutEvent
onChunk(null)                                   // signals "clear subtitle"
```

Why it looks like this:
- **Pre-synthesis** hides latency — audio never gaps between chunks.
- **onChunk fires at playback start**, so the subtitle shows what is being *spoken*.
  `null` at the end clears the overlay — if subtitles ever stick, a code path skipped
  the `finally { onChunk(null); }`.
- **NAudio** (`WaveOutEvent.PlaybackStopped` → TaskCompletionSource) is the completion
  signal; WinRT's own playback events were unreliable. WAV for OneCore, MP3
  (`Mp3FileReader`, format `audio-24khz-48kbitrate-mono-mp3`) for Edge.
- **Cancellation**: `Stop()` cancels the CTS and stops the current output; every await
  is followed by a token check. New code paths must keep that property.
- Sentence-packing history: chunk cap of 80 chars audibly stopped TTS mid-sentence;
  current regime packs whole sentences to maxLen (200 in local v0.3.0; the v0.2.2
  repo default is 280 — read the actual call before citing a number).

## Engine selection & fallback (module side, `SpeakTextAsync`)

Engine setting `"edge"` tries Edge first; **any Edge exception falls back to the
offline voice with a user toast** ("online voice unavailable — using offline voice").
Preserve this ordering in changes: capture/OCR failures abort, but *speech* failures
degrade gracefully. Offline voice selection: exact-ish name match on the VoiceName
setting → else first installed voice with a matching language prefix → else default +
a toast telling the user how to install voices. Missing-voice reports are usually a
Windows-side install issue, not a bug.

## Edge TTS — protocol knowledge (read before touching EdgeTtsService/WebSocketLite)

This speaks the unofficial Edge "Read Aloud" endpoint
(`speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1`), protocol
mirrored from the `edge-tts` project. Load-bearing details:

- **Why a hand-written WebSocket exists**: .NET Framework's `ClientWebSocket` refuses
  to set `User-Agent` and other headers the endpoint requires. `WebSocketLite` is a
  minimal RFC 6455 client (TLS 1.2, client-masked frames, fragment reassembly,
  ping→pong). It is deliberately minimal — resist generalizing it.
- **Auth**: `TrustedClientToken` (fixed constant) + `Sec-MS-GEC` =
  SHA-256(Windows FILETIME floored to 5-minute blocks + token), uppercase hex, plus
  `Sec-MS-GEC-Version: 1-<full Chromium version>`, a Chromium-matching User-Agent,
  the read-aloud chrome-extension Origin, and a random 16-byte `muid` cookie.
  Consequences: **a wrong system clock (> ±5 min) breaks the handshake** — that is a
  real user-diagnosable failure; and Microsoft occasionally rotates expectations, so
  a sudden global Edge failure usually means "bump the pinned Chromium version"
  (currently 143.0.3650.75) or re-check against upstream edge-tts, not a local bug.
- **Session shape**: one fresh WebSocket **per chunk**, 15 s timeout; send
  `speech.config` (JSON, boundaries disabled, mp3 format), then the SSML message
  (voice + prosody rate as a percentage, ±50/+100 clamp); binary frames carry
  `[2-byte big-endian header length][header][mp3 bytes]`; a text frame containing
  `Path:turn.end` ends the turn. Empty audio → throw (which triggers the fallback).
- Curated voice list + `VoiceForLanguage` map live in the service; extending a
  translation target language means adding its neural voice there too.
- Known gap (improvement list): no retry — one transient network blip on chunk N
  kills the whole read instead of retrying once or falling back mid-read.

## Subtitles

`SubtitleOverlay` renders via the shared GDI `TextRenderer` (full diacritics —
see gdi-text-rendering). Layout facts:

- Wrapping is **pixel-accurate** GDI measurement into a box of 45% screen width;
  the local v0.3.0 additionally has a character-based `WrapLines` at **42 chars/line**
  (Netflix subtitle convention) for chunk display. Two wrapping systems exist for two
  jobs — do not "unify" them without checking both call sites.
- Text/pos changes cross threads via `_pendingSubtitle` + `_subtitleDirty` (volatile)
  and are applied **only in `Update`** — the foundations threading rule.
- Position is stored as % of screen (`SubtitleX/Y` settings); drag-edit mode writes
  the % back on drop. Shadow = same texture rendered dark at +2px offset.
- `SanitizeForDisplay` maps typographic dashes/quotes/ellipsis to ASCII and blanks
  other exotica; note it currently also blanks non-ASCII *symbols* — diacritic letters
  survive via `char.IsLetterOrDigit`, so Czech/German text is safe.

## Translation (three modes; `TranslationService` = unofficial Google gtx endpoint)

| Mode | What is translated | Where |
|---|---|---|
| `off` | nothing | — |
| `subtitles` | each chunk, async, **subtitles only** — speech stays original | `OnTtsChunk` per chunk |
| `full` | the entire text **before** speaking; voice switches to the target language (`VoiceForLanguage`) | `SpeakTextAsync` |

Failure contract: translation problems **never block reading** — full-mode failure
toasts and speaks the original; per-chunk failure silently shows the original chunk.
Keep this contract.

Known weaknesses (improvement list): the gtx call is a single GET with the whole text
in the query string — very long lorebooks can exceed URL limits and fail full-mode
translation (fix: translate per chunk/paragraph); per-chunk mode guards against stale
*sessions* (`_speakSession`) but not against out-of-order arrival *within* a session —
a slow chunk-N translation can overwrite chunk-N+1's subtitle (fix: attach chunk index
to the guard); nothing is cached, so replaying a translated book re-pays the network
(the catalog already has `TranslatedText/TranslatedLang` fields to cache into).

## Symptom router

| Symptom | Look at |
|---|---|
| Silence, no error | engine setting vs installed voices; `Stop()` raced a new read; NAudio device |
| Stops mid-sentence | chunking regressed to char-splitting — check SplitChunks call sites |
| Edge fails for everyone at once | Chromium pin / protocol drift; check upstream edge-tts |
| Edge fails for one user | system clock (Sec-MS-GEC ±5 min); firewall to bing.com |
| Subtitle stuck after reading | missing `onChunk(null)` path |
| Subtitle shows original in subtitles-mode | translation exception (by design) — log has the reason |
| Subtitle briefly shows wrong chunk | the out-of-order per-chunk race above |
| Wrong-language voice in full mode | `VoiceForLanguage` lacks the target |
