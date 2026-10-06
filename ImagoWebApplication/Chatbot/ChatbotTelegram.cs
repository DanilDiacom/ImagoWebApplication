using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ImagoWebApplication.Chatbot {

    /// <summary>
    /// Связь чат-бота с Telegram (свой бот сайта imagodt.cz, токен — "Telegram:BotToken"):
    ///  - менеджер пишет боту /start — становится подписчиком (/end — отписка);
    ///  - заявка приходит подписчикам с кнопкой «Ответить клиенту»;
    ///  - кнопка -> бот просит ответить на его сообщение; ответ переводится на чешский и уходит в чат на сайте и на e-mail клиента;
    ///  - затем кнопками: «Добавить в базу знаний» (бот начинает так отвечать всем) или «Только этому клиенту».
    /// Обновления Telegram забирает TelegramPollingService (каждые несколько секунд).
    /// Нажимать кнопки и отвечать могут только подписчики бота.
    /// </summary>
    public static class ChatbotTelegram {

        private const string CbReply = "rep:";
        private const string CbLearn = "learn:";
        private const string CbNoLearn = "nolearn:";
        private const string CbUnlearn = "unlearn:";

        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);
        private static int _lastUpdateId;
        private static string? _token;

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(_token);

        public static void Configure(IConfiguration configuration) {
            var token = configuration["Telegram:BotToken"];
            _token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        }

        private static TelegramBotClient CreateClient() {
            if (!IsConfigured) throw new InvalidOperationException("Telegram bot není nastaven: chybí Telegram:BotToken");
            return new TelegramBotClient(_token!);
        }

        #region Отправка заявки

        // Отправленные сообщения заявок (заявка -> текст и сообщения у подписчиков) — чтобы дописывать дополнения
        // редактированием, без новых уведомлений. После перезапуска сайта дополнения видны при нажатии «Ответить».
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, (string Text, List<(long Chat, int Message)> Messages)> SentEscalations =
            new System.Collections.Concurrent.ConcurrentDictionary<int, (string, List<(long, int)>)>();

        private static InlineKeyboardMarkup ReplyKeyboard(int escalationId) {
            return new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("✍️ Odpovědět zákazníkovi", CbReply + escalationId));
        }

        public static void SendEscalation(string text, int escalationId, ILogger logger) {
            var client = CreateClient();
            var keyboard = ReplyKeyboard(escalationId);
            var messages = new List<(long, int)>();
            foreach (var subscriber in ChatbotRepository.GetSubscribers()) {
                try {
                    var m = client.SendTextMessageAsync(new ChatId(subscriber.ChatId), text, replyMarkup: keyboard).GetAwaiter().GetResult();
                    messages.Add((subscriber.ChatId, m.MessageId));
                }
                catch (Exception ex) {
                    logger.LogWarning(ex, "Chatbot: не удалось отправить заявку #{Id} подписчику {Chat}", escalationId, subscriber.ChatId);
                }
            }
            if (messages.Count == 0) throw new Exception("Požadavek nebyl doručen žádnému odběrateli v Telegramu (odběratelé napíšou botovi /start)");

            SentEscalations[escalationId] = (text, messages);
            // старые записи не копим
            foreach (var old in SentEscalations.Keys.Where(k => k < escalationId - 500).ToList()) SentEscalations.TryRemove(old, out _);
        }

        /// <summary>Сообщение всем подписчикам без кнопок (заявка, когда база недоступна).</summary>
        public static void Broadcast(string text, ILogger logger) {
            var client = CreateClient();
            var sent = 0;
            foreach (var subscriber in ChatbotRepository.GetSubscribers()) {
                try {
                    client.SendTextMessageAsync(new ChatId(subscriber.ChatId), Limit(text)).GetAwaiter().GetResult();
                    sent++;
                }
                catch (Exception ex) {
                    logger.LogWarning(ex, "Chatbot: не удалось отправить сообщение подписчику {Chat}", subscriber.ChatId);
                }
            }
            if (sent == 0) throw new Exception("Zpráva nebyla doručena žádnému odběrateli v Telegramu");
        }

        /// <summary>
        /// Антиспам: новый вопрос клиента дописывается в уже отправленное сообщение заявки (редактирование — без уведомления).
        /// </summary>
        public static void AppendToEscalation(int escalationId, string addition, ILogger logger) {
            if (!SentEscalations.TryGetValue(escalationId, out var sent)) {
                // Исходное сообщение не отредактировать (сайт перезапускался) — отправляем ОДНО сообщение-дополнение,
                // следующие дополнения будут дописываться уже в него
                var escalation = ChatbotRepository.GetEscalation(escalationId);
                var head = $"➕ Nové dotazy k požadavku #{escalationId}" + (escalation != null && !string.IsNullOrEmpty(escalation.CustomerName) ? $" ({escalation.CustomerName}, {escalation.CustomerContact})" : "") + ":";
                var first = head + "\n• " + addition.Replace("\n", "\n• ");
                try {
                    SendEscalation(Limit(first, 3500), escalationId, logger);
                    logger.LogInformation("Chatbot: заявка #{Id} — отправлено сообщение-дополнение (исходное сообщение недоступно для редактирования)", escalationId);
                }
                catch (Exception ex) {
                    logger.LogError(ex, "Chatbot: не удалось отправить дополнение к заявке #{Id}", escalationId);
                }
                return;
            }
            var text = sent.Text + $"\n\n➕ Doplnění ({DateTime.Now:dd.MM HH:mm}):\n• " + addition.Replace("\n", "\n• ");
            if (text.Length > 4000) text = sent.Text + "\n\n➕ Jsou tu nové dotazy — klikněte na „Odpovědět zákazníkovi“ a uvidíte všechny.";
            SentEscalations[escalationId] = (text, sent.Messages);

            var client = CreateClient();
            foreach (var (chat, message) in sent.Messages) {
                try {
                    client.EditMessageTextAsync(new ChatId(chat), message, Limit(text), replyMarkup: ReplyKeyboard(escalationId)).GetAwaiter().GetResult();
                }
                catch (Exception ex) {
                    logger.LogWarning("Chatbot: не удалось дописать заявку #{Id} у подписчика {Chat}: {Error}", escalationId, chat, ex.Message);
                }
            }
        }

        #endregion

        #region Приём обновлений

        /// <summary>Забирает новые обновления у Telegram и обрабатывает их (подписка, кнопки, ответы).</summary>
        public static async Task ProcessUpdatesAsync(ChatbotService service, ILogger logger, CancellationToken ct = default) {
            if (!await Lock.WaitAsync(0, ct)) return;   // уже идёт обработка
            try {
                var client = CreateClient();
                if (_lastUpdateId == 0) _lastUpdateId = ChatbotRepository.GetLastUpdateId() ?? 0;

                foreach (var update in await client.GetUpdatesAsync(_lastUpdateId + 1, 50, 0, cancellationToken: ct)) {
                    _lastUpdateId = Math.Max(_lastUpdateId, update.Id);
                    try {
                        if (!Store(update)) continue;   // уже обработано

                        if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null) {
                            await HandleCallbackAsync(client, service, update.CallbackQuery, logger);
                        }
                        else if (update.Type == UpdateType.Message && update.Message != null) {
                            await HandleMessageAsync(client, service, update.Message, logger);
                        }
                    }
                    catch (Exception ex) {
                        logger.LogError(ex, "Chatbot: ошибка обработки обновления Telegram {Id}", update.Id);
                    }
                }

                CleanupUpdates();
            }
            finally {
                Lock.Release();
            }
        }

        /// <summary>Сохраняет обновление в [chatbot_telegram_update] — по нему считается, что уже обработано.</summary>
        private static bool Store(Update update) {
            var text = update.Message != null ? (update.Message.Text ?? "[není text]")
                     : update.CallbackQuery != null ? "callback:" + update.CallbackQuery.Data
                     : "[" + update.Type + "]";
            return ChatbotRepository.TryStoreUpdate(update.Id, text);
        }

        private static DateTime _lastCleanup = DateTime.MinValue;

        private static void CleanupUpdates() {
            if (DateTime.Now - _lastCleanup < TimeSpan.FromHours(6)) return;
            _lastCleanup = DateTime.Now;
            ChatbotRepository.DeleteUpdatesOlderThan(DateTime.Now.AddDays(-30));
        }

        private static bool IsSubscriber(long chatId) {
            return ChatbotRepository.GetSubscribers().Any(s => s.ChatId == chatId);
        }

        private static string Who(User? user) {
            if (user == null) return "";
            return !string.IsNullOrEmpty(user.Username) ? "@" + user.Username : $"{user.FirstName} {user.LastName}".Trim();
        }

        #endregion

        #region Сообщения

        private static async Task HandleMessageAsync(TelegramBotClient client, ChatbotService service, Message message, ILogger logger) {
            var chatId = message.Chat.Id;
            var text = message.Text?.Trim() ?? "";

            // Подписка на заявки
            var cmd = text.ToLower();
            if (cmd == "/start") {
                ChatbotRepository.AddSubscriber(message.From?.Id ?? chatId, message.From?.Username, message.From?.LanguageCode, chatId);
                await client.SendTextMessageAsync(new ChatId(chatId), "Odběr je aktivní: sem budou chodit dotazy zákazníků z chatu na webu imagodt.cz. Odhlásit se můžete příkazem /end");
                logger.LogInformation("Chatbot: новый подписчик Telegram {Who}", Who(message.From));
                return;
            }
            if (cmd == "/end") {
                ChatbotRepository.RemoveSubscriber(message.From?.Id ?? chatId);
                await client.SendTextMessageAsync(new ChatId(chatId), "Odběr byl pozastaven");
                return;
            }

            // Ответ клиенту: ответ (Reply) на сообщение бота с номером заявки
            var replyTo = message.ReplyToMessage;
            if (replyTo?.From == null || !replyTo.From.IsBot) return;
            var match = Regex.Match(replyTo.Text ?? "", @"(?:požadav\w*|заявк\w*)\s*#(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success) return;

            if (!IsSubscriber(chatId)) {
                await client.SendTextMessageAsync(new ChatId(chatId), "Odpovídat zákazníkům mohou jen odběratelé bota (/start).");
                return;
            }
            if (string.IsNullOrWhiteSpace(text)) {
                await client.SendTextMessageAsync(new ChatId(chatId), "Pošlete odpověď jako text (fotky a soubory se zákazníkovi nepředávají).", replyToMessageId: message.MessageId);
                return;
            }

            await SendAnswerAsync(client, service, int.Parse(match.Groups[1].Value), text, Who(message.From), chatId, message.MessageId, logger);
        }

        public class AnswerResult {
            public ChatEscalation? Escalation { get; set; }
            public string Translated { get; set; } = "";
            public string? Language { get; set; }
            public bool ToChat { get; set; }
            public string? Email { get; set; }        // ответ ушёл на этот адрес
            public string? EmailError { get; set; }   // письмо не отправилось
        }

        /// <summary>Доставка ответа менеджера клиенту: перевод (по умолчанию на чешский), в чат на сайте и на e-mail, если клиент оставил почту.</summary>
        public static async Task<AnswerResult> DeliverAnswerAsync(ChatbotService service, int escalationId, string answer, string answeredBy, ILogger logger) {
            var result = new AnswerResult();
            var escalation = ChatbotRepository.GetEscalation(escalationId);
            if (escalation == null) return result;
            result.Escalation = escalation;

            var conversation = service.GetById(escalation.ConversationId, escalation.VisitorId);
            var language = !string.IsNullOrEmpty(escalation.Language) ? escalation.Language : conversation?.Language;
            var translated = await service.TranslateForCustomerAsync(answer, language);

            var toChat = false;
            if (conversation != null) {
                service.AddStaffAnswer(conversation, translated);
                toChat = true;
            }

            // На e-mail, если клиент оставил почту
            if (IsEmail(escalation.CustomerContact)) {
                var email = escalation.CustomerContact.Trim();
                try {
                    await ChatbotEmail.SendAsync(email, escalation.CustomerName, ChatbotTexts.Get("Chatbot_EmailSubject"), BuildEmail(translated, escalation));
                    result.Email = email;
                }
                catch (Exception ex) {
                    logger.LogError(ex, "Chatbot: nepodařilo se odeslat e-mail zákazníkovi (požadavek #{Id})", escalationId);
                    result.EmailError = ex.Message;
                }
            }

            ChatbotRepository.SetEscalationAnswer(escalationId, answer, translated, answeredBy);
            logger.LogInformation("Chatbot: ответ на заявку #{Id} от {By}: чат={Chat}, e-mail={Email}", escalationId, answeredBy, toChat, result.Email);

            result.Translated = translated;
            result.Language = language;
            result.ToChat = toChat;
            return result;
        }

        private static string BuildEmail(string answer, ChatEscalation escalation) {
            static string Html(string text) => System.Net.WebUtility.HtmlEncode(text).Replace("\n", "<br/>");
            string T(string key) => Html(ChatbotTexts.Get(key));

            var sb = new StringBuilder();
            sb.Append("<div style=\"font-family:Arial,sans-serif;font-size:15px;color:#1b2027\">");
            sb.Append($"<p>{T("Chatbot_EmailGreeting")}{(string.IsNullOrWhiteSpace(escalation.CustomerName) ? "" : " " + Html(escalation.CustomerName))},</p>");
            sb.Append($"<p>{Html(answer)}</p>");
            if (!string.IsNullOrWhiteSpace(escalation.QuestionText)) {
                sb.Append($"<hr style=\"border:none;border-top:1px solid #dbe3f0\"/><p style=\"color:#6c757d\"><i>{T("Chatbot_EmailYourQuestion")}:</i><br/>{Html(escalation.QuestionText)}</p>");
            }
            sb.Append($"<p>{T("Chatbot_EmailSignature")}<br/><b style=\"color:#1d4f99\">IMAGO D&amp;T, s.r.o.</b><br/>Mandysova 1410/26, 500 12 Hradec Králové<br/>" +
                      "+420 602 411 872 · <a href=\"mailto:imagodt@imagodt.cz\" style=\"color:#265fbf\">imagodt@imagodt.cz</a> · <a href=\"https://imagodt.cz\" style=\"color:#265fbf\">imagodt.cz</a></p>");
            sb.Append("</div>");
            return sb.ToString();
        }

        private static async Task SendAnswerAsync(TelegramBotClient client, ChatbotService service, int escalationId, string answer, string answeredBy,
            long chatId, int replyToMessageId, ILogger logger) {

            var delivered = await DeliverAnswerAsync(service, escalationId, answer, answeredBy, logger);
            var escalation = delivered.Escalation;
            if (escalation == null) {
                await client.SendTextMessageAsync(new ChatId(chatId), $"Požadavek #{escalationId} nebyl nalezen.", replyToMessageId: replyToMessageId);
                return;
            }

            var where = new List<string>();
            if (delivered.ToChat) where.Add("do chatu na webu");
            if (delivered.Email != null) where.Add("na e-mail " + delivered.Email);

            var sb = new StringBuilder();
            if (where.Count == 0) {
                sb.AppendLine($"⚠️ Požadavek #{escalationId}: odpověď se nepodařilo doručit (konverzace už byla smazána a e-mail se neodeslal). Kontaktujte zákazníka přímo.");
            }
            else {
                sb.AppendLine($"✅ Odpověď na požadavek #{escalationId} byla odeslána {string.Join(" a ", where)}.");
            }
            if (delivered.EmailError != null) {
                sb.AppendLine($"⚠️ E-mail zákazníkovi se nepodařilo odeslat: {delivered.EmailError}");
            }
            sb.AppendLine($"Kontakt zákazníka: {escalation.CustomerName}, {escalation.CustomerContact}");
            if (!string.Equals(delivered.Translated.Trim(), answer.Trim(), StringComparison.Ordinal)) {
                sb.AppendLine();
                sb.AppendLine("Zákazník dostal (přeloženo):");
                sb.AppendLine(delivered.Translated);
            }
            sb.AppendLine();
            sb.AppendLine("Přidat tuto odpověď do znalostní báze bota? Bot pak bude sám takto odpovídat na podobné dotazy.");

            var keyboard = new InlineKeyboardMarkup(new[] {
                InlineKeyboardButton.WithCallbackData("📚 Přidat do znalostní báze", CbLearn + escalationId),
                InlineKeyboardButton.WithCallbackData("🚫 Jen pro tohoto zákazníka", CbNoLearn + escalationId),
            });
            await client.SendTextMessageAsync(new ChatId(chatId), Limit(sb.ToString()), replyToMessageId: replyToMessageId, replyMarkup: keyboard);

            // Остальным подписчикам — чтобы не отвечали на ту же заявку второй раз
            foreach (var s in ChatbotRepository.GetSubscribers().Where(s => s.ChatId != chatId)) {
                try { await client.SendTextMessageAsync(new ChatId(s.ChatId), $"ℹ️ Na požadavek #{escalationId} odpověděl(a) {answeredBy}:\n{Limit(answer, 1000)}"); }
                catch { }
            }
        }

        #endregion

        #region Кнопки

        private static async Task HandleCallbackAsync(TelegramBotClient client, ChatbotService service, CallbackQuery callback, ILogger logger) {
            var chatId = callback.Message?.Chat.Id ?? callback.From.Id;
            var data = callback.Data ?? "";

            if (!IsSubscriber(chatId)) {
                await SafeAnswerAsync(client, callback.Id, "Bez přístupu: jen pro odběratele bota");
                return;
            }

            if (TryId(data, CbReply, out var replyId)) {
                await SafeAnswerAsync(client, callback.Id);
                var escalation = ChatbotRepository.GetEscalation(replyId);
                if (escalation == null) {
                    await client.SendTextMessageAsync(new ChatId(chatId), $"Požadavek #{replyId} nebyl nalezen.");
                    return;
                }
                var sb = new StringBuilder();
                if (escalation.DateAnswered.HasValue) {
                    sb.AppendLine($"ℹ️ Na tento požadavek už odpověděl(a) {escalation.AnsweredBy} ({escalation.DateAnswered:dd.MM HH:mm}). Nová odpověď bude zákazníkovi poslána navíc.");
                    sb.AppendLine();
                }
                sb.AppendLine($"✍️ Odpověď na požadavek #{replyId} — {escalation.CustomerName}, kontakt: {escalation.CustomerContact}.");
                if (!string.IsNullOrWhiteSpace(escalation.QuestionText)) {
                    sb.AppendLine();
                    sb.AppendLine("Dotazy zákazníka:");
                    foreach (var q in escalation.QuestionText.Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(10)) {
                        sb.AppendLine("• " + (q.Length > 300 ? q.Substring(0, 300) + "…" : q));
                    }
                    sb.AppendLine();
                }
                sb.AppendLine("Napište odpověď jako odpověď (Reply) na tuto zprávu. Zákazník ji dostane česky do chatu na webu" +
                              (IsEmail(escalation.CustomerContact) ? " a na e-mail." : "."));
                await client.SendTextMessageAsync(new ChatId(chatId), sb.ToString(), replyMarkup: new ForceReplyMarkup());
                return;
            }

            if (TryId(data, CbLearn, out var learnId)) {
                await SafeAnswerAsync(client, callback.Id, "Přidávám do znalostní báze…");
                await RemoveButtons(client, callback);
                await LearnAsync(client, service, learnId, Who(callback.From), chatId, logger);
                return;
            }

            if (TryId(data, CbNoLearn, out var noLearnId)) {
                await SafeAnswerAsync(client, callback.Id, "OK");
                await RemoveButtons(client, callback);
                await client.SendTextMessageAsync(new ChatId(chatId), $"Odpověď na požadavek #{noLearnId} zůstala jen pro tohoto zákazníka, do znalostní báze nebyla přidána.");
                return;
            }

            if (TryId(data, CbUnlearn, out var knowledgeId)) {
                ChatbotRepository.DeactivateKnowledge(knowledgeId);
                ChatbotService.InvalidateLearnedKnowledge();
                await SafeAnswerAsync(client, callback.Id, "Smazáno");
                await RemoveButtons(client, callback);
                await client.SendTextMessageAsync(new ChatId(chatId), $"🗑 Záznam #{knowledgeId} byl smazán ze znalostní báze bota.");
                logger.LogInformation("Chatbot: запись базы знаний #{Id} удалена ({By})", knowledgeId, Who(callback.From));
                return;
            }

            await SafeAnswerAsync(client, callback.Id);
        }

        /// <summary>Добавляет ответ по заявке в базу знаний (общая запись без личных данных).</summary>
        public static async Task<(int Id, string Question, string Answer)?> CreateKnowledgeAsync(ChatbotService service, int escalationId, string by, ILogger logger) {
            var escalation = ChatbotRepository.GetEscalation(escalationId);
            if (escalation == null || string.IsNullOrWhiteSpace(escalation.AnswerOriginal) || escalation.KnowledgeId.HasValue) return null;

            var question = !string.IsNullOrWhiteSpace(escalation.QuestionText) ? escalation.QuestionText : escalation.QuestionRu;
            var entry = await service.MakeKnowledgeEntryAsync(question, escalation.AnswerOriginal)
                        ?? (escalation.QuestionRu.Length > 0 ? escalation.QuestionRu : question, escalation.AnswerOriginal);

            var knowledgeId = ChatbotRepository.InsertKnowledge(entry.Question, entry.Answer, escalationId, by);
            ChatbotRepository.SetEscalationKnowledge(escalationId, knowledgeId);
            ChatbotService.InvalidateLearnedKnowledge();
            logger.LogInformation("Chatbot: ответ по заявке #{Esc} добавлен в базу знаний (#{Id}, {By})", escalationId, knowledgeId, by);
            return (knowledgeId, entry.Question, entry.Answer);
        }

        private static async Task LearnAsync(TelegramBotClient client, ChatbotService service, int escalationId, string by, long chatId, ILogger logger) {
            var escalation = ChatbotRepository.GetEscalation(escalationId);
            if (escalation == null || string.IsNullOrWhiteSpace(escalation.AnswerOriginal)) {
                await client.SendTextMessageAsync(new ChatId(chatId), $"Na požadavek #{escalationId} zatím není odpověď — nejdřív odpovězte zákazníkovi.");
                return;
            }
            if (escalation.KnowledgeId.HasValue) {
                await client.SendTextMessageAsync(new ChatId(chatId), $"Odpověď na požadavek #{escalationId} už je ve znalostní bázi (záznam #{escalation.KnowledgeId}).");
                return;
            }

            var created = await CreateKnowledgeAsync(service, escalationId, by, logger);
            if (created == null) {
                await client.SendTextMessageAsync(new ChatId(chatId), $"Odpověď na požadavek #{escalationId} se nepodařilo přidat do znalostní báze.");
                return;
            }

            var text = $"📚 Přidáno do znalostní báze bota (záznam #{created.Value.Id}). Bot ho už používá v odpovědích.\n\n" +
                       $"Dotaz: {created.Value.Question}\nOdpověď: {created.Value.Answer}";
            var keyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("🗑 Smazat ze znalostní báze", CbUnlearn + created.Value.Id));
            await client.SendTextMessageAsync(new ChatId(chatId), Limit(text), replyMarkup: keyboard);
        }

        /// <summary>
        /// Подтверждение нажатия кнопки. Если кнопку нажали, пока сайт был выключен, Telegram отвечает
        /// «query is too old» — это не мешает обработать само нажатие.
        /// </summary>
        private static async Task SafeAnswerAsync(TelegramBotClient client, string callbackId, string? text = null) {
            try { await client.AnswerCallbackQueryAsync(callbackId, text); }
            catch { }
        }

        private static async Task RemoveButtons(TelegramBotClient client, CallbackQuery callback) {
            if (callback.Message == null) return;
            try { await client.EditMessageReplyMarkupAsync(new ChatId(callback.Message.Chat.Id), callback.Message.MessageId, null); }
            catch { }
        }

        #endregion

        private static bool TryId(string data, string prefix, out int id) {
            id = 0;
            return data.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(data.Substring(prefix.Length), out id);
        }

        private static bool IsEmail(string? contact) {
            return !string.IsNullOrWhiteSpace(contact) && Regex.IsMatch(contact.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }

        private static string Limit(string text, int max = 4000) {
            return text.Length > max ? text.Substring(0, max) + "…" : text;
        }
    }
}
