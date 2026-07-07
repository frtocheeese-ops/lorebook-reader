# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview
Blish HUD module for Guild Wars 2 (GW2) — reads in-game lorebooks aloud via screenshot → OCR → TTS, with subtitles, translation, and encyclopedia. Written in C# targeting .NET Framework 4.8.

## Build & Test
```bash
dotnet restore
dotnet build
# Output: bin/Debug/net48/LorebookReader.bhm
# Copy .bhm to Blish HUD modules folder to test in-game
```
Rebuild after changes: `dotnet build` or in Visual Studio `Ctrl+Alt+F7` (Rebuild Solution).

## Architecture

**Capture pipeline** (triggered by keybind or speaker button click):
1. `ScreenCapture.Grab` → Win32 screenshot of GW2 client area
2. `ParchmentDetector.Find` → warm-luminance region; if null and conversation mode ON → `ConversationDetector.Find`
3. Crop: `ParchmentDetector.InnerCrop` or `ConversationDetector.TextCrop`
4. `OcrService.RecognizeAsync` → 2× upscale + grayscale (inverted for conversation); Windows.Media.Ocr
5. `TextCleaner.CleanForTts` → strip noise lines, fix confusable chars, trim trailing garbage
6. `LorebookCatalog.AddCaptured` → persist to JSON catalog
7. `TextCleaner.SplitChunks` → sentence-level TTS chunks (maxLen=200)
8. `TtsService` / `EdgeTtsService` → audio; each chunk fires `OnTtsChunk` → subtitles via `SubtitleOverlay`

**Game loop** (`Update` runs at game framerate):
- Detection (`DetectForButton`) runs on a background `Task` every 1000 ms, writes `_bookVisible`/`_convVisible` with `volatile bool`; `Update` reads them to position/show the three overlay buttons.
- Subtitle text (`_subtitleDirty` flag) and position are applied only in `Update` to stay on the render thread.

**Key files:**
- `LorebookReaderModule.cs` — module entry point; orchestrates settings, keybinds, detection loop, all three pipelines (Read/SaveOnly/Append), subtitle positioning, encyclopedia window
- `ConversationDetector.cs` — detects NPC dialogue via warm brown header bar (R>G>B transition) with cold rows above and bright text below (eliminates false positives from game textures)
- `ParchmentDetector.cs` — detects lorebook parchment (high luminance, low chroma)
- `DialogZoneCalibrator.cs` — full-screen draggable/resizable frame (Ctrl+Alt+Z) to mark the dialogue text area; the calibrated zone (per resolution) replaces heuristic panel-finding for OCR + button anchoring
- `OcrService.cs` — Windows.Media.Ocr (WinRT); 2× upscale + grayscale ColorMatrix preprocessing; inverted ColorMatrix for conversation (white-on-dark) text
- `TextCleaner.cs` — confusable-char fixes, prosodic chunking with tiered split patterns, subtitle word-wrap at 42 chars/line
- `TtsService.cs` — offline Windows OneCore TTS; `EdgeTtsService.cs` — online Edge neural voices via hand-written RFC6455 WebSocket (`WebSocketLite.cs`), MP3 decoded with NAudio
- `LorebookCatalog.cs` — JSON persistence via `JavaScriptSerializer` (`System.Web.Extensions`)
- `SubtitleOverlay.cs` — GDI-rendered subtitles; `TextRenderer.cs` — GDI+→XNA Texture2D (required for diacritics)
- `EncyclopediaView.cs`, `LorebookSettingsView.cs`, `ParchmentTextPanel.cs` — Blish HUD UI

## Key Technical Decisions
- **Header bar detection**: warm horizontal strip (R>G>B, R-B≥8, lum 20-80) with cold rows 4-6 above (= game world) and bright pixels 4-12 below (= dialog text). Eliminates false positives from game textures that have warm rows both above and below.
- **ParchmentDetector** uses luminance+chromaticity (lum>185, chroma<60) with 8×8 cell grid.
- **Dialogue zone calibration** (v0.3.0): the warm-header heuristic is fragile per-frame (some pages collapse, buttons occasionally mis-anchor). The user marks the narrative text area once per resolution (Ctrl+Alt+Z). When set, `ConversationDetector.MeasureInZone` measures bright text inside the zone (no header search) → tight OCR crop + stable button anchor; heuristic `FindHit` is the fallback when no zone is calibrated. Stored as `DialogZone` setting = `x,y,w,h,resW,resH` in client px (invalidated when resolution changes). Button visibility gates on in-zone bright fraction 0.03–0.6 (text-like, not dark/sky).
- **OCR confusable chars**: 0↔O, 1↔I/l, |↔I, J→I, 5↔S, 8↔B. Numbers preserved in numeric tokens (sequences, ordinals like 3rd, time after colon).
- **TTS chunking**: sentence-level (maxLen=200), falls back through semicolons → em-dashes → conjunction commas → any comma → word split.
- **GDI text rendering** required for diacritics (Blish HUD built-in fonts lack them).
- **NuGet packages**: `BlishHUD` with `ExcludeAssets="runtime;contentFiles;analyzers"` (required per official example); `Microsoft.Windows.SDK.Contracts` for WinRT OCR/TTS on .NET Framework; `NAudio` for Edge TTS MP3; `System.ComponentModel.Composition` as NuGet (not a bare Reference) to avoid asset exclusion failures.
- **ForceRestore MSBuild target** required for SSRD build host (doesn't run dotnet restore for SDK-style projects).

## Coding Conventions
- Namespace: `Frtal.LorebookReader`
- Module namespace in manifest: `vrae.lorebook_reader`
- C# with `unsafe` for LockBits pixel access in detectors
- Settings via `SettingEntry<T>` pattern; exposed `internal` to `LorebookSettingsView`
- Module export: `[Export(typeof(Module))]` with `[ImportingConstructor]`
- Czech inline comments are intentional (author's language) — don't translate or remove them

## What NOT to Do
- Don't modify `Properties/launchSettings.json` — it has local paths
- Don't add `.slnx` solution files — SSRD build host doesn't support them
- Don't commit `bin/`, `obj/`, `pkg/`, `.vs/`
- Don't remove the `ForceRestore` target from csproj — SSRD builds will break
- ArenaNet ToS: no memory reading, no input automation. Screenshot+OCR and overlay only.

## Git Workflow
```bash
git add -A
git status  # ALWAYS check before committing
git commit -m "description"
git push
```

## Testing
Test at 2560×1440 resolution. Key test scenarios:
1. Open lorebook → three buttons appear → Read/Save/Append work
2. Conversation mode ON (Ctrl+Alt+C) → open NPC dialogue → buttons appear → OCR captures text
3. Subtitles display with word wrap, TTS reads complete sentences
4. Encyclopedia saves and replays books correctly
5. Calibrate dialogue zone (Ctrl+Alt+Z) → drag a frame over the story text → Save → across pages, Ctrl+Alt+D shows `Zone measure` with the text inside the zone and OCR captures full text
