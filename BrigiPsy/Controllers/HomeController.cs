using Microsoft.AspNetCore.Mvc;
using BrigiPsy.Models;
using MailKit.Net.Smtp;
using MimeKit;
using MailKit.Security;
using System.Security.Authentication;

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
                    // Create the email message
                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress("BrigiPsy Website", "postmaster@borbasbrigitta.com"));
                    message.To.Add(new MailboxAddress("Brigitta", "info@borbasbrigitta.com")); // Replace with your actual email address
                    message.Subject = $"időpontkérés - {model.Name}";

                    message.Body = new TextPart("plain")
                    {
                        Text = $"Név: {model.Name}\nEmail: {model.Email}\nÜzenet:\n{model.Üzenet}"
                    };

                    // Send the email using the SMTP server provided by your hosting provider
                    using (var client = new SmtpClient())
                    {
                        // Accept all SSL certificates (in case the server uses self-signed certificates)
                        client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                        // Connect to the SMTP server
                        await client.ConnectAsync("smtp.forpsi.com", 587, SecureSocketOptions.StartTls);

                        // Authenticate with the SMTP server
                        await client.AuthenticateAsync("postmaster@borbasbrigitta.com", "4Tpu2T-DR3"); // Replace with your actual password

                        // Send the email
                        await client.SendAsync(message);

                        // Disconnect from the SMTP server
                        await client.DisconnectAsync(true);
                    }

                    TempData["Success"] = "Üzenetét sikeresen elküldtük.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    // Optionally, log the exception details for debugging
                    // For now, we display a generic error message to the user
                    ModelState.AddModelError("", "Hiba történt az üzenet küldése során. Kérjük, próbálja meg később.");
                }
            }

            // If we got this far, something failed; redisplay form
            return View("Index", model);
        }
    }
}
