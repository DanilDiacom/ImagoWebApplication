using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;

namespace ImagoLib.Models {
    public class DictionaryEntryForImages {
        public int Id { get; set; }
        public int PageId { get; set; }
        public string EntryKey { get; set; }
        public byte[] ImageData { get; set; }
        public string ImageName { get; set; }


        private static DictionaryEntryForImages FromDataReader(IDataReader dr) {
            return new DictionaryEntryForImages {
                Id = dr.GetInt32(0),
                PageId = dr.GetInt32(1),
                EntryKey = dr.GetString(2),
                ImageData = dr.IsDBNull(3) ? null : (byte[])dr[3],
                ImageName = dr.IsDBNull(4) ? null : dr.GetString(4) 
            };
        }

        public static void SaveEntry(DictionaryEntryForImages entry) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "IF EXISTS (SELECT 1 FROM DictionaryEntriesImages WHERE PageId = @pageId AND [EntryKey] = @key) " +
                                  "UPDATE DictionaryEntriesImages SET ImageUrl = @imageData, ImageName = @imageName WHERE PageId = @pageId AND [EntryKey] = @key " +
                                  "ELSE " +
                                  "INSERT INTO DictionaryEntriesImages (PageId, [EntryKey], ImageUrl, ImageName) VALUES (@pageId, @key, @imageData, @imageName)";

                Db.SetParam(cmd, "@pageId", entry.PageId);
                Db.SetParam(cmd, "@key", entry.EntryKey);
                Db.SetParam(cmd, "@imageData", entry.ImageData);
                Db.SetParam(cmd, "@imageName", entry.ImageName);

                cmd.ExecuteNonQuery();
            }
        }

        public static void SaveEntryForEditing(DictionaryEntryForImages entry) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "IF EXISTS (SELECT 1 FROM EditingDictionaryEntriesForFoto WHERE PageId = @pageId AND [EntryKey] = @key) " +
                                  "UPDATE EditingDictionaryEntriesForFoto SET Image = @imageData, ImageName = @imageName WHERE PageId = @pageId AND [EntryKey] = @key " +
                                  "ELSE " +
                                  "INSERT INTO EditingDictionaryEntriesForFoto (PageId, [EntryKey], Image, ImageName) VALUES (@pageId, @key, @imageData, @imageName)";

                Db.SetParam(cmd, "@pageId", entry.PageId);
                Db.SetParam(cmd, "@key", entry.EntryKey);
                Db.SetParam(cmd, "@imageData", entry.ImageData);
                Db.SetParam(cmd, "@imageName", entry.ImageName);

                cmd.ExecuteNonQuery();
            }
        }

        public static DictionaryEntryForImages GetEntry(int pageId, string key) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, PageId, [EntryKey], ImageUrl, ImageName FROM DictionaryEntriesImages WHERE PageId = @pageId AND [EntryKey] = @key";
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

        public static ObservableCollection<DictionaryEntryForImages> GetEntriesForPage(int pageId) {
            var entries = new ObservableCollection<DictionaryEntryForImages>();
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, PageId, [EntryKey], ImageUrl, ImageName FROM DictionaryEntriesImages WHERE PageId = @pageId";
                Db.SetParam(cmd, "@pageId", pageId);

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        entries.Add(FromDataReader(dr));
                    }
                }
            }
            return entries;
        }

        public static ObservableCollection<DictionaryEntryForImages> GetAllEntries() {
            var entries = new ObservableCollection<DictionaryEntryForImages>();
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, PageId, EntryKey, ImageUrl, ImageName FROM DictionaryEntriesImages";

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        entries.Add(FromDataReader(dr));
                    }
                }
            }
            return entries;
        }

        public static DictionaryEntryForImages GetEntryByKey(string entryKey) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, PageId, [EntryKey], ImageUrl, ImageName FROM DictionaryEntriesImages WHERE [EntryKey] = @key";
                Db.SetParam(cmd, "@key", entryKey);

                using (var dr = cmd.ExecuteReader()) {
                    if (dr.Read()) {
                        return FromDataReader(dr);
                    }
                }
            }
            return null;
        }
        public static void UpdateImage(int pageId, string entryKey, byte[] imageData, string imageName) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "UPDATE DictionaryEntriesImages " +
                                  "SET ImageUrl = @imageData, ImageName = @imageName " +
                                  "WHERE PageId = @pageId AND [EntryKey] = @key";

                Db.SetParam(cmd, "@pageId", pageId);
                Db.SetParam(cmd, "@key", entryKey);
                Db.SetParam(cmd, "@imageData", imageData);
                Db.SetParam(cmd, "@imageName", imageName);

                cmd.ExecuteNonQuery();
            }
        }

        public static void InsertEntryIfNotExists(DictionaryEntryForImages entry) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = @"
            IF NOT EXISTS (
                SELECT 1 FROM DictionaryEntriesImages WHERE PageId = @pageId AND [EntryKey] = @key
            )
            BEGIN
                INSERT INTO DictionaryEntriesImages (PageId, [EntryKey], ImageUrl, ImageName)
                VALUES (@pageId, @key, @imageData, @imageName)
            END";

                Db.SetParam(cmd, "@pageId", entry.PageId);
                Db.SetParam(cmd, "@key", entry.EntryKey);
                Db.SetParam(cmd, "@imageData", entry.ImageData);
                Db.SetParam(cmd, "@imageName", entry.ImageName);

                cmd.ExecuteNonQuery();
            }
        }

        public static void DeleteEntriesImageByPageId(int pageId) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM DictionaryEntriesImages WHERE PageId = @pageId";
                Db.SetParam(cmd, "@pageId", pageId);
                cmd.ExecuteNonQuery();
            }
        }







        public static void UpdateImageForEditing(int pageId, string entryKey, byte[] imageData, string imageName) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "UPDATE EditingDictionaryEntriesForFoto " +
                                  "SET Image = @imageData, ImageName = @imageName " +
                                  "WHERE PageId = @pageId AND [EntryKey] = @key";

                Db.SetParam(cmd, "@pageId", pageId);
                Db.SetParam(cmd, "@key", entryKey);
                Db.SetParam(cmd, "@imageData", imageData);
                Db.SetParam(cmd, "@imageName", imageName);

                cmd.ExecuteNonQuery();
            }
        }



        public static void SyncEditingDictionary() {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM EditingDictionaryEntriesForFoto"; 

                cmd.ExecuteNonQuery();

                cmd.CommandText = "INSERT INTO EditingDictionaryEntriesForFoto (PageId, EntryKey, Image, ImageName) " +
                                  "SELECT PageId, EntryKey, ImageUrl, ImageName FROM DictionaryEntriesImages";
                cmd.ExecuteNonQuery();
            }
        }

        public static ObservableCollection<DictionaryEntryForImages> GetEntriesForEditing(int pageId) {
            var entries = new ObservableCollection<DictionaryEntryForImages>();
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, PageId, [EntryKey], Image, ImageName FROM EditingDictionaryEntriesForFoto WHERE PageId = @pageId";
                Db.SetParam(cmd, "@pageId", pageId);

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        entries.Add(FromDataReader(dr));
                    }
                }
            }
            return entries;
        }

        public static List<DictionaryEntryForImages> GetAllEntriesForEditing() {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT * FROM EditingDictionaryEntriesForFoto";

                var reader = cmd.ExecuteReader();
                var entries = new List<DictionaryEntryForImages>();

                while (reader.Read()) {
                    var entry = new DictionaryEntryForImages {
                        PageId = reader.GetInt32(reader.GetOrdinal("PageId")),
                        EntryKey = reader.GetString(reader.GetOrdinal("EntryKey")),
                        ImageData = reader.IsDBNull(reader.GetOrdinal("Image")) ? null : (byte[])reader["Image"],
                        ImageName = reader.IsDBNull(reader.GetOrdinal("ImageName")) ? null : reader.GetString(reader.GetOrdinal("ImageName")) 
                    };
                    entries.Add(entry);
                }

                return entries;
            }
        }

        public static void DeleteImageById(int id) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM EditingDictionaryEntriesForFoto WHERE Id = @id";
                Db.SetParam(cmd, "@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public static void DeleteEntryFromMainTable(int id) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM DictionaryEntriesImages WHERE Id = @id";
                Db.SetParam(cmd, "@id", id);
                cmd.ExecuteNonQuery();
            }
        }
    }
}