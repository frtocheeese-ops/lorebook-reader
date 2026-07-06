# ConversationDetector — exact algorithm (v0.3.0, LOCAL — not on GitHub yet)

File: `ConversationDetector.cs` in the local working tree (not yet pushed). This document
records the last calibrated state from the June 2026 sessions. **The local file is
the source of truth — diff these numbers against it before relying on them.**
Reached as "v5" after four failed strategies (see History below).

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
6. `TextCrop(panel)`: skip top **6%** (NPC name/title zone), take **55%** of panel
   height (values tried: 60% → 80% → 55%; 80% leaked the player response options —
   "Read on." showed up in OCR — 55% excludes them).
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
| v5 | warm strip + thin + isolated + text-below | current — survives the corpus of real 1440p screenshots |

## Stable anchors inventory (for future multi-anchor work)

Identified on real screenshots as consistent across dialogs: brown header bar (used),
NPC name on black plate, small X close button top-right, green response arrows.
A future scoring detector should require 2-of-4 before accepting.
