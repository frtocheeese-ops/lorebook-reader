using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Frtal.LorebookReader {

    /// <summary>Jedna povídka ze seznamu na GW2 wiki.</summary>
    public sealed class ShortStory {
        public string Title;      // název, jak se uloží do codexu
        public string Page;       // název stránky na wiki (i s podtržítky)
        public string Writer;
        public string Published;
        public string Timeline;   // zařazení v čase (wiki sloupec)

        public string Url =>
            "https://wiki.guildwars2.com/wiki/" + Page;
    }

    /// <summary>
    /// Oficiální povídky (Short stories) z GW2 wiki. Texty se do modulu
    /// NEBALÍ — stáhnou se až na vyžádání uživatele a uloží do jeho codexu,
    /// vždy s uvedeným zdrojem. Seznam:
    /// https://wiki.guildwars2.com/wiki/Short_story
    /// </summary>
    public static class ShortStoryLibrary {

        public const string Expansion = "Short Stories";

        // Seznam podle https://wiki.guildwars2.com/wiki/Short_story
        // (pořadí = podle data vydání). Vícedílné příběhy jsou rozepsané
        // po kapitolách, protože každá má na wiki vlastní stránku.
        // Vynecháno: „Drooburt's Last Wintersday" — nemá stránku na wiki,
        // odkazuje mimo ni, takže by se nedala načíst.
        public static readonly ShortStory[] All = {
            new ShortStory {
                Title = "Mr. Sparkles, A Tale of the Asura",
                Page = "Mr._Sparkles,_A_Tale_of_the_Asura",
                Writer = "Jeff Grubb", Published = "January 28, 2012",
                Timeline = "Before the personal story"
            },
            new ShortStory {
                Title = "Braham's Story",
                Page = "Braham's_Story",
                Writer = "Angel McCoy", Published = "March 28, 2013",
                Timeline = "Before Living World Season 1"
            },
            new ShortStory {
                Title = "Rox's Tale",
                Page = "Rox's_Tale",
                Writer = "Angel McCoy", Published = "May 6, 2013",
                Timeline = "Before Living World Season 1"
            },
            new ShortStory {
                Title = "Welcome to Paradise",
                Page = "Welcome_to_Paradise",
                Writer = "Scott McGough", Published = "May 16, 2013",
                Timeline = "Preceding The Secret of Southsun"
            },
            new ShortStory {
                Title = "Canach's Story: An After-Hours Meeting",
                Page = "Canach's_Story:_An_After-Hours_Meeting",
                Writer = "Scott McGough", Published = "May 22, 2013",
                Timeline = "Preceding Last Stand at Southsun"
            },
            new ShortStory {
                Title = "Marjory's Story: The Last Straw",
                Page = "Marjory's_Story:_The_Last_Straw",
                Writer = "Angel McCoy", Published = "June 19–21, 2013",
                Timeline = "Before Living World Season 1"
            },
            new ShortStory {
                Title = "Aetherblade Pirates: Look up!",
                Page = "Aetherblade_Pirates:_Look_up!",
                Writer = "Angel McCoy", Published = "June 26, 2013",
                Timeline = "Preceding Sky Pirates of Tyria"
            },
            new ShortStory {
                Title = "The Trek of the Zephyrites",
                Page = "Short_Story:_The_Trek_of_the_Zephyrites",
                Writer = "Angel McCoy", Published = "July 11, 2013",
                Timeline = "Approx. 1320 AE"
            },
            new ShortStory {
                Title = "Evon Gnashblade Disembarks",
                Page = "Short_Story:_Evon_Gnashblade_Disembarks",
                Writer = "Angel McCoy", Published = "July 26, 2013",
                Timeline = "Before Living World Season 1"
            },
            new ShortStory {
                Title = "A Shipwreck: How Ellen Met Magnus",
                Page = "A_Shipwreck:_How_Ellen_Met_Magnus",
                Writer = "Angel McCoy", Published = "July 26, 2013",
                Timeline = "Before Living World Season 1"
            },
            new ShortStory {
                Title = "Delegation",
                Page = "Short_Story:_Delegation",
                Writer = "Scott McGough", Published = "August 8, 2013",
                Timeline = "Preceding Queen's Jubilee"
            },
            new ShortStory {
                Title = "What Scarlet Saw",
                Page = "Short_Story:_What_Scarlet_Saw",
                Writer = "Scott McGough", Published = "August 23, 2013",
                Timeline = "Approx. 1304–1321 AE"
            },
            new ShortStory {
                Title = "A Message from Queen Jennah",
                Page = "A_Message_from_Queen_Jennah",
                Writer = "Writer not credited", Published = "September 3, 2013",
                Timeline = "After Queen's Jubilee"
            },
            new ShortStory {
                Title = "Shadowbox",
                Page = "Short_Story:_Shadowbox",
                Writer = "John Ryan", Published = "September 4, 2013",
                Timeline = "Before Living World Season 1"
            },
            new ShortStory {
                Title = "Twilight Preparations",
                Page = "Short_Story:_Twilight_Preparations",
                Writer = "Scott McGough", Published = "September 30, 2013",
                Timeline = "Preceding Twilight Assault"
            },
            new ShortStory {
                Title = "The Family Business",
                Page = "The_Family_Business",
                Writer = "John Ryan", Published = "October 17, 2013",
                Timeline = "Approx. 825 AE"
            },
            new ShortStory {
                Title = "Scarlet's Dossier",
                Page = "Scarlet's_Dossier:_A_history_of_Scarlet's_attacks_on_Tyria",
                Writer = "Rubi Bayer", Published = "January 7–13, 2014",
                Timeline = "Preceding The Origins of Madness"
            },
            new ShortStory {
                Title = "Lionguard Security Force",
                Page = "Lionguard_Security_Force",
                Writer = "Writer not credited", Published = "February 28, 2014",
                Timeline = "During Escape from and Battle for Lion's Arch"
            },
            new ShortStory {
                Title = "The Reaper's Bounty",
                Page = "The_Reaper's_Bounty",
                Writer = "John Smith", Published = "October 21–24, 2014",
                Timeline = "Timeline placement not stated"
            },
            new ShortStory {
                Title = "Notes from Rata Novus",
                Page = "Notes_from_Rata_Novus",
                Writer = "Ross Beeley", Published = "July 13–20, 2016",
                Timeline = "Between Heart of Thorns and Living World Season 3"
            },
            new ShortStory {
                Title = "An Interview with Queen Jennah",
                Page = "An_Interview_with_Queen_Jennah",
                Writer = "Aaron Linde", Published = "July 21, 2016",
                Timeline = "Between Heart of Thorns and Living World Season 3"
            },
            new ShortStory {
                Title = "Tyrian Travels: Chapter One",
                Page = "Tyrian_Travels:_Chapter_One",
                Writer = "Anatoli Ingram", Published = "August 31, 2016",
                Timeline = "After Heart of Thorns"
            },
            new ShortStory {
                Title = "Tyrian Travels: Chapter Two",
                Page = "Tyrian_Travels:_Chapter_Two",
                Writer = "Anatoli Ingram", Published = "September 12, 2016",
                Timeline = "After Heart of Thorns"
            },
            new ShortStory {
                Title = "Tyrian Travels: Chapter Three",
                Page = "Tyrian_Travels:_Chapter_Three",
                Writer = "Anatoli Ingram", Published = "October 10, 2016",
                Timeline = "After Heart of Thorns"
            },
            new ShortStory {
                Title = "Tyrian Travels: Chapter Four",
                Page = "Tyrian_Travels:_Chapter_Four",
                Writer = "Anatoli Ingram", Published = "October 31, 2016",
                Timeline = "After Heart of Thorns"
            },
            new ShortStory {
                Title = "Tyrian Travels: Chapter Five",
                Page = "Tyrian_Travels:_Chapter_Five",
                Writer = "Anatoli Ingram", Published = "December 1, 2016",
                Timeline = "After Heart of Thorns"
            },
            new ShortStory {
                Title = "Tyrian Travels: Chapter Six",
                Page = "Tyrian_Travels:_Chapter_Six",
                Writer = "Anatoli Ingram", Published = "January 12, 2017",
                Timeline = "After Heart of Thorns"
            },
            new ShortStory {
                Title = "Requiem: Rytlock",
                Page = "Requiem:_Rytlock",
                Writer = "Alex Kain, Samantha Wallschlaeger",
                Published = "January 29, 2019",
                Timeline = "Between All or Nothing and War Eternal"
            },
            new ShortStory {
                Title = "Requiem: Zafirah",
                Page = "Requiem:_Zafirah",
                Writer = "Alex Kain, Samantha Wallschlaeger",
                Published = "April 9, 2019",
                Timeline = "Between All or Nothing and War Eternal"
            },
            new ShortStory {
                Title = "Requiem: Caithe",
                Page = "Requiem:_Caithe",
                Writer = "Alex Kain, Samantha Wallschlaeger",
                Published = "May 7, 2019",
                Timeline = "Between All or Nothing and War Eternal"
            }
        };

        /// <summary>Značka obrázku v textu knihy. Čtečka ji rozpozná a místo
        /// řádku vykreslí obrázek (soubor v podsložce images/).</summary>
        public const string ImagePrefix = "⟦IMG:";
        public const string ImageSuffix = "⟧";

        /// <summary>Stáhne povídku a složí text pro codex: poznámka z wiki,
        /// samotný text s ilustracemi a na konci zdroj.</summary>
        /// <param name="imageDir">kam uložit stažené ilustrace (null = bez nich)</param>
        public static async Task<string> FetchAsync(
                ShortStory story, string imageDir = null,
                CancellationToken ct = default) {
            string html = await DownloadAsync(story.Url, ct)
                .ConfigureAwait(false);

            // Obsah článku = <div class="... mw-parser-output ...">. Třída
            // NENÍ sama — wiki přidává mw-content-ltr, lang atd., takže se
            // hledá regulárem (přesná shoda dřív selhala a do knihy se
            // dostalo menu i patička wiki — 26.7.2026).
            var open = Regex.Match(html,
                @"<div[^>]*class=""[^""]*mw-parser-output[^""]*""[^>]*>");
            if (!open.Success)
                throw new InvalidOperationException(
                    "Unexpected wiki page layout — article body not found.");
            string body = html.Substring(open.Index + open.Length);
            foreach (string stop in new[] {
                         "<div class=\"printfooter\"", "<div id=\"catlinks\"",
                         "id=\"catlinks\"", "<!-- NewPP" }) {
                int cut = body.IndexOf(stop, StringComparison.Ordinal);
                if (cut > 0) { body = body.Substring(0, cut); break; }
            }

            // wiki dělí stránku nadpisy <h2>; nás zajímá sekce "Text"
            var parts = Regex.Split(body, @"<h2[^>]*>");
            string intro = parts.Length > 0 ? parts[0] : "";
            string textSection = "";
            for (int i = 1; i < parts.Length; i++) {
                string headline = HeadlineOf(parts[i]);
                if (headline.Equals("Text", StringComparison.OrdinalIgnoreCase)
                    || headline.StartsWith("Text",
                        StringComparison.OrdinalIgnoreCase)) {
                    textSection = parts[i];
                    break;
                }
            }
            if (textSection.Length == 0 && parts.Length > 1)
                textSection = parts[1];   // fallback: první sekce

            // samotný nadpis sekce („Text") do knihy nepatří — uříznout
            // všechno až za </h2>
            textSection = AfterHeading(textSection);

            // ilustrace: <img> nahradit značkou ještě PŘED odstraněním tagů,
            // aby zůstaly na svém místě v textu
            textSection = MarkImages(textSection);

            var sb = new StringBuilder();
            const string Rule = "· · ·";

            string note = CleanHtml(StripBanners(intro));
            if (note.Length > 0) {
                sb.Append("Note from Wiki").Append("\n\n")
                  .Append(note).Append("\n\n")
                  .Append(Rule).Append("\n\n");
            }

            sb.Append(CleanHtml(textSection));

            sb.Append("\n\n").Append(Rule).Append("\n\n")
              .Append("Source: Guild Wars 2 Wiki — ").Append(story.Title)
              .Append('\n').Append(story.Url);

            string text = sb.ToString().Trim();

            // stáhnout ilustrace a značky přepsat na místní soubory
            if (!string.IsNullOrEmpty(imageDir))
                text = await DownloadImagesAsync(text, imageDir, ct)
                    .ConfigureAwait(false);
            else
                text = Regex.Replace(text,
                    @"⟦IMG:[^⟧]+⟧\s*", "");   // bez složky obrázky vynechat

            return text;
        }

        /// <summary>&lt;img&gt; → značka se vzdálenou adresou.</summary>
        private static string MarkImages(string html) {
            return Regex.Replace(html, @"<img[^>]*>", m => {
                var src = Regex.Match(m.Value, @"src=""([^""]+)""");
                if (!src.Success) return " ";
                string url = src.Groups[1].Value;
                // miniatury nahradit plnou verzí (…/thumb/a/ab/X.png/240px-X.png)
                url = Regex.Replace(url, @"/thumb(/.+?\.(?:png|jpg|jpeg|gif))/[^/]+$",
                    "$1", RegexOptions.IgnoreCase);
                if (url.StartsWith("//")) url = "https:" + url;
                else if (url.StartsWith("/")) url = "https://wiki.guildwars2.com" + url;
                // ikonky a drobnosti nechceme
                if (url.IndexOf("/skins/", StringComparison.OrdinalIgnoreCase) >= 0)
                    return " ";
                return "\n\n" + ImagePrefix + url + ImageSuffix + "\n\n";
            }, RegexOptions.IgnoreCase);
        }

        /// <summary>Stáhne obrázky ze značek do imageDir a značky přepíše na
        /// názvy souborů. Co se nepovede, tiše vypadne.</summary>
        private static async Task<string> DownloadImagesAsync(
                string text, string imageDir, CancellationToken ct) {
            System.IO.Directory.CreateDirectory(imageDir);
            var matches = Regex.Matches(text, @"⟦IMG:([^⟧]+)⟧");
            int done = 0;
            foreach (Match m in matches) {
                string url = m.Groups[1].Value;
                string marker = m.Value;
                if (done >= 8) {                       // rozumný strop
                    text = text.Replace(marker, "");
                    continue;
                }
                try {
                    string name = SafeFileName(url);
                    string path = System.IO.Path.Combine(imageDir, name);
                    if (!System.IO.File.Exists(path)) {
                        byte[] data = await DownloadBytesAsync(url, ct)
                            .ConfigureAwait(false);
                        if (data.Length < 512) throw new Exception("too small");
                        System.IO.File.WriteAllBytes(path, data);
                    }
                    text = text.Replace(marker, ImagePrefix + name + ImageSuffix);
                    done++;
                } catch {
                    text = text.Replace(marker, "");   // obrázek prostě nebude
                }
            }
            return text;
        }

        private static string SafeFileName(string url) {
            string leaf = url.Substring(url.LastIndexOf('/') + 1);
            leaf = Uri.UnescapeDataString(leaf);
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                leaf = leaf.Replace(c, '_');
            return leaf.Length > 80 ? leaf.Substring(leaf.Length - 80) : leaf;
        }

        private static async Task<byte[]> DownloadBytesAsync(
                string url, CancellationToken ct) {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent =
                "LorebookCodexAndTTS/0.8 (Blish HUD module; "
                + "https://github.com/frtocheeese-ops/lorebook-reader)";
            request.Timeout = 20000;
            using (ct.Register(() => { try { request.Abort(); } catch { } }))
            using (var response = (HttpWebResponse)
                       await request.GetResponseAsync().ConfigureAwait(false))
            using (var stream = response.GetResponseStream())
            using (var ms = new System.IO.MemoryStream()) {
                await stream.CopyToAsync(ms).ConfigureAwait(false);
                return ms.ToArray();
            }
        }

        // ----------------------------------------------------------------

        private static async Task<string> DownloadAsync(
                string url, CancellationToken ct) {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            // slušné chování vůči wiki: identifikovat se
            request.UserAgent =
                "LorebookCodexAndTTS/0.8 (Blish HUD module; "
                + "https://github.com/frtocheeese-ops/lorebook-reader)";
            request.Timeout = 20000;

            using (ct.Register(() => { try { request.Abort(); } catch { } }))
            using (var response = (HttpWebResponse)
                       await request.GetResponseAsync().ConfigureAwait(false))
            using (var stream = response.GetResponseStream())
            using (var reader = new System.IO.StreamReader(stream, Encoding.UTF8))
                return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        private static string Slice(string src, string from, string to) {
            int a = src.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return "";
            a += from.Length;
            int b = src.IndexOf(to, a, StringComparison.Ordinal);
            return b < 0 ? src.Substring(a) : src.Substring(a, b - a);
        }

        /// <summary>Zahodí zbytek nadpisu (text uvnitř h2) a vrátí až obsah
        /// sekce.</summary>
        private static string AfterHeading(string sectionHtml) {
            int end = sectionHtml.IndexOf("</h2>", StringComparison.Ordinal);
            return end < 0 ? sectionHtml : sectionHtml.Substring(end + 5);
        }

        private static string HeadlineOf(string sectionHtml) {
            var m = Regex.Match(sectionHtml,
                "id=\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value.Replace('_', ' ') : "";
        }

        /// <summary>Vyhodí wiki hlášky (copied verbatim…) a navigační boxy.</summary>
        private static string StripBanners(string html) {
            html = Regex.Replace(html,
                @"<table[^>]*>.*?</table>", " ", RegexOptions.Singleline);
            html = Regex.Replace(html,
                @"<div[^>]*class=""[^""]*(messagebox|notice|dablink|hatnote)[^""]*""[^>]*>.*?</div>",
                " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html,
                @"This article is copied[^<]*", " ", RegexOptions.IgnoreCase);
            return html;
        }

        /// <summary>Popisky obrázků (alt/figcaption) — dokud čtečka neumí
        /// vykreslovat obrázky, aspoň se neztratí informace, že tam jsou.</summary>
        private static List<string> ImageCaptions(string html) {
            var res = new List<string>();
            foreach (Match m in Regex.Matches(html, @"<img[^>]*alt=""([^""]+)""")) {
                string alt = Decode(m.Groups[1].Value).Trim();
                if (alt.Length > 2 && !res.Contains(alt)) res.Add(alt);
            }
            return res;
        }

        /// <summary>HTML → čistý text se zachovanými odstavci.</summary>
        private static string CleanHtml(string html) {
            // pryč s neviditelným balastem
            html = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", " ",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<span[^>]*class=""mw-editsection"".*?</span>",
                " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            // strukturu na značky
            html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</(p|div|li|h[1-6]|blockquote)>", "\n\n",
                RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<li[^>]*>", "- ", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<[^>]+>", "");
            html = Decode(html);
            // úklid mezer a prázdných řádků
            html = html.Replace("\r", "");
            html = Regex.Replace(html, @"[ \t]+", " ");
            html = Regex.Replace(html, @" ?\n ?", "\n");
            html = Regex.Replace(html, @"\n{3,}", "\n\n");
            return DropWikiChrome(html).Trim();
        }

        /// <summary>Poslední síto na úrovni textu: vyhodí řádky, které patří
        /// wiki, ne povídce (hlavička stránky, upozornění o verbatim kopii,
        /// navigační odkazy). Dělá se až nad čistým textem, aby to fungovalo
        /// bez ohledu na to, jak je banner v HTML zabalený.</summary>
        private static string DropWikiChrome(string text) {
            string[] drop = {
                "From Guild Wars 2 Wiki",
                "Jump to navigation",
                "Jump to search",
                "This article is copied",
                "verbatim from an official source",
                "Retrieved from",
                "[edit]"
            };
            var keep = new List<string>();
            foreach (string line in text.Split('\n')) {
                string t = line.Trim();
                bool bad = false;
                foreach (string d in drop)
                    if (t.IndexOf(d, StringComparison.OrdinalIgnoreCase) >= 0) {
                        bad = true; break;
                    }
                if (!bad) keep.Add(line);
            }
            return string.Join("\n", keep);
        }

        private static string Decode(string s) =>
            WebUtility.HtmlDecode(s ?? "").Replace(' ', ' ');
    }
}
