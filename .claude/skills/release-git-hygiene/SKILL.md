---
name: release-git-hygiene
description: Git workflow, packaging hygiene, and the release checklist for Lorebook Reader. Use this before EVERY commit, push, version bump, zip, or SSRD publish; whenever editing .gitignore; whenever local config files (launchSettings.json) or build outputs might leak into the repo or a release artifact; and when writing release notes. Contains the canonical post-mortem of the launchSettings leak — required reading before packaging anything.
---

# Release & Git Hygiene

## The incident this skill exists for (read once, remember forever)

`Properties/launchSettings.json` — a local IDE debug config — was committed with
placeholder paths (`C:\DOPLN\CESTU\...`) and shipped inside deployment zips. Users
(and the developer) who extracted a zip over a working tree had their **real local
config overwritten by placeholders**, silently breaking F5 debugging. The lesson
generalizes: *anything machine-specific must never be tracked, and release artifacts
must be built from an explicit whitelist, not "zip the folder".*

⚠ **Verified state as of 2026-07-02**: the fix was applied locally, but on
`origin/main` the file is **still tracked with placeholder paths and .gitignore does
not list it**. Adding a path to .gitignore does NOT untrack an already-tracked file.
The complete remediation (do this if not yet done — check first with
`git ls-files Properties/`):

```bash
git rm --cached Properties/launchSettings.json
printf '\nProperties/launchSettings.json\n' >> .gitignore
git add .gitignore
git commit -m "Stop tracking local launchSettings.json (machine-specific paths)"
git push
# local file stays on disk with the REAL paths; it just leaves version control
```

A correct local `launchSettings.json` points `executablePath` at the machine's
`Blish HUD.exe` and passes `--debug --module "<repo>\bin\Debug\net48\LorebookReader.bhm"`.
Never write anyone's actual local paths into tracked files, skills, or docs — describe
them ("your Blish HUD install", "your repo's bin output") instead.

## Standing git rules (project conventions, learned the hard way)

- `.gitignore` minimum: `bin/ obj/ pkg/ .vs/ *.user *.suo packages/` **plus**
  `Properties/launchSettings.json`. If .gitignore ever goes missing/overwritten,
  bin+obj land in the repo — check for it after any zip-extraction into the tree.
- **Always** `git status` between `git add -A` and `git commit`. This is the single
  cheapest guard against the whole leak class. Reviewers should reject any workflow
  transcript that skips it.
- `git config core.autocrlf false` before committing on Windows (avoids CRLF churn
  poisoning diffs).
- Force-push is acceptable only on fresh solo repos; this repo now has a published
  history — treat `--force` as forbidden without explicit human sign-off.
- Branches: unpublished features (like conversation capture pre-release) live on a
  feature branch **that is still pushed to GitHub**. "Local-only until it's ready" is
  how weeks of work end up with zero backup — the v0.3.0 work sat only on one disk
  for a while; don't repeat that. Push early, publish later: an unmerged branch on
  GitHub leaks nothing to SSRD (SSRD builds what you Publish, from main).

## Release checklist (run top to bottom; skipping steps is how incidents happen)

1. **Corpus + tests green** (see testing-validation). Detector-constant changes
   without a corpus run don't ship.
2. **Version bump** in `manifest.json` (and anywhere the version is echoed — README,
   release notes). SSRD refuses reused versions; every publish needs a fresh one.
3. **Clean-tree build parity**: `rm -rf obj bin && dotnet build` → `.bhm` exists.
   (Simulates the SSRD host; see ssrd-build-publish.)
4. **Artifact audit** — list the zip/bhm contents and check against the whitelist:
   sources, csproj, manifest.json, LICENSE, README, nuget.config, ref/ icons.
   **Must NOT contain**: launchSettings.json, bin/, obj/, .vs/, corpus/, screenshots
   used for calibration, any file with machine paths. One command:
   `unzip -l <artifact> | grep -Ei 'launchsettings|bin/|obj/|\.vs|corpus'` → empty.
5. **Local in-game smoke** of the built .bhm (the manual matrix's short form).
6. Commit (status-checked), push, then the SSRD flow (ssrd-build-publish skill):
   Prerelease → review → Public.
7. **Release notes**: keep the project's habit of writing them for the community —
   document any generalizable finding (the ForceRestore note in v0.2.2 is the model).
   Deliberately *omit* unpublished features (the README intentionally doesn't mention
   conversation capture until it ships — keep docs in lockstep with what users have).

## Repo layout reminders

Icons in `ref/` are monochrome white silhouettes on transparency (Blish tints them);
PNG, 16×16 or 32×32. `LICENSE` is MIT, Copyright (c) 2026 Vrae (cheeese.8640).
New-module bootstrap sequence lives in the project instructions and
blish-module-foundations references.
