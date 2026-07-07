using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Čištění OCR výstupu pro plynulé TTS čtení.
    /// 1:1 port pravidel vyladěných v Python prototypu.
    /// </summary>
    public static class TextCleaner {

        private static readonly Regex Vowels = new Regex("[aeiouAEIOU]");
        private static readonly Regex NonLetters = new Regex("[^A-Za-z']");

        private static bool IsValidWord(string w) =>
            NonLetters.Replace(w, "").Length >= 2 && Vowels.IsMatch(w);

        private static bool IsGoodLine(string line) {
            var words = line.Split(new[] { ' ' },
                                   StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return false;
            return (double)words.Count(IsValidWord) / words.Length >= 0.5;
        }

        public static string CleanForTts(string raw) {
            var lines = raw.Split('\n')
                           .Select(l => l.Trim())
                           .Where(l => l.Length > 0)
                           .ToList();

            // dekorační šum nad a pod textem (řádky bez slov)
            while (lines.Count > 0 && !IsGoodLine(lines[0]))
                lines.RemoveAt(0);
            while (lines.Count > 0 && !IsGoodLine(lines[lines.Count - 1]))
                lines.RemoveAt(lines.Count - 1);

            string text = Regex.Replace(string.Join(" ", lines), @"\s+", " ");

            // samostatné '|' před malým písmenem bývá 'I'; jinde dekorace
            text = Regex.Replace(text, @"(?<![\w])\|(?=\s+[a-z])", "I");
            text = Regex.Replace(text, @"(?<![\w])\|(?![\w])", " ");

            // OCR čte uvozovky (") jako "11" — smazat jen přilepené výskyty;
            // skutečná čísla ("11 days") zůstávají
            text = Regex.Replace(text, @"(?<=[A-Za-z,.!?;:])11(?=\s|$)", " ");
            text = Regex.Replace(text, @"(?<!\w)11(?=[A-Za-z])", " ");
            text = Regex.Replace(text, @" {2,}", " ");

            // opravit zaměnitelné znaky (0↔O, 1↔I/l, |↔I atd.)
            // osamocené "1" před malým písmenem → "I" (jako "I wake")
            text = Regex.Replace(text, @"(?<!\w)1(?=\s+[a-z])", "I");
            // "J" na začátku věty/slova → "I" (GW2 font: I vypadá jako J)
            text = Regex.Replace(text, @"(?<![A-Za-z])J(?=\s+[a-z])", "I");
            text = FixConfusableChars(text);

            // oříznout šum za poslední dokončenou větou
            Match m = Regex.Match(text, @".*[.!?][""')\]]*", RegexOptions.Singleline);
            if (m.Success) text = m.Value;

            return text.Trim();
        }

        /// <summary>
        /// Oprava záměn digit ↔ písmeno, které Windows OCR dělá v próze.
        /// V lorebookech je text z 99% próza (ne čísla), takže
        /// prioritizujeme písmena. Čísla ponecháváme jen když:
        ///   - tvoří sekvenci (42, 100, 1327)
        ///   - následují za dvojtečkou (3:00)
        ///   - jsou součástí řadové číslovky (2nd, 3rd)
        ///
        /// Zaměnitelné páry:
        ///   0 ↔ O/o    1 ↔ I/l    | ↔ I/l    5 ↔ S    8 ↔ B
        /// </summary>
        private static string FixConfusableChars(string text) {
            if (string.IsNullOrEmpty(text)) return text;

            // Zpracovat po slovech, zachovat mezery a interpunkci
            var result = new System.Text.StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length) {
                // Neslovní znak — zkopírovat jak je
                if (!char.IsLetterOrDigit(text[i]) && text[i] != '|') {
                    result.Append(text[i]);
                    i++;
                    continue;
                }

                // Extrahovat "slovo" (písmena + číslice + |)
                int start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i])
                                           || text[i] == '|' || text[i] == '\''))
                    i++;
                string word = text.Substring(start, i - start);

                // Je to čistě číselné? (42, 1327, 3rd, 10th) → nechat
                if (IsNumericToken(word)) {
                    result.Append(word);
                    continue;
                }

                // Čas za dvojtečkou? (: 00, :30) → nechat
                if (start >= 1 && text[start - 1] == ':'
                    && word.Length <= 2 && IsAllDigits(word)) {
                    result.Append(word);
                    continue;
                }

                // Slovo má mix písmen a číslic/| → opravit číslice na písmena
                result.Append(FixWord(word));
            }
            return result.ToString();
        }

        /// <summary>Opraví zaměnitelné znaky v jednom slově.</summary>
        private static string FixWord(string word) {
            var sb = new System.Text.StringBuilder(word.Length);
            for (int i = 0; i < word.Length; i++) {
                char c = word[i];
                char prev = i > 0 ? word[i - 1] : ' ';
                char next = i < word.Length - 1 ? word[i + 1] : ' ';

                switch (c) {
                    case '0':
                        // 0 → O na začátku slova (vždy velké: Once, Old, Over...)
                        // 0 → O/o uprostřed slova podle okolí
                        if (HasLetterContext(prev, next)) {
                            if (i == 0)
                                sb.Append('O');
                            else
                                sb.Append(char.IsUpper(NearestLetter(word, i))
                                    ? 'O' : 'o');
                        } else {
                            sb.Append(c);
                        }
                        break;

                    case '1':
                        // 1 → I na začátku slova ("1t" → "It")
                        // 1 → l uprostřed slova (nejčastější OCR záměna:
                        //     1 vypadá jako l, ne jako i — i má tečku)
                        if (HasLetterContext(prev, next)) {
                            if (i == 0 || prev == ' ' || prev == '.'
                                || prev == '!' || prev == '?')
                                sb.Append('I');
                            else
                                sb.Append('l');
                        } else {
                            sb.Append(c);
                        }
                        break;

                    case '|':
                        // | → I na začátku slova, l uprostřed
                        if (i == 0 && char.IsLower(next))
                            sb.Append('I');
                        else if (char.IsLetter(prev) || char.IsLetter(next))
                            sb.Append('l');
                        else
                            sb.Append('I');
                        break;

                    case '5':
                        // 5 → S jen pokud jasně obklopené písmeny
                        if (HasLetterContext(prev, next)
                            && char.IsLetter(prev) && char.IsLetter(next))
                            sb.Append(char.IsUpper(prev) ? 'S' : 's');
                        else
                            sb.Append(c);
                        break;

                    case '8':
                        // 8 → B jen pokud jasně obklopené písmeny
                        if (HasLetterContext(prev, next)
                            && char.IsLetter(prev) && char.IsLetter(next))
                            sb.Append(char.IsUpper(prev) ? 'B' : 'b');
                        else
                            sb.Append(c);
                        break;

                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Je token čistě číselný (42, 1327, 3rd, 10th, 1,000)?</summary>
        private static bool IsNumericToken(string word) {
            if (string.IsNullOrEmpty(word)) return false;
            // Řadové číslovky: 1st, 2nd, 3rd, 4th, 10th, 21st...
            if (Regex.IsMatch(word, @"^\d+(st|nd|rd|th)$",
                              RegexOptions.IgnoreCase))
                return true;
            // Čistá čísla (včetně čárek/teček): 42, 1,000, 3.14
            if (Regex.IsMatch(word, @"^[\d,.']+$"))
                return true;
            return false;
        }

        private static bool IsAllDigits(string s) {
            foreach (char c in s)
                if (!char.IsDigit(c)) return false;
            return s.Length > 0;
        }

        /// <summary>Má znak na obou stranách písmeno (= uprostřed slova)?</summary>
        private static bool HasLetterContext(char prev, char next) {
            return char.IsLetter(prev) || char.IsLetter(next);
        }

        /// <summary>Najde nejbližší písmeno ve slově (pro odhad case).</summary>
        private static char NearestLetter(string word, int pos) {
            // Hledat doprava, pak doleva
            for (int d = 1; d < word.Length; d++) {
                if (pos + d < word.Length && char.IsLetter(word[pos + d]))
                    return word[pos + d];
                if (pos - d >= 0 && char.IsLetter(word[pos - d]))
                    return word[pos - d];
            }
            return 'a'; // fallback lowercase
        }

        private static bool IsVowel(char c) {
            c = char.ToLower(c);
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';
        }
        /// <summary>
        /// Rozdělí text na chunky pro TTS engine.
        ///
        /// TTS potřebuje CELÉ VĚTY pro správnou prosodii — nikdy
        /// nepřerušovat uprostřed fráze. Dlouhé věty (> maxLen)
        /// se dělí na klauze (středník, pomlčka, čárka+spojka).
        ///
        /// Titulky zalamuje SubtitleOverlay pixel-přesně; šířku boxu
        /// omezuje modul na ~42 znaků (Netflix standard).
        /// </summary>
        public static System.Collections.Generic.List<string> SplitChunks(
                string text, int maxLen = 200) {
            if (string.IsNullOrWhiteSpace(text))
                return new System.Collections.Generic.List<string>();

            // 1) Rozdělit na věty
            string[] sentences = Regex.Split(text, @"(?<=[.!?\u2026])\s+");
            var phrases = new System.Collections.Generic.List<string>();

            foreach (string sent in sentences) {
                string s = sent.Trim();
                if (s.Length == 0) continue;

                if (s.Length <= maxLen) {
                    phrases.Add(s);
                } else {
                    // 2) Rozdělit dlouhou větu na klauze
                    SplitLongSentence(s, maxLen, phrases);
                }
            }

            // 3) Sloučit velmi krátké fráze se sousední
            var result = new System.Collections.Generic.List<string>();
            string buf = "";
            foreach (string p in phrases) {
                if (buf.Length == 0) {
                    buf = p;
                } else if (buf.Length + 1 + p.Length <= maxLen) {
                    // obě se vejdou → sloučit
                    buf = buf + " " + p;
                } else {
                    // buf je hotový chunk
                    result.Add(buf);
                    buf = p;
                }
            }
            if (buf.Length > 0) result.Add(buf);

            return result;
        }

        /// <summary>Rozdělí dlouhou větu na klauze podle prosodických hranic.</summary>
        private static void SplitLongSentence(string sent, int maxLen,
                System.Collections.Generic.List<string> output) {
            // Prioritizované dělící vzory (od nejsilnějšího po nejslabší):
            string[][] splitPatterns = {
                // 1) Středník, dvojtečka — silná hranice
                new[] { @";\s+", @":\s+" },
                // 2) Pomlčka (em-dash, en-dash, --)
                new[] { @"\s*[\u2014\u2013]\s*", @"\s+--\s+" },
                // 3) Spojky s čárkou — přirozené frázové hranice
                new[] { @",\s+and\s+", @",\s+but\s+", @",\s+or\s+",
                        @",\s+yet\s+", @",\s+so\s+" },
                // 4) Vztažné věty s čárkou
                new[] { @",\s+who\s+", @",\s+which\s+", @",\s+that\s+",
                        @",\s+where\s+", @",\s+when\s+" },
                // 5) Jakékoli čárky (záložní)
                new[] { @",\s+" },
            };

            // Zkusit každou úroveň, dokud se nepodaří rozdělit
            foreach (var patterns in splitPatterns) {
                if (TrySplitAtPatterns(sent, patterns, maxLen, output))
                    return;
            }

            // Žádný vzor nezabrala → brutální split po slovech
            ForceSplitByWords(sent, maxLen, output);
        }

        /// <summary>Zkusí rozdělit větu podle zadaných regex vzorů.</summary>
        private static bool TrySplitAtPatterns(string sent, string[] patterns,
                int maxLen, System.Collections.Generic.List<string> output) {
            // Najít všechny dělící body
            var splits = new System.Collections.Generic.List<int>();
            foreach (string pattern in patterns) {
                foreach (Match m in Regex.Matches(sent, pattern,
                                                   RegexOptions.IgnoreCase)) {
                    splits.Add(m.Index + m.Length);
                }
            }
            if (splits.Count == 0) return false;

            splits.Sort();

            // Rozdělit na fragmenty
            var fragments = new System.Collections.Generic.List<string>();
            int pos = 0;
            foreach (int splitAt in splits) {
                if (splitAt > pos && splitAt <= sent.Length) {
                    // Najít konec interpunkce/mezery
                    string frag = sent.Substring(pos, splitAt - pos).Trim();
                    if (frag.Length > 0)
                        fragments.Add(frag);
                    pos = splitAt;
                }
            }
            if (pos < sent.Length) {
                string rest = sent.Substring(pos).Trim();
                if (rest.Length > 0) fragments.Add(rest);
            }

            // Ověřit, že aspoň nějaký fragment je ≤ maxLen
            bool anyFits = false;
            foreach (string f in fragments)
                if (f.Length <= maxLen) { anyFits = true; break; }
            if (!anyFits) return false;

            // Přidat fragmenty (dlouhé rekurzivně dodělit)
            foreach (string f in fragments) {
                if (f.Length <= maxLen)
                    output.Add(f);
                else
                    SplitLongSentence(f, maxLen, output);
            }
            return true;
        }

        /// <summary>Záložní split po slovech (když nic jiného nefunguje).</summary>
        private static void ForceSplitByWords(string sent, int maxLen,
                System.Collections.Generic.List<string> output) {
            string[] words = sent.Split(' ');
            string line = "";
            foreach (string w in words) {
                if (line.Length == 0) {
                    line = w;
                } else if (line.Length + 1 + w.Length <= maxLen) {
                    line += " " + w;
                } else {
                    output.Add(line);
                    line = w;
                }
            }
            if (line.Length > 0) output.Add(line);
        }
        /// <summary>Náhrada znaků, které nemusí být v atlasu herního
        /// bitmap fontu (typografické pomlčky, uvozovky, trojtečka...).</summary>
        public static string SanitizeForDisplay(string s) {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) {
                switch (c) {
                    case '\u2014': case '\u2013': sb.Append('-'); break;
                    case '\u2018': case '\u2019': sb.Append('\''); break;
                    case '\u201C': case '\u201D': sb.Append('"'); break;
                    case '\u2026': sb.Append("..."); break;
                    default:
                        if ((c >= ' ' && c < 127) || char.IsLetterOrDigit(c))
                            sb.Append(c);
                        else
                            sb.Append(' ');
                        break;
                }
            }
            // zalamování je věcí SubtitleOverlay (pixel-přesné podle
            // GDI metrik) — druhý, znakový wrap tady způsoboval
            // nerovnoměrné řádky při malém/velkém fontu
            return sb.ToString();
        }
    }
}
