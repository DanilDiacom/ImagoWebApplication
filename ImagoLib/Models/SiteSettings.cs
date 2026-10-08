using System.Security.Cryptography;

namespace ImagoLib.Models {

    /// <summary>Настройки сайта в таблице [SiteSettings] (скрипт: ImagoWebApplication/Sql/admin_drafts.sql).</summary>
    public static class SiteSettings {

        private const string PreviewTokenKey = "PreviewToken";

        /// <summary>
        /// Ключ предпросмотра черновика: сайт показывает черновик (неопубликованные тексты, фото и стили) только по адресу с ?nahled=&lt;ключ&gt;.
        /// Ключ знают только админка и сайт (оба читают его из базы); при первом обращении создаётся случайный.
        /// </summary>
        public static string GetPreviewToken() {
            var token = Get(PreviewTokenKey);
            if (!string.IsNullOrEmpty(token)) return token;

            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "IF NOT EXISTS (SELECT 1 FROM SiteSettings WHERE [Key] = @key) INSERT INTO SiteSettings ([Key], [Value]) VALUES (@key, @value)";
                Db.SetParam(cmd, "@key", PreviewTokenKey);
                Db.SetParam(cmd, "@value", token);
                cmd.ExecuteNonQuery();
            }
            return Get(PreviewTokenKey) ?? token;   // если ключ одновременно создала другая копия — берём записанный
        }

        public static string? Get(string key) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT [Value] FROM SiteSettings WHERE [Key] = @key";
                Db.SetParam(cmd, "@key", key);
                var r = cmd.ExecuteScalar();
                return r == null || r == DBNull.Value ? null : (string)r;
            }
        }
    }
}
