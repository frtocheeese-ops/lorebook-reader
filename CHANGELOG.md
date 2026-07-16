# Changelog

All notable changes to Lorebook Reader are documented here.
This project follows [Semantic Versioning](https://semver.org).

## 0.7.0 — 2026-07-16

The encyclopedia visual remake. (Includes the unreleased 0.6.0 work.)

### Added
- **Expansion rail.** The expansion filter is now a left-hand rail listing
  every expansion with its icon and book count, plus "All books" and
  "No expansion". The rail collapses to icons only (« button) so the window
  stays compact on low resolutions; the state is remembered.
- **Book reader.** The preview is now a real book: page 0 is a cover with the
  title in a serif book font, decorative rules, the capture date and location
  (date shown in each user's own regional format), and — when an expansion is
  set — the expansion logo pressed onto the cover as a slightly rotated stamp
  (`ref/xp_*_big.png`, falls back to the small icon). Pages turn one at a time
  with a page-turn animation (‹ ›), and a page counter sits at the bottom.
- **Expansion presets.** The Expansion field in the editor is a dropdown with
  Core, Heart of Thorns, Path of Fire, Icebrood Saga, End of Dragons, Secrets
  of the Obscure, Janthir Wilds and Visions of Eternity (older custom values
  are kept). Matching icons (`ref/xp_*.png`) show in the rail, the list and
  the preview.
- **NEW badge.** Books you have not opened yet show a gold NEW tag in the
  list; it disappears once you open them. Appending a page marks the book NEW
  again. Existing catalogs are not affected (old books never start as NEW).
- **Delete confirmation.** Deleting a book now shows green Confirm / red
  Cancel buttons, so a stray click can no longer wipe an entry.

### Changed
- The encyclopedia no longer has a size limit — the "Catalog size" setting is
  gone and nothing is trimmed automatically. Existing catalogs are untouched.

### Fixed
- Section headings (bold/italic lines such as "The Flamebearer") were being
  merged into the following sentence. Short lines followed directly by text
  are now kept as their own block, so headed sections read correctly in the
  encyclopedia.

## 0.5.1 — 2026-07-14

### Changed
- Subtitles now sync to the voice at the word level. As the narrator speaks, the
  module reads word-boundary timing from the speech engine (both the offline
  Windows voices and the Edge neural voices) and switches each cue exactly when
  the voice reaches it, instead of estimating from reading speed. It falls back
  to the estimate if a voice reports no word timing, or in subtitles-only
  translation mode.

## 0.5.0 — 2026-07-14

### Added
- Paragraphs are now preserved. OCR detects paragraph breaks from the vertical
  gaps between lines, and the Encyclopedia stores and shows the text with its
  original paragraph structure instead of one run-on block.
- Subtitles are now short, condensed cues (at most two lines) that advance as
  the text is read, instead of showing a whole block at once. (Precise
  word-level sync with the voice is planned for a later update.)

## 0.4.2 — 2026-07-14

### Fixed
- The last line of a lorebook or dialogue was often dropped. The "trim trailing
  noise" step cut everything after the last sentence-ending period, so whenever
  OCR missed a closing period (common — it is a tiny dot) the final sentence was
  lost. It now only strips genuine trailing junk (page numbers, stray marks) and
  keeps a real closing sentence even without its period.

## 0.4.1 — 2026-07-12

### Fixed
- Lorebook text could be cut off at certain resolution / UI-size combinations.
  You can now calibrate the lorebook OCR area visually: open a book (wait for
  the buttons to appear), press `Ctrl+Alt+B` (or *Settings → Calibrate lorebook
  OCR area*), drag the frame over the book text, and Save. Only the size/shape
  is stored; the book's position is still detected automatically on every read.
  Falls back to automatic detection when not set.
- Appending a second page to an already-saved book did not update the open
  Encyclopedia window until it was closed and reopened. The preview now
  refreshes immediately.

## 0.4.0 — 2026-07-07

### Added
- **NPC dialogue & story-journal reading.** Conversation capture mode
  (`Ctrl+Alt+C`) detects open NPC dialogue / personal-story journal windows and
  reads them aloud, saving them to the encyclopedia just like lorebooks.
- **Dialogue zone calibration** (`Ctrl+Alt+Z`). A one-time, draggable and
  resizable frame lets you mark exactly where dialogue text appears on your
  screen. Once set (per resolution), OCR reads that fixed area and the action
  buttons anchor to it — far more reliable than automatic panel detection across
  the game's many lighting conditions. Manage it from *Module Settings →
  Calibrate dialogue zone* / *Clear calibration*.

### Changed
- Conversation OCR now measures the exact text extent, so long lines are no
  longer clipped on the right (e.g. "…scared. It's" / "…probably" are captured
  in full).
- Action buttons redrawn in a lighter, borderless style.

### Fixed
- Dialogue detection on bright daytime scenes — a translucent header over a
  light background no longer defeats detection (two-pass warm scan + per-row
  retry).
- Right-edge truncation of long dialogue lines.

## Earlier

See the Git history for the 0.3.x conversation-capture groundwork and 0.2.2 (the
SSRD `ForceRestore` build-host fix, written up for the wider Blish HUD
community).
