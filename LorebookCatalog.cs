using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Frtal.LorebookReader {

    /// <summary>Jeden lorebook v encyklopedii se všemi metadaty.</summary>
    public class LorebookEntry {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        public string TimestampUtc { get; set; }

        // uživatelská metadata
        public string ColorTag { get; set; }      // název barvy, viz Palette
        public string IconKey { get; set; }       // klíč ikony (volitelné)
        public string Expansion { get; set; }      // datadisk
        public string Theme { get; set; }          // téma
        public string Location { get; set; }       // kde pořízeno
        public string Notes { get; set; }          // volná poznámka

        // uložený překlad (volitelný, generuje se na vyžádání)
        public string TranslatedText { get; set; }
        public string TranslatedLang { get; set; }

        public LorebookEntry() {
            Id = Guid.NewGuid().ToString("N");
        }

        public string DisplayTitle =>
            string.IsNullOrWhiteSpace(Title)
                ? LorebookCatalog.MakeFallbackTitle(Text)
                : Title;

        public DateTime TimestampLocal {
            get {
                return DateTime.TryParse(TimestampUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var dt) ? dt.ToLocalTime() : DateTime.MinValue;
            }
        }

        /// <summary>Souhrn metadat pro vyhledávání i zobrazení.</summary>
        public string MetadataLine {
            get {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Expansion)) parts.Add(Expansion);
                if (!string.IsNullOrWhiteSpace(Theme))     parts.Add(Theme);
                if (!string.IsNullOrWhiteSpace(Location))  parts.Add(Location);
                return string.Join(" · ", parts);
            }
        }
    }

    /// <summary>Barevné štítky pro vizuální rozdělení knih.</summary>
    public static class Palette {
        public static readonly (string Name, int R, int G, int B)[] Colors = {
            ("None",   180, 180, 180),
            ("Red",    200,  70,  70),
            ("Orange", 210, 130,  50),
            ("Yellow", 210, 195,  80),
            ("Green",   90, 180,  90),
            ("Blue",    80, 140, 210),
            ("Purple", 160, 100, 200),
            ("Teal",    70, 180, 180)
        };

        public static (int R, int G, int B) Resolve(string name) {
            foreach (var c in Colors)
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                    return (c.R, c.G, c.B);
            return (180, 180, 180);
        }
    }

    public enum SortMode { NewestFirst, OldestFirst, TitleAZ, TitleZA, ColorTag }

    /// <summary>
    /// Katalog lorebooků: in-memory seznam s perzistencí do JSON,
    /// vyhledáváním, řazením, filtrováním a export/import pro sdílení.
    /// </summary>
    public sealed class LorebookCatalog {

        private readonly List<LorebookEntry> _entries = new List<LorebookEntry>();
        private readonly string _filePath;
        private int _capacity;
        private readonly object _lock = new object();

        public event Action Changed;

        public LorebookCatalog(string directory, int capacity) {
            _capacity = Math.Max(1, capacity);
            _filePath = Path.Combine(directory, "catalog.json");
            Load();
        }

        public void SetCapacity(int capacity) {
            lock (_lock) {
                _capacity = Math.Max(1, capacity);
                TrimToCapacity();
                Save();
            }
            Changed?.Invoke();
        }

        public IReadOnlyList<LorebookEntry> All {
            get { lock (_lock) return _entries.ToList(); }
        }

        /// <summary>Přidá nově přečtený lorebook (auto-capture).
        /// Deduplikuje stejný text, aby opětovné čtení nezahltilo katalog.</summary>
        public LorebookEntry AddCaptured(string title, string text) {
            if (string.IsNullOrWhiteSpace(text)) return null;
            LorebookEntry entry;
            lock (_lock) {
                var existing = _entries.FirstOrDefault(e => e.Text == text);
                if (existing != null) {
                    existing.TimestampUtc = DateTime.UtcNow.ToString("o");
                    Save();
                    entry = existing;
                } else {
                    entry = new LorebookEntry {
                        Title = string.IsNullOrWhiteSpace(title)
                            ? MakeFallbackTitle(text) : title,
                        Text = text,
                        TimestampUtc = DateTime.UtcNow.ToString("o"),
                        ColorTag = "None"
                    };
                    _entries.Insert(0, entry);
                    TrimToCapacity();
                    Save();
                }
            }
            Changed?.Invoke();
            return entry;
        }

        public void Update(LorebookEntry entry) {
            lock (_lock) {
                int idx = _entries.FindIndex(e => e.Id == entry.Id);
                if (idx >= 0) _entries[idx] = entry;
                Save();
            }
            Changed?.Invoke();
        }

        /// <summary>Připojí text k nejnovějšímu záznamu (další stránka téže
        /// knihy). Vrací dotčený záznam, nebo null když je katalog prázdný.</summary>
        public LorebookEntry AppendToLatest(string text) {
            if (string.IsNullOrWhiteSpace(text)) return null;
            LorebookEntry latest;
            lock (_lock) {
                latest = _entries
                    .OrderByDescending(e => e.TimestampUtc)
                    .FirstOrDefault();
                if (latest == null) return null;
                // nepřipojovat tentýž text dvakrát po sobě (dvojklik apod.)
                if (!string.IsNullOrEmpty(latest.Text)
                    && latest.Text.TrimEnd().EndsWith(text.TrimEnd(),
                        StringComparison.Ordinal))
                    return latest;
                latest.Text = string.IsNullOrEmpty(latest.Text)
                    ? text
                    : latest.Text.TrimEnd() + "\n\n" + text.TrimStart();
                latest.TimestampUtc = DateTime.UtcNow.ToString("o");
                // překlad se připojením znehodnotí -> zrušit, ať nesedí půl na půl
                latest.TranslatedText = null;
                latest.TranslatedLang = null;
                Save();
            }
            Changed?.Invoke();
            return latest;
        }

        public void Remove(string id) {
            lock (_lock) {
                _entries.RemoveAll(e => e.Id == id);
                Save();
            }
            Changed?.Invoke();
        }

        public void Clear() {
            lock (_lock) { _entries.Clear(); Save(); }
            Changed?.Invoke();
        }

        /// <summary>Vrátí filtrovaný a seřazený pohled na katalog.</summary>
        public List<LorebookEntry> Query(string search, SortMode sort,
                                         string colorFilter = null,
                                         string expansionFilter = null) {
            lock (_lock) {
                IEnumerable<LorebookEntry> q = _entries;

                if (!string.IsNullOrWhiteSpace(search)) {
                    string s = search.Trim();
                    q = q.Where(e =>
                        Contains(e.DisplayTitle, s) ||
                        Contains(e.Text, s) ||
                        Contains(e.Expansion, s) ||
                        Contains(e.Theme, s) ||
                        Contains(e.Location, s) ||
                        Contains(e.Notes, s));
                }
                if (!string.IsNullOrWhiteSpace(colorFilter) && colorFilter != "All")
                    q = q.Where(e => string.Equals(e.ColorTag, colorFilter,
                        StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(expansionFilter)
                    && expansionFilter != "All")
                    q = q.Where(e => string.Equals(e.Expansion, expansionFilter,
                        StringComparison.OrdinalIgnoreCase));

                switch (sort) {
                    case SortMode.OldestFirst:
                        q = q.OrderBy(e => e.TimestampUtc); break;
                    case SortMode.TitleAZ:
                        q = q.OrderBy(e => e.DisplayTitle,
                            StringComparer.OrdinalIgnoreCase); break;
                    case SortMode.TitleZA:
                        q = q.OrderByDescending(e => e.DisplayTitle,
                            StringComparer.OrdinalIgnoreCase); break;
                    case SortMode.ColorTag:
                        q = q.OrderBy(e => e.ColorTag ?? "")
                             .ThenByDescending(e => e.TimestampUtc); break;
                    default:
                        q = q.OrderByDescending(e => e.TimestampUtc); break;
                }
                return q.ToList();
            }
        }

        /// <summary>Distinktní hodnoty datadisků pro filtr dropdown.</summary>
        public List<string> DistinctExpansions() {
            lock (_lock) {
                return _entries
                    .Select(e => e.Expansion)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x).ToList();
            }
        }

        // --------------------------- export / import ---------------------------

        public void ExportToFile(string path) {
            lock (_lock) {
                string json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 }
                    .Serialize(_entries);
                File.WriteAllText(path, json);
            }
        }

        /// <summary>Importuje a sloučí (podle Id; nové se přidají, shodné přepíší).
        /// Vrací počet nově přidaných/aktualizovaných.</summary>
        public int ImportFromFile(string path, bool merge = true) {
            string json = File.ReadAllText(path);
            var incoming = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 }
                .Deserialize<List<LorebookEntry>>(json);
            if (incoming == null) return 0;

            int count = 0;
            lock (_lock) {
                if (!merge) _entries.Clear();
                foreach (var e in incoming) {
                    if (string.IsNullOrWhiteSpace(e.Id))
                        e.Id = Guid.NewGuid().ToString("N");
                    int idx = _entries.FindIndex(x => x.Id == e.Id);
                    if (idx >= 0) _entries[idx] = e;
                    else _entries.Add(e);
                    count++;
                }
                _entries.Sort((a, b) => string.CompareOrdinal(
                    b.TimestampUtc ?? "", a.TimestampUtc ?? ""));
                Save();
            }
            Changed?.Invoke();
            return count;
        }

        // --------------------------- helpers ---------------------------

        public static string MakeFallbackTitle(string text) {
            if (string.IsNullOrEmpty(text)) return "(untitled)";
            var words = text.Split(new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            string title = string.Join(" ", words.Take(6));
            return words.Length > 6 ? title + "…" : title;
        }

        private static bool Contains(string haystack, string needle) =>
            !string.IsNullOrEmpty(haystack) &&
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private void TrimToCapacity() {
            while (_entries.Count > _capacity)
                _entries.RemoveAt(_entries.Count - 1);
        }

        private void Load() {
            try {
                if (!File.Exists(_filePath)) return;
                var loaded = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 }
                    .Deserialize<List<LorebookEntry>>(File.ReadAllText(_filePath));
                if (loaded != null) {
                    _entries.Clear();
                    _entries.AddRange(loaded);
                    foreach (var e in _entries)
                        if (string.IsNullOrWhiteSpace(e.Id))
                            e.Id = Guid.NewGuid().ToString("N");
                }
            } catch { /* poškozený soubor -> prázdný katalog */ }
        }

        private void Save() {
            try {
                string json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 }
                    .Serialize(_entries);
                File.WriteAllText(_filePath, json);
            } catch { /* nekritické */ }
        }
    }
}
