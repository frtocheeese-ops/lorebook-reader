# Code review rubric (used by the code-reviewer subagent — and by humans)

Verdicts per finding: **blocker** (must fix before merge) / **should** (fix now or
file it visibly) / **nit** (author's call). A review without at least one concrete
observation about testing evidence is incomplete.

## 1. Invariants (blocker territory)
Walk testing-validation/references/invariants.md against the diff. Any touched
invariant without explicit justification in the change description → blocker.

## 2. Correctness
- Threading: does anything mutate UI outside Update()? Any new plain-bool guard
  written from multiple threads? Any await without a following cancellation check in
  a speak/capture loop?
- Coordinate spaces: bitmap px vs SpriteScreen units converted at every crossing?
- Resource lifecycle: every new IDisposable disposed in Unload; every += has a −=.
- Geometry: new rectangles expressed as fractions, clamped to frame bounds?
- Serialization: additive-only changes to LorebookEntry; old catalog.json still loads?

## 3. Failure behavior
- Does the change preserve graceful degradation (Edge→offline, translation→original,
  paint→hide)? Does any new network/OS call have a timeout and a user-visible outcome?
- What happens on the FIRST bad input (null OCR, empty text, zero-size window)?

## 4. Evidence
- Detector/cleaner change → corpus run linked, one-parameter rule respected,
  old→new values in the message? (No corpus evidence = blocker for these files.)
- Behavioral change → which manual-matrix rows ran, with what result?
- Clean-tree build (`rm -rf obj bin && dotnet build`) confirmed?

## 5. Hygiene
- git status output shown before commit? Artifacts whitelist untouched? No
  machine-specific paths introduced into tracked files? Version bump if manifest or
  user-visible behavior changed? Docs updated — or correctly withheld for
  unpublished features?

## 6. Scope
- Diff does one thing? Opportunistic "cleanups" of load-bearing oddities (ForceRestore,
  invert flag, sentence chunking) reverted or justified?

## Output format the reviewer must produce
```
VERDICT: approve | approve-with-shoulds | request-changes
BLOCKERS: (file:line — what — why — suggested fix)
SHOULDS: ...
NITS: ...
EVIDENCE CHECK: build? corpus? matrix rows? git status?
INVARIANTS TOUCHED: none | list with justification assessment
```
