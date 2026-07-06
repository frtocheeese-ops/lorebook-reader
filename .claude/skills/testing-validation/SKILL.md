---
name: testing-validation
description: How changes to Lorebook Reader are validated — the manual in-game test matrix, the golden-corpus regression policy for detection/OCR/cleanup, and the definition of done. Use this before declaring ANY task finished, before every commit that changes behavior, before merges and releases, when someone asks "is this safe to ship", and when deciding how much testing a given change needs. Cheap sessions especially: this skill is what stands between a plausible-looking diff and a regression.
---

# Testing & Validation

The project has no CI yet (improvement list) — discipline substitutes for it. The two
pillars: **offline regression corpora** for everything that is a pure function of an
image or a string, and a **manual in-game matrix** for everything that isn't.

## Definition of done (any behavioral change)

1. `dotnet build` succeeds **from a clean tree** (`rm -rf obj bin` first — SSRD parity).
2. Relevant corpus is green (see below); if the change created a new failure case,
   that case was *added to the corpus first* and now passes.
3. The affected rows of the manual matrix pass in-game at 2560×1440, Windowed
   Fullscreen.
4. No new artifact-hygiene violations (release-git-hygiene checklist item 4).
5. Behavior change is reflected where users learn about it (README/settings text) —
   or deliberately withheld if the feature is unpublished.
6. For reviewers: the diff touches nothing on the "load-bearing invariants" list
   (references/invariants.md) without explicit justification.

## Corpus policy (pure-function layers)

Detection (`ParchmentDetector`, `ConversationDetector`) and text processing
(`TextCleaner`) are pure Bitmap→Rectangle / string→string functions with no Blish
dependency — **keep them that way**; it is what makes offline testing possible.

- Detection corpus: `gw2-panel-detection/references/calibration-playbook.md`
  (frame.png + expected.json per case; coverage across dark/bright/warm scenes).
- Text corpus: `ocr-text-pipeline/references/golden-corpus.md` (raw→expected pairs;
  seed cases listed there, including the ones that pin real historical fixes).
- Enforcement rule: **no detector-constant or cleaner-rule change merges without a
  green corpus run linked in the change description.** If the corpus doesn't exist in
  your session yet, creating the minimal harness + the cases relevant to your change
  IS part of the change.
- Harness shape: a small net48 console project compiling the detector/cleaner sources
  directly. Milliseconds per text case, well under a second per frame — there is no
  cost argument for skipping it.

## Manual in-game matrix (full form for releases; affected rows for commits)

Environment: 2560×1440, Windowed Fullscreen, real GW2 client.

| # | Scenario | Pass means |
|---|---|---|
| 1 | Open a lorebook | three buttons appear next to the parchment within ~1–2 s; disappear when closed |
| 2 | Read (Ctrl+Alt+R or button) | speech starts, reads full pages, no mid-sentence stops, Stop (Ctrl+Alt+S) halts immediately |
| 3 | Save / Append buttons | toasts confirm; entry appears/extends in encyclopedia; append twice in a row doesn't duplicate |
| 4 | Subtitles | shown while speaking, wrapped (no edge clipping), cleared at end; drag-edit persists position across reload |
| 5 | Conversation mode (Ctrl+Alt+C; unpublished) | buttons appear on an NPC dialog; OCR text complete at line ends (spot-check the classic words: nothing truncated like "scared"→"sca") and free of response options ("Read on.") |
| 6 | Voice engines | Windows voice works; Edge voice works; disconnect network mid-Edge-read → toast + offline fallback |
| 7 | Translation modes | subtitles-mode: speech original, subtitles translated; full-mode: both translated, voice switches language; break the network → original text, reading continues |
| 8 | Encyclopedia | search/sort/filters behave; edit+save persists; export writes a file; import merges it |
| 9 | Non-English sanity (if touched) | de/fr/es client text isn't silently trimmed (known IsValidWord bug — see ocr-text-pipeline) |
| 10 | Lifecycle | disable+enable the module in Blish: no crash, no duplicate buttons/handlers, settings intact |
| 11 | Negative detection | wander bright snow / warm-lit / dark-cave scenes with no UI open: buttons stay hidden (brief flickers are the documented known issue — new *persistent* false positives are regressions) |

## Reporting results

State results as evidence, not adjectives: which rows ran, on what scene/resolution,
exact failing words or rects for detection issues (that phrasing is what makes the
calibration loop work — see the playbook). "Tested, works" is not a test report.
