# Changelog

All notable changes to Lorebook Codex and TTS are documented here.
This project follows [Semantic Versioning](https://semver.org).

## 0.8.0 — 2026-07-18

The module is now **Lorebook Codex and TTS**. This release turns the
encyclopedia into a proper in-game codex: hand-painted artwork throughout,
full-window reading and editing, and a round of stability fixes for people who
catalogue a lot of books by hand.

### Added
- **Full-window reading.** A button at the top of the book expands it across
  the whole window for comfortable reading. The rail and list step aside and
  only the book, its page-turn buttons and the font size controls remain. The
  same button brings the normal layout back.
- **Full-window editing.** Pressing Edit now expands the editor across the
  entire frame, with the metadata fields on the left and a large text box on
  the right. Press "Done editing" to return to browsing.

### Changed
- **Renamed to "Lorebook Codex and TTS".** Only the display name changed. Your
  saved books and settings are untouched.
- **Hand-painted artwork throughout.** New icons for the in-world buttons
  (read aloud, save, append) with hover variants, a monochrome open-book icon
  that blends into the corner toolbar, carved stone and gold page-turn discs,
  matching buttons for the full-window toggle, a new aged parchment page with
  worn edges, gold corner flourishes on covers, and an optional book-themed
  frame for the window itself.
- **Wax seal on plain covers.** Books without an expansion now carry a red wax
  seal, so every cover has a mark (books with an expansion keep their logo
  stamp).
- **Bigger window and a larger editor font.** The codex opens larger, is still
  freely resizable, and the edit box uses a bigger, more readable font. The
  window resets to the new default size once.
- **Reading polish.** The page turn now eases in and out instead of moving
  linearly, pages carry a subtle aged patina at their edges, rail and list rows
  highlight on mouse-over, unopened books pulse gold on their spine, and the
  reader fades in softly when a book opens.
- The full-window toggle and its restore button are clearly visible on both the
  parchment and the dark window.
- README refreshed for the new interface, including both calibrations
  (Ctrl+Alt+Z for dialogues, Ctrl+Alt+B for lorebooks).

### Fixed
- **Editing no longer freezes Blish HUD.** The editor used to write the whole
  catalogue to disk on every keystroke. Edits are now saved shortly after you
  stop typing, when you leave the editor, and when you close the window, which
  removes the lock-ups people hit during long manual edits.
- **Clicking in the editor no longer crashes Blish HUD.** Clicking below the
  last line of the text box could take the whole overlay down. The box is now
  sized tightly to its text so that click target no longer exists.
- **The text editor scrolls.** Long books used to run off the bottom with no
  way to reach the text below.
- **Bullet points and vertical lists survive capture.** Bulleted and numbered
  lists, and table-like blocks such as name registries, stay on their own lines
  instead of being merged into one run-on paragraph.
- **Translating a saved book no longer risks a crash.** The result was updating
  the interface from a background thread; it now hands the update back to the
  main thread.
- The old backup icons folder is no longer packed into the module file.

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
