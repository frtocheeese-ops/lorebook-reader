---
name: encyclopedia-catalog
description: The Lorebook Reader encyclopedia — LorebookCatalog persistence (catalog.json), LorebookEntry schema, dedup/append/trim semantics, export/import, and the EncyclopediaView UI. Use this whenever adding or changing a field on saved lorebooks, whenever catalog data is lost/duplicated/corrupted, when touching Save/Load/Import/Export, when changing history capacity behavior, or when modifying the encyclopedia window. This skill also lists the known data-safety hazards — read it BEFORE any persistence change.
---

# Encyclopedia & Catalog

## Storage model

- One JSON file: `catalog.json` inside the module's data directory
  (`DirectoriesManager.GetFullDirectoryPath("lorebook_reader")` — granted by the
  manifest `directories` entry). Serializer: `JavaScriptSerializer`
  (System.Web.Extensions), `MaxJsonLength` raised to 64 MB.
- In-memory `List<LorebookEntry>` behind a single `_lock`; every mutation saves the
  whole file and raises `Changed` (which the module turns into `_catalogDirty` →
  encyclopedia refresh in `Update` — the standard threading bridge).
- `LorebookEntry` fields: Id (guid "N"), Title, Text, TimestampUtc (round-trip "o"
  format), user metadata (ColorTag from the fixed `Palette`, IconKey, Expansion,
  Theme, Location, Notes), and cached translation (TranslatedText, TranslatedLang).
  `DisplayTitle` falls back to the first 6 words of Text.

## Behavioral contracts (rely on these; don't break them silently)

- **AddCaptured dedup**: an *exactly* identical Text refreshes the existing entry's
  timestamp instead of inserting. (Weakness: one OCR-jitter character defeats it —
  improvement: normalized similarity. Until then, near-duplicate reports are expected
  behavior, not corruption.)
- **AppendToLatest**: appends a page to the newest entry with a `\n\n` seam, refuses
  to append the same trailing text twice (double-click guard), and **invalidates the
  cached translation** (half-translated books are worse than none). Keep that
  invalidation for any future mutation of Text.
- **Capacity trim**: the HistoryCapacity setting trims the list from the tail after
  inserts. ⚠ Hazard: the tail can be a **user-curated entry with notes and tags** —
  capacity currently doesn't distinguish curated from auto-captured. Fix direction
  (improvement list): exempt entries with any user metadata from trimming, or trim
  only untagged ones. Do not raise the default silently instead of fixing it.
- **Import** merges by Id (replace on match, add otherwise), regenerates missing Ids,
  re-sorts newest-first. Import reads the newest `lorebook_export*.json` in the data
  folder (there is no file-picker in Blish).

## ⚠ Data-safety hazards (top of the engineering-review priority list)

1. **Non-atomic Save**: `File.WriteAllText` directly onto `catalog.json`. A crash or
   power cut mid-write truncates the file.
2. **Silent-corruption Load**: any parse error is swallowed → the module starts with
   an *empty* catalog and the next Save **overwrites the corrupt-but-recoverable
   file with the empty list**. The user's entire encyclopedia dies without a message.
3. Combined fix (the required shape when anyone touches persistence):
   write to `catalog.json.tmp` → `File.Replace` (keeps `catalog.json.bak`); on load
   failure, rename the bad file to `catalog.corrupt-<timestamp>.json`, try the `.bak`,
   and toast the user. Add a corpus/unit case that feeds truncated JSON.
4. **Schema evolution**: there is no `SchemaVersion` field. JavaScriptSerializer is
   tolerant (unknown fields ignored, missing fields default), so *additive* changes
   are safe — but add `SchemaVersion` with the first future migration-worthy change,
   and never rename or repurpose an existing property (old files must keep loading).

## EncyclopediaView (UI) essentials

Master-detail: search box (matches title, text, and all metadata fields, case-
insensitive) + sort dropdown (Newest/Oldest/Title A–Z/Z–A/ColorTag) + color and
expansion filters → list rows → detail panel with three modes (Empty/Preview/Edit).
Preview renders on the parchment texture via the shared TextRenderer; Edit uses the
soft-break wrap trick documented in gdi-text-rendering. The view is rebuilt fresh on
window open and refreshed via the `_catalogDirty` flag — heavy per-entry work belongs
in the row-build path, never per frame.

## Checklist for "add a field to saved books"

1. Add the property to `LorebookEntry` (additive only; sensible default).
2. Decide: searchable? → extend `Query`'s match set. Displayed? → `MetadataLine`
   and/or the editor. Trim-protecting? → the capacity-hazard logic above.
3. Export/Import need no change (whole-object serialization) — but add a load test
   with an OLD catalog.json to prove backward compatibility.
4. Version bump + note in release notes (users' files are involved).
