---
name: blish-module-foundations
description: Foundations for developing Blish HUD modules for Guild Wars 2 in this repo (Lorebook Reader, C#, .NET Framework 4.8). Use this whenever creating a new module, adding a new .cs file, touching the .csproj or manifest.json, adding settings or keybinds, adding UI controls (CornerIcon, windows, buttons), doing anything cross-thread with the game loop, or when a proposed feature might touch ArenaNet ToS limits (input automation, memory reading). Consult it even for "small" changes — the conventions here prevent whole classes of bugs.
---

# Blish HUD Module Foundations (Lorebook Reader)

The project is a Blish HUD overlay module for Guild Wars 2. Blish HUD hosts the module
inside its own XNA/MonoGame process that draws over the game window. The module never
touches the game process itself — everything it knows comes from screenshots, OCR, and
(potentially) MumbleLink shared memory.

## Hard limits — ArenaNet ToS (never negotiate these)

- **No input automation.** No simulated keypresses or mouse clicks, ever — not "just one
  key", not "held keys", not via a helper .exe. A past session explored simulating a held
  Insert key for "show player names"; the conclusion was firm: any software-originated
  input is a ToS violation. (Hardware-level key lock in keyboard firmware — Razer/Corsair/
  Logitech — is the only compliant workaround, and a Blish module cannot provide it.)
- **No memory reading or writing** of the GW2 process. No runtime editing of `Local.dat`
  (GW2 only reads it at startup anyway).
- **Allowed:** screen capture + OCR, overlay rendering, MumbleLink (read-only shared
  memory GW2 itself publishes), the official GW2 web API.

If a feature idea can only work by breaking one of these, say so plainly and stop —
suggest a compliant alternative instead. Do not implement it "behind a setting".

## Project shape

- Language/target: C# on `net48` (`LangVersion=latest`, `AllowUnsafeBlocks=true` — the
  detectors use `unsafe` LockBits pixel access).
- Namespace: `Frtal.LorebookReader`. Manifest namespace: `vrae.lorebook_reader`
  (permanent once registered on SSRD — never change it).
- One class per file, file name == class name. Comments in the codebase are largely
  Czech; keep new comments consistent with the file you're editing.
- Templates live next to this skill in `references/`:
  - `csproj-template.xml` — the exact csproj shape including the **ForceRestore target
    (never remove it — see the ssrd-build-publish skill for why)**.
  - `manifest-template.json` — SSRD-valid manifest (`contributors` array, `.dll` package).
  - `module-skeleton.cs` — minimal module class with MEF export and settings.

## Core patterns (all verified in the shipped code)

### Settings
```csharp
private SettingEntry<float> _speakingRate;

protected override void DefineSettings(SettingCollection settings) {
    _speakingRate = settings.DefineSetting(
        "SpeakingRate", 1.0f,
        () => "Speaking rate",
        () => "1.0 = normal speed.");
    _speakingRate.SetRange(0.5f, 2.0f);
}
```
Setting keys are persistent identifiers — renaming a key silently resets every user's
value. Add new settings; don't rename existing keys.

### Keybinds
```csharp
_readKeybind = settings.DefineSetting("ReadKeybind",
    new KeyBinding(ModifierKeys.Ctrl | ModifierKeys.Alt, Keys.R),
    () => "Read lorebook", () => "...");
// In OnModuleLoaded:
_readKeybind.Value.Enabled = true;
_readKeybind.Value.Activated += OnReadActivated;
// In Unload — ALWAYS detach:
_readKeybind.Value.Activated -= OnReadActivated;
```

### Threading: the single most important rule in this codebase
Background work (capture, OCR, TTS, translation) runs in `Task.Run`. **UI state is only
mutated inside `Update(GameTime)`** on the game thread. The bridge is: background thread
sets a `volatile` flag + payload fields; `Update` notices the flag and applies it.
Real examples: `_bookVisible`/`_bookBox` (detection result → button placement),
`_subtitleDirty`/`_pendingSubtitle` (TTS chunk → subtitle text), `_catalogDirty`
(catalog change → encyclopedia refresh). Follow this pattern for anything new; touching
Blish controls from a worker thread produces intermittent crashes that are miserable to
reproduce.

Known soft spot: the `_readBusy` / `_detectBusy` guards are plain bools written from
multiple threads. If you rework them, use `Interlocked.CompareExchange` — don't add more
plain-bool guards.

### Coordinate spaces (three of them — mixing them up is a classic bug)
1. **Screen pixels** — what `ScreenCapture.Grab` returns (GW2 client area, physical px).
2. **Bitmap pixels** — detector rectangles live here (same scale as 1, origin at client
   top-left).
3. **SpriteScreen units** — Blish UI coordinates, affected by Blish's UI scaling.
   Conversion (see `Update`): `scaleX = spriteScreen.X / gw2ClientRect.Width`, then
   multiply bitmap coords. Any control positioned from a detector rect must go through
   this conversion.

### Resource lifecycle
Everything created in `LoadAsync`/`OnModuleLoaded` is disposed in `Unload`: controls,
CornerIcon, windows, TextRenderer, TTS services. Event handlers are detached. When you
add a disposable field, add its disposal to `Unload` in the same commit — users
enable/disable modules at runtime, and leaks/dangling handlers survive a module reload.

### WinRT on net48
`Windows.Media.Ocr` and `Windows.Media.SpeechSynthesis` work on .NET Framework via the
`Microsoft.Windows.SDK.Contracts` NuGet package (already referenced). No extra setup.
Audio playback goes through NAudio (`WaveOutEvent`), not WinRT playback — WinRT playback
completion events were unreliable; NAudio's `PlaybackStopped` is the dependable signal.

### GDI text (diacritics)
Blish/GW2 bitmap fonts lack diacritics. Any user-visible text that can contain Czech,
German, French, Japanese etc. must go through the shared `TextRenderer` (GDI+ →
Texture2D). See the `gdi-text-rendering` skill before adding any new text UI.

## Environment facts

- GW2 must run in **Windowed Fullscreen** (game default). Exclusive fullscreen cannot be
  captured by `CopyFromScreen` — this is the #1 "nothing works" user report.
- Reference test resolution: 2560×1440. Detection constants were calibrated there;
  the design intent is resolution-relative fractions, but only 1440p is field-verified.
- Local build: `dotnet build` → `bin/Debug/net48/LorebookReader.bhm`; copy the `.bhm`
  into the Blish HUD modules folder to test in-game.

## When adding a feature, walk this checklist

1. ToS-compliant? (See hard limits above.)
2. Which thread does each part run on, and where does the result cross back into
   `Update`? Name the volatile flag.
3. New disposables → added to `Unload`? New event subscriptions → detached?
4. Any new user-visible text → through `TextRenderer`?
5. Any new file that must ship → does the csproj copy it (`CopyToOutputDirectory`)?
6. Then read `testing-validation` for the definition of done, and `release-git-hygiene`
   before committing.
