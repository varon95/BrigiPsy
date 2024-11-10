using Microsoft.AspNetCore.Mvc;
using BrigiPsy.Models;
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactFormModel model)
        {
            if (ModelState.IsValid)
            {
                try
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
                        await client.AuthenticateAsync("borbasbrege@gmail.com", "cauu ctus ijcm xlcs");
                        await client.SendAsync(message);
                        await client.DisconnectAsync(true);
                    }

                    TempData["Success"] = "Üzenetét sikeresen elküldtük.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    // Log the exception details (optional)
                    // ModelState.AddModelError(string.Empty, ex.Message);
                    ModelState.AddModelError("", "Hiba történt az üzenet küldése során. Kérjük, próbálja meg később.");
                }
            }

            // If we got this far, something failed; redisplay form
            return View("Index", model);
        }
    }
}
