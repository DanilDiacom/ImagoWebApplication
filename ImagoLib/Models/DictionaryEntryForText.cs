using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;

namespace ImagoLib.Models {
    public class DictionaryEntryForText {
        public int Id { get; set; }
        public int PageId { get; set; }
        public string EntryKey { get; set; }
        public string ContentText { get; set; }

        /// <summary>
        /// Понятное имя поля для админки: «Kontakty_Partner3_Phone» -> «Spolupracovník 3 · Telefon» (подписи — BlockTemplates).
        /// </summary>
        public string DisplayName => BlockTemplates.FieldDisplayName(EntryKey);

        /// <summary>Текст без форматирования (для списка текстов в админке).</summary>
        public string PlainText => HtmlLite.ToPlainText(ContentText).Replace('\n', ' ');

        /// <summary>
        /// Блок, к которому относится поле: «Kontakty_Partner3_Phone» -> «Kontakty_Partner3» (часть ключа до номера включительно).
        /// Поля без номера (Kontakty_Title) к блокам не относятся — null.
        /// </summary>
        public static string GetBlockKey(string entryKey) {
            var m = System.Text.RegularExpressions.Regex.Match(entryKey ?? "", @"^(.+?\d+)_[^_]+$");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>Следующий свободный номер блока группы в черновике (Kontakty_Partner7 после Kontakty_Partner6).</summary>
        public static int NextBlockNumber(int pageId, string group) {
            var numbers = GetEntriesForEditind(pageId)
                .Select(e => System.Text.RegularExpressions.Regex.Match(e.EntryKey, "^" + System.Text.RegularExpressions.Regex.Escape(group) + @"(\d+)_"))
                .Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).ToList();
            return numbers.Count == 0 ? 1 : numbers.Max() + 1;
        }

        /// <summary>
        /// Сохраняет несколько текстов в черновик одной транзакцией (поля блока: ключ -> текст).
        /// Пустой текст сохраняется пустым — на сайте эта строка не показывается.
        /// </summary>
        public static void SaveEntriesForEditing(int pageId, IDictionary<string, string> values) {
            using (var db = Db.Get())
            using (var tx = db.BeginTransaction()) {
                foreach (var (key, value) in values) Upsert(db, tx, pageId, key, (value ?? "").Trim());
                tx.Commit();
            }
        }

        private static void Upsert(IDbConnection db, IDbTransaction tx, int pageId, string key, string text) {
            var cmd = db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "IF EXISTS (SELECT 1 FROM EditingDictionaryEntries WHERE PageId = @pageId AND [EntryKey] = @key) " +
                              "UPDATE EditingDictionaryEntries SET ContentText = @textValue WHERE PageId = @pageId AND [EntryKey] = @key " +
                              "ELSE INSERT INTO EditingDictionaryEntries (PageId, [EntryKey], ContentText) VALUES (@pageId, @key, @textValue)";
            Db.SetParam(cmd, "@pageId", pageId);
            Db.SetParam(cmd, "@key", key);
            Db.SetParam(cmd, "@textValue", text);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Удаляет тексты (поля блока) и их стили из черновика. На сайте исчезнут после «Publikovat».</summary>
        public static void DeleteEntriesForEditing(int pageId, IEnumerable<string> keys) {
            using (var db = Db.Get())
            using (var tx = db.BeginTransaction()) {
                foreach (var key in keys) {
                    var cmd = db.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM EditingDictionaryEntries WHERE PageId = @pageId AND [EntryKey] = @key; " +
                                      "DELETE FROM EditingTextStyles WHERE [EntryKey] = @key";
                    Db.SetParam(cmd, "@pageId", pageId);
                    Db.SetParam(cmd, "@key", key);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
        }

        /// <summary>
        /// Меняет местами содержимое двух блоков (перестановка «⬆ / ⬇»): пары ключей одного поля в обоих блоках.
        /// Ключи остаются на месте, переезжают тексты и стили — так работают и блоки шаблонов, и строки параметров приборов.
        /// </summary>
        public static void SwapEntryContentsForEditing(int pageId, IEnumerable<(string KeyA, string KeyB)> pairs) {
            var draft = GetEntriesForEditind(pageId).GroupBy(e => e.EntryKey).ToDictionary(g => g.Key, g => g.First().ContentText ?? "");
            var styles = TextStyle.GetAllStyles(draft: true).ToDictionary(s => s.EntryKey);
            using (var db = Db.Get())
            using (var tx = db.BeginTransaction()) {
                foreach (var (a, b) in pairs) {
                    Upsert(db, tx, pageId, a, draft.TryGetValue(b, out var tb) ? tb : "");
                    Upsert(db, tx, pageId, b, draft.TryGetValue(a, out var ta) ? ta : "");

                    // стили тоже меняются местами
                    var del = db.CreateCommand();
                    del.Transaction = tx;
                    del.CommandText = "DELETE FROM EditingTextStyles WHERE EntryKey IN (@a, @b)";
                    Db.SetParam(del, "@a", a);
                    Db.SetParam(del, "@b", b);
                    del.ExecuteNonQuery();
                    foreach (var (key, from) in new[] { (a, b), (b, a) }) {
                        if (!styles.TryGetValue(from, out var st)) continue;
                        var ins = db.CreateCommand();
                        ins.Transaction = tx;
                        ins.CommandText = "INSERT INTO EditingTextStyles (EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration, TextColor) " +
                                          "VALUES (@key, @ff, @fs, @fw, @fst, @td, @tc)";
                        Db.SetParam(ins, "@key", key);
                        Db.SetParam(ins, "@ff", st.FontFamily); Db.SetParam(ins, "@fs", st.FontSize); Db.SetParam(ins, "@fw", st.FontWeight);
                        Db.SetParam(ins, "@fst", st.FontStyle); Db.SetParam(ins, "@td", st.TextDecoration); Db.SetParam(ins, "@tc", st.TextColor);
                        ins.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
        }

        private static DictionaryEntryForText FromDataReader(IDataReader dr) {
            return new DictionaryEntryForText {
                Id = dr.GetInt32(0),
                PageId = dr.GetInt32(1),
                EntryKey = dr.GetString(2),
                ContentText = dr.IsDBNull(3) ? null : dr.GetString(3)
            };
        }

        public static void SaveEntry(DictionaryEntryForText entry) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "IF EXISTS (SELECT 1 FROM DictionaryEntries WHERE PageId = @pageId AND [EntryKey] = @key) " +
                                  "UPDATE DictionaryEntries SET ContentText = @textValue WHERE PageId = @pageId AND [EntryKey] = @key " +
                                  "ELSE " +
                                  "INSERT INTO DictionaryEntries (PageId, [EntryKey], ContentText) VALUES (@pageId, @key, @textValue)";

                Db.SetParam(cmd, "@pageId", entry.PageId);
                Db.SetParam(cmd, "@key", entry.EntryKey);
                Db.SetParam(cmd, "@textValue", entry.ContentText);

                cmd.ExecuteNonQuery();
            }
        }

        public static DictionaryEntryForText GetEntry(int pageId, string key) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT id, pageId, [key], textValue FROM DictionaryEntries WHERE pageId = @pageId AND [key] = @key";
                Db.SetParam(cmd, "@pageId", pageId);
                Db.SetParam(cmd, "@key", key);

                using (var dr = cmd.ExecuteReader()) {
                    if (dr.Read()) {
                        return FromDataReader(dr);
                    }
                }
            }
            return null;
        }

        public static ObservableCollection<DictionaryEntryForText> GetEntriesForPage(int pageId) => GetEntriesForPage(pageId, draft: false);

        /// <summary>Тексты страницы: опубликованные (сайт) или черновик (предпросмотр админки).</summary>
        public static ObservableCollection<DictionaryEntryForText> GetEntriesForPage(int pageId, bool draft) {
            var entries = new ObservableCollection<DictionaryEntryForText>();
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = $"SELECT Id, PageId, [EntryKey], ContentText FROM {(draft ? "EditingDictionaryEntries" : "DictionaryEntries")} WHERE PageId = @pageId ORDER BY Id";
                Db.SetParam(cmd, "@pageId", pageId);

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        entries.Add(FromDataReader(dr));
                    }
                }
            }
            return entries;
        }

        public static ObservableCollection<DictionaryEntryForText> GetAllEntries() => GetAllEntries(draft: false);

        /// <summary>Все тексты сайта: опубликованные или черновик (предпросмотр админки).</summary>
        public static ObservableCollection<DictionaryEntryForText> GetAllEntries(bool draft) {
            var entries = new ObservableCollection<DictionaryEntryForText>();
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = $"SELECT Id, PageId, EntryKey, ContentText FROM {(draft ? "EditingDictionaryEntries" : "DictionaryEntries")} ORDER BY Id";

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        entries.Add(FromDataReader(dr));
                    }
                }
            }
            return entries;
        }


        public static DictionaryEntryForText GetEntryByKey(string entryKey) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, PageId, [EntryKey], ContentText FROM DictionaryEntries WHERE [EntryKey] = @key";
                Db.SetParam(cmd, "@key", entryKey);

                using (var dr = cmd.ExecuteReader()) {
                    if (dr.Read()) {
                        return FromDataReader(dr);
                    }
                }
            }
            return null;
        }

        public static void InsertEntryIfNotExists(DictionaryEntryForText entry) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = @"
            IF NOT EXISTS (
                SELECT 1 FROM DictionaryEntries WHERE PageId = @pageId AND [EntryKey] = @key
            )
            BEGIN
                INSERT INTO DictionaryEntries (PageId, [EntryKey], ContentText)
                VALUES (@pageId, @key, @textValue)
            END
            -- и в черновик: «Publikovat» делает сайт копией черновика, без этого новый текст при публикации удалился бы
            IF NOT EXISTS (
                SELECT 1 FROM EditingDictionaryEntries WHERE PageId = @pageId AND [EntryKey] = @key
            )
            BEGIN
                INSERT INTO EditingDictionaryEntries (PageId, [EntryKey], ContentText)
                VALUES (@pageId, @key, @textValue)
            END";

                Db.SetParam(cmd, "@pageId", entry.PageId);
                Db.SetParam(cmd, "@key", entry.EntryKey);
                Db.SetParam(cmd, "@textValue", entry.ContentText);

                cmd.ExecuteNonQuery();
            }
        }

        public static void DeleteEntriesByPageId(int pageId) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM DictionaryEntries WHERE PageId = @pageId";
                Db.SetParam(cmd, "@pageId", pageId);
                cmd.ExecuteNonQuery();
            }
        }







        public static void SyncEditingDictionary() {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM EditingDictionaryEntries";  // Очищаем таблицу редактирования

                // Копируем все записи из основной таблицы в таблицу редактирования
                cmd.ExecuteNonQuery();

                cmd.CommandText = "INSERT INTO EditingDictionaryEntries (PageId, EntryKey, ContentText) " +
                                  "SELECT PageId, EntryKey, ContentText FROM DictionaryEntries";
                cmd.ExecuteNonQuery();
            }
        }

        public static ObservableCollection<DictionaryEntryForText> GetEntriesForEditind(int pageId) {
            var entries = new ObservableCollection<DictionaryEntryForText>();
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, PageId, [EntryKey], ContentText FROM EditingDictionaryEntries WHERE PageId = @pageId ORDER BY Id";
                Db.SetParam(cmd, "@pageId", pageId);

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        entries.Add(FromDataReader(dr));
                    }
                }
            }
            return entries;
        }
        public static void SaveEntryForEditing(DictionaryEntryForText entry) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "IF EXISTS (SELECT 1 FROM EditingDictionaryEntries WHERE PageId = @pageId AND [EntryKey] = @key) " +
                                  "UPDATE EditingDictionaryEntries SET ContentText = @textValue WHERE PageId = @pageId AND [EntryKey] = @key " +
                                  "ELSE " +
                                  "INSERT INTO EditingDictionaryEntries (PageId, [EntryKey], ContentText) VALUES (@pageId, @key, @textValue)";

                Db.SetParam(cmd, "@pageId", entry.PageId);
                Db.SetParam(cmd, "@key", entry.EntryKey);
                Db.SetParam(cmd, "@textValue", entry.ContentText);

                cmd.ExecuteNonQuery();
            }
        }
        public static List<DictionaryEntryForText> GetAllEntriesForEditing() {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT * FROM EditingDictionaryEntries";

                var reader = cmd.ExecuteReader();
                var entries = new List<DictionaryEntryForText>();

                while (reader.Read()) {
                    var entry = new DictionaryEntryForText {
                        PageId = reader.GetInt32(reader.GetOrdinal("PageId")),
                        EntryKey = reader.GetString(reader.GetOrdinal("EntryKey")),
                        ContentText = reader.GetString(reader.GetOrdinal("ContentText"))
                    };
                    entries.Add(entry);
                }

                return entries;
            }
        }

    }
}
