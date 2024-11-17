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
                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress("BrigiPsy Website", "no-reply@brigipsy.hu"));
                    message.To.Add(new MailboxAddress("Brigitta", "borbasbrege@gmail.com"));
                    message.Subject = $"időpontkérés - {model.Name}";

                    message.Body = new TextPart("plain")
                    {
                        Text = $"Név: {model.Name}\nEmail: {model.Email}\nÜzenet:\n{model.Üzenet}"
                    };

                    using (var client = new SmtpClient())
                    {
                        client.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                        client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                        // Try connecting to the SMTP server
                        try
                        {
                            await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                        }
                        catch (Exception ex)
                        {
                            // Log the exception details for connection failure
                            Console.WriteLine("Failed to connect to the SMTP server.");
                            Console.WriteLine(ex.ToString());
                            throw; // Re-throw the exception to be caught by the outer catch block
                        }

                        // Check if the client is connected
                        if (!client.IsConnected)
                        {
                            Console.WriteLine("Client is not connected after ConnectAsync call.");
                            throw new Exception("SMTP client failed to connect.");
                        }

                        // Try authenticating with the SMTP server
                        try
                        {
                            await client.AuthenticateAsync("borbasbrege@gmail.com", "cauu ctus ijcm xlcs");
                        }
                        catch (Exception ex)
                        {
                            // Log the exception details for authentication failure
                            Console.WriteLine("Failed to authenticate with the SMTP server.");
                            Console.WriteLine(ex.ToString());
                            throw; // Re-throw the exception to be caught by the outer catch block
                        }

                        // Send the email
                        await client.SendAsync(message);
                        await client.DisconnectAsync(true);
                    }

                    TempData["Success"] = "Üzenetét sikeresen elküldtük.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    // Log the exception details
                    Console.WriteLine("An error occurred while sending the email.");
                    Console.WriteLine(ex.ToString());

                    // Optionally, you can display the error message to the user
                    ModelState.AddModelError("", "Hiba történt az üzenet küldése során. Kérjük, próbálja meg később.");

                    // Return the view with the model to display validation errors
                    return View("Index", model);
                }
            }

            // If we got this far, something failed; redisplay form
            return View("Index", model);
        }
    }
}
