using System.Data;
using System.Text.Json;

namespace ImagoLib.Models {

    /// <summary>Запись истории публикаций страницы (для «Vrátit verzi»).</summary>
    public class PublishHistoryItem {
        public int Id { get; set; }
        public int PageId { get; set; }
        public DateTime PublishedAt { get; set; }
        public string PublishedBy { get; set; } = "";
        public int TextCount { get; set; }
        public string Display => $"{PublishedAt:dd.MM.yyyy HH:mm}  —  {TextCount} textů" + (string.IsNullOrEmpty(PublishedBy) ? "" : $"  ({PublishedBy})");
    }

    /// <summary>
    /// Черновик страницы и публикация.
    /// Админка правит черновик (EditingDictionaryEntries, EditingDictionaryEntriesForFoto, EditingTextStyles),
    /// сайт показывает опубликованное (DictionaryEntries, DictionaryEntriesImages, TextStyles).
    /// «Publikovat» делает опубликованную страницу точной копией черновика, «Zahodit změny» — наоборот.
    /// Перед каждой публикацией опубликованные тексты и стили страницы сохраняются в PublishHistory.
    /// </summary>
    public static class PageDraft {

        private const int HistoryPerPage = 30;

        // Записи со своими таблицами (mítinky, novinky): черновик — Editing*-таблицы с теми же Id (DraftTables).
        // Порядок: сначала родительская таблица, затем дочерние (фото, параметры).
        private record RecordTable(string Name, string[] Columns);
        private static readonly Dictionary<int, RecordTable[]> RecordPages = new() {
            [38] = new[] {   // Mítink společnosti
                new RecordTable("Meetings", new[] { "Title", "Location", "Description", "Feedback", "CreatedAt", "UpdatedAt" }),
                new RecordTable("MeetingPhotos", new[] { "MeetingId", "PhotoName", "PhotoData" }),
            },
            [8] = new[] {    // Novinky
                new RecordTable("Noviny", new[] { "PostedDate", "Title", "Description", "Comment", "IconPhotoName", "IconPhoto" }),
                new RecordTable("NovinyPhotos", new[] { "NovinyId", "PhotoName", "PhotoData" }),
                new RecordTable("NovinyParameters", new[] { "NovinyId", "ParameterName", "ParameterValue" }),
            },
        };

        private static string Differs(RecordTable t, string a, string b) =>
            string.Join(" OR ", t.Columns.Select(c => $"NOT ({a}.[{c}] = {b}.[{c}] OR ({a}.[{c}] IS NULL AND {b}.[{c}] IS NULL))"));

        /// <summary>Сколько записей (mítinky / novinky) в черновике отличается от сайта.</summary>
        private static int CountRecordChanges(IDbConnection db, IDbTransaction? tx, int pageId) {
            if (!RecordPages.TryGetValue(pageId, out var tables)) return 0;
            var total = 0;
            foreach (var t in tables) {
                var cmd = db.CreateCommand();
                cmd.Transaction = tx;
                var e = "Editing" + t.Name;
                cmd.CommandText = $"SELECT (SELECT COUNT(*) FROM [{e}] x LEFT JOIN [{t.Name}] m ON m.Id = x.Id WHERE m.Id IS NULL OR {Differs(t, "x", "m")}) " +
                                  $"+ (SELECT COUNT(*) FROM [{t.Name}] m WHERE NOT EXISTS (SELECT 1 FROM [{e}] x WHERE x.Id = m.Id))";
                total += Convert.ToInt32(cmd.ExecuteScalar());
            }
            return total;
        }

        /// <summary>Записи черновика -> сайт (from = Editing*, to = основные) или обратно (Zahodit změny), с теми же Id.</summary>
        private static void MirrorRecords(IDbConnection db, IDbTransaction tx, int pageId, bool toSite) {
            if (!RecordPages.TryGetValue(pageId, out var tables)) return;
            string From(RecordTable t) => toSite ? "Editing" + t.Name : t.Name;
            string To(RecordTable t) => toSite ? t.Name : "Editing" + t.Name;
            void Run(string sql) { var c = db.CreateCommand(); c.Transaction = tx; c.CommandTimeout = 120; c.CommandText = sql; c.ExecuteNonQuery(); }

            // удаление: сначала дочерние таблицы
            foreach (var t in tables.Reverse())
                Run($"DELETE m FROM [{To(t)}] m WHERE NOT EXISTS (SELECT 1 FROM [{From(t)}] x WHERE x.Id = m.Id)");
            // изменение и добавление: сначала родительская
            foreach (var t in tables) {
                var set = string.Join(", ", t.Columns.Select(c => $"m.[{c}] = x.[{c}]"));
                Run($"UPDATE m SET {set} FROM [{To(t)}] m JOIN [{From(t)}] x ON x.Id = m.Id WHERE {Differs(t, "x", "m")}");
                var cols = "[Id], " + string.Join(", ", t.Columns.Select(c => $"[{c}]"));
                Run($"SET IDENTITY_INSERT [{To(t)}] ON; " +
                    $"INSERT INTO [{To(t)}] ({cols}) SELECT {cols} FROM [{From(t)}] x WHERE NOT EXISTS (SELECT 1 FROM [{To(t)}] m WHERE m.Id = x.Id) ORDER BY x.Id; " +
                    $"SET IDENTITY_INSERT [{To(t)}] OFF;");
            }
        }

        // Ключи текстов страницы (в черновике и на сайте) — стили привязаны к ключу текста, а не к странице
        private const string PageKeys = "(SELECT EntryKey FROM EditingDictionaryEntries WHERE PageId = @pageId UNION SELECT EntryKey FROM DictionaryEntries WHERE PageId = @pageId)";

        private const string StyleDiff = "ISNULL(s.FontFamily,'') <> ISNULL(e.FontFamily,'') OR ISNULL(s.FontSize,'') <> ISNULL(e.FontSize,'') OR ISNULL(s.FontWeight,'') <> ISNULL(e.FontWeight,'') " +
                                         "OR ISNULL(s.FontStyle,'') <> ISNULL(e.FontStyle,'') OR ISNULL(s.TextDecoration,'') <> ISNULL(e.TextDecoration,'') OR ISNULL(s.TextColor,'') <> ISNULL(e.TextColor,'')";

        /// <summary>Сколько изменений черновика ещё не опубликовано, по страницам (только страницы с изменениями).</summary>
        public static Dictionary<int, int> CountChangesByPage() {
            var result = new Dictionary<int, int>();
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = $@"
SELECT PageId, COUNT(*) FROM (
    -- тексты: новые, изменённые, удалённые
    SELECT e.PageId FROM EditingDictionaryEntries e
        LEFT JOIN DictionaryEntries d ON d.PageId = e.PageId AND d.EntryKey = e.EntryKey
        WHERE d.Id IS NULL OR ISNULL(d.ContentText, N'') <> ISNULL(e.ContentText, N'')
    UNION ALL
    SELECT d.PageId FROM DictionaryEntries d
        WHERE NOT EXISTS (SELECT 1 FROM EditingDictionaryEntries e WHERE e.PageId = d.PageId AND e.EntryKey = d.EntryKey)
    -- фото: новые, заменённые, удалённые
    UNION ALL
    SELECT e.PageId FROM EditingDictionaryEntriesForFoto e
        LEFT JOIN DictionaryEntriesImages d ON d.PageId = e.PageId AND d.EntryKey = e.EntryKey
        WHERE d.Id IS NULL OR ISNULL(d.ImageName, N'') <> ISNULL(e.ImageName, N'') OR d.ImageUrl <> e.Image
    UNION ALL
    SELECT d.PageId FROM DictionaryEntriesImages d
        WHERE NOT EXISTS (SELECT 1 FROM EditingDictionaryEntriesForFoto e WHERE e.PageId = d.PageId AND e.EntryKey = d.EntryKey)
    -- стили текстов страницы
    UNION ALL
    SELECT t.PageId FROM EditingTextStyles e
        JOIN EditingDictionaryEntries t ON t.EntryKey = e.EntryKey
        LEFT JOIN TextStyles s ON s.EntryKey = e.EntryKey
        WHERE s.Id IS NULL OR {StyleDiff}
    UNION ALL
    SELECT t.PageId FROM TextStyles s
        JOIN DictionaryEntries t ON t.EntryKey = s.EntryKey
        WHERE NOT EXISTS (SELECT 1 FROM EditingTextStyles e WHERE e.EntryKey = s.EntryKey)
) x GROUP BY PageId";
                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) result[dr.GetInt32(0)] = dr.GetInt32(1);
                }
                foreach (var pageId in RecordPages.Keys) {
                    var n = CountRecordChanges(db, null, pageId);
                    if (n > 0) result[pageId] = (result.TryGetValue(pageId, out var c) ? c : 0) + n;
                }
            }
            return result;
        }

        public static int CountChanges(int pageId) => CountChangesByPage().TryGetValue(pageId, out var n) ? n : 0;

        /// <summary>Публикует страницу: тексты, фото и стили черновика -> на сайт. Предыдущая версия — в историю.</summary>
        public static void Publish(int pageId, string publishedBy) {
            using (var db = Db.Get())
            using (var tx = db.BeginTransaction()) {
                SaveHistory(db, tx, pageId, publishedBy);

                Exec(db, tx, pageId, $@"
-- тексты
DELETE d FROM DictionaryEntries d
WHERE d.PageId = @pageId AND NOT EXISTS (SELECT 1 FROM EditingDictionaryEntries e WHERE e.PageId = d.PageId AND e.EntryKey = d.EntryKey);
UPDATE d SET d.ContentText = e.ContentText
FROM DictionaryEntries d JOIN EditingDictionaryEntries e ON e.PageId = d.PageId AND e.EntryKey = d.EntryKey
WHERE d.PageId = @pageId AND ISNULL(d.ContentText, N'') <> ISNULL(e.ContentText, N'');
INSERT INTO DictionaryEntries (PageId, EntryKey, ContentText)
SELECT e.PageId, e.EntryKey, e.ContentText FROM EditingDictionaryEntries e
WHERE e.PageId = @pageId AND NOT EXISTS (SELECT 1 FROM DictionaryEntries d WHERE d.PageId = e.PageId AND d.EntryKey = e.EntryKey)
ORDER BY e.Id;

-- фото
DELETE d FROM DictionaryEntriesImages d
WHERE d.PageId = @pageId AND NOT EXISTS (SELECT 1 FROM EditingDictionaryEntriesForFoto e WHERE e.PageId = d.PageId AND e.EntryKey = d.EntryKey);
UPDATE d SET d.ImageUrl = e.Image, d.ImageName = e.ImageName, d.ModifiedAt = GETDATE()
FROM DictionaryEntriesImages d JOIN EditingDictionaryEntriesForFoto e ON e.PageId = d.PageId AND e.EntryKey = d.EntryKey
WHERE d.PageId = @pageId AND (ISNULL(d.ImageName, N'') <> ISNULL(e.ImageName, N'') OR d.ImageUrl <> e.Image);
INSERT INTO DictionaryEntriesImages (PageId, EntryKey, ImageUrl, ImageName)
SELECT e.PageId, e.EntryKey, e.Image, e.ImageName FROM EditingDictionaryEntriesForFoto e
WHERE e.PageId = @pageId AND NOT EXISTS (SELECT 1 FROM DictionaryEntriesImages d WHERE d.PageId = e.PageId AND d.EntryKey = e.EntryKey)
ORDER BY e.Id;

-- стили текстов страницы
DELETE s FROM TextStyles s
WHERE s.EntryKey IN {PageKeys} AND NOT EXISTS (SELECT 1 FROM EditingTextStyles e WHERE e.EntryKey = s.EntryKey);
UPDATE s SET s.FontFamily = e.FontFamily, s.FontSize = e.FontSize, s.FontWeight = e.FontWeight, s.FontStyle = e.FontStyle,
             s.TextDecoration = e.TextDecoration, s.TextColor = e.TextColor
FROM TextStyles s JOIN EditingTextStyles e ON e.EntryKey = s.EntryKey
WHERE s.EntryKey IN {PageKeys} AND ({StyleDiff});
INSERT INTO TextStyles (EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration, TextColor)
SELECT e.EntryKey, e.FontFamily, e.FontSize, e.FontWeight, e.FontStyle, e.TextDecoration, e.TextColor FROM EditingTextStyles e
WHERE e.EntryKey IN {PageKeys} AND NOT EXISTS (SELECT 1 FROM TextStyles s WHERE s.EntryKey = e.EntryKey);");

                MirrorRecords(db, tx, pageId, toSite: true);   // mítinky / novinky
                tx.Commit();
            }
        }

        /// <summary>«Zahodit změny»: черновик страницы снова равен опубликованной версии.</summary>
        public static void Discard(int pageId) {
            using (var db = Db.Get())
            using (var tx = db.BeginTransaction()) {
                // стили — по ключам страницы ДО того, как тексты черновика будут заменены
                Exec(db, tx, pageId, $@"
DECLARE @keys TABLE (EntryKey NVARCHAR(255) PRIMARY KEY);
INSERT INTO @keys SELECT EntryKey FROM {PageKeys} k;
DELETE FROM EditingTextStyles WHERE EntryKey IN (SELECT EntryKey FROM @keys);
INSERT INTO EditingTextStyles (EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration, TextColor, TextAlignment)
SELECT EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration, TextColor, TextAlignment FROM TextStyles WHERE EntryKey IN (SELECT EntryKey FROM @keys);

DELETE FROM EditingDictionaryEntries WHERE PageId = @pageId;
INSERT INTO EditingDictionaryEntries (PageId, EntryKey, ContentText)
SELECT PageId, EntryKey, ContentText FROM DictionaryEntries WHERE PageId = @pageId ORDER BY Id;

DELETE FROM EditingDictionaryEntriesForFoto WHERE PageId = @pageId;
INSERT INTO EditingDictionaryEntriesForFoto (PageId, EntryKey, Image, ImageName)
SELECT PageId, EntryKey, ImageUrl, ImageName FROM DictionaryEntriesImages WHERE PageId = @pageId ORDER BY Id;");
                MirrorRecords(db, tx, pageId, toSite: false);  // mítinky / novinky
                tx.Commit();
            }
        }

        public static List<PublishHistoryItem> GetHistory(int pageId) {
            var list = new List<PublishHistoryItem>();
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, PageId, PublishedAt, PublishedBy, Texts FROM PublishHistory WHERE PageId = @pageId ORDER BY PublishedAt DESC";
                Db.SetParam(cmd, "@pageId", pageId);
                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        var texts = JsonSerializer.Deserialize<List<HistoryText>>(dr.GetString(4)) ?? new List<HistoryText>();
                        list.Add(new PublishHistoryItem {
                            Id = dr.GetInt32(0), PageId = dr.GetInt32(1), PublishedAt = dr.GetDateTime(2),
                            PublishedBy = dr.IsDBNull(3) ? "" : dr.GetString(3), TextCount = texts.Count,
                        });
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// «Vrátit verzi»: тексты и стили страницы из истории -> в черновик (на сайт — после «Publikovat»).
        /// Это версия, которая была на сайте ДО публикации с этой датой. Фото в историю не сохраняются.
        /// </summary>
        public static void RestoreToDraft(int historyId) {
            using (var db = Db.Get())
            using (var tx = db.BeginTransaction()) {
                var cmd = db.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT PageId, Texts, Styles FROM PublishHistory WHERE Id = @id";
                Db.SetParam(cmd, "@id", historyId);
                int pageId; List<HistoryText> texts; List<TextStyle> styles;
                using (var dr = cmd.ExecuteReader()) {
                    if (!dr.Read()) throw new InvalidOperationException("Verze nebyla nalezena.");
                    pageId = dr.GetInt32(0);
                    texts = JsonSerializer.Deserialize<List<HistoryText>>(dr.GetString(1)) ?? new List<HistoryText>();
                    styles = dr.IsDBNull(2) ? new List<TextStyle>() : JsonSerializer.Deserialize<List<TextStyle>>(dr.GetString(2)) ?? new List<TextStyle>();
                }

                Exec(db, tx, pageId, $@"
DELETE FROM EditingTextStyles WHERE EntryKey IN (SELECT EntryKey FROM EditingDictionaryEntries WHERE PageId = @pageId);
DELETE FROM EditingDictionaryEntries WHERE PageId = @pageId;");

                foreach (var t in texts) {
                    var ins = db.CreateCommand();
                    ins.Transaction = tx;
                    ins.CommandText = "INSERT INTO EditingDictionaryEntries (PageId, EntryKey, ContentText) VALUES (@pageId, @key, @text)";
                    Db.SetParam(ins, "@pageId", pageId);
                    Db.SetParam(ins, "@key", t.k);
                    Db.SetParam(ins, "@text", t.t);
                    ins.ExecuteNonQuery();
                }
                foreach (var s in styles) {
                    var ins = db.CreateCommand();
                    ins.Transaction = tx;
                    ins.CommandText = "DELETE FROM EditingTextStyles WHERE EntryKey = @key; " +
                                      "INSERT INTO EditingTextStyles (EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration, TextColor) " +
                                      "VALUES (@key, @ff, @fs, @fw, @fst, @td, @tc)";
                    Db.SetParam(ins, "@key", s.EntryKey);
                    Db.SetParam(ins, "@ff", s.FontFamily); Db.SetParam(ins, "@fs", s.FontSize); Db.SetParam(ins, "@fw", s.FontWeight);
                    Db.SetParam(ins, "@fst", s.FontStyle); Db.SetParam(ins, "@td", s.TextDecoration); Db.SetParam(ins, "@tc", s.TextColor);
                    ins.ExecuteNonQuery();
                }
                tx.Commit();
            }
        }

        private class HistoryText {
            public string k { get; set; } = "";
            public string? t { get; set; }
        }

        /// <summary>Снимок опубликованных текстов и стилей страницы перед публикацией; старые записи (больше 30) удаляются.</summary>
        private static void SaveHistory(IDbConnection db, IDbTransaction tx, int pageId, string publishedBy) {
            var texts = new List<HistoryText>();
            var read = db.CreateCommand();
            read.Transaction = tx;
            read.CommandText = "SELECT EntryKey, ContentText FROM DictionaryEntries WHERE PageId = @pageId ORDER BY Id";
            Db.SetParam(read, "@pageId", pageId);
            using (var dr = read.ExecuteReader()) {
                while (dr.Read()) texts.Add(new HistoryText { k = dr.GetString(0), t = dr.IsDBNull(1) ? null : dr.GetString(1) });
            }
            if (texts.Count == 0) return;   // страница ещё ни разу не публиковалась — сохранять нечего

            var styles = new List<TextStyle>();
            var readStyles = db.CreateCommand();
            readStyles.Transaction = tx;
            readStyles.CommandText = "SELECT EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration, TextColor FROM TextStyles " +
                                     "WHERE EntryKey IN (SELECT EntryKey FROM DictionaryEntries WHERE PageId = @pageId)";
            Db.SetParam(readStyles, "@pageId", pageId);
            using (var dr = readStyles.ExecuteReader()) {
                while (dr.Read()) {
                    styles.Add(new TextStyle {
                        EntryKey = dr.GetString(0),
                        FontFamily = dr.IsDBNull(1) ? null : dr.GetString(1), FontSize = dr.IsDBNull(2) ? null : dr.GetString(2),
                        FontWeight = dr.IsDBNull(3) ? null : dr.GetString(3), FontStyle = dr.IsDBNull(4) ? null : dr.GetString(4),
                        TextDecoration = dr.IsDBNull(5) ? null : dr.GetString(5), TextColor = dr.IsDBNull(6) ? null : dr.GetString(6),
                    });
                }
            }

            var ins = db.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO PublishHistory (PageId, PublishedAt, PublishedBy, Texts, Styles) VALUES (@pageId, GETDATE(), @by, @texts, @styles); " +
                              $"DELETE FROM PublishHistory WHERE PageId = @pageId AND Id NOT IN (SELECT TOP {HistoryPerPage} Id FROM PublishHistory WHERE PageId = @pageId ORDER BY PublishedAt DESC, Id DESC)";
            Db.SetParam(ins, "@pageId", pageId);
            Db.SetParam(ins, "@by", publishedBy);
            Db.SetParam(ins, "@texts", JsonSerializer.Serialize(texts));
            Db.SetParam(ins, "@styles", JsonSerializer.Serialize(styles));
            ins.ExecuteNonQuery();
        }

        private static void Exec(IDbConnection db, IDbTransaction tx, int pageId, string sql) {
            var cmd = db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.CommandTimeout = 120;
            Db.SetParam(cmd, "@pageId", pageId);
            cmd.ExecuteNonQuery();
        }
    }
}
