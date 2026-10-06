using Microsoft.Extensions.Caching.Memory;

namespace ImagoWebApplication.Chatbot {

    /// <summary>
    /// Фоновая проверка обновлений Telegram каждые несколько секунд: подписка (/start, /end),
    /// кнопки «Ответить клиенту» / «Добавить в базу знаний» и ответы клиентам (Chatbot/ChatbotTelegram.cs).
    ///
    /// Работает, если задан "Telegram:BotToken" и "Telegram:ReceiveUpdates": true — на ОДНОМ сервере.
    /// Если обновления одного бота забирают две копии сайта (например, сервер и локальная), они делят сообщения между собой.
    /// В IIS для пула приложения нужно "Start mode: AlwaysRunning" (и "Preload enabled" у сайта), иначе после простоя проверка засыпает.
    /// </summary>
    public class TelegramPollingService : BackgroundService {

        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<TelegramPollingService> _logger;

        public TelegramPollingService(IConfiguration configuration, IMemoryCache cache, IWebHostEnvironment env, ILogger<TelegramPollingService> logger) {
            _configuration = configuration;
            _cache = cache;
            _env = env;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            if (!string.Equals(_configuration["Telegram:ReceiveUpdates"], "true", StringComparison.OrdinalIgnoreCase)) {
                _logger.LogInformation("Telegram: фоновая проверка выключена (Telegram:ReceiveUpdates != true)");
                return;
            }
            if (string.IsNullOrWhiteSpace(_configuration["Telegram:BotToken"])) {
                _logger.LogWarning("Telegram: не задан Telegram:BotToken — заявки из чата в Telegram не уходят, ответы не принимаются");
                return;
            }

            var interval = int.TryParse(_configuration["Telegram:PollSeconds"], out var s) && s > 0 ? s : 3;
            _logger.LogInformation("Telegram: фоновая проверка обновлений каждые {Seconds} с", interval);

            var service = new ChatbotService(_configuration, _cache, _env, _logger);
            while (!stoppingToken.IsCancellationRequested) {
                try {
                    await ChatbotTelegram.ProcessUpdatesAsync(service, _logger, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                    break;
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Telegram: ошибка фоновой проверки обновлений");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ContinueWith(_ => { });   // не спамим лог при сбое сети
                }
                await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken).ContinueWith(_ => { });
            }
        }
    }
}
