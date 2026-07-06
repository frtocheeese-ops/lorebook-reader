# Manifest rules (SSRD review requirements)

- `contributors` must be an ARRAY of {name, username}. The old `author` object is
  rejected by SSRD (requirement since ~2025).
- `package` must end in `.dll` and match `<AssemblyName>`.
- `namespace` is PERMANENT after SSRD registration. Never change it.
- `directories` grants the module its persistent data folder via
  `DirectoriesManager.GetFullDirectoryPath("module_name")` — the catalog lives there.
- Every manifest change (even description-only) requires a version bump; SSRD never
  accepts the same version twice.
- `dependencies.bh.blishhud` sets the minimum Blish HUD version users need.
