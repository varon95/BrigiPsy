using Microsoft.AspNetCore.Mvc;
using BrigiPsy.Models;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;

namespace BrigiPsy.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View(new ContactFormModel());
        }

        public IActionResult DataUsage()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Contact(ContactFormModel model)
        {
            if (ModelState.IsValid)
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("BrigiPsy Website", "no-reply@brigipsy.hu"));
                message.To.Add(new MailboxAddress("Brigitta", "borbasbrege@gmail.com"));
                message.Subject = $"időpontkérés - {model.Name}";

                message.Body = new TextPart("plain")
                {
                    Text = $"Név: {model.Name}\nEmail: {model.Email}\nÜzenet:\n{model.Message}"
                };

                using (var client = new SmtpClient())
                {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    await client.ConnectAsync("smtp.gmail.com", 587, false);
                    await client.AuthenticateAsync("borbasbrege@gmail.com", "kJkcal8966091");
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                TempData["Success"] = "Üzenetét sikeresen elküldtük.";
                return RedirectToAction("Index");
            }
            else
            {
                return View(model);
            }
        }
    }
}
