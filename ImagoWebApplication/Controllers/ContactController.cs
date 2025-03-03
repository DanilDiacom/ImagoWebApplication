using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using MimeKit;
using MailKit.Net.Smtp;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace ImagoWebApplication.Controllers {
    public class ContactController : Controller {
        [HttpPost]
        public async Task<IActionResult> SendEmail(string name, string email, string subject, string text) {
           
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Webmaster", "danilaceredko@gmail.com"));
            message.To.Add(new MailboxAddress(name, email));
            message.Subject = subject;

            var body = new TextPart("plain") {
                Text = $"Kontaktní osoba: {name}\nE-mailová adresa: {email}\nPředmět: {subject}\nZpráva: {text}"
            };

            message.Body = body;

            using (var client = new SmtpClient()) {
                try {
                    await client.ConnectAsync("smtp.example.com", 587, false); // Замените на ваш SMTP сервер
                    await client.AuthenticateAsync("your-email@example.com", "your-password"); // Ваши email и пароль
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
                catch (Exception ex) {
                    Console.WriteLine(ex.Message);
                    return View("Error");
                }
            }

            return RedirectToAction("Confirmation");
        }

        public IActionResult Confirmation() {
            return View(); // Страница подтверждения отправки сообщения
        }
    }
}
