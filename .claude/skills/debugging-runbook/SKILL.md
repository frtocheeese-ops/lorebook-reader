---
name: debugging-runbook
description: Symptom-first troubleshooting router for Lorebook Reader. Use this FIRST for any bug report or unexpected behavior — module won't load or build, buttons missing or flickering, no speech, wrong or garbled text, subtitles misbehaving, Edge/translation failures, catalog data problems, crashes on unload. It routes each symptom to the owning skill with the fastest diagnostic step, so cheap sessions don't burn context rediscovering the pipeline.
---

# Debugging Runbook

Work symptom-first. Identify the failing **stage**, then open the owning skill.
The pipeline, for orientation:

capture → detect → crop → preprocess → OCR → clean → [translate] → chunk → speak + subtitle
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↘ save to catalog

First moves for *any* report: (1) Blish HUD log (module logs voices at startup,
detection results, every caught exception with context); (2) ask for resolution +
display mode + client language + graphics settings; (3) reproduce before fixing.

## Build & load

| Symptom | Fast check | Owner skill |
|---|---|---|
| NETSDK1004 / no .bhm on SSRD | ForceRestore target present in csproj? | ssrd-build-publish |
| Builds on SSRD but not locally | clean-tree repro: `rm -rf obj bin && dotnet build` | ssrd-build-publish |
| Module not listed in Blish | manifest valid? namespace/package match dll? .bhm actually copied? | blish-module-foundations |
| Loads then instantly errors | log first exception; usually a missing ref/ asset or WinRT contract | blish-module-foundations |
| Crash when disabling module | something new isn't disposed / handler not detached in Unload | blish-module-foundations |

## Detection & buttons

| Symptom | Fast check | Owner skill |
|---|---|---|
| No buttons on an open book | "using center fallback" in log? → detector missed; get the exact screenshot | gw2-panel-detection |
| Buttons flicker on scenery | known 1-frame false positive (README) — persistent = regression; which scene? | gw2-panel-detection |
| Buttons appear but no useful OCR ("no readable text found") | fallback rect OCR'd scenery, or crop off — inspect the crop | gw2-panel-detection |
| Everything dead, capture black | exclusive fullscreen — user must switch to Windowed Fullscreen | ocr-text-pipeline (Capture) |

## Text quality

| Symptom | Fast check | Owner skill |
|---|---|---|
| Words truncated at line ends / responses ("Read on.") included | geometric — the symptom table maps word→knob | gw2-panel-detection |
| Garbled characters (0/O, 5/S, 8/B, J/I, `11` quotes) | which stage: raw OCR vs after cleaning? | ocr-text-pipeline |
| Whole lines missing on de/fr/es clients | the known IsValidWord ASCII bug | ocr-text-pipeline |
| Conversation OCR = garbage, lorebook fine | invert-preprocessing flag on the conversation path | ocr-text-pipeline |
| OCR empty, "language pack not installed" | Windows language pack missing | ocr-text-pipeline |

## Speech & subtitles

| Symptom | Fast check | Owner skill |
|---|---|---|
| Silence, no toast | installed voices in startup log; engine setting; a racing Stop | tts-subtitles-translation |
| Stops mid-sentence | chunking regressed (char-split) — check SplitChunks call sites | tts-subtitles-translation |
| Edge fails for ONE user | their system clock (Sec-MS-GEC ±5 min window) | tts-subtitles-translation |
| Edge fails for EVERYONE | protocol drift — run the maintenance procedure | tts-subtitles-translation (references/edge-protocol-maintenance) |
| Subtitle stuck after reading ends | an onChunk(null) path was skipped | tts-subtitles-translation |
| Subtitles show original in subtitles-mode | per-chunk translation failed (by design, logged) | tts-subtitles-translation |
| Diacritics as boxes / missing glyphs | text bypassed TextRenderer | gdi-text-rendering |

## Catalog & data

| Symptom | Fast check | Owner skill |
|---|---|---|
| Encyclopedia suddenly EMPTY | ⚠ stop the module before it saves again — the silent-corruption overwrite hazard; secure catalog.json first | encyclopedia-catalog |
| Near-duplicate entries | exact-match dedup + OCR jitter (known) | encyclopedia-catalog |
| Curated old books vanish | capacity trim ate tagged entries (known hazard) | encyclopedia-catalog |
| Import "did nothing" | it reads the newest lorebook_export*.json from the data folder — was the file there? | encyclopedia-catalog |

## When the runbook doesn't match

You've likely found something new: capture the evidence (screenshot / raw OCR string /
log excerpt), add it as a corpus case or matrix row, fix it, and then **add the
symptom to this runbook** — that is how the runbook stays worth reading. Follow the
agent-team-workflows skill for anything bigger than a one-liner.
