---
name: gdi-text-rendering
description: The shared GDI+ TextRenderer that gives Lorebook Reader full-Unicode text (Czech, German, Japanese diacritics) inside Blish HUD, plus the ParchmentTextPanel that uses it. Use this whenever adding ANY new user-visible text element, whenever diacritics render as boxes/missing glyphs, whenever text UI causes GC pressure or texture leaks, when tuning fonts or wrapping, or before creating a Label with a Blish built-in font for content that could contain non-ASCII.
---

# GDI Text Rendering

## Why this exists (and when you must use it)

Blish HUD's built-in GW2 bitmap fonts have no diacritics. Any text a user might see in
Czech, German, French, Spanish, Japanese, etc. — subtitles, parchment previews,
translated content, titles from OCR — **must** render through the shared
`TextRenderer` instance (`module.SharedTextRenderer`). Plain Blish `Label`s are fine
only for fixed English UI strings (settings labels, button tooltips).

## How it works

`TextRenderer` draws a single line with GDI+ (`System.Drawing`) into a 32bpp bitmap,
converts BGRA → **premultiplied** RGBA (XNA requirement — skipping premultiplication
gives dark halos around glyphs), and uploads it as a `Texture2D`.

- Font: first installed of `Cantarell → Segoe UI → Tahoma → Arial` (Cantarell is the
  closest to GW2's look), size in **pixels** (`GraphicsUnit.Pixel`),
  `GenericTypographic` + `AntiAliasGridFit` for measurement/draw consistency.
- **Cache**: key = `size|bold|color|text`, FIFO capacity 60, evicted textures are
  disposed. Consequences: (a) a line rendered in two colors (text + shadow) costs two
  slots — ~30 distinct visible lines fit; (b) rapidly-changing unique text (e.g. a
  per-frame counter) would thrash the cache — don't feed it such text; (c) callers
  must NOT dispose returned textures (the cache owns them) and must NOT keep them
  across frames (eviction may dispose them) — re-request each Paint, it's a dict hit.
- `MeasureWidth` / `WrapText` / `LineHeight` provide pixel-accurate layout. Note each
  Measure call news up a throwaway Bitmap+Graphics+Font — fine at subtitle scale;
  if you ever wrap book-length text per frame, cache the Font per size and reuse one
  measuring Graphics (known cheap optimization, not yet needed).

## Consumers & their patterns

- **SubtitleOverlay**: wraps into 45%-of-screen box, centers each line, draws shadow
  (same string, dark, +2 px offset) then text. Paint is wrapped in try/catch that
  hides the overlay on failure — rendering must never crash the game overlay.
- **ParchmentTextPanel / ParchmentContent**: scrollable book preview on the parchment
  texture, ink color RGB(48,36,20), same wrap machinery, relayouts on width/font
  change only (never per frame).
- **EncyclopediaView editor**: uses `WrapForEdit`/`UnwrapFromEdit` with a soft-break
  marker (zero-width space + `\n`, `"\u200B\n"`) so the TextBox wraps visually without
  polluting the stored text — preserve that round-trip if touching the editor.

## Rules for new text UI

1. Could the string ever be non-English? → TextRenderer, not a Blish font.
2. Layout work (wrapping, measuring) happens on content change, not in Paint.
3. Paint only draws cached textures; wrap Paint in try/catch that degrades (hide)
   rather than throws.
4. One shared TextRenderer per module (it owns a texture cache tied to the
   GraphicsDevice); it is created in `LoadAsync` and disposed in `Unload` — never
   construct private instances per control.
5. Sanitize OCR-sourced strings for the *game-font* path only; the GDI path needs no
   sanitization (that is its whole point).
