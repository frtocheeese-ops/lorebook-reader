# Calibration playbook — how detection constants get changed safely

This is the distilled process from the sessions that produced the current constants.
Follow it literally; every shortcut here was tried and regretted.

## The loop

1. **Collect evidence, not vibes.** Get the actual screenshot where detection failed
   (2560×1440 PNG, unscaled). If a debug-dump mode exists (planned: setting/keybind
   saving frame + detected rect + raw OCR + cleaned text into the module data folder),
   use it — that is exactly what the Python prototype's Ctrl+Alt+D did and what made
   its calibration fast.
2. **Name the failure.** "OCR wrong" is useless. "The words *scared* and *probably*
   are truncated at line ends" or "*Read on.* appears at the bottom" maps directly to
   a geometric knob (see the symptom table in SKILL.md).
3. **Form a geometric hypothesis** before touching code. Measure on the screenshot if
   possible (pixel-inspect the header strip: is it warm by the current definition?).
4. **Change ONE constant.** Commit message states old → new value and the evidence.
5. **Re-test on (a) the failing screenshot, (b) at least 3 previously-passing ones.**
   A fix that breaks a previous pass is not a fix; it means the model of the UI is
   wrong — go back to step 3 rather than oscillating the constant.
6. **Record the new value + rationale** in the algorithm reference file next to this
   playbook. Un-documented constants rot into superstition.

## Building the golden corpus (do this once, benefit forever)

- Sources: existing calibration screenshots, fresh debug dumps, community reports.
- Coverage axes: dark cave / night / snow / warm-lit zone (e.g. orange lighting) /
  bright daylight; dialog present vs absent; lorebook present vs absent; long vs
  short dialogue text; at minimum 1440p, ideally also 1080p and 4K.
- Store as `corpus/<case-name>/frame.png` + `expected.json`
  (`{"panel": [x,y,w,h] | null, "mustContainWords": [...], "mustNotContain": [...]}`)
  in a separate repo or a non-shipped folder (must NOT enter the .bhm or the module
  repo's release path).
- Harness: a small net48 console project referencing the detector sources directly
  (they are pure Bitmap→Rectangle functions with no Blish dependency — keep them
  that way precisely so this harness stays possible). It loads each frame, runs
  detection (+ optionally OCR + TextCleaner), and asserts expected.json.
- Rule enforced by review: **a PR changing any detector constant links a green
  corpus run.**

## Environment gotchas

- Screenshots must come from **Windowed Fullscreen** captures of the real client —
  JPEG re-compression and scaled images shift both luminance and chroma enough to
  invalidate threshold work.
- GW2 post-processing options (bloom, light adaptation) shift luminance; if a user
  report can't be reproduced, ask for their graphics settings.
- Remember Format24bppRgb byte order (B,G,R) when writing any pixel-inspection tool.
