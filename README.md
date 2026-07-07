# Lorebook Reader

A [Blish HUD](https://blishhud.com) module for **Guild Wars 2** that reads
open lorebooks aloud and keeps a searchable encyclopedia of everything
you've read — so you can keep playing while you listen to the story.

![icon](ref/book.png)

## Features

- **Read aloud** — open a lorebook in-game and press a keybind (default
  `Ctrl+Alt+R`) or click the speaker icon next to the book. The text is
  captured with on-screen OCR and read with text-to-speech, so your
  gameplay is never interrupted.
- **Read NPC dialogues & story journals** — turn on conversation capture
  (`Ctrl+Alt+C`) to also read personal-story journals and NPC dialogue
  windows aloud, not just lorebooks. A quick one-time calibration
  (`Ctrl+Alt+Z`) marks where dialogue text appears so OCR stays reliable —
  see [Reading NPC dialogues](#reading-npc-dialogues-ocr).
- **On-screen subtitles** — optional captions in a Guild Wars 2-style
  font with adjustable size, opacity and drag-to-place position. Full
  diacritics for every language.
- **Encyclopedia** — every book you read is saved to a searchable,
  sortable catalog. Add color tags, edit titles, record metadata
  (expansion, theme, where you found it), fix OCR errors, append later
  pages, and read any saved book again on demand.
- **Voices** — uses your installed offline Windows voices by default.
  Optional online neural voices (Microsoft Edge) are available as an
  opt-in for more natural narration.
- **Translation** — optional translation of subtitles and/or speech into
  a range of languages (opt-in, uses a free online service).
- **Export / import** — share your encyclopedia with friends as a JSON
  file.

## Privacy & third-party services

By default everything runs **locally** on your machine (Windows OCR and
Windows offline voices). Two optional features send text to third-party
online services, and are **off by default**:

- **Edge neural voices** — sends the book text to a free Microsoft
  speech endpoint to synthesize higher-quality narration.
- **Translation** — sends the book text to a free online translation
  endpoint.

Both are clearly marked in the settings and must be enabled manually. If
either service is unavailable, the module automatically falls back to the
offline voice or the original text.

## Requirements

- Guild Wars 2 running in **Windowed Fullscreen** (the default)
- Blish HUD 1.2 or newer
- Windows 10/11 (uses built-in Windows OCR and speech)
- For best OCR, install the language pack matching your GW2 client
  language under *Settings → Time & Language*

## Installation

Once released, install via Blish HUD's built-in module repository:
open the Blish HUD menu → **Manage Modules** → find *Lorebook Reader* →
install and enable.

## Usage

1. Open a lorebook in-game.
2. Press `Ctrl+Alt+R` (rebindable) or click the speaker icon by the book.
3. Click the book icon in the top-left icon bar to open the
   **Encyclopedia** and browse everything you've read.

### Reading NPC dialogues (OCR)

Personal-story journals and NPC dialogue windows can be read too. Unlike a
lorebook's solid parchment, they sit over the moving game world, so the module
needs to know where the dialogue text appears. You tell it once:

1. **Turn on conversation capture** — press `Ctrl+Alt+C`, or tick *Conversation
   capture mode* in the module settings. (A tip pops up the first time.)
2. **Calibrate the zone** — open any dialogue, then press `Ctrl+Alt+Z` (or
   *Settings → Calibrate dialogue zone*). A frame appears on screen.
3. **Drag the frame over the dialogue text only** — move it by its body, resize
   it with the corner handles. Cover the narrative text; leave out the
   "Read on." / "Close" options and the journal icon on the right. Click
   **Save zone**.
4. **Read as usual** — with a dialogue open, press `Ctrl+Alt+R` or click the
   speaker button. The text is saved to the Encyclopedia like any book.

The zone is stored **per screen resolution** — recalibrate (or use *Clear
calibration*) if you change your resolution or UI size. Without calibration the
module falls back to automatic detection, which works but is less reliable
across the game's lighting conditions. Keep GW2 in **Windowed Fullscreen** so
the screen capture works.

## Building from source

Requires Visual Studio 2022 with the *.NET desktop development* workload
and the *.NET Framework 4.8 targeting pack*. Open `LorebookReader.csproj`,
restore NuGet packages, and build. The output
`bin\Debug\net48\LorebookReader.bhm` is the installable module.

## License

[MIT](LICENSE)

## Acknowledgements

Built on the [Blish HUD](https://blishhud.com) module framework. Neural
voice synthesis uses the same endpoint as Microsoft Edge's Read Aloud;
translation uses a free Google Translate endpoint. Neither is an official,
supported API.
