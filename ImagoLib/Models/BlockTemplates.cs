using System.Text.RegularExpressions;

namespace ImagoLib.Models {

    /// <summary>Поле блока: имя в ключе («Phone») и подпись в админке («Telefon»).</summary>
    public class BlockField {
        public string Name { get; init; } = "";
        public string Label { get; init; } = "";
        public bool Required { get; init; }
        public bool Multiline { get; init; }
        public string Hint { get; init; } = "";
    }

    /// <summary>
    /// Вид блока страницы: «Kontakty_Partner» + номер + «_» + поле => «Kontakty_Partner3_Phone».
    /// Страница сайта показывает столько блоков, сколько их есть в базе (см. Views/Home/Kontakty.cshtml),
    /// поэтому блоки можно добавлять, удалять и переставлять в ImagoAdmin.
    /// Новый вид блока = шаблон здесь + показ блоков этой группы в разметке страницы.
    /// </summary>
    public class BlockTemplate {
        public string Group { get; init; } = "";       // «Kontakty_Partner»
        public int PageId { get; init; }
        public string Title { get; init; } = "";       // «Spolupracovník»
        public string NameField { get; init; } = "Name";
        public IReadOnlyList<BlockField> Fields { get; init; } = Array.Empty<BlockField>();

        /// <summary>
        /// Строки «název: hodnota» страницы прибора: блоком считается любая пара *_Label / *_Value (в том числе старые ключи
        /// вроде Weight_Label), новые строки получают ключи Group{N}_Label / Group{N}_Value.
        /// </summary>
        public bool LabelValueRows { get; init; }

        public string BlockKey(int number) => Group + number;
        public string EntryKey(int number, string field) => $"{Group}{number}_{field}";
    }

    /// <summary>Найденный на странице блок: шаблон, порядковый номер и ключи его полей (поле -> ключ текста).</summary>
    public class BlockInstance {
        public BlockTemplate Template { get; init; } = null!;
        public int Number { get; init; }
        public IReadOnlyDictionary<string, string> FieldKeys { get; init; } = new Dictionary<string, string>();
        public string Label { get; init; } = "";
        /// <summary>Чем блок помечен в разметке сайта: data-block у блоков шаблонов, у строк параметров — data-key названия.</summary>
        public string Key => Template.LabelValueRows ? FieldKeys["Label"] : Template.BlockKey(Number);
        public bool HasDataBlock => !Template.LabelValueRows;
    }

    public static class BlockTemplates {

        public const int KontaktyPageId = 10;
        public const int NavodyPageId = 46;    // «Návody k přístrojům» (меню: /Diacom/DeviceDiacom?id=46, также /Diacom/Navody)
        public const int DevicesParentId = 5;  // «Přístroje Diacom»: подстраницы — страницы приборов
        public const int PricePageId = 45;     // «CENY PŘÍSTROJŮ DIACOM» — подстраница приборов, но не прибор

        public static readonly IReadOnlyList<BlockTemplate> All = new List<BlockTemplate> {
            new BlockTemplate {
                Group = "Kontakty_Person", PageId = KontaktyPageId, Title = "Kontaktní osoba",
                Fields = new[] {
                    new BlockField { Name = "Name", Label = "Jméno", Required = true },
                    new BlockField { Name = "Role", Label = "Funkce", Hint = "např. Jednatelka, Specialista prodeje" },
                    new BlockField { Name = "Mobile", Label = "Mobil" },
                    new BlockField { Name = "Email", Label = "E-mail" },
                }
            },
            new BlockTemplate {
                Group = "Kontakty_Partner", PageId = KontaktyPageId, Title = "Spolupracovník",
                Fields = new[] {
                    new BlockField { Name = "Region", Label = "Nadpis bloku", Hint = "např. Spolupráce v Čechách" },
                    new BlockField { Name = "Type", Label = "Typ spolupráce", Hint = "např. Obchodně školící servisní centrum" },
                    new BlockField { Name = "Name", Label = "Jméno / firma", Required = true },
                    new BlockField { Name = "Address", Label = "Adresa" },
                    new BlockField { Name = "Country", Label = "Země" },
                    new BlockField { Name = "Phone", Label = "Telefon" },
                    new BlockField { Name = "Email", Label = "E-mail" },
                    new BlockField { Name = "ICO", Label = "IČO" },
                    new BlockField { Name = "Skype", Label = "Skype" },
                    new BlockField { Name = "MapUrl", Label = "Odkaz na mapu", Hint = "adresa z Google Maps (https://…)" },
                }
            },
            new BlockTemplate {
                Group = "Navody_Video", PageId = NavodyPageId, Title = "Video", NameField = "Title",
                Fields = new[] {
                    new BlockField { Name = "Title", Label = "Název videa", Required = true, Hint = "např. DIACOM-SOLO-Ionizer — návod" },
                    new BlockField { Name = "Url", Label = "Odkaz na YouTube", Required = true, Hint = "zkopírujte adresu videa z YouTube (https://www.youtube.com/watch?v=… nebo https://youtu.be/…)" },
                    new BlockField { Name = "Description", Label = "Popis", Multiline = true, Hint = "nepovinné — krátký text pod videem" },
                }
            },
        };

        /// <summary>Подписи полей страницы, которые не входят в блоки.</summary>
        private static readonly Dictionary<string, string> PageFieldLabels = new Dictionary<string, string> {
            { "Kontakty_Title", "Nadpis stránky" },
            { "Kontakty_Intro1", "Úvodní text 1" }, { "Kontakty_Intro2", "Úvodní text 2" }, { "Kontakty_Intro3", "Úvodní text 3" },
            { "Kontakty_Firm_Name", "Firma · Název" }, { "Kontakty_Firm_Person", "Firma · Kontaktní osoba" },
            { "Kontakty_Firm_Street", "Firma · Ulice" }, { "Kontakty_Firm_City", "Firma · PSČ a město" },
            { "Kontakty_Firm_Phone", "Firma · Telefon" }, { "Kontakty_Firm_Mobile", "Firma · Mobil" },
            { "Kontakty_Firm_Email", "Firma · E-mail" }, { "Kontakty_Firm_ICO", "Firma · IČ" }, { "Kontakty_Firm_DIC", "Firma · DIČ" },
            { "Kontakty_Firm_Court", "Firma · Zápis v rejstříku" },
            { "Kontakty_Prov_Title", "Provozovna · Nadpis" }, { "Kontakty_Prov_Name", "Provozovna · Název" },
            { "Kontakty_Prov_Person", "Provozovna · Vedoucí" }, { "Kontakty_Prov_Street", "Provozovna · Ulice" },
            { "Kontakty_Prov_City", "Provozovna · PSČ a město" }, { "Kontakty_Prov_Mobile", "Provozovna · Mobil" },
            { "Kontakty_Prov_Email", "Provozovna · E-mail" },
            { "Kontakty_Map_Title", "Nadpis mapy" },
            { "Kontakty_Persons_Title", "Nadpis · Kontaktní osoby" },
            { "Kontakty_Partners_Title", "Nadpis · Spolupracujeme" },
            { "Navody_Title", "Nadpis stránky" },
            { "Navody_Intro", "Úvodní text" },
        };

        // Поля, которые сайт подставляет в ссылки и адреса (mailto:, href, телефон) — форматирование в них сломало бы ссылку
        private static readonly string[] PlainSuffixes = { "Email", "Url", "MapUrl", "Phone", "Mobile", "ICO", "DIC", "Skype" };

        /// <summary>Поле без форматирования (жирный/ссылки недоступны в редакторе): e-mail, odkaz, telefon, IČO…</summary>
        public static bool IsPlainField(string entryKey) =>
            PlainSuffixes.Any(s => (entryKey ?? "").EndsWith("_" + s, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// ID видео YouTube из любой ссылки (watch?v=, youtu.be/, shorts/, embed/) — для встроенного плеера на сайте.
        /// null — ссылка не на YouTube.
        /// </summary>
        public static string? YouTubeId(string? url) {
            var m = Regex.Match(url ?? "", @"(?:youtube(?:-nocookie)?\.com/(?:watch\?(?:.*&)?v=|embed/|shorts/|live/)|youtu\.be/)([A-Za-z0-9_-]{11})");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>Шаблоны блоков страницы. Страницы приборов (подстраницы «Přístroje Diacom») получают строки параметров.</summary>
        public static List<BlockTemplate> ForPage(int pageId, int? parentId = null) {
            var list = All.Where(t => t.PageId == pageId).ToList();
            if (parentId == DevicesParentId && pageId != PricePageId && pageId != NavodyPageId) list.Add(DeviceParams(pageId));
            return list;
        }

        /// <summary>Строки параметров прибора («Hmotnost: cca 10 kg»). Ключи новых строк — Device{pageId}_Param{N}_Label/_Value (уникальные на всём сайте).</summary>
        public static BlockTemplate DeviceParams(int pageId) => new BlockTemplate {
            Group = $"Device{pageId}_Param", PageId = pageId, Title = "Parametr", NameField = "Label", LabelValueRows = true,
            Fields = new[] {
                new BlockField { Name = "Label", Label = "Název parametru", Required = true, Hint = "např. Hmotnost:" },
                new BlockField { Name = "Value", Label = "Hodnota", Hint = "např. cca 10 kg" },
            }
        };

        /// <summary>Блоки страницы по её текстам (в порядке шаблонов, внутри — по номеру / порядку на странице).</summary>
        public static List<BlockInstance> FindBlocks(IEnumerable<DictionaryEntryForText> entries, IReadOnlyList<BlockTemplate> templates) {
            var list = entries.ToList();
            var texts = list.GroupBy(e => e.EntryKey).ToDictionary(g => g.Key, g => g.First().ContentText ?? "");
            var result = new List<BlockInstance>();

            foreach (var t in templates) {
                if (t.LabelValueRows) {
                    // строки параметров — в порядке текстов на странице (как их выводит сайт)
                    var n = 0;
                    foreach (var e in list.Where(e => e.EntryKey.EndsWith("_Label"))) {
                        var valueKey = e.EntryKey.Substring(0, e.EntryKey.Length - "_Label".Length) + "_Value";
                        if (!texts.ContainsKey(valueKey)) continue;
                        result.Add(new BlockInstance {
                            Template = t, Number = ++n,
                            FieldKeys = new Dictionary<string, string> { ["Label"] = e.EntryKey, ["Value"] = valueKey },
                            Label = $"{HtmlLite.ToPlainText(texts[e.EntryKey]).TrimEnd(':', ' ')}: {HtmlLite.ToPlainText(texts[valueKey])}",
                        });
                    }
                    continue;
                }
                var numbers = list.Select(e => Regex.Match(e.EntryKey, "^" + Regex.Escape(t.Group) + @"(\d+)_"))
                                  .Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).Distinct().OrderBy(x => x);
                foreach (var n in numbers) {
                    var name = texts.TryGetValue(t.EntryKey(n, t.NameField), out var v) ? HtmlLite.ToPlainText(v) : "";
                    result.Add(new BlockInstance {
                        Template = t, Number = n,
                        FieldKeys = t.Fields.ToDictionary(f => f.Name, f => t.EntryKey(n, f.Name)),
                        Label = string.IsNullOrWhiteSpace(name) ? "(bez jména)" : name,
                    });
                }
            }
            return result;
        }

        /// <summary>«Kontakty_Partner3_Phone» -> (шаблон Spolupracovník, 3, поле Phone). null — поле не из блока.</summary>
        public static (BlockTemplate Template, int Number, string Field)? ParseEntryKey(string entryKey) {
            foreach (var t in All) {
                var m = Regex.Match(entryKey ?? "", "^" + Regex.Escape(t.Group) + @"(\d+)_(.+)$");
                if (m.Success) return (t, int.Parse(m.Groups[1].Value), m.Groups[2].Value);
            }
            return null;
        }

        /// <summary>«Kontakty_Partner3» -> (шаблон, 3). null — не блок.</summary>
        public static (BlockTemplate Template, int Number)? ParseBlockKey(string blockKey) {
            foreach (var t in All) {
                var m = Regex.Match(blockKey ?? "", "^" + Regex.Escape(t.Group) + @"(\d+)$");
                if (m.Success) return (t, int.Parse(m.Groups[1].Value));
            }
            return null;
        }

        /// <summary>Понятное имя поля: «Spolupracovník 3 · Telefon», «Firma · E-mail»; для остальных — по ключу («Step 1 · Title»).</summary>
        public static string FieldDisplayName(string entryKey) {
            if (string.IsNullOrEmpty(entryKey)) return "";
            if (PageFieldLabels.TryGetValue(entryKey, out var label)) return label;

            var parsed = ParseEntryKey(entryKey);
            if (parsed != null) {
                var (t, n, field) = parsed.Value;
                var f = t.Fields.FirstOrDefault(x => x.Name == field);
                return $"{t.Title} {n} · {f?.Label ?? field}";
            }

            // строки параметров прибора: «Weight_Label» -> «Parametr · Weight · název»
            if (entryKey.EndsWith("_Label")) return "Parametr · " + Pretty(entryKey[..^6]) + " · název";
            if (entryKey.EndsWith("_Value")) return "Parametr · " + Pretty(entryKey[..^6]) + " · hodnota";

            return Pretty(entryKey);
        }

        private static string Pretty(string key) {
            var parts = key.Split('_', StringSplitOptions.RemoveEmptyEntries);
            var shown = parts.Length > 1 ? parts.Skip(1) : parts;
            return string.Join(" · ", shown.Select(p => Regex.Replace(p, @"(?<=[A-Za-z])(?=\d)", " ")));
        }
    }
}
