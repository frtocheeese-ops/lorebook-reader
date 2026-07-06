# ConversationDetector — exact algorithm (v0.3.0, branch feature/conversation-capture)

File: `ConversationDetector.cs`. This document records the calibrated state as of
2026-07-06 (v6). **The code is the source of truth — diff these numbers against it
before relying on them.** Reached as "v6" after five superseded strategies (History).

## Warm-pixel definition (the brown header bar)

A pixel is *warm* iff: `R > G > B` AND `R − B ≥ 8` AND `20 ≤ lum ≤ 80` AND `R ≥ 30`.
Dark bronze/brown — too dark for terrain highlights, too warm for shadows.

## Detection steps

1. Scan only the **top 50%** of the frame (dialogs anchor high; halves the work and
   removes ground-texture false positives).
2. 8×8 cells; a cell is *warm* if ≥ **35%** of its pixels are warm.
3. Find the longest horizontal run of warm cells per row; a header candidate needs
   ≥ **25 cells** (~200 px @1920, ~266 px @2560).
4. Structural confirmations (these are what distinguish the header from a warm rock):
   - **Thin**: the warm strip is 1–3 cell rows tall, not a wide warm area.
   - **Isolated**: rows immediately above AND below the strip are *not* warm.
   - **Text beneath**: bright pixels (lum > **170**) exist below the strip
     (the white dialogue text).
5. Derive the panel from the header span `(hx, hw)`:
   - `panelLeft  = hx − 0.70 * hw` (clamped ≥ 0)
   - `panelRight = hx + hw + 0.25 * hw` (clamped ≤ frame width)
   - The asymmetry is the key discovery: **the header bar sits to the RIGHT of the
     text area**, so most of the text is left of the header. Early symmetric
     expansions (5%/20px, then 12%/15–20%) consistently cut off line endings —
     the words "scared", "It's", "probably" were the recurring evidence.
6. **v6 (2026-07-06): the OCR crop is MEASURED, not fraction-derived.**
   `FindHit()` returns `ConversationHit { Panel, TextArea, Solidity }`.
   `TextArea` comes from `MeasureTextArea()`: within the calibrated vertical zone
   (top skip **18%** of panel height, zone height **40%** — these fractions remain
   the guard against the NPC title/response echo above and the response options
   below; "Read on." must never enter OCR), it scans the already-computed
   bright-cell grid in a window **wider than the panel** (−0.10·headerW left,
   +0.20·headerW right), takes the longest run of text columns (cell = text at
   ≥ 3 bright px; gaps ≤ 4 cells tolerated as word spacing — bigger gaps separate
   distant bright UI like the quest tracker), measures min/max text rows on that
   run (row = text at ≥ 2 text cells), and pads by 2 cells horizontally / 1
   vertically (WinRT OCR drops words touching the crop edge). Empty measurement →
   fraction fallback (`TextCrop`, now fallback-only).
   *Evidence:* dump_20260706_093527 @2560×1440 — text ended at x≈1421, fraction
   panelRight was 1416 → OCR dropped the final word "was"; measured TextArea also
   trims ~220 px of background noise on the left.
7. Action buttons are placed with an **18% panel-width offset** so they never overlap
   the region OCR reads.

## OCR handoff

Dialogue text is light-on-dark → `OcrService` is called with **inverted**
preprocessing for conversation crops (parchment crops are dark-on-light and are NOT
inverted). If you see garbage OCR from conversations, check the invert flag first.

## History — why the failed strategies failed (do not retry them)

| v | Strategy | Death |
|---|---|---|
| v1–v2 | dark-pixel panel mass | dark caves/night = everything is "panel" |
| v3 | bright-on-dark text blocks | bright terrain & UI text everywhere |
| v4 | warm strip, no structure checks | warm terrain textures (wood, cliffs) |
| v5 | warm strip + thin + isolated + text-below | panel-fraction OCR crop truncated edge words ("scared", "It's", "probably", "was") |
| v6 | v5 detection + TextArea measured from bright-cell extents (`ConversationHit`) | current — fixes edge truncation, trims background noise; fraction crop kept as fallback |

## Stable anchors inventory (for future multi-anchor work)

Identified on real screenshots as consistent across dialogs: brown header bar (used),
NPC name on black plate, small X close button top-right, green response arrows.
A future scoring detector should require 2-of-4 before accepting.
