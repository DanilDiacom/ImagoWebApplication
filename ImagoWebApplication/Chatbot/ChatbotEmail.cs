using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ImagoWebApplication.Chatbot {

    /// <summary>
    /// Письмо клиенту с ответом менеджера (тот же SMTP, что у формы контактов).
    /// Настройки "Email:*" (Host, Port, User, From, FromName), пароль "Email:Password" — в User Secrets / на сервере, не в коде.
    /// </summary>
    public static class ChatbotEmail {

        private static IConfiguration? _configuration;

        public static void Configure(IConfiguration configuration) {
            _configuration = configuration;
        }

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration?["Email:Password"]);

        public static async Task SendAsync(string to, string toName, string subject, string html) {
            if (_configuration == null || !IsConfigured) throw new InvalidOperationException("E-mail není nastaven: chybí Email:Password");

            var host = _configuration["Email:Host"] ?? "smtp.forpsi.com";
            var port = int.TryParse(_configuration["Email:Port"], out var p) ? p : 587;
            var user = _configuration["Email:User"] ?? "info@imagodt.cz";
            var from = _configuration["Email:From"] ?? user;
            var fromName = _configuration["Email:FromName"] ?? "IMAGO D&T, s.r.o.";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, from));
            message.To.Add(new MailboxAddress(toName, to));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

            using var client = new SmtpClient();
            client.Timeout = 30000;
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTlsWhenAvailable);
            await client.AuthenticateAsync(user, _configuration["Email:Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
