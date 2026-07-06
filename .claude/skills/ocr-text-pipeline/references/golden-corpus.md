# Golden text corpus — regression cases for TextCleaner

Purpose: every OCR-correction and cleanup rule exists because of a real failing
string. Keep those strings, assert on them forever. Cheap insurance against the
classic failure mode of cleanup code: a new rule silently mangling old-correct text.

## Format

`corpus/text/<case>.json`:
```json
{
  "raw":        "the exact string OCR produced",
  "expected":   "what CleanForTts + FixConfusableChars must output",
  "mustKeep":   ["Level 80", "11 days"],
  "note":       "why this case exists / which rule it pins"
}
```

## Seed cases (from real calibration history — start the corpus with these)

| raw fragment | expected | pins |
|---|---|---|
| `J wake before dawn` | `I wake before dawn` | J→I regex (space-separated form) |
| `8ecause of the 5torm` | `Because of the Storm` | 8↔B, 5↔S in alphabetic context |
| `Level 80 boost` | `Level 80 boost` (unchanged) | numeric-token preservation |
| `He said,11Hello11` | `He said, Hello` (quotes-as-11 removed) | the `11` quote artifact |
| `11 days remained` | `11 days remained` (unchanged) | real numbers survive |
| `\| will not fail` | `I will not fail` | standalone pipe → I before lowercase |
| `~~~ * ~~~\nActual text here.` | `Actual text here.` | decorative line trimming |
| line of accented words (de/fr/es) | must NOT be trimmed | the IsValidWord ASCII bug (currently FAILS — fix tracked in SKILL.md) |

Also pin the words that drove panel calibration as end-to-end cases (frame → text):
`scared`, `It's`, `probably` must appear; `Read on.` must not.

## Harness

Same net48 console harness as the detection corpus (see
gw2-panel-detection/references/calibration-playbook.md) — TextCleaner is a static
class with pure string functions and zero Blish dependencies; keep it that way so it
stays trivially testable. Text cases run in milliseconds; there is no excuse to skip
them on any TextCleaner change.
