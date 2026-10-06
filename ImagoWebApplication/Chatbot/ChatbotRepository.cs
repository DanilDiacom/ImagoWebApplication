using System.Data;
using System.Data.SqlClient;
using System.Text.Json;
using ImagoLib.Models;

namespace ImagoWebApplication.Chatbot {

    /// <summary>
    /// Хранение данных чат-бота в базе imagodt (скрипт: Chatbot/Sql/create_chatbot_tables.sql).
    /// </summary>
    public static class ChatbotRepository {

        private const string Columns =
            "[id],[visitor_id],[date_create],[date_update],[is_closed],[language],[page_url],[summary_ru]," +
            "[history_summary],[summarized_count],[messages],[user_messages],[customer_name],[customer_contact]," +
            "[escalated_count],[escalated_at],[date_escalated]";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private static SqlCommand Command(IDbConnection db, string sql) {
            var cmd = (SqlCommand)db.CreateCommand();
            cmd.CommandText = sql;
            return cmd;
        }

        #region Переписки ([chatbot_conversation])

        /// <summary>Последняя открытая переписка посетителя, обновлённая не раньше since.</summary>
        public static ChatConversation? FindLatest(Guid visitorId, DateTime since) {
            using (var db = Db.Get()) {
                var cmd = Command(db, $"SELECT TOP 1 {Columns} FROM [chatbot_conversation] " +
                                      "WHERE [is_closed]=0 AND [date_update]>=@since AND [visitor_id]=@vId ORDER BY [date_update] DESC");
                cmd.Parameters.AddWithValue("@since", since);
                cmd.Parameters.AddWithValue("@vId", visitorId);
                using (var dr = cmd.ExecuteReader()) {
                    return dr.Read() ? FromDataReader(dr) : null;
                }
            }
        }

        public static ChatConversation? GetConversation(Guid id) {
            using (var db = Db.Get()) {
                var cmd = Command(db, $"SELECT {Columns} FROM [chatbot_conversation] WHERE [id]=@id");
                cmd.Parameters.AddWithValue("@id", id);
                using (var dr = cmd.ExecuteReader()) {
                    return dr.Read() ? FromDataReader(dr) : null;
                }
            }
        }

        public static void Insert(ChatConversation c) {
            using (var db = Db.Get()) {
                var cmd = Command(db, $"INSERT INTO [chatbot_conversation] ({Columns}) VALUES " +
                                      "(@id,@vId,@dCreate,@dUpdate,@closed,@lang,@page,@summary,@hSummary,@sCount,@messages,@uMessages,@cName,@cContact,@eCount,@eAt,@dEsc)");
                AddParameters(cmd, c);
                cmd.ExecuteNonQuery();
            }
        }

        public static void Update(ChatConversation c) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "UPDATE [chatbot_conversation] SET [visitor_id]=@vId, [date_update]=@dUpdate, [is_closed]=@closed, " +
                                      "[language]=@lang, [page_url]=@page, [summary_ru]=@summary, [history_summary]=@hSummary, [summarized_count]=@sCount, " +
                                      "[messages]=@messages, [user_messages]=@uMessages, [customer_name]=@cName, [customer_contact]=@cContact, " +
                                      "[escalated_count]=@eCount, [escalated_at]=@eAt, [date_escalated]=@dEsc WHERE [id]=@id");
                AddParameters(cmd, c);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>«Новый диалог»: закрыть все открытые переписки посетителя.</summary>
        public static void CloseAll(Guid visitorId) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "UPDATE [chatbot_conversation] SET [is_closed]=1 WHERE [is_closed]=0 AND [visitor_id]=@vId");
                cmd.Parameters.AddWithValue("@vId", visitorId);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Удаляет переписки без сообщений дольше срока хранения. Возвращает число удалённых.</summary>
        public static int DeleteOlderThan(DateTime date) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "DELETE FROM [chatbot_conversation] WHERE [date_update]<@date");
                cmd.Parameters.AddWithValue("@date", date);
                return cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region Заявки в Telegram ([chatbot_escalation])

        public static int InsertEscalation(ChatEscalation e) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "INSERT INTO [chatbot_escalation] ([conversation_id],[visitor_id],[date_create],[language],[customer_name],[customer_contact],[question_ru],[question_text]) " +
                                      "OUTPUT INSERTED.[id] VALUES (@cId,@vId,@date,@lang,@name,@contact,@qRu,@qText)");
                cmd.Parameters.AddWithValue("@cId", e.ConversationId);
                cmd.Parameters.AddWithValue("@vId", e.VisitorId);
                cmd.Parameters.AddWithValue("@date", e.DateCreate);
                cmd.Parameters.AddWithValue("@lang", Nullable(e.Language, 10));
                cmd.Parameters.AddWithValue("@name", Nullable(e.CustomerName, 100));
                cmd.Parameters.AddWithValue("@contact", Nullable(e.CustomerContact, 100));
                cmd.Parameters.AddWithValue("@qRu", Nullable(e.QuestionRu, 1000));
                cmd.Parameters.AddWithValue("@qText", Nullable(e.QuestionText, int.MaxValue));
                return (int)cmd.ExecuteScalar();
            }
        }

        /// <summary>Антиспам: новые вопросы клиента дописываются в уже открытую заявку.</summary>
        public static void AppendEscalationQuestion(int id, string questionText, string? questionRu) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "UPDATE [chatbot_escalation] SET " +
                                      "[question_text]=CASE WHEN [question_text] IS NULL THEN @q ELSE [question_text]+CHAR(10)+@q END, " +
                                      "[question_ru]=COALESCE(@qRu, [question_ru]) WHERE [id]=@id");
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@q", questionText);
                cmd.Parameters.AddWithValue("@qRu", Nullable(questionRu, 1000));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Сколько заявок по переписке отправлено в Telegram начиная с since.</summary>
        public static int CountEscalationsSince(Guid conversationId, DateTime since) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "SELECT COUNT(*) FROM [chatbot_escalation] WHERE [conversation_id]=@cId AND [date_create]>=@since");
                cmd.Parameters.AddWithValue("@cId", conversationId);
                cmd.Parameters.AddWithValue("@since", since);
                return (int)cmd.ExecuteScalar();
            }
        }

        public static int? GetLastEscalationId(Guid conversationId) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "SELECT MAX([id]) FROM [chatbot_escalation] WHERE [conversation_id]=@cId");
                cmd.Parameters.AddWithValue("@cId", conversationId);
                var r = cmd.ExecuteScalar();
                return r == null || r == DBNull.Value ? null : (int)r;
            }
        }

        public static ChatEscalation? GetEscalation(int id) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "SELECT [id],[conversation_id],[visitor_id],[date_create],[language],[customer_name],[customer_contact],[question_ru],[question_text]," +
                                      "[answer_original],[answer_sent],[answered_by],[date_answered],[knowledge_id] FROM [chatbot_escalation] WHERE [id]=@id");
                cmd.Parameters.AddWithValue("@id", id);
                using (var dr = cmd.ExecuteReader()) {
                    if (!dr.Read()) return null;
                    return new ChatEscalation {
                        Id = dr.GetInt32(0),
                        ConversationId = dr.GetGuid(1),
                        VisitorId = dr.GetGuid(2),
                        DateCreate = dr.GetDateTime(3),
                        Language = dr.IsDBNull(4) ? "" : dr.GetString(4),
                        CustomerName = dr.IsDBNull(5) ? "" : dr.GetString(5),
                        CustomerContact = dr.IsDBNull(6) ? "" : dr.GetString(6),
                        QuestionRu = dr.IsDBNull(7) ? "" : dr.GetString(7),
                        QuestionText = dr.IsDBNull(8) ? "" : dr.GetString(8),
                        AnswerOriginal = dr.IsDBNull(9) ? "" : dr.GetString(9),
                        AnswerSent = dr.IsDBNull(10) ? "" : dr.GetString(10),
                        AnsweredBy = dr.IsDBNull(11) ? "" : dr.GetString(11),
                        DateAnswered = dr.IsDBNull(12) ? null : dr.GetDateTime(12),
                        KnowledgeId = dr.IsDBNull(13) ? null : dr.GetInt32(13),
                    };
                }
            }
        }

        /// <summary>Сохраняет ответ. Если отвечают несколько раз — ответы дописываются.</summary>
        public static void SetEscalationAnswer(int id, string original, string sent, string answeredBy) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "UPDATE [chatbot_escalation] SET " +
                                      "[answer_original]=CASE WHEN [answer_original] IS NULL THEN @orig ELSE [answer_original]+CHAR(10)+CHAR(10)+@orig END, " +
                                      "[answer_sent]=CASE WHEN [answer_sent] IS NULL THEN @sent ELSE [answer_sent]+CHAR(10)+CHAR(10)+@sent END, " +
                                      "[answered_by]=@by, [date_answered]=@date WHERE [id]=@id");
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@orig", original);
                cmd.Parameters.AddWithValue("@sent", sent);
                cmd.Parameters.AddWithValue("@by", Nullable(answeredBy, 100));
                cmd.Parameters.AddWithValue("@date", DateTime.Now);
                cmd.ExecuteNonQuery();
            }
        }

        public static void SetEscalationKnowledge(int id, int? knowledgeId) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "UPDATE [chatbot_escalation] SET [knowledge_id]=@kId WHERE [id]=@id");
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@kId", knowledgeId.HasValue ? knowledgeId.Value : (object)DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region База знаний из ответов ([chatbot_knowledge])

        public static int InsertKnowledge(string question, string answer, int? escalationId, string createdBy) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "INSERT INTO [chatbot_knowledge] ([question],[answer],[escalation_id],[created_by],[date_create],[is_active]) " +
                                      "OUTPUT INSERTED.[id] VALUES (@q,@a,@eId,@by,@date,1)");
                cmd.Parameters.AddWithValue("@q", question);
                cmd.Parameters.AddWithValue("@a", answer);
                cmd.Parameters.AddWithValue("@eId", escalationId.HasValue ? escalationId.Value : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@by", Nullable(createdBy, 100));
                cmd.Parameters.AddWithValue("@date", DateTime.Now);
                return (int)cmd.ExecuteScalar();
            }
        }

        public static void DeactivateKnowledge(int id) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "UPDATE [chatbot_knowledge] SET [is_active]=0 WHERE [id]=@id");
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Активные записи, новые первыми.</summary>
        public static List<(int Id, string Question, string Answer, DateTime Date)> GetActiveKnowledge(int limit) {
            var list = new List<(int, string, string, DateTime)>();
            using (var db = Db.Get()) {
                var cmd = Command(db, $"SELECT TOP {limit} [id],[question],[answer],[date_create] FROM [chatbot_knowledge] WHERE [is_active]=1 ORDER BY [date_create] DESC");
                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) list.Add((dr.GetInt32(0), dr.GetString(1), dr.GetString(2), dr.GetDateTime(3)));
                }
            }
            return list;
        }

        #endregion

        #region Telegram: подписчики и обработанные обновления

        public static List<TelegramSubscriber> GetSubscribers() {
            var list = new List<TelegramSubscriber>();
            using (var db = Db.Get()) {
                var cmd = Command(db, "SELECT [user_id],[user_name],[chat_id] FROM [chatbot_telegram_subscriber]");
                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        list.Add(new TelegramSubscriber {
                            UserId = dr.GetInt64(0),
                            UserName = dr.IsDBNull(1) ? "" : dr.GetString(1),
                            ChatId = dr.GetInt64(2),
                        });
                    }
                }
            }
            return list;
        }

        public static void AddSubscriber(long userId, string? userName, string? userLang, long chatId) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "IF EXISTS (SELECT 1 FROM [chatbot_telegram_subscriber] WHERE [user_id]=@uId) " +
                                      "UPDATE [chatbot_telegram_subscriber] SET [user_name]=@uName, [user_lang]=@uLang, [chat_id]=@cId WHERE [user_id]=@uId " +
                                      "ELSE INSERT INTO [chatbot_telegram_subscriber] ([user_id],[user_name],[user_lang],[chat_id],[date_create]) VALUES (@uId,@uName,@uLang,@cId,@date)");
                cmd.Parameters.AddWithValue("@uId", userId);
                cmd.Parameters.AddWithValue("@uName", Nullable(userName, 100));
                cmd.Parameters.AddWithValue("@uLang", Nullable(userLang, 10));
                cmd.Parameters.AddWithValue("@cId", chatId);
                cmd.Parameters.AddWithValue("@date", DateTime.Now);
                cmd.ExecuteNonQuery();
            }
        }

        public static void RemoveSubscriber(long userId) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "DELETE FROM [chatbot_telegram_subscriber] WHERE [user_id]=@uId");
                cmd.Parameters.AddWithValue("@uId", userId);
                cmd.ExecuteNonQuery();
            }
        }

        public static int? GetLastUpdateId() {
            using (var db = Db.Get()) {
                var r = Command(db, "SELECT MAX([id]) FROM [chatbot_telegram_update]").ExecuteScalar();
                return r == null || r == DBNull.Value ? null : (int)r;
            }
        }

        /// <summary>Отмечает обновление как обработанное. false — оно уже было обработано раньше.</summary>
        public static bool TryStoreUpdate(int id, string text) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "IF NOT EXISTS (SELECT 1 FROM [chatbot_telegram_update] WHERE [id]=@id) " +
                                      "INSERT INTO [chatbot_telegram_update] ([id],[date_create],[text]) VALUES (@id,@date,@text)");
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@date", DateTime.Now);
                cmd.Parameters.AddWithValue("@text", Nullable(text, 500));
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        /// <summary>Старые обработанные обновления не копим (последнее оставляем — от него считается offset).</summary>
        public static void DeleteUpdatesOlderThan(DateTime date) {
            using (var db = Db.Get()) {
                var cmd = Command(db, "DELETE FROM [chatbot_telegram_update] WHERE [date_create]<@date AND [id]<(SELECT MAX([id]) FROM [chatbot_telegram_update])");
                cmd.Parameters.AddWithValue("@date", date);
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        private static void AddParameters(SqlCommand cmd, ChatConversation c) {
            cmd.Parameters.AddWithValue("@id", c.Id);
            cmd.Parameters.AddWithValue("@vId", c.VisitorId);
            cmd.Parameters.AddWithValue("@dCreate", c.DateCreate);
            cmd.Parameters.AddWithValue("@dUpdate", c.DateUpdate);
            cmd.Parameters.AddWithValue("@closed", c.IsClosed);
            cmd.Parameters.AddWithValue("@lang", Nullable(c.Language, 10));
            cmd.Parameters.AddWithValue("@page", Nullable(c.PageUrl, 300));
            cmd.Parameters.AddWithValue("@summary", Nullable(c.SummaryRu, 1000));
            cmd.Parameters.AddWithValue("@hSummary", Nullable(c.HistorySummary, int.MaxValue));
            cmd.Parameters.AddWithValue("@sCount", c.SummarizedCount);
            cmd.Parameters.AddWithValue("@messages", JsonSerializer.Serialize(c.Turns, JsonOptions));
            cmd.Parameters.AddWithValue("@uMessages", c.UserMessages);
            cmd.Parameters.AddWithValue("@cName", Nullable(c.CustomerName, 100));
            cmd.Parameters.AddWithValue("@cContact", Nullable(c.CustomerContact, 100));
            cmd.Parameters.AddWithValue("@eCount", c.EscalatedCount);
            cmd.Parameters.AddWithValue("@eAt", c.EscalatedAt);
            cmd.Parameters.AddWithValue("@dEsc", c.DateEscalated.HasValue ? c.DateEscalated.Value : (object)DBNull.Value);
        }

        private static object Nullable(string? value, int maxLength) {
            if (string.IsNullOrEmpty(value)) return DBNull.Value;
            return value.Length > maxLength ? value.Substring(0, maxLength) : value;
        }

        private static ChatConversation FromDataReader(IDataReader dr) {
            var c = new ChatConversation {
                Id = dr.GetGuid(0),
                VisitorId = dr.GetGuid(1),
                DateCreate = dr.GetDateTime(2),
                DateUpdate = dr.GetDateTime(3),
                IsClosed = dr.GetBoolean(4),
                Language = dr.IsDBNull(5) ? "" : dr.GetString(5),
                PageUrl = dr.IsDBNull(6) ? "" : dr.GetString(6),
                SummaryRu = dr.IsDBNull(7) ? "" : dr.GetString(7),
                HistorySummary = dr.IsDBNull(8) ? "" : dr.GetString(8),
                SummarizedCount = dr.GetInt32(9),
                UserMessages = dr.GetInt32(11),
                CustomerName = dr.IsDBNull(12) ? "" : dr.GetString(12),
                CustomerContact = dr.IsDBNull(13) ? "" : dr.GetString(13),
                EscalatedCount = dr.GetInt32(14),
                EscalatedAt = dr.GetInt32(15),
                DateEscalated = dr.IsDBNull(16) ? null : dr.GetDateTime(16),
            };
            try {
                var turns = JsonSerializer.Deserialize<List<ChatTurn>>(dr.GetString(10), JsonOptions);
                if (turns != null) c.Turns.AddRange(turns);
            }
            catch (JsonException) { }
            return c;
        }
    }

    public class TelegramSubscriber {
        public long UserId { get; set; }
        public string UserName { get; set; } = "";
        public long ChatId { get; set; }
    }
}
