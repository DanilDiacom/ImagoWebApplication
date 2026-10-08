using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using ImagoLib.Models;

namespace ImagoAdmin {

    /// <summary>
    /// Преобразование «лёгкого HTML» текстов сайта (HtmlLite: strong, em, u, br, a href) в документ редактора и обратно.
    /// Пользователь видит в редакторе форматированный текст, теги не видны. Всё, что вставлено с другим
    /// оформлением (цвета, шрифты из Word), при сохранении отбрасывается — остаются только жирный, курсив, подчёркивание и ссылки.
    /// </summary>
    public static class RichTextHtml {

        private static readonly Regex Token = new Regex(
            @"<\s*(?<close>/)?\s*(?<tag>strong|b|em|i|u|br)\b[^>]*>" +
            @"|<\s*a\s[^>]*?href\s*=\s*(?:""(?<href>[^""]*)""|'(?<href>[^']*)')[^>]*>" +
            @"|<\s*(?<aclose>/)\s*a\s*>" +
            @"|\r?\n",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>HTML -> документ редактора.</summary>
        public static FlowDocument ToDocument(string? html) {
            var paragraph = new Paragraph();
            int bold = 0, italic = 0, underline = 0;
            string? href = null;
            var text = html ?? "";
            var pos = 0;

            void AddText(string raw) {
                if (raw.Length == 0) return;
                var run = new Run(WebUtility.HtmlDecode(raw));
                if (bold > 0) run.FontWeight = FontWeights.Bold;
                if (italic > 0) run.FontStyle = FontStyles.Italic;
                if (underline > 0) run.TextDecorations = TextDecorations.Underline;
                if (href != null && Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var uri)) {
                    paragraph.Inlines.Add(new Hyperlink(run) { NavigateUri = uri });
                }
                else {
                    paragraph.Inlines.Add(run);
                }
            }

            foreach (Match m in Token.Matches(text)) {
                AddText(text.Substring(pos, m.Index - pos));
                pos = m.Index + m.Length;

                if (m.Value.EndsWith("\n")) { paragraph.Inlines.Add(new LineBreak()); continue; }
                if (m.Groups["aclose"].Success) { href = null; continue; }
                if (m.Groups["href"].Success) { href = WebUtility.HtmlDecode(m.Groups["href"].Value).Trim(); continue; }

                var close = m.Groups["close"].Success;
                switch (m.Groups["tag"].Value.ToLowerInvariant()) {
                    case "br": paragraph.Inlines.Add(new LineBreak()); break;
                    case "strong": case "b": bold = Math.Max(0, bold + (close ? -1 : 1)); break;
                    case "em": case "i": italic = Math.Max(0, italic + (close ? -1 : 1)); break;
                    case "u": underline = Math.Max(0, underline + (close ? -1 : 1)); break;
                }
            }
            AddText(text.Substring(pos));

            var doc = new FlowDocument(paragraph) { PagePadding = new Thickness(4) };
            return doc;
        }

        private record Segment(string Text, bool Bold, bool Italic, bool Underline, string? Href);

        /// <summary>Документ редактора -> HTML. plain = true: только текст (для e-mailů, odkazů, telefonů).</summary>
        public static string ToHtml(FlowDocument doc, bool plain) {
            var segments = new List<Segment>();
            var firstBlock = true;
            foreach (var block in doc.Blocks) {
                if (block is not Paragraph p) continue;
                if (!firstBlock) segments.Add(new Segment("\n", false, false, false, null));   // новый абзац = перенос строки
                firstBlock = false;
                foreach (var inline in p.Inlines) Collect(inline, segments, underline: false, href: null);
            }

            // лишние переносы в конце (RichTextBox всегда держит пустой абзац)
            while (segments.Count > 0 && segments[^1].Text == "\n") segments.RemoveAt(segments.Count - 1);

            if (plain) return string.Concat(segments.Select(s => s.Text)).Replace("\n", " ").Trim();

            var sb = new StringBuilder();
            foreach (var group in Merge(segments)) {
                if (group.Text == "\n") { sb.Append("<br>"); continue; }
                var t = Encode(group.Text);
                if (group.Underline) t = $"<u>{t}</u>";
                if (group.Italic) t = $"<em>{t}</em>";
                if (group.Bold) t = $"<strong>{t}</strong>";
                if (group.Href != null) t = $"<a href=\"{Encode(group.Href).Replace("\"", "&quot;")}\">{t}</a>";
                sb.Append(t);
            }
            return sb.ToString();
        }

        private static void Collect(Inline inline, List<Segment> segments, bool underline, string? href) {
            switch (inline) {
                case Run run:
                    var u = underline || (run.TextDecorations?.Any(d => d.Location == TextDecorationLocation.Underline) ?? false);
                    // у ссылки подчёркивание — оформление самой ссылки, а не текста
                    if (href != null && !underline) u = false;
                    var text = run.Text.Replace("\r\n", "\n").Replace('\r', '\n');
                    if (text.Length > 0) segments.Add(new Segment(text, run.FontWeight >= FontWeights.SemiBold, run.FontStyle == FontStyles.Italic, u, href));
                    break;
                case LineBreak:
                    segments.Add(new Segment("\n", false, false, false, null));
                    break;
                case Hyperlink link:
                    var target = link.NavigateUri?.OriginalString;
                    var safe = target != null && HtmlLite.IsSafeHref(target) ? target : null;
                    foreach (var child in link.Inlines) Collect(child, segments, underline, safe);
                    break;
                case Span span:
                    var spanUnderline = underline || (span.TextDecorations?.Any(d => d.Location == TextDecorationLocation.Underline) ?? false);
                    foreach (var child in span.Inlines) Collect(child, segments, spanUnderline, href);
                    break;
            }
        }

        // Соседние куски с одинаковым оформлением — одним тегом
        private static IEnumerable<Segment> Merge(List<Segment> segments) {
            Segment? current = null;
            foreach (var s in segments) {
                if (s.Text == "\n") {
                    if (current != null) { yield return current; current = null; }
                    yield return s;
                    continue;
                }
                if (current != null && current.Bold == s.Bold && current.Italic == s.Italic && current.Underline == s.Underline && current.Href == s.Href) {
                    current = current with { Text = current.Text + s.Text };
                }
                else {
                    if (current != null) yield return current;
                    current = s;
                }
            }
            if (current != null) yield return current;
        }

        // Экранируем только то, что нужно для HTML (буквы с диакритикой остаются как есть)
        private static string Encode(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
