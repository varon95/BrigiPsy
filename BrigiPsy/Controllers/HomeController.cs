using System.Diagnostics;
using System.Text.Json;
using BrigiPsy.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MimeKit;

namespace BrigiPsy.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _environment;

        public HomeController(
            IHttpClientFactory httpClientFactory,
            ILogger<HomeController> logger,
            IWebHostEnvironment environment)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _environment = environment;
        }

        public IActionResult Index()
        {
            return View(new ContactFormModel());
        }

        public IActionResult DataUsage()
        {
            return View();
        }

        public IActionResult Translation()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("contact")]
        [RequestSizeLimit(32 * 1024)]
        public async Task<IActionResult> Contact(
            ContactFormModel model,
            [FromForm(Name = "g-recaptcha-response")] string recaptchaResponse)
        {
            if (!string.IsNullOrWhiteSpace(model.Website))
            {
                _logger.LogWarning("Contact form honeypot was triggered.");
                TempData["Success"] = "Üzenetét sikeresen elküldtük.";
                return RedirectToAction(nameof(Index));
            }

            if (!await IsReCaptchaValid(recaptchaResponse))
            {
                ModelState.AddModelError("", "A reCAPTCHA ellenőrzése sikertelen. Kérjük, próbálja újra.");
            }

            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            try
            {
                var safeName = model.Name.Replace('\r', ' ').Replace('\n', ' ').Trim();

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("BrigiPsy Website", "postmaster@borbasbrigitta.com"));
                message.To.Add(new MailboxAddress("Brigitta", "info@borbasbrigitta.com"));
                message.Subject = $"időpontkérés - {safeName}";
                message.Body = new TextPart("plain")
                {
                    Text = $"Név: {model.Name}\nEmail: {model.Email}\nÜzenet:\n{model.Üzenet}"
                };

                using var client = new SmtpClient();
                await client.ConnectAsync("smtp.forpsi.com", 587, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync("postmaster@borbasbrigitta.com", "4Tpu2T-DR3");
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                TempData["Success"] = "Üzenetét sikeresen elküldtük.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send contact form email.");
                ModelState.AddModelError("", "Hiba történt az üzenet küldése során. Kérjük, próbálja meg később.");
                return View("Index", model);
            }
        }

        private async Task<bool> IsReCaptchaValid(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            try
            {
                var client = _httpClientFactory.CreateClient("recaptcha");
                using var response = await client.PostAsync(
                    "https://www.google.com/recaptcha/api/siteverify",
                    new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("secret", "6LdM6q4qAAAAAIK379L4yyDM3Kn5RaTTmkc_P9zH"),
                        new KeyValuePair<string, string>("response", token)
                    }));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("reCAPTCHA verification service returned status code {StatusCode}.", response.StatusCode);
                    return false;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var jsonDocument = JsonDocument.Parse(jsonResponse);
                var root = jsonDocument.RootElement;

                if (!root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
                {
                    return false;
                }

                if (!root.TryGetProperty("hostname", out var hostnameProperty))
                {
                    return false;
                }

                var hostname = hostnameProperty.GetString();
                if (!IsAllowedRecaptchaHostname(hostname))
                {
                    _logger.LogWarning("reCAPTCHA hostname validation failed for {Hostname}.", hostname);
                    return false;
                }

                if (root.TryGetProperty("action", out var actionProperty))
                {
                    var action = actionProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(action) && !string.Equals(action, "submit", StringComparison.Ordinal))
                    {
                        _logger.LogWarning("reCAPTCHA action validation failed for {Action}.", action);
                        return false;
                    }
                }

                if (root.TryGetProperty("score", out var scoreProperty) &&
                    scoreProperty.TryGetDouble(out var score) &&
                    score < 0.5)
                {
                    _logger.LogWarning("reCAPTCHA score was below the accepted threshold.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "reCAPTCHA validation failed.");
                return false;
            }
        }

        private bool IsAllowedRecaptchaHostname(string? hostname)
        {
            if (string.Equals(hostname, "borbasbrigitta.com", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hostname, "www.borbasbrigitta.com", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return _environment.IsDevelopment() &&
                   (string.Equals(hostname, "localhost", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(hostname, "127.0.0.1", StringComparison.OrdinalIgnoreCase));
        }
    }
}
