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

        public static ObservableCollection<DictionaryEntryForText> GetEntriesForPage(int pageId) {
            var entries = new ObservableCollection<DictionaryEntryForText>();
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, PageId, [EntryKey], ContentText FROM DictionaryEntries WHERE PageId = @pageId";
                Db.SetParam(cmd, "@pageId", pageId);

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        entries.Add(FromDataReader(dr));
                    }
                }
            }
            return entries;
        }

        public static ObservableCollection<DictionaryEntryForText> GetAllEntries() {
            var entries = new ObservableCollection<DictionaryEntryForText>();
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, PageId, EntryKey, ContentText FROM DictionaryEntries";

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
                cmd.CommandText = "SELECT Id, PageId, [EntryKey], ContentText FROM EditingDictionaryEntries WHERE PageId = @pageId";
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
