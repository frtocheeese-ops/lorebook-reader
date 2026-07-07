# Knihovna dovedností — Lorebook Reader

Předávková knihovna pro budoucí údržbu projektu. Cíl: aby juniorní/středně pokročilí
inženýři a menší modely (třídy Sonnet/Haiku) dokázali projekt debugovat, rozšiřovat,
validovat a publikovat na úrovni, kterou dosud držela jedna hlava. Skilly kódují
nejen *jak* věci fungují, ale hlavně *proč* — každá konstanta tu má svůj příběh
a každý „divný" kus kódu své zdůvodnění.

## Instalace

Zkopíruj celou složku `.claude/` do kořene repozitáře
(vedle `LorebookReader.csproj`) a commitni ji. Claude Code pak:

- **skilly** (`.claude/skills/<název>/SKILL.md`) načítá automaticky podle
  `description` ve frontmatteru, nebo ručně přes `/<název-skillu>`;
- **subagenty** (`.claude/agents/<název>.md`) volá přes Agent tool, `@zmínku`,
  nebo sám podle jejich `description`.

Skilly a agenti jsou psané anglicky (spolehlivější triggerování modelů a konzistence
s CLAUDE.md); tento index a revizní dokument jsou česky.

## Mapa skillů

| Skill | Kdy sáhnout |
|---|---|
| `blish-module-foundations` | kostra modulu, csproj/manifest, settings, keybindy, vlákna, ToS limity |
| `ssrd-build-publish` | build na SSRD, NETSDK1004, ForceRestore, registrace, publish flow |
| `gw2-panel-detection` | detekce pergamenu/konverzací, kalibrace konstant, false positives |
| `ocr-text-pipeline` | capture → preprocess → WinRT OCR → TextCleaner, opravy záměn znaků |
| `tts-subtitles-translation` | OneCore + Edge TTS, chunking, titulky, tři režimy překladu |
| `gdi-text-rendering` | diakritika, sdílený TextRenderer, pravidla pro nové textové UI |
| `encyclopedia-catalog` | catalog.json, schéma záznamů, dedup/append/trim, export/import |
| `release-git-hygiene` | git workflow, checklist vydání, launchSettings post-mortem |
| `testing-validation` | definition of done, golden corpus, manuální testovací matice, invarianty |
| `debugging-runbook` | **první zastávka u každého bugu** — symptom → vlastnický skill |
| `agent-team-workflows` | orchestrace rolí (scout → implement → review → validate), gaty |

Subagenti v `.claude/agents/`: `scout`, `implementer`, `code-reviewer`,
`detection-calibrator`, `release-validator`. Standardní tok práce a pravidla, kdy
kterého nasadit, popisuje skill `agent-team-workflows`.

## Tři pravidla, která drží kvalitu

1. **Runbook první.** Každý bug začíná v `debugging-runbook`, ne v náhodném souboru.
2. **Implementátor ≠ reviewer.** Netriviální diff vždy projde nezávislým
   `code-reviewer` agentem s rubrikou; jeho „request-changes" je gate.
3. **Důkazy, ne přídavná jména.** Konkrétní chybějící slova, rect, zelený corpus run,
   výstup `git status`. „Otestováno, funguje" není test report.

## Údržba knihovny

Když se skill rozejde s kódem, platí kód — a skill se opraví **v téže session**.
Každá session, která objeví nový failure mode, nechá repo chytřejší: přidá corpus
case, řádek do runbooku, nebo aktualizuje skill. Hloubková revize metod s
prioritizovanými návrhy zlepšení je v `docs/engineering-review-2026-07.md`
(P0 položky tam nejsou akademické — dvě se týkají reálné ztráty dat).
