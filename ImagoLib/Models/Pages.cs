using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using Microsoft.Data.SqlClient;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ImagoLib.Models {
    public partial class Pages : ObservableObject {
        [ObservableProperty] public int m_Id;
        [ObservableProperty] public string m_Title;
        [ObservableProperty] public string m_Url;
        [ObservableProperty] public int? m_ParentId;
        /// <summary>Порядок в меню (среди страниц одного уровня).</summary>
        [ObservableProperty] public int m_SortOrder;
        /// <summary>Скрыта в меню сайта (по прямому адресу страница доступна).</summary>
        [ObservableProperty] public bool m_IsHidden;

        public ObservableCollection<Pages> SubPages { get; set; } = new ObservableCollection<Pages>();

        /// <summary>Неопубликованные изменения черновика страницы (ImagoAdmin показывает «● N» у страницы в дереве).</summary>
        [ObservableProperty] public int m_PendingChanges;

        public string Badge => PendingChanges > 0 ? $"  ● {PendingChanges}" : "";

        partial void OnPendingChangesChanged(int value) => OnPropertyChanged(nameof(Badge));

        private static Pages FromDataReader(IDataReader dr) {
            return new Pages() {
                Id = dr.GetInt32(0),
                Title = dr.GetString(1),
                Url = dr.GetString(2),
                ParentId = dr.IsDBNull(3) ? null : dr.GetInt32(3),
                SortOrder = dr.IsDBNull(4) ? int.MaxValue : dr.GetInt32(4),
                IsHidden = !dr.IsDBNull(5) && dr.GetBoolean(5),
            };
        }

        public static ObservableCollection<Pages> GetPagesHierarchy() {
            var allPages = new List<Pages>();

            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                // новые страницы (SortOrder ещё не задан) — в конец своего уровня
                cmd.CommandText = "SELECT id, title, url, parentId, SortOrder, IsHidden FROM Pages ORDER BY ISNULL(SortOrder, 2147483647), id";
                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        allPages.Add(FromDataReader(dr));
                    }
                }
            }

            // Построение иерархии
            var pageDictionary = allPages.ToDictionary(p => p.Id);
            foreach (var page in allPages) {
                if (page.ParentId.HasValue && pageDictionary.ContainsKey(page.ParentId.Value)) {
                    pageDictionary[page.ParentId.Value].SubPages.Add(page);
                }
            }

            return new ObservableCollection<Pages>(pageDictionary.Values.Where(p => !p.ParentId.HasValue));
        }

        /// <summary>
        /// Сохраняет меню из ImagoAdmin («☰ Menu»): название, порядок и скрытие страниц. Сразу видно на сайте.
        /// </summary>
        public static void SaveMenu(IEnumerable<Pages> pages) {
            using (var db = Db.Get())
            using (var tx = db.BeginTransaction()) {
                foreach (var p in pages) {
                    var cmd = db.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "UPDATE Pages SET title = @title, SortOrder = @sort, IsHidden = @hidden WHERE id = @id";
                    Db.SetParam(cmd, "@id", p.Id);
                    Db.SetParam(cmd, "@title", p.Title);
                    Db.SetParam(cmd, "@sort", p.SortOrder);
                    Db.SetParam(cmd, "@hidden", p.IsHidden);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
        }

        public static int InsertPageIfNotExists(Pages page) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = @"
        DECLARE @InsertedId INT;
        
        IF NOT EXISTS (
            SELECT 1 FROM Pages WHERE title = @title AND url = @url
        )
        BEGIN
            INSERT INTO Pages (title, url, parentId)
            VALUES (@title, @url, @parentId);
            
            SET @InsertedId = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            SELECT @InsertedId = Id FROM Pages WHERE title = @title AND url = @url;
        END
        
        SELECT @InsertedId AS InsertedId;";

                Db.SetParam(cmd, "@title", page.Title);
                Db.SetParam(cmd, "@url", page.Url);
                Db.SetParam(cmd, "@parentId", (object?)page.ParentId ?? DBNull.Value);

                // Выполняем запрос и получаем ID
                using (var reader = cmd.ExecuteReader()) {
                    if (reader.Read()) {
                        return reader.GetInt32(reader.GetOrdinal("InsertedId"));
                    }
                }

                throw new Exception("Не удалось получить ID созданной или существующей страницы");
            }
        }

        public static bool DeletePage(int pageId) {
            using (var db = Db.Get()) {
                var transaction = db.BeginTransaction();
                try {
                    // 4. Удаляем саму страницу
                    var cmdDeletePage = db.CreateCommand();
                    cmdDeletePage.CommandText = "DELETE FROM Pages WHERE Id = @pageId";
                    Db.SetParam(cmdDeletePage, "@pageId", pageId);
                    int affectedRows = cmdDeletePage.ExecuteNonQuery();

                    transaction.Commit();
                    return affectedRows > 0;
                }
                catch {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public static bool DeletePageWithDependencies(int pageId) {
            using (var db = Db.Get()) {
                var transaction = db.BeginTransaction();
                try {
                    // 1. Удаляем связанные текстовые записи
                    var cmdText = db.CreateCommand();
                    cmdText.CommandText = "DELETE FROM DictionaryEntries WHERE PageId = @pageId";
                    cmdText.Transaction = transaction;
                    Db.SetParam(cmdText, "@pageId", pageId);
                    cmdText.ExecuteNonQuery();

                    // 1a. И из черновика — иначе «Publikovat» вернул бы тексты удалённой страницы
                    var cmdEditingText = db.CreateCommand();
                    cmdEditingText.CommandText = "DELETE FROM EditingDictionaryEntries WHERE PageId = @pageId";
                    cmdEditingText.Transaction = transaction;
                    Db.SetParam(cmdEditingText, "@pageId", pageId);
                    cmdEditingText.ExecuteNonQuery();

                    // 2. Удаляем связанные изображения (основная таблица)
                    var cmdImages = db.CreateCommand();
                    cmdImages.CommandText = "DELETE FROM DictionaryEntriesImages WHERE PageId = @pageId";
                    cmdImages.Transaction = transaction;
                    Db.SetParam(cmdImages, "@pageId", pageId);
                    cmdImages.ExecuteNonQuery();

                    // 3. Удаляем записи из таблицы редактирования фото
                    var cmdEditingPhotos = db.CreateCommand();
                    cmdEditingPhotos.CommandText = "DELETE FROM EditingDictionaryEntriesForFoto WHERE PageId = @pageId";
                    cmdEditingPhotos.Transaction = transaction;
                    Db.SetParam(cmdEditingPhotos, "@pageId", pageId);
                    cmdEditingPhotos.ExecuteNonQuery();

                    // 4. Удаляем саму страницу
                    var cmdPage = db.CreateCommand();
                    cmdPage.CommandText = "DELETE FROM Pages WHERE Id = @pageId";
                    cmdPage.Transaction = transaction;
                    Db.SetParam(cmdPage, "@pageId", pageId);
                    int affectedRows = cmdPage.ExecuteNonQuery();

                    transaction.Commit();
                    return affectedRows > 0;
                }
                catch {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }
}