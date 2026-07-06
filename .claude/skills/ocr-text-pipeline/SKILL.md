---
name: ocr-text-pipeline
description: The screenshot→OCR→cleanup text pipeline of Lorebook Reader (ScreenCapture, OcrService, TextCleaner). Use this whenever OCR output is wrong, garbled, truncated, or empty; when adding or tuning a text-cleanup/confusable-character rule; when supporting a new OCR language or a non-English GW2 client; when changing preprocessing (grayscale, upscale, inversion); or when TTS reads nonsense that traces back to recognition rather than speech. Also consult before editing CleanForTts, FixConfusableChars, or SplitChunks.
---

# OCR Text Pipeline

Order of operations (each stage's output is the next stage's input — debug by
inspecting between stages, not at the end):

```
ScreenCapture.Grab (client area, 24bpp)
  → detector crop (see gw2-panel-detection)
  → OcrService.Preprocess: grayscale (ColorMatrix) + 2× bicubic upscale
      [+ INVERT for conversation crops — light-on-dark text; v0.3.0 local]
  → Windows.Media.Ocr (WinRT) — language from the OcrLanguage setting
  → TextCleaner.CleanForTts (noise trim + artifact fixes)
      [+ TextCleaner.FixConfusableChars — v0.3.0 local]
  → consumers: TTS chunking / subtitles / catalog storage
```

## Capture

`ScreenCapture.Grab(hwnd)` = Win32 `GetClientRect` + `ClientToScreen` +
`Graphics.CopyFromScreen`. Works for windowed and **Windowed Fullscreen** only —
exclusive fullscreen returns black/garbage; that is a user-environment issue, not a
code bug. Fallback when the handle is bad: primary-screen metrics. Windows smaller
than 200×200 are rejected.

## OCR engine facts

- `OcrEngine.TryCreateFromLanguage(tag)` needs the **Windows language pack installed**;
  otherwise it returns null and the code falls back to user-profile languages, then
  throws a clear message. "OCR suddenly bad after user changed client language" is
  almost always a missing language pack — point users at Settings → Time & Language.
- Supported client languages: en-US, de-DE, fr-FR, es-ES (the OcrLanguage setting).
- The 2× bicubic upscale before OCR measurably improves WinRT recognition of GW2's
  smallish text — don't remove it to "save time".
- Result lines are joined with `\n`; per-word confidence exists on `OcrResult` but is
  currently unused (a known improvement: use low-confidence words to gate corrections).
- `RecognizeLineAsync` is the single-line variant used for book titles (whitespace
  collapsed, 2–60 char sanity gate applied by the caller).

## TextCleaner.CleanForTts — the v0.2.2 rules and their reasons

1. **Decorative-noise trimming**: leading/trailing lines are dropped while they fail
   `IsGoodLine` (≥50% of words "valid": ≥2 letters + contains a vowel). This kills
   ornament rows OCR reads as `~~~ * ~~~`.
2. `|` handling: a standalone `|` before a lowercase word → `I` (it *is* an I in GW2's
   font); other standalone `|` → space (frame ornaments).
3. **The `11` artifact**: OCR reads the `"` quote glyph as `11`. Rules delete `11`
   only when glued to a letter/punctuation boundary; real numbers like "11 days"
   survive. When touching this, keep both test phrases in mind.
4. Everything after the last sentence-final punctuation (`.!?` + closing quotes) is
   trimmed — page-edge noise.

### ⚠ Known real bug — non-English clients

`IsValidWord` uses ASCII-only classes (`[^A-Za-z']`, vowels `[aeiou]`). German/French/
Spanish words made of accented letters ("früh", "âgé") strip to too-few letters or no
vowel → whole *legitimate* lines get trimmed as "decoration" at step 1. If you work on
de/fr/es support, fix this first: use Unicode letter classes (`\p{L}`) and a per-language
vowel set, and add corpus cases for each language. Until fixed, treat any "text missing
on non-English client" report as this bug.

## FixConfusableChars (v0.3.0, local — verify against the file)

Fixes OCR confusions in **alphabetic context only**: 0↔O, 1↔I/l, |↔I/l, 5↔S, 8↔B.
The guard that makes it safe: **numeric tokens are preserved** — "Level 80" keeps its
8 and 0; "8ecause" becomes "Because". Plus the GW2-font special: leading `J` before a
space + lowercase word → `I` (regex `(?<![A-Za-z])J(?=\s+[a-z])` → `"I"`; the earlier
version without `\s+` never matched because OCR emits "J wake", not "Jwake").

**Adding a new correction rule — the required procedure:**
1. Capture the failing raw OCR string into the golden corpus first (see
   `references/golden-corpus.md`).
2. Write the narrowest rule that fixes it (prefer context guards over global replace).
3. Run the corpus: the new case passes AND all "mustNotChange" phrases (numbers,
   proper nouns) still pass. The 5↔S and 8↔B pairs are the most overcorrection-prone.
4. Document the rule + example in the code comment, as the existing rules do.

## Chunking (consumer side, but tuned here)

`SplitChunks` packs whole sentences (`(?<=[.!?…])\s+` splitter) into buffers up to
`maxLen`. History: default 80 chunked mid-sentence and TTS audibly stopped and
restarted — raised to sentence-packing with maxLen 200 (v0.3.0; repo v0.2.2 default is
280 — check the call sites for the live value). The splitter has no abbreviation
handling ("Mr. Smith" splits) — known improvement, harmless for TTS pauses, matters if
chunks ever drive translation caching. Subtitle line-wrapping is a *separate* concern
(42 chars/line, Netflix standard — see tts-subtitles-translation).

## Debugging a "wrong text" report — the ladder

1. Is the **crop** right? (gw2-panel-detection symptom table.) Wrong-crop ≠ OCR bug.
2. Is the **raw OCR** wrong? Get/save the pre-clean string. If raw is right and final
   is wrong → a cleaner rule overreached; binary-search the rules.
3. Raw wrong on a correct crop → preprocessing (invert flag for conversations?
   language pack installed? upscale intact?).
4. Only then consider engine limits — and add the case to the corpus either way.
