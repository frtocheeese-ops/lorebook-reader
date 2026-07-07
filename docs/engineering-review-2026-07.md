# Hloubková revize metod — Lorebook Reader
*Předávková revize, červenec 2026*

## Metodika

Revize stojí na třech zdrojích: (1) **skutečný zdrojový kód v0.2.2** — repo
`frtocheeese-ops/lorebook-reader` naklonované 2. 7. 2026 (commit `dfb6f2e`),
přečteno všech 15 souborů, 3 579 řádků; (2) **historie vývojových konverzací**
(Python prototyp → C# port → pět verzí ConversationDetectoru → kalibrace v0.3.0);
(3) **CLAUDE.md a projektové instrukce**. Kde tvrzení pochází jen z historie
(lokální, nepushnutá v0.3.0), je to explicitně označeno.

Formát nálezů: co je dnes → proč je to problém/přednost → návrh → kam je to
zakódováno v knihovně skillů.

---

## A. Co je navrženo dobře (a proč to nerozbíjet)

Tohle není zdvořilost — je to seznam rozhodnutí, která reviewer musí bránit,
protože vypadají „divně" a lákají k refaktoru:

1. **Detekce přes strukturu a přechody, ne absolutní prahy.** Pět mrtvých verzí
   ConversationDetectoru (dark-pixel, bright-on-dark, warm bez struktury) dokazuje,
   že v GW2 absolutní prahy nepřežijí noc, sníh ani teplé osvětlení. Finální
   strategie — tenký izolovaný teplý pruh + studené řádky nad/pod + jasný text
   pod ním; u pergamenu jas+chroma+tvarové filtry blobů — je správná třída řešení.
2. **Pipelinovaná syntéza TTS** (generuj dávku N+1, zatímco hraje N) — elegantní
   skrytí latence, shodné pro oba enginy; `onChunk` při startu přehrávání drží
   titulky synchronní s řečí, `finally { onChunk(null) }` je čistí.
3. **Graceful degradation jako systémová vlastnost**: Edge → offline hlas + toast;
   překlad → originál; Paint titulků → skrýt overlay; chybějící hlas → jazykový
   prefix → default + návod. Chyby řeči nikdy neshodí čtení, které může pokračovat.
4. **Vláknový vzor volatile-flag → aplikace v Update()** — důsledně dodržený
   (`_bookVisible`, `_subtitleDirty`, `_catalogDirty`). Je to jediná věc, která
   v Blish overlay drží stabilitu.
5. **GDI TextRenderer s FIFO cache a premultiplied alpha** — správné řešení
   diakritiky, správně sdílená jedna instance, správná disposal disciplína.
6. **WebSocketLite** — minimální RFC 6455 klient existuje z dobrého důvodu
   (ClientWebSocket na net48 zakazuje potřebné hlavičky) a jeho minimalismus je
   feature; kompletní Sec-MS-GEC podpis je netriviální reverzní inženýrství.
7. **ForceRestore target a dokumentační kultura release notes** — nález se
   nepublikoval jen do kódu, ale i komunitě. Tohle je přesně vzor, který knihovna
   skillů institucionalizuje.
8. **Kalibrační metoda „pojmenuj chybějící slovo"** — 'scared'/'It's'/'probably'
   jako důkazní materiál mapující symptom na geometrický knoflík. Povýšeno na
   povinný proces v calibration-playbooku.

---

## B. Nálezy a návrhy — prioritizovaně

### P0 — bezpečnost dat a repozitáře (řešit před další feature prací)

**P0.1 — launchSettings.json je pořád trackovaný na origin/main.**
Ověřeno klonem: soubor s placeholder cestami `C:\DOPLN\CESTU\...` je v repu
a `.gitignore` ho neobsahuje. Lokální fix z paměti se na GitHub nedostal — a
`.gitignore` sám o sobě už trackovaný soubor neodtrackuje. Dokud to tak je,
incident „zip přepsal funkční konfiguraci" se může opakovat každým release.
→ `git rm --cached Properties/launchSettings.json` + záznam do `.gitignore` +
push. Přesný postup: skill `release-git-hygiene` (sekce post-mortem).

**P0.2 — v0.3.0 existuje jen na jednom disku.**
ConversationDetector.cs, FixConfusableChars, prosodické chunky — nic z toho není
na GitHubu (repo je na v0.2.2). Jeden vadný disk = týdny práce pryč. „Lokálně,
dokud to není hotové" a „nepublikované" jsou dvě různé věci: nepushnutá feature
branch na GitHubu nic nezveřejňuje (SSRD staví jen to, co se Publishne z main).
→ Okamžitě `git checkout -b feature/conversation-capture && git push -u origin …`.
Zakódováno jako standing rule v `release-git-hygiene`.

**P0.3 — katalog umí potichu zabít celou encyklopedii.**
`Save()` = neatomický `File.WriteAllText`; pád uprostřed zápisu soubor usekne.
`Load()` každou chybu parsování spolkne → modul nastartuje s prázdným katalogem
a **nejbližší Save přepíše poškozený-ale-obnovitelný soubor prázdným seznamem**.
Uživatel přijde o sbírku bez jediné hlášky. Bonus: capacity trim (default 10)
maže od konce seznamu i **ručně otagované knihy s poznámkami**.
→ Zápis přes `.tmp` + `File.Replace` (drží `.bak`); při chybě loadu přejmenovat
na `catalog.corrupt-<čas>.json`, zkusit `.bak`, informovat uživatele; capacity
trimovat jen auto-záznamy bez uživatelských metadat. Detailně ve skillu
`encyclopedia-catalog` (sekce hazardů) + řádek v runbooku „encyklopedie prázdná".

### P1 — kvalita a obrana proti regresím (největší pákový efekt)

**P1.1 — vrátit debug-dump, který se ztratil při portu z Pythonu.**
Prototyp měl Ctrl+Alt+D: uložil celý snímek + detekovaný výřez. C# verze to
nemá — a proto každá kalibrace běží jako ruční ping-pong se screenshoty.
→ Setting/keybind „Save debug capture": frame.png + rect + raw OCR + cleaned
text do datové složky modulu. Je to enabler pro P1.2 i pro použitelné community
bug reporty. Nejvyšší poměr užitek/pracnost v celém seznamu.

**P1.2 — golden corpus + offline regresní harness.**
Detektory i TextCleaner jsou čisté funkce bez závislosti na Blish (skvělé
rozhodnutí — zachovat!). Chybí jen: složka corpus/ (frame.png + expected.json;
raw→expected textové páry) a malá net48 konzole, která je projede. Pravidlo:
**žádná změna konstanty detektoru ani pravidla cleaneru bez zeleného corpus
runu.** Seed případy (J→I, 8ecause, Level 80, artefakt „11", scared/It's/
probably, „Read on." nesmí) jsou vyjmenované v `ocr-text-pipeline/references/
golden-corpus.md` a `gw2-panel-detection/references/calibration-playbook.md`.

**P1.3 — reálný bug: ne-anglické klienty ořezává legitimní text.**
`TextCleaner.IsValidWord` je ASCII-only (`[^A-Za-z']`, samohlásky `aeiou`).
Německá/francouzská/španělská slova z akcentovaných písmen („früh", „âgé")
neprojdou → celé správné řádky se zahodí jako „dekorace". Modul přitom de-DE,
fr-FR, es-ES oficiálně podporuje v nastavení OCR.
→ Unicode třídy (`\p{L}`) + jazyková sada samohlásek; corpus case pro každý
jazyk. Zdokumentováno jako Known real bug v `ocr-text-pipeline`.

**P1.4 — dedup katalogu je exact-match.**
Jediný OCR-jitter znak vyrobí duplicitní záznam. → normalizovaná podobnost
(lowercase, collapse whitespace, např. poměr shodných trigramů > 0.97).

### P2 — robustnost běhu

**P2.1 — Edge TTS bez retry a s křehkými konstantami.** Jeden síťový výpadek
na dávce N zabije celé čtení (fallback je až na úrovni celého SpeakAsync).
→ 1× retry per chunk; při opakovaném selhání mid-read přepnout zbytek na
OneCore; protokolová údržba (rotace Chromium pinu, diagnóza špatných hodin —
Sec-MS-GEC má ±5min okno) je sepsaná v `tts-subtitles-translation/references/
edge-protocol-maintenance.md` včetně postupu proti upstream `edge-tts`.

**P2.2 — překlad: URL limit a per-chunk race.** Full-mode posílá celou knihu
v jednom GET query stringu → dlouhé lorebooky můžou přetéct limit URL a spadnout;
subtitles-mode hlídá session (`_speakSession`), ale ne pořadí dávek — pomalý
překlad dávky N přepíše titulek dávky N+1. → překládat po dávkách/odstavcích;
guard rozšířit o index dávky; cache do už existujících polí
`TranslatedText/TranslatedLang` (replay knihy dnes platí síť znovu).

**P2.3 — splitter vět bez zkratek.** `(?<=[.!?…])\s+` seká „Mr. Smith".
Pro TTS pauzu neškodné, pro budoucí cache překladů po dávkách už ano.
→ malý seznam zkratek + ochrana elips.

**P2.4 — drobné souběhy a alokace.** `_readBusy`/`_detectBusy` jsou obyčejné
booly psané z více vláken (→ `Interlocked.CompareExchange`); `TryReadHeader`
používá `.GetAwaiter().GetResult()` uvnitř async metody (→ prostě `await`);
detekční smyčka 1 Hz alokuje ~11 MB bitmapu na tik (na 1440p) — dnes únosné,
při víc detektorech reuse buffer / downsample před skenem.

### P3 — architektura a proces

**P3.1 — LorebookReaderModule (704 ř.) jako orchestrátor všeho.** Settings,
keybindy, detekční smyčka, tři pipeline, titulky, encyklopedie. Funguje, ale
je to místo s nejvyšší kognitivní zátěží pro nováčka. → postupně vytáhnout
`CapturePipeline` (Capture→Detect→OCR→Clean jako testovatelný celek) a
`IPanelDetector` rozhraní pro oba detektory; bez big-bang refaktoru.

**P3.2 — CI jako zrcadlo SSRD.** GitHub Actions na `windows-latest`:
`rm -rf obj bin` ekvivalent + `dotnet build` + artifact-audit grep + (po P1.2)
corpus run. Levná pojistka, že „builds on SSRD" se testuje každým pushem.

**P3.3 — multi-anchor detekce a temporal smoothing.** Inventář stabilních kotev
(header bar, NPC jmenovka, X button, zelené šipky odpovědí) už existuje z
kalibrace — dnes se používá jen header. Skórování 2-ze-4 + požadavek N po sobě
jdoucích zásahů před zobrazením tlačítek zabije i „flicker na texturách",
za který se dnes omlouvá README v Known Issues.

**P3.4 — rozlišení mimo 1440p.** Návrh je správně ve frakcích, ale terénně
ověřená je jen 1440p. Do matice přidat 1080p (nejčastější u uživatelů) a
případně UI-scale z MumbleLink identity.

---

## C. Jak revize vstoupila do knihovny skillů

Každý nález má „domov": P0.1+P0.2 → `release-git-hygiene`; P0.3 →
`encyclopedia-catalog` + runbook; P1.1+P1.2 → `gw2-panel-detection/references/
calibration-playbook.md` + `testing-validation`; P1.3 → `ocr-text-pipeline`;
P2.1 → `tts-subtitles-translation` + reference; zbytek v „Known open
improvements" sekcích příslušných skillů. Sekce A je zakódovaná jako
`testing-validation/references/invariants.md` — seznam, který `code-reviewer`
agent hlídá proti „vylepšování".

Doporučené pořadí prací pro první session s knihovnou:
**P0.2 → P0.1 → P1.1 → P0.3 → P1.2** — od té chvíle je projekt zálohovaný,
hygienický a každá další změna detekce/cleaneru má regresní síť.
