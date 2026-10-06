using ImagoWebApplication.Chatbot;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ImagoWebApplication.Controllers {

    // Чат-бот сайта (виджет в правом нижнем углу, Views/Shared/_ChatWidget.cshtml)
    public class ChatbotController : Controller {

        // Посетителя узнаём по cookie
        private const string VisitorCookie = "imago_chat_vid";

        private readonly IConfiguration _configuration;
        private readonly ChatbotService _service;

        public ChatbotController(IConfiguration configuration, IMemoryCache cache, IWebHostEnvironment env, ILogger<ChatbotController> logger) {
            _configuration = configuration;
            _service = new ChatbotService(configuration, cache, env, logger);
        }

        public class MessageRequest {
            public string? Message { get; set; }
            public string? Page { get; set; }
        }

        public class ContactRequest {
            public string? Name { get; set; }
            public string? Contact { get; set; }
            public string? Page { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Message([FromBody] MessageRequest request) {
            if (!ChatbotService.IsEnabled(_configuration)) return NotFound();
            if (!IsSameOrigin()) return StatusCode(403);
            if (request == null) return BadRequest();

            var text = (request.Message ?? "").Trim();
            if (text.Length == 0) return BadRequest();
            if (text.Length > ChatbotService.MaxMessageLength) text = text.Substring(0, ChatbotService.MaxMessageLength);

            var conversation = _service.GetCurrent(VisitorId());

            if (_service.IsRateLimited(ClientIp())) {
                return Json(new ChatReply {
                    ConversationId = conversation.Id,
                    Reply = ChatbotTexts.Get("Chatbot_RateLimited"),
                    Error = true,
                    HasContact = conversation.HasContact
                });
            }

            var reply = await _service.SendAsync(conversation, text, TrimPage(request.Page));
            return Json(reply);
        }

        [HttpPost]
        public IActionResult Contact([FromBody] ContactRequest request) {
            if (!ChatbotService.IsEnabled(_configuration)) return NotFound();
            if (!IsSameOrigin()) return StatusCode(403);
            if (request == null) return BadRequest();

            var conversation = _service.GetCurrent(VisitorId());

            if (!ChatbotService.IsValidName(request.Name) || !ChatbotService.IsValidContact(request.Contact)) {
                return Json(new ChatReply { ConversationId = conversation.Id, Reply = ChatbotTexts.Get("Chatbot_ContactInvalid"), AskContact = true, Error = true });
            }

            conversation.CustomerName = request.Name!.Trim();
            conversation.CustomerContact = request.Contact!.Trim();
            if (!string.IsNullOrEmpty(request.Page)) conversation.PageUrl = TrimPage(request.Page);

            var ok = _service.Escalate(conversation);
            _service.Save(conversation);

            return Json(new ChatReply {
                ConversationId = conversation.Id,
                Reply = ChatbotTexts.Get(ok ? "Chatbot_ContactThanks" : "Chatbot_ServiceError"),
                Escalated = ok,
                HasContact = conversation.HasContact,
                WaitingReply = ChatbotService.IsWaitingReply(conversation),
                Error = !ok
            });
        }

        // Кнопка «Předat dotaz»: контакты известны — все накопленные вопросы уходят в Telegram одной заявкой
        [HttpPost]
        public IActionResult Send() {
            if (!ChatbotService.IsEnabled(_configuration)) return NotFound();
            if (!IsSameOrigin()) return StatusCode(403);

            var conversation = _service.GetCurrent(VisitorId());
            string T(string key) => ChatbotTexts.Get(key);

            if (!conversation.HasContact) {
                return Json(new ChatReply { ConversationId = conversation.Id, Reply = T("Chatbot_ContactTitle"), AskContact = true });
            }
            if (!ChatbotService.HasPendingQuestions(conversation)) {
                return Json(new ChatReply { ConversationId = conversation.Id, Reply = T("Chatbot_NothingToSend"), HasContact = true,
                    WaitingReply = ChatbotService.IsWaitingReply(conversation) });
            }

            var ok = _service.SendPending(conversation);
            return Json(new ChatReply {
                ConversationId = conversation.Id,
                Reply = T(ok ? "Chatbot_ContactThanks" : "Chatbot_ServiceError"),
                Escalated = ok,
                HasContact = true,
                AskConfirm = !ok,
                WaitingReply = ChatbotService.IsWaitingReply(conversation),
                Error = !ok
            });
        }

        // Переписка за последние дни — показывается, когда посетитель возвращается на сайт
        [HttpGet]
        public IActionResult History() {
            if (!ChatbotService.IsEnabled(_configuration)) return NotFound();
            var conversation = _service.GetCurrent(VisitorId());
            return Json(_service.GetHistory(conversation));
        }

        // «Nový chat»: бот забывает прошлую переписку
        [HttpPost]
        public IActionResult New() {
            if (!ChatbotService.IsEnabled(_configuration)) return NotFound();
            if (!IsSameOrigin()) return StatusCode(403);
            _service.StartNew(VisitorId());
            return Json(new { ok = true });
        }

        private Guid VisitorId() {
            if (!Guid.TryParse(Request.Cookies[VisitorCookie], out var id)) id = Guid.NewGuid();
            // cookie продлевается при каждом обращении
            Response.Cookies.Append(VisitorCookie, id.ToString(), new CookieOptions {
                Expires = DateTimeOffset.Now.AddDays(30),
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });
            return id;
        }

        // Запросы только со страниц нашего сайта
        private bool IsSameOrigin() {
            var origin = Request.Headers["Origin"].FirstOrDefault();
            if (string.IsNullOrEmpty(origin)) return true;
            return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                   && string.Equals(uri.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase);
        }

        private string ClientIp() {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private static string TrimPage(string? page) {
            page ??= "";
            return page.Length > 300 ? page.Substring(0, 300) : page;
        }
    }
}
