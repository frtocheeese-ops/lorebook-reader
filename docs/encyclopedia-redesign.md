# Encyclopedia visual remake — research & design proposal

Date: 2026-07-16 · Status: IMPLEMENTED in 0.7.0 (phases A + B: collapsible
expansion rail with counts, NEW badge, single-page book reader with cover,
expansion stamp and page-turn animation). Phase C (covers, completion header)
remains open.

## Why

The encyclopedia works, but it looks like a generic list + detail panel. The
goal of the remake is to make it feel like an in-game *grimoire*: a collection
you enjoy opening, browsing and completing — the same pull that map completion
and achievements have. (First concrete step, expansion presets + icons, shipped
in 0.6.0.)

## Survey — how other games do in-game libraries

Patterns worth stealing, from the games most often cited as the best codex/
journal implementations (see sources at the bottom):

**Mass Effect (codex)** — the gold standard. Two things make it: *narrated
entries* (a voice reads the codex to you — exactly what we already do with TTS,
worth leaning into as our identity) and a split between "primary" (short,
essential) and "secondary" (deep lore) entries. Takeaway: reading aloud is a
first-class way to consume a codex; keep Play prominent in the redesign.

**The Witcher 3 (glossary/bestiary)** — every entry has an illustration and a
consistent two-column layout: art + title left, scrollable text right.
Categories as top tabs. Takeaway: even a small piece of art (expansion icon,
book cover, color tag as a leather spine) makes entries feel collectible.

**Skyrim / TES (books)** — books are objects: parchment spread, two facing
pages, page-turn. We already render on a parchment texture; the remake can go
from "one long scroll" to a *two-page spread* with page flipping (our
ParchmentTextPanel already measures lines precisely, so pagination is cheap).

**Dragon Age (codex)** — unlock/progress framing: categories show "12/30
found". Takeaway: a per-expansion counter ("Janthir Wilds: 7 books") gives
collection hunters a number to chase. We can't know the total in the world,
but "books collected per expansion" still scratches the itch.

**Metroid Prime (logbook) / AC (encyclopedia)** — scanning/collecting in the
world feeds the library; the log is the reward. Our capture buttons already are
that loop; the library should celebrate new entries (e.g. a "NEW" badge until
first opened).

**Genshin Impact (archive)** — clean tab structure (books/weapons/fauna),
completion %, and a "new entry" dot. Modern, legible; good model for tabs.

**GW2 itself (Story Journal)** — the in-game benchmark our users already know:
left column = seasons/expansions with their logos, right = chapters with
progress. Matching this structure makes the module feel native to the game.

## Extracted principles

1. Navigation: categories (expansions) as a persistent left rail or top tabs,
   with icons; search stays global.
2. Identity: every entry gets a visual anchor — expansion icon + color tag
   (existing data, no new art needed); later optional per-book cover.
3. Reading view: parchment two-page spread, page turn buttons (‹ ›), font
   size controls kept; Play/Stop prominent (narrated codex is our Mass Effect
   move).
4. Collection feel: per-expansion counts, "NEW" badge on unopened entries,
   optional completion header ("42 books collected").
5. Consistency with GW2: reuse Story Journal layout language (left rail with
   expansion logos = what users already navigate).

## Proposed layout (Blish HUD controls, no new dependencies)

```
+----------------------------------------------------------------------+
| [search........................]  [sort v]  [color v]        42 books |
+----------+-----------------------------------------------------------+
| ALL (42) |  [icon] Book title                    ← two-page parchment |
| Core (9) |  meta line · NEW                        spread w/ ‹ › page |
| HoT (4)  |  [icon] Book title                     turn, Play/Stop,   |
| PoF (6)  |  ...                                   Edit, Delete(2x),  |
| IBS (3)  |                                        A−/A+              |
| EoD (5)  |                                                           |
| SotO (8) |                                                           |
| JW (7)   |                                                           |
| VoE (0)  |                                                           |
+----------+-----------------------------------------------------------+
```

- Left rail: expansion filter buttons (icon + name + count) replacing the
  current expansion dropdown; "ALL" on top. Data: `DistinctExpansions()` +
  counts from the catalog (cheap).
- **Rail is collapsible** (user request, 2026-07-16): a «/» toggle at the top
  shrinks it to icons only (~38–40 px wide, names as tooltips) so the window
  stays usable on low resolutions. The collapsed/expanded state persists as a
  bool setting. Mockup: `encyclopedia_redesign_mockup_collapsible_rail`.
- Middle: entry list as now (color spine + icon + title + meta), plus NEW
  badge (needs one bool `Opened` on LorebookEntry, default true for existing).
- Right: reading view. Phase A keeps the current single parchment. Phase B
  (revised per user feedback 2026-07-16 — NO two-column spread): a **single
  page** with a **page-turn animation**, paginated from measured lines.
  - **Page 0 = cover page**: book title in a stylistic serif font (larger,
    centered, thin rules under it) + the date/location caption at the bottom.
  - **Expansion stamp** on the cover: rotated ~-8°, inked look (border +
    ~80 % opacity), expansion icon + name. No expansion set → no stamp, title
    only.
  - Turn animation in Blish: no 3D — approximate by horizontally squeezing the
    outgoing page texture (scaleX 1→0 anchored at the turning edge), then the
    incoming page 0→1 from the other edge (~450 ms total, Glide/Tween or
    manual lerp in Paint). Mockup: `encyclopedia_mockup_single_page_flip_cover`.

## Phasing

- **Phase A (cheap, high impact):** left rail with icons+counts, NEW badge,
  Play button restyle, keep current reading panel. No data migration except
  `Opened`.
- **Phase B:** two-page parchment spread with page turning; header art.
- **Phase C (optional):** per-book covers (user-pickable icon from a small
  set), completion header, "recently added" shelf on top.

## Assets needed

`ref/xp_core.png, xp_hot.png, xp_pof.png, xp_ibs.png, xp_eod.png,
xp_soto.png, xp_jw.png, xp_voe.png` — small (~32–64 px) PNGs with
transparency. 0.6.0 already loads them when present (list + preview).
Missing from the user-supplied set: **End of Dragons** logo.

## Research round 2 (2026-07-16) — visual polish backlog

Second pass after phases A + B shipped. Sources: parchment/book UI kits,
Skyrim book-UI mod ecosystem, game-juice writeups (links below). Ideas ranked
by cost; none block publishing 0.7.0.

### Code-only quick wins (no new art) — SHIPPED in 0.7.1
1. **Easing on the page turn.** Juice guides: UI eases OUT, never linear.
   Our squeeze is linear; use quadratic ease-in for the closing half and
   ease-out for the opening half (2-line change in the `_turnT` mapping).
2. **Drop cap (iniciála).** ~~Illuminated-manuscript initial letter.~~
   TRIED in 0.7.1 and REJECTED by the author (2026-07-16) — did not fit the
   look. Do not re-propose.
3. **Aged page edges.** Procedural vignette: 2–3 nested 1px strips + soft
   alpha band around the parchment rect (FaintInk * 0.15…0.05). Instantly
   less "flat texture".
4. **Hover states.** Rail rows and list rows highlight on MouseEntered
   (BackgroundColor lerp) — the whole UI feels alive for one event handler.
5. **Unread glow instead of NEW text.** Skyrim's most-endorsed book QoL mod
   is literally "Unread Books Glow" — proven desire. Subtle gold pulse on
   the row's spine strip (sin over Update) for unopened books.
6. **Ease the reader into view.** Fade-in (Opacity 0→1 over ~150 ms) when a
   book opens; cheap, hides the rebuild pop.

### Needs small art (user-supplied PNGs, ref/)
7. **Parchment with deckled edges** — replace flat `parchment.png` with a
   bordered, aged-edge version (book kits above are reference).
8. **Corner flourishes** on the cover (4 small ornament PNGs, or one
   rotated 4×) around the title block.
9. **Wax-seal variant of the stamp** — expansion logo pressed into a seal
   blob at the cover's bottom-right, instead of/next to the flat imprint.
10. **Custom window art** for the encyclopedia StandardWindow (book-themed
    frame instead of the generic Blish window texture).

### Bigger swings (later)
11. **Reading progress dots** under the page (● ● ○ ○) instead of "1 / 4".
12. **Open-book animation** when selecting a title (cover slides open).
13. **Page-turn sound** (Blish audio; needs a licensed/UI-safe SFX).
14. Phase C from round 1 still open: per-book covers, completion header.

## Sources

- Game UI Database, Codex & Journal category:
  https://www.gameuidatabase.com/index.php?tag=18&scrn=92
- ResetEra: "I love in-game encyclopedias/wikis" (Mass Effect narrated codex,
  AC encyclopedia, Metroid logbook, Tactics Ogre codex):
  https://www.resetera.com/threads/i-love-in-game-encyclopedias-wikis-what-are-some-good-examples.577597/
- GW2 Wiki, Story Journal: https://wiki.guildwars2.com/wiki/Story_Journal
- GW2 Wiki, Hero panel: https://wiki.guildwars2.com/wiki/Hero_panel
- ArenaNet, Introducing the Story Journal:
  https://www.guildwars2.com/en/news/introducing-the-story-journal/

Round 2 sources:

- Parchment game UI kit (aged edges, panel anatomy):
  https://gamedeveloperstudio.itch.io/parchment-game-user-interface-kit
- Denizen Design, inspiration notes for the parchment kit:
  https://buymeacoffee.com/denizensg/inspiration-parchment-game-ui-kit
- GameAnalytics, "Squeezing more juice out of your game design" (easing rules):
  https://www.gameanalytics.com/blog/squeezing-more-juice-out-of-your-game-design
- Skyrim mods proving demand: Unread Books Glow
  (https://www.nexusmods.com/skyrimspecialedition/mods/1296), Convenient
  Reading UI (https://www.nexusmods.com/skyrimspecialedition/mods/50202),
  Book UI Plus (https://www.nexusmods.com/skyrim/mods/91459)
- Game UI Database, Skyrim screens:
  https://www.gameuidatabase.com/gameData.php?id=287
- Medieval illuminated manuscripts (drop caps, borders):
  https://kdi.umn.edu/resources/medieval-illuminated-manuscripts
