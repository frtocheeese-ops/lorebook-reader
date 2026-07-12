# Changelog

All notable changes to Lorebook Reader are documented here.
This project follows [Semantic Versioning](https://semver.org).

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
