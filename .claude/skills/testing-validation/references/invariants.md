# Load-bearing invariants — reviewers block changes to these without justification

Each of these looks like a candidate for "cleanup". Each is load-bearing. A change
touching one must explain why it is safe, in the change description.

1. **ForceRestore MSBuild target** in the csproj — SSRD host builds die without it
   (NETSDK1004). Looks redundant locally by design.
2. **No .slnx files** in the repo — SSRD host can't build them.
3. **UI mutation only in Update()**; background→UI via volatile flag + payload.
4. **Detectors and TextCleaner stay pure and Blish-free** (Bitmap/string in → value
   out) — the entire offline-testing strategy depends on it.
5. **Fractions, not raw pixels**, in every derived rectangle (resolution survival).
6. **Sentence-boundary chunking** for TTS (never split mid-sentence; the 80-char
   splitter regression is audible).
7. **onChunk(null) in a finally** at end of every speak path (subtitle clear).
8. **Edge failure degrades to offline voice + toast**; translation failure degrades
   to original text. Speech-path errors never abort a read that could continue.
9. **Manifest namespace `vrae.lorebook_reader` is permanent**; version never reused.
10. **Setting keys are permanent identifiers** (rename = silent user reset).
11. **Numeric-token preservation** in confusable-char fixes ("Level 80" survives).
12. **AppendToLatest invalidates cached translation** on text mutation.
13. **launchSettings.json untracked**; release artifacts built from a whitelist.
14. **ToS wall**: no input automation, no memory access — no exceptions, no settings
    that enable them.
15. **README/docs describe only published features** (conversation capture stays out
    until it ships).
