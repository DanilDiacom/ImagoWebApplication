using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ImagoLib.Models {
    /// <summary>
    /// Стиль текста (шрифт, размер, цвет…) по ключу текста. Опубликованные стили — TextStyles,
    /// черновик админки — EditingTextStyles (на сайт попадает только по «Publikovat», см. PageDraft).
    /// </summary>
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

        internal static string Table(bool draft) => draft ? "EditingTextStyles" : "TextStyles";

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
            };
        }

        public static void SaveTextStyleAsync(TextStyle style) => SaveTextStyle(style, draft: false);

        public static void SaveTextStyle(TextStyle style, bool draft) {
            var table = Table(draft);
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = $@"
                IF EXISTS (SELECT 1 FROM {table} WHERE EntryKey = @EntryKey)
                    UPDATE {table}
                    SET FontFamily = @FontFamily,
                        FontSize = @FontSize,
                        FontWeight = @FontWeight,
                        FontStyle = @FontStyle,
                        TextDecoration = @TextDecoration,
                        TextColor = @TextColor
                    WHERE EntryKey = @EntryKey
                ELSE
                    INSERT INTO {table} (EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration, TextColor)
                    VALUES (@EntryKey, @FontFamily, @FontSize, @FontWeight, @FontStyle, @TextDecoration, @TextColor)";

                Db.SetParam(cmd, "@EntryKey", style.EntryKey);
                Db.SetParam(cmd, "@FontFamily", style.FontFamily);
                Db.SetParam(cmd, "@FontSize", style.FontSize);
                Db.SetParam(cmd, "@FontWeight", style.FontWeight);
                Db.SetParam(cmd, "@FontStyle", style.FontStyle);
                Db.SetParam(cmd, "@TextDecoration", style.TextDecoration);
                Db.SetParam(cmd, "@TextColor", style.TextColor);

                cmd.ExecuteNonQuery();
            }
        }

        public static TextStyle GetTextStyle(string entryKey) => GetTextStyle(entryKey, draft: false);

        public static TextStyle GetTextStyle(string entryKey, bool draft) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = $@"
            SELECT FontFamily, FontSize, FontWeight, FontStyle, TextDecoration, TextColor
            FROM {Table(draft)}
            WHERE EntryKey = @EntryKey";
                Db.SetParam(cmd, "@EntryKey", entryKey);

                using (var reader = cmd.ExecuteReader()) {
                    if (reader.Read()) {
                        return new TextStyle {
                            EntryKey = entryKey,
                            FontFamily = reader.IsDBNull(0) ? null : reader.GetString(0),
                            FontSize = reader.IsDBNull(1) ? null : reader.GetString(1),
                            FontWeight = reader.IsDBNull(2) ? null : reader.GetString(2),
                            FontStyle = reader.IsDBNull(3) ? null : reader.GetString(3),
                            TextDecoration = reader.IsDBNull(4) ? null : reader.GetString(4),
                            TextColor = reader.IsDBNull(5) ? null : reader.GetString(5)
                        };
                    }
                }
            }
            return null;
        }

        public static List<TextStyle> GetAllStyles() => GetAllStyles(draft: false);

        public static List<TextStyle> GetAllStyles(bool draft) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = $"SELECT EntryKey, FontFamily, FontSize, FontWeight, FontStyle, TextDecoration,TextColor FROM {Table(draft)}";
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
                        });
                    }
                }

                // если по ошибке у ключа несколько строк — берём одну, чтобы ToDictionary на сайте не падал
                return styles.GroupBy(s => s.EntryKey).Select(g => g.First()).ToList();
            }
        }

        public static void DeleteTextStyle(string entryKey) => DeleteTextStyle(entryKey, draft: false);

        /// <summary>Убирает стиль текста — на сайте текст снова в оформлении по умолчанию («Výchozí styl»).</summary>
        public static void DeleteTextStyle(string entryKey, bool draft) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = $"DELETE FROM {Table(draft)} WHERE EntryKey = @EntryKey";
                Db.SetParam(cmd, "@EntryKey", entryKey);

                cmd.ExecuteNonQuery();
            }
        }
    }
}
