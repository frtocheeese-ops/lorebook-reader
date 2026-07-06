---
name: ssrd-build-publish
description: Everything about building and publishing Blish HUD modules through the SSRD portal (ssrd.blishhud.com). Use this whenever a build fails on SSRD but works locally, whenever NETSDK1004 appears, whenever preparing a release/publish/version bump, when editing manifest.json or the csproj build targets, when registering a new module, or when the SSRD review process is involved. Also use before touching anything named ForceRestore.
---

# SSRD: Build Host & Publishing

SSRD (ssrd.blishhud.com) is the official Blish HUD module registry. It pulls the repo
from GitHub, builds it on its own host, and distributes the resulting `.bhm`. The host
has quirks that cost this project real debugging time — they are encoded here so nobody
pays that price twice.

## The build host, in one paragraph

The host has a dotnet SDK (10.x at last check) but **does not run `dotnet restore` for
SDK-style projects** before building. Without restore, `project.assets.json` doesn't
exist and MSBuild fails with **NETSDK1004** ("Assets file not found") — the build "runs"
but produces no `.bhm` artifact. The fix that lives in the csproj is the `ForceRestore`
target: it self-invokes `dotnet restore` when the assets file is missing, guarded by a
`ForceRestoreRunning` property so it doesn't recurse.

```xml
<Target Name="ForceRestore"
        BeforeTargets="CollectPackageReferences;ResolvePackageAssets"
        Condition="!Exists('$(BaseIntermediateOutputPath)project.assets.json') AND '$(ForceRestoreRunning)' != 'true'">
  <Exec Command="dotnet restore &quot;$(MSBuildProjectFullPath)&quot; -p:ForceRestoreRunning=true" />
</Target>
```

**Never remove or "clean up" this target.** It looks redundant locally (local builds
restore fine without it) — that is exactly why it keeps getting flagged as dead code.
It is load-bearing on SSRD only. This finding was published in the v0.2.2 release notes
for the wider Blish community.

Second host quirk: the `.slnx` solution format is not supported. Don't add `.slnx`
files to the repo; the plain csproj (no solution) builds fine.

`nuget.config` in the repo pins nuget.org as the only source — keep it; it makes the
host restore deterministic.

## Local build parity check

Before pushing anything that touches build files, prove parity locally:

```bash
rm -rf obj bin          # simulate the host's cold state (no assets file)
dotnet build            # ForceRestore must kick in and the build must succeed
ls bin/Debug/net48/*.bhm
```

If that sequence works from a clean tree, SSRD will almost certainly build it too.

## Publishing flow

1. **Register** the module in SSRD via the Git URI (must end in `.git`).
   The manifest `namespace` becomes permanent at this step.
2. Set up the **GitHub webhook** (Push events) so SSRD sees new commits.
3. **Version bump first.** SSRD never accepts the same version twice — every Publish
   (including "just a manifest description fix") needs a new `version` in manifest.json.
   Keep the csproj/README version mentions in sync in the same commit.
4. **Publish → Prerelease** (first release of anything is always prerelease).
5. Wait for review — an automated bot pass plus a human reviewer; turnaround has ranged
   from minutes to about a week.
6. After approval and in-game testing of the prerelease build → **Publish → Public**.

Manifest requirements the review actually checks (details + template in
`blish-module-foundations/references/`): `contributors` array (not the legacy `author`
object), `package` ending in `.dll`, `manifest_version: 1`, and a `dependencies` entry
for `bh.blishhud`.

## When the host build fails anyway

- Read the SSRD build log first; match the error against this file.
- Reproduce locally from a clean tree (commands above). "Builds locally" without the
  `rm -rf obj bin` step proves nothing about the host.
- If it's genuinely host-side, the SSRD reviewer (Freesnöw) is responsive on the Blish
  HUD Discord (#module-dev-discussion). Report: exact error text, confirmation that a
  clean local build succeeds, and the commit hash. That combination got the ForceRestore
  issue diagnosed quickly last time.

## Release-adjacent duties

Publishing is not just the button. Before any Publish, run the checklist in
`release-git-hygiene` (packaging hygiene, launchSettings guard, version sync) and the
matrix in `testing-validation`. Release notes should keep the project's habit of
documenting community-relevant findings (the ForceRestore write-up is the model).
