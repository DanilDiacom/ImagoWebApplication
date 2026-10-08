using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ImagoLib.Models {

    /// <summary>
    /// «Лёгкий HTML» текстов сайта: жирный, курсив, подчёркивание, перенос строки и ссылки.
    /// ImagoAdmin сохраняет форматирование только этими тегами, сайт выводит тексты через Sanitize:
    /// разрешённые теги остаются, всё остальное экранируется (никаких скриптов, стилей и чужой разметки).
    /// </summary>
    public static class HtmlLite {

        private static readonly Regex Token = new Regex(
            @"<\s*(?<close>/)?\s*(?<tag>strong|b|em|i|u|br)\b[^>]*>" +              // простые теги
            @"|<\s*a\s[^>]*?href\s*=\s*(?:""(?<href>[^""]*)""|'(?<href>[^']*)')[^>]*>" +   // <a href="…">
            @"|<\s*/\s*a\s*>" +
            @"|&(?:#\d{1,7}|#x[0-9a-fA-F]{1,6}|[a-zA-Z][a-zA-Z0-9]{1,31});",          // готовые сущности (&nbsp; &amp;…)
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Безопасный HTML для вывода на сайт (Html.Raw).</summary>
        public static string Sanitize(string? text) {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length + 16);
            var pos = 0;
            foreach (Match m in Token.Matches(text)) {
                sb.Append(Encode(text.Substring(pos, m.Index - pos)));
                pos = m.Index + m.Length;

                var v = m.Value;
                if (v[0] == '&') { sb.Append(v); continue; }
                if (m.Groups["tag"].Success) {
                    var tag = m.Groups["tag"].Value.ToLowerInvariant();
                    if (tag == "br") sb.Append("<br>");
                    else sb.Append(m.Groups["close"].Success ? $"</{tag}>" : $"<{tag}>");
                    continue;
                }
                if (m.Groups["href"].Success) {
                    var href = WebUtility.HtmlDecode(m.Groups["href"].Value).Trim();
                    if (!IsSafeHref(href)) { sb.Append("<a>"); continue; }   // небезопасная ссылка — без адреса (закроется </a>)
                    var external = href.StartsWith("http", StringComparison.OrdinalIgnoreCase);
                    sb.Append($"<a href=\"{Encode(href)}\"{(external ? " target=\"_blank\" rel=\"noopener\"" : "")}>");
                    continue;
                }
                sb.Append("</a>");
            }
            sb.Append(Encode(text.Substring(pos)));
            return sb.ToString();
        }

        // Экранируем только служебные символы HTML — буквы с диакритикой остаются как есть (WebUtility.HtmlEncode превращает «ý» в &#253;)
        private static string Encode(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

        /// <summary>Ссылки только на веб, почту, телефон и страницы сайта (никаких javascript: и т. п.).</summary>
        public static bool IsSafeHref(string href) =>
            Regex.IsMatch(href, @"^(https?://|mailto:|tel:|/(?!/)|#)", RegexOptions.IgnoreCase);

        /// <summary>Текст без форматирования (для атрибутов вроде href/title, списков в админке, чат-бота).</summary>
        public static string ToPlainText(string? html) {
            if (string.IsNullOrEmpty(html)) return "";
            var s = Regex.Replace(html, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, "<[^>]+>", "");
            return WebUtility.HtmlDecode(s);
        }

        /// <summary>Тексты страницы для представлений сайта: ключ -> безопасный HTML.</summary>
        public static Dictionary<string, string> ToSafeDictionary(IEnumerable<DictionaryEntryForText> entries) =>
            entries.GroupBy(e => e.EntryKey).ToDictionary(g => g.Key, g => Sanitize(g.First().ContentText));
    }
}
