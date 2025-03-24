using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ImagoLib.Models {
    public class TextStyle {
        public int Id { get; set; }
        public string EntryKey { get; set; }
        public string FontFamily { get; set; }
        public string FontSize { get; set; }
        public string FontWeight { get; set; }
        public string FontStyle { get; set; }
        public string TextDecoration { get; set; }
        public string TextColor { get; set; }  // Новый параметр
        public string TextAlignment { get; set; }  // Новый параметр

        private static TextStyle FromDataReader(IDataReader dr) {
            return new TextStyle {
                Id = dr.GetInt32(0),
                EntryKey = dr.GetString(1),
                FontFamily = dr.IsDBNull(2) ? null : dr.GetString(2),
                FontSize = dr.IsDBNull(3) ? null : dr.GetString(3),
                FontWeight = dr.IsDBNull(4) ? null : dr.GetString(4),
                FontStyle = dr.IsDBNull(5) ? null : dr.GetString(5),
                TextDecoration = dr.IsDBNull(6) ? null : dr.GetString(6),
            };
        }

        public static async Task SaveTextStyleAsync(TextStyle style) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = @"
            IF EXISTS (SELECT 1 FROM TextStyles WHERE EntryKey = @EntryKey)
                UPDATE TextStyles 
                SET FontFamily = @FontFamily, 
                    FontSize = @FontSize, 
                    FontWeight = @FontWeight, 
                    FontStyle = @FontStyle, 
                    TextDecoration = @TextDecoration WHERE EntryKey = @EntryKey
            ELSE
                INSERT INTO TextStyles (EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration)
                VALUES (@EntryKey, @FontFamily, @FontSize, @FontWeight, @FontStyle, @TextDecoration)";

                Db.SetParam(cmd, "@EntryKey", style.EntryKey);
                Db.SetParam(cmd, "@FontFamily", style.FontFamily);
                Db.SetParam(cmd, "@FontSize", style.FontSize);
                Db.SetParam(cmd, "@FontWeight", style.FontWeight);
                Db.SetParam(cmd, "@FontStyle", style.FontStyle);
                Db.SetParam(cmd, "@TextDecoration", style.TextDecoration);

                cmd.ExecuteNonQuery(); // Используем асинхронный метод
            }
        }

        public static TextStyle GetTextStyle(string entryKey) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT FontFamily, FontSize, FontWeight, FontStyle, TextDecoration FROM TextStyles WHERE EntryKey = @EntryKey";
                Db.SetParam(cmd, "@EntryKey", entryKey);

                using (var reader = cmd.ExecuteReader()) {
                    if (reader.Read()) {
                        return new TextStyle {
                            EntryKey = entryKey,
                            FontFamily = reader["FontFamily"]?.ToString(),
                            FontSize = reader["FontSize"]?.ToString(),
                            FontWeight = reader["FontWeight"]?.ToString(),
                            FontStyle = reader["FontStyle"]?.ToString(),
                            TextDecoration = reader["TextDecoration"]?.ToString()
                        };
                    }
                }
            }

            return null; // Если стили не найдены
        }

        public static List<TextStyle> GetAllStyles() {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration FROM TextStyles";
                var styles = new List<TextStyle>();

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        styles.Add(new TextStyle {
                            EntryKey = dr.GetString(0),
                            FontFamily = dr.IsDBNull(1) ? null : dr.GetString(1),
                            FontSize = dr.IsDBNull(2) ? null : dr.GetString(2),
                            FontWeight = dr.IsDBNull(3) ? null : dr.GetString(3),
                            FontStyle = dr.IsDBNull(4) ? null : dr.GetString(4),
                            TextDecoration = dr.IsDBNull(5) ? null : dr.GetString(5),
                        });
                    }
                }

                return styles;
            }
        }

        public static void DeleteTextStyle(string entryKey) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM TextStyles WHERE EntryKey = @EntryKey";
                Db.SetParam(cmd, "@EntryKey", entryKey);

                cmd.ExecuteNonQuery();
            }
        }
    }
}
