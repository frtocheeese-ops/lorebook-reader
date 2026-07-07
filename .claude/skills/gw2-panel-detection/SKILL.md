---
name: gw2-panel-detection
description: How Lorebook Reader finds GW2 UI panels (lorebook parchment, NPC conversation dialogs) in raw screenshots, and how to calibrate or debug that detection. Use this whenever detection misses a panel, fires on the wrong thing (false positive), OCR captures cut-off or extra text, a new resolution/UI-scale must be supported, or any constant in ParchmentDetector.cs / ConversationDetector.cs is about to change. Also use it when designing detection for any NEW GW2 UI element — the calibration method here is the reusable part.
---

# GW2 Panel Detection

Two detectors exist. Both scan a raw `Bitmap` of the GW2 client area in 8×8 pixel cells
using `unsafe` LockBits, and both return a `Rectangle?` in bitmap coordinates. They share
a philosophy that took five failed iterations to learn:

> **Absolute pixel thresholds fail in GW2. Detect structure and transitions instead.**
> Bright-pixel tests match snow, sky, and moonlit grass; dark-pixel tests match caves.
> What survives every environment is the *relationship* between regions: a warm strip
> with cold rows above it and bright text below it; a large low-chroma blob with a
> book-like aspect ratio.

Read `references/parchment-algorithm.md` and `references/conversation-algorithm.md` for
the exact per-detector logic and every constant with its rationale. Read
`references/calibration-playbook.md` **before changing any constant** — it is the
process that produced the current values and the only safe way to change them.

## Quick mental model

**ParchmentDetector (lorebooks, shipped in v0.2.2):** parchment = a big contiguous blob
of bright (lum > 185), nearly colorless (max−min RGB < 60) pixels. Cells pass at ≥45%
qualifying pixels; blobs are flood-filled; a candidate must have solidity > 0.6, h/w
ratio 0.9–3.0, and sane screen-fraction bounds. Largest qualifying blob wins.
`InnerCrop` then shaves the decorative frame (3% x, 4% top, 2% bottom).

**ConversationDetector (NPC dialogs, v0.3.0 — local, not yet on GitHub):** the anchor is
the brown header bar: a *thin* horizontal strip of warm pixels (R > G > B, R−B ≥ 8,
lum 20–80, R ≥ 30), at least ~25 cells wide, in the top half of the screen, with cold
rows above **and** below (isolated strip, not a wide warm texture) and bright (lum > 170)
text pixels beneath. The panel is then derived from the header span: expand width ~70%
left / ~25% right (the header sits right of the text area), and `TextCrop` takes 6% skip
from the top and 55% of the height (more would swallow the player response options like
"Read on."). Verify exact constants against the local `ConversationDetector.cs` — this
skill records the last calibrated state, the file is the source of truth.

## Invariants — do not break these while "improving" things

1. **Fractions, not pixels.** Every derived rectangle is a fraction of the detected
   anchor or of screen size. A raw pixel constant is a bug at any other resolution.
   (Only field-verified resolution so far: 2560×1440 — treat others as untested.)
2. **One parameter per iteration.** During calibration, change exactly one constant,
   re-test, record. Two changes at once made past sessions un-debuggable.
3. **Named failure evidence.** A detection bug report is "the words *scared*, *It's*,
   *probably* are missing / *Read on.* leaked in" — specific words tie a symptom to a
   geometric cause (right edge cut → widen right; responses leaked → lower TextCrop).
4. **No threshold change ships without a corpus run** (see `testing-validation` and the
   calibration playbook). If the corpus doesn't exist yet in your session, building it
   from debug dumps is part of the change, not optional gold-plating.

## Symptom → geometric cause cheat sheet

| Symptom | Likely cause | Knob |
|---|---|---|
| Last word of dialogue lines truncated | panel too narrow on the left/right of header | width expansion fractions |
| Player responses ("Read on.") in OCR | TextCrop height too tall | 55% height crop |
| NPC name / title in OCR | top skip too small | 6% top skip |
| Detector fires on rocks/wood textures | warm strip not thin/isolated enough | thin-strip + cold-above/below checks |
| Detector fires on snow/bright scene (parchment) | shape filters too loose | solidity / ratio / fraction bounds |
| Nothing detected in dark scenes | this is what absolute thresholds did — verify transition logic wasn't replaced |
| Buttons overlap the text OCR reads | button offset (18% of panel width) regressed |

## Performance notes (relevant when touching the loop)

The module polls detection at 1 Hz (`Update` → `Task.Run(DetectForButton)`), and each
poll allocates a full-resolution 24bpp Bitmap (~11 MB at 1440p) plus a full-frame scan.
Acceptable today; if you add more detectors or raise the poll rate, first reuse a
persistent capture buffer and/or scan a downsampled frame — don't multiply the
allocation. Never run detection on the game thread.

## Known open improvements (prioritized in docs/engineering-review)

- Restore the prototype's **debug-dump** (Python version had Ctrl+Alt+D saving the full
  frame + detected crop; the C# port lost it). It is the enabler for everything else.
- Golden screenshot corpus + offline regression harness.
- Multi-anchor confirmation for conversations (header bar + X button + NPC name plate +
  green response arrows were all identified as stable anchors; only the header is used).
- Temporal smoothing (require N consecutive hits before showing buttons) to kill
  1-frame false positives, which the README currently apologizes for in Known Issues.
- UI-scale awareness via MumbleLink identity (uisz) once resolutions beyond 1440p matter.
