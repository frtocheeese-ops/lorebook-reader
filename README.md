# Lorebook Reader

A [Blish HUD](https://blishhud.com) module for **Guild Wars 2** that reads
open lorebooks aloud and keeps a searchable encyclopedia of everything
you've read — so you can keep playing while you listen to the story.

## What It Looks Like

### Open a lorebook → three action buttons appear
<img width="505" height="640" alt="image" src="https://github.com/user-attachments/assets/48e3e5bf-afb4-4df6-8164-3a8dd4570be6" />


Click **🔊** to read aloud + save, **➕** to save only, or **⬇️** to
append this page to the last saved book.

### Subtitles while playing
<img width="685" height="391" alt="image (1)" src="https://github.com/user-attachments/assets/dcab43ed-8268-406b-b51f-687eadd329db" />


Optional on-screen captions with full diacritics, adjustable size and
position.

### Encyclopedia — your personal lorebook collection
<img width="685" height="520" alt="image (2)" src="https://github.com/user-attachments/assets/1530cf62-998f-4283-8124-58a381e22408" />


Search, sort, tag, edit, translate, and replay any book you've saved.

## Features

### 📖 Read Aloud
Open a lorebook in-game and press a keybind (default `Ctrl+Alt+R`) or
click the speaker icon that appears next to the book. The module captures
the text with on-screen OCR and reads it with text-to-speech while you
keep playing.

### 🗨️ Read NPC Dialogues & Story Journals
Turn on **conversation capture** (`Ctrl+Alt+C`) to also read personal-story
journals and NPC dialogue windows aloud — not just lorebooks. Because these
sit over the moving game world, a quick one-time **calibration** (`Ctrl+Alt+Z`)
marks where the dialogue text appears so OCR stays reliable. See
[Reading NPC Dialogues](#reading-npc-dialogues-ocr).

### 💾 Quick-Save & Append
Three clickable icons appear next to every detected lorebook:
- **🔊 Speaker** — read aloud and save to encyclopedia
- **➕ Save** — save to encyclopedia without reading (useful when you
  want to read it yourself but keep it for later)
- **⬇️ Append** — append the current page to the last saved book (for
  multi-page lorebooks that spread across several pages)

### 💬 Subtitles
Optional on-screen subtitles displayed while reading:
- Full diacritics for all languages (uses GDI+ rendering, not the
  limited in-game font)
- Adjustable font size (Small / Medium / Large / Huge)
- Adjustable opacity (20–100%)
- Drag-to-place positioning — click "Edit subtitle position" in
  settings, drag the sample text where you want it
- Shadow rendering for readability over any background

### 📚 Encyclopedia
Every book you read or save is stored in a searchable, sortable catalog:
- **Search** across titles, text, and all metadata
- **Sort** by newest, oldest, title A–Z / Z–A, or color tag
- **Color tags** — tag books with colors (Red, Orange, Yellow, Green,
  Blue, Purple, Teal) for visual organization
- **Metadata** — record expansion, theme, location where you found it,
  and free-form notes
- **Edit text** — fix OCR errors or typos directly in the encyclopedia
- **Translate** — translate any saved book into 11 languages and store
  the translation alongside the original
- **Preview** — read books on a parchment-style panel with adjustable
  font size (A+ / A−)
- **Replay** — play any saved book through TTS at any time
- **Export / Import** — share your collection with friends as a JSON file

Access the encyclopedia by clicking the **book icon** in the top-left
Blish HUD icon bar.

### 🗣️ Voice Options
Two voice engines are available:

| Engine | Privacy | Quality | Requires |
|--------|---------|---------|----------|
| **Windows voices** (default) | Fully offline — nothing leaves your PC | Functional | Windows speech voices installed |
| **Edge neural voices** (opt-in) | Sends text to Microsoft endpoint | Natural, human-like | Internet connection |

The module defaults to **Windows voices** (offline). Edge neural voices
must be explicitly enabled in settings. If the online service is ever
unavailable, the module automatically falls back to the offline voice.

Available neural voices include English (US, GB, AU), German, French,
and Spanish — both male and female options.

### 🌍 Translation
Optionally translate the narration and/or subtitles into another
language. Three modes:

| Mode | What it does |
|------|-------------|
| **Off** (default) | No translation — original text only |
| **Subtitles only** | Speech stays in original language, subtitles show translation |
| **Subtitles + speech** | Both subtitles and speech are translated; voice automatically switches to a native speaker for the target language |

Supported languages: Czech, German, Spanish, French, Italian, Polish,
Portuguese, Russian, Japanese, Korean, Chinese.

Translation uses a free online endpoint and must be explicitly enabled.
If unavailable, the module falls back to the original text.

## Installation

1. Install [Blish HUD](https://blishhud.com) if you haven't already.
2. In Blish HUD, open the menu → **Manage Modules**.
3. Search for **Lorebook Reader** → install and enable.

## Usage

### Reading a Lorebook
1. Open any lorebook in-game (click on it in the world).
2. Three small icons appear to the right of the book:
   - Click **🔊** to read aloud + save
   - Click **➕** to save only
   - Click **⬇️** to append this page to the last saved book
3. Or press **Ctrl+Alt+R** (rebindable) to read the currently open book.
4. Press **Ctrl+Alt+S** (rebindable) to stop reading at any time.

### Reading NPC Dialogues (OCR)

Personal-story journals and NPC dialogue windows can be read too. Unlike a
lorebook's solid parchment, they sit over the moving game world, so the module
needs to know where the dialogue text appears. You tell it once:

1. **Turn on conversation capture** — press **Ctrl+Alt+C**, or tick
   *Conversation capture mode* in the module settings. (A tip pops up the first
   time.)
2. **Calibrate the zone** — open any dialogue, then press **Ctrl+Alt+Z** (or
   *Settings → Calibrate dialogue zone*). A frame appears on screen.
3. **Drag the frame over the dialogue text only** — move it by its body, resize
   it with the corner handles. Cover the narrative text; leave out the
   "Read on." / "Close" options and the journal icon on the right. Click
   **Save zone**.
4. **Read as usual** — with a dialogue open, press **Ctrl+Alt+R** or click the
   speaker button. The text is saved to the Encyclopedia like any book.

The zone is stored **per screen resolution** — recalibrate (or use *Clear
calibration*) if you change your resolution or UI size. Without calibration the
module falls back to automatic detection, which works but is less reliable
across the game's lighting conditions.

### Using the Encyclopedia
1. Click the **book icon** in the top-left Blish HUD icon bar.
2. Browse, search, or filter your saved lorebooks.
3. Click any book to preview it on parchment.
4. Use **▶ Play** to hear it again, **Edit** to fix text or add metadata.
5. **Export** your collection to share, **Import** to merge a friend's.

## Settings

All settings are accessible in Blish HUD → Manage Modules → Lorebook
Reader → Settings (gear icon).
<img width="685" height="509" alt="image (3)" src="https://github.com/user-attachments/assets/1caa3385-3349-4070-952c-eeedd52da163" />



### Keybinds
| Setting | Default | Description |
|---------|---------|-------------|
| Read lorebook | `Ctrl+Alt+R` | Captures and reads the open lorebook or dialogue |
| Stop reading | `Ctrl+Alt+S` | Stops current TTS playback |
| Toggle conversation capture | `Ctrl+Alt+C` | Turn NPC-dialogue detection on/off |
| Calibrate dialogue zone | `Ctrl+Alt+Z` | Mark where dialogue text appears (once per resolution) |

### Detection
| Setting | Default | Description |
|---------|---------|-------------|
| Show speaker icon on open books | On | Display clickable icons next to detected lorebooks |
| Conversation capture mode | Off | Also detect NPC dialogue / story-journal windows |
| Calibrate dialogue zone | — | Draw a frame over the dialogue text for reliable OCR |
| Clear calibration | — | Forget the saved zone and use automatic detection |

### Voice
| Setting | Default | Description |
|---------|---------|-------------|
| Voice engine | Windows (offline) | Choose between offline Windows voices or online Edge neural voices |
| Windows voice | (auto) | Pick a specific installed voice, or leave on auto to match OCR language |
| Edge neural voice | en-GB-RyanNeural | Select from curated list of natural-sounding voices |
| Speaking rate | 1.0× | Speed of narration (0.5× to 2.0×) |

### OCR
| Setting | Default | Description |
|---------|---------|-------------|
| OCR language | en-US | Must match your GW2 client language for best accuracy. Options depend on installed Windows language packs |

### Subtitles
| Setting | Default | Description |
|---------|---------|-------------|
| Show subtitles | On | Display text overlay while reading |
| Subtitle opacity | 90% | Transparency of subtitle background |
| Subtitle position X/Y | 50% / 82% | Position on screen (or drag in edit mode) |
| Subtitle size | Medium (24) | Small (18) / Medium (24) / Large (32) / Huge (36) |
| Edit position | — | Enter drag mode to reposition subtitles with mouse |

### Translation
| Setting | Default | Description |
|---------|---------|-------------|
| Translation | Off | Off / Subtitles only / Subtitles + speech |
| Translate to | Czech | Target language for translation |

### Catalog
| Setting | Default | Description |
|---------|---------|-------------|
| Catalog size | 10 | Maximum number of books to keep (5–100) |

## Privacy & Third-Party Services

By default **everything runs locally** on your machine:
- **OCR**: Windows built-in OCR engine (no data sent anywhere)
- **Voices**: Windows offline TTS voices (no data sent anywhere)

Two optional features send text to third-party online services and are
**off by default**:

- **Edge neural voices** — sends book text to a free Microsoft speech
  endpoint (the same one used by Edge browser's Read Aloud feature).
  Clearly marked in settings as "(online)".
- **Translation** — sends book text to a free online translation
  endpoint. Clearly marked in settings.

Both must be explicitly enabled by the user. Both are clearly labeled in
the settings UI. If either service is unavailable, the module
automatically falls back to the offline voice or original text.

**The module does not read game memory.** It works purely via screenshot
capture and OCR overlay, which is permitted by ArenaNet's Terms of
Service.

## Known Issues
As the module checks for a specific pattern and brightness marks, 
it may occur that the three buttons appear on some surfaces similiar to the parchment texture.
However, since the scan is performed in a matter of seconds it shouldn't be very noticable.

For NPC dialogues, calibrating the dialogue zone (Ctrl+Alt+Z) avoids this
entirely — the buttons then anchor to your marked area instead of relying on
automatic detection.

## Requirements

- [Blish HUD](https://blishhud.com) 1.2 or newer
- Guild Wars 2 in **Windowed Fullscreen** mode (the default)
- Windows 10 or 11
- For best OCR accuracy: install the Windows language pack matching your
  GW2 client language (Settings → Time & Language → Language & region →
  Add a language)

## How It Works (Technical)

The module captures a screenshot of the GW2 client area, detects the
lorebook parchment using luminance + chromaticity analysis, crops the
text region, runs Windows OCR on it, cleans up the result, and feeds it
to the selected TTS engine. NPC dialogues are detected the same way, or —
when you calibrate a dialogue zone — read from that fixed screen area for
reliability. Everything happens in the background — your gameplay is never
interrupted.

## Building from Source

Requires:
- Visual Studio 2022 with the *.NET desktop development* workload
- .NET Framework 4.8 targeting pack

```
git clone https://github.com/frtocheeese-ops/lorebook-reader.git
cd lorebook-reader
dotnet restore
dotnet build
```

The output `bin\Debug\net48\LorebookReader.bhm` is the installable module
file. Copy it to your Blish HUD modules folder to test.

### Note for module developers

The csproj includes a `ForceRestore` MSBuild target that ensures NuGet
restore runs before build. This is required because the SSRD build host
does not run `dotnet restore` for SDK-style projects. Without this
target, SSRD builds fail with `NETSDK1004`. See the
[release notes](https://github.com/frtocheeese-ops/lorebook-reader/releases)
for details.

## License

[MIT](LICENSE) — Copyright (c) 2026 Vrae (cheeese.8640)

## Acknowledgements

Built on the [Blish HUD](https://blishhud.com) module framework.

Neural voice synthesis uses the same endpoint as Microsoft Edge's Read
Aloud feature. Translation uses a free Google Translate endpoint.
Neither is an official, supported API — they are opt-in features that
may become unavailable at any time.
