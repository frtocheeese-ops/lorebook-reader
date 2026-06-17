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

            // oříznout šum za poslední dokončenou větou
            Match m = Regex.Match(text, @".*[.!?][""')\]]*", RegexOptions.Singleline);
            if (m.Success) text = m.Value;

            return text.Trim();
        }
        /// <summary>Rozdělí text po větách do dávek ~maxLen znaků
        /// pro průběžné generování TTS (port z prototypu).</summary>
        public static System.Collections.Generic.List<string> SplitChunks(
                string text, int maxLen = 280) {
            string[] sents = Regex.Split(text, @"(?<=[.!?\u2026])\s+");
            var chunks = new System.Collections.Generic.List<string>();
            string buf = "";
            foreach (string s in sents) {
                if (buf.Length == 0 || buf.Length + s.Length + 1 <= maxLen) {
                    buf = (buf + " " + s).Trim();
                } else {
                    chunks.Add(buf);
                    buf = s;
                }
            }
            if (buf.Length > 0) chunks.Add(buf);
            return chunks;
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
            return sb.ToString();
        }
    }
}
