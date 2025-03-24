using Microsoft.AspNetCore.Mvc;
using MimeKit;
using MailKit.Net.Smtp;
using System.Threading.Tasks;

namespace ImagoWebApplication.Controllers {
    public class ContactController : Controller {
        [HttpPost]
        public async Task<IActionResult> SendEmail(string name, string email, string subject, string text) {
            try {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("imagodt.cz", "info@imagodt.cz"));
                message.To.Add(new MailboxAddress(name, email));
                message.Subject = subject ?? "Zpráva z kontaktního formuláře";

                message.Body = new TextPart("plain") {
                    Text = $"Kontaktní osoba: {name}\nE-mailová adresa: {email}\nPředmět: {subject}\nZpráva: {text}"
                };

                using (var client = new SmtpClient()) {
                    await client.ConnectAsync("smtp.forpsi.com", 587, false);
                    await client.AuthenticateAsync("info@imagodt.cz", "#Saragosa2025");
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                return Content("true"); 
            }
            catch (Exception ex) {
                return Content("false");
            }
        }
    }
}