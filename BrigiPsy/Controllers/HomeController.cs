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
        private readonly IConfiguration _configuration;

        public HomeController(
            IHttpClientFactory httpClientFactory,
            ILogger<HomeController> logger,
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _environment = environment;
            _configuration = configuration;
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

            if (!model.AcceptPrivacyPolicy)
            {
                ModelState.AddModelError(
                    nameof(ContactFormModel.AcceptPrivacyPolicy),
                    "Az adatkezelési nyilatkozat elfogadása kötelező.");
            }

            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            if (!await IsReCaptchaValid(recaptchaResponse))
            {
                ModelState.AddModelError("", "A reCAPTCHA ellenőrzése sikertelen. Kérjük, próbálja újra.");
                return View("Index", model);
            }

            var smtpHost = _configuration["Smtp:Host"];
            var smtpUsername = _configuration["Smtp:Username"];
            var smtpPassword = _configuration["Smtp:Password"];
            var fromAddress = _configuration["Smtp:FromAddress"];
            var fromName = _configuration["Smtp:FromName"];
            var toAddress = _configuration["Smtp:ToAddress"];
            var toName = _configuration["Smtp:ToName"];

            if (string.IsNullOrWhiteSpace(smtpHost) ||
                string.IsNullOrWhiteSpace(smtpUsername) ||
                string.IsNullOrWhiteSpace(smtpPassword) ||
                string.IsNullOrWhiteSpace(fromAddress) ||
                string.IsNullOrWhiteSpace(toAddress))
            {
                _logger.LogError("SMTP configuration is incomplete. Ensure Smtp__Password is configured on the server.");
                ModelState.AddModelError("", "Hiba történt az üzenet küldése során. Kérjük, próbálja meg később.");
                return View("Index", model);
            }

            var smtpPort = 587;
            if (int.TryParse(_configuration["Smtp:Port"], out var configuredPort))
            {
                smtpPort = configuredPort;
            }

            try
            {
                var safeName = model.Name.Replace('\r', ' ').Replace('\n', ' ').Trim();

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName ?? string.Empty, fromAddress));
                message.To.Add(new MailboxAddress(toName ?? string.Empty, toAddress));
                message.Subject = $"időpontkérés - {safeName}";
                message.Body = new TextPart("plain")
                {
                    Text = $"Név: {model.Name}\nEmail: {model.Email}\nÜzenet:\n{model.Üzenet}"
                };

                using var client = new SmtpClient();
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(smtpUsername, smtpPassword);
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

            var secretKey = _configuration["Recaptcha:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                _logger.LogError("reCAPTCHA configuration is incomplete. Ensure Recaptcha__SecretKey is configured on the server.");
                return false;
            }

            try
            {
                var client = _httpClientFactory.CreateClient("recaptcha");
                using var response = await client.PostAsync(
                    "https://www.google.com/recaptcha/api/siteverify",
                    new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("secret", secretKey),
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
