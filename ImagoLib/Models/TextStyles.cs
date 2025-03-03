using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

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
                TextColor = dr.IsDBNull(7) ? null : dr.GetString(7),
                TextAlignment = dr.IsDBNull(8) ? null : dr.GetString(8)
            };
        }

        public static void SaveTextStyle(TextStyle style) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = @"
                    IF EXISTS (SELECT 1 FROM TextStyles WHERE EntryKey = @EntryKey)
                        UPDATE TextStyles 
                        SET FontFamily = @FontFamily, 
                            FontSize = @FontSize, 
                            FontWeight = @FontWeight, 
                            FontStyle = @FontStyle, 
                            TextDecoration = @TextDecoration,
                            TextColor = @TextColor, 
                            TextAlignment = @TextAlignment
                        WHERE EntryKey = @EntryKey
                    ELSE
                        INSERT INTO TextStyles (EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration, TextColor, TextAlignment)
                        VALUES (@EntryKey, @FontFamily, @FontSize, @FontWeight, @FontStyle, @TextDecoration, @TextColor, @TextAlignment)";

                Db.SetParam(cmd, "@EntryKey", style.EntryKey);
                Db.SetParam(cmd, "@FontFamily", style.FontFamily);
                Db.SetParam(cmd, "@FontSize", style.FontSize);
                Db.SetParam(cmd, "@FontWeight", style.FontWeight);
                Db.SetParam(cmd, "@FontStyle", style.FontStyle);
                Db.SetParam(cmd, "@TextDecoration", style.TextDecoration);
                Db.SetParam(cmd, "@TextColor", style.TextColor);
                Db.SetParam(cmd, "@TextAlignment", style.TextAlignment);

                cmd.ExecuteNonQuery();
            }
        }

        public static TextStyle GetTextStyle(string entryKey) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration, TextColor, TextAlignment FROM TextStyles WHERE EntryKey = @EntryKey";
                Db.SetParam(cmd, "@EntryKey", entryKey);

                using (var dr = cmd.ExecuteReader()) {
                    if (dr.Read()) {
                        return FromDataReader(dr);
                    }
                }
            }
            return null;
        }

        public static List<TextStyle> GetAllStyles() {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration, TextColor, TextAlignment FROM TextStyles";
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
                            TextColor = dr.IsDBNull(6) ? null : dr.GetString(6),
                            TextAlignment = dr.IsDBNull(7) ? null : dr.GetString(7)
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
