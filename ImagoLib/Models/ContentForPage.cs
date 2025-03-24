using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ImagoLib.Models {
    public class ContentForPage {
        public int Id { get; set; }
        public int PageId { get; set; }
        public string ContentText { get; set; }

        private static ContentForPage FromDataReader(IDataReader dr) {
            return new ContentForPage {
                Id = dr.GetInt32(0),
                PageId = dr.GetInt32(1),
                ContentText = dr.GetString(2)
            };
        }
        public static void SaveText(ContentForPage content) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "IF EXISTS (SELECT 1 FROM PageContent WHERE pageId = @pageId) " +
                                  "UPDATE PageContent SET contentText = @contentText WHERE pageId = @pageId " +
                                  "ELSE " +
                                  "INSERT INTO PageContent (pageId, contentText) VALUES (@pageId, @contentText)";

                Db.SetParam(cmd, "@pageId", content.PageId);
                Db.SetParam(cmd, "@contentText", content.ContentText);

                cmd.ExecuteNonQuery();
            }
        }


        public static ContentForPage GetPageContent(int pageId) {
            using (var db = Db.Get()) // подключение к базе
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT id, pageId, contentText FROM PageContent WHERE pageId = @pageId";
                Db.SetParam(cmd, "@pageId", pageId);
                using (var dr = cmd.ExecuteReader()) {
                    if (dr.Read()) {
                        return FromDataReader(dr);
                    }
                }
            }
            return null;
        }

        public static void SaveContent(int pageId, string contentText) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "IF EXISTS (SELECT 1 FROM PageContent WHERE pageId = @pageId) " +
                                  "UPDATE PageContent SET contentText = @contentText WHERE pageId = @pageId " +
                                  "ELSE " +
                                  "INSERT INTO PageContent (pageId, contentText) VALUES (@pageId, @contentText)";
                Db.SetParam(cmd, "@pageId", pageId);
                Db.SetParam(cmd, "@contentText", contentText);

                cmd.ExecuteNonQuery();
            }
        }
    }
}
