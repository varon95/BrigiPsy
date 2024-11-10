// Models/ContactFormModel.cs
using System.ComponentModel.DataAnnotations;

namespace BrigiPsy.Models
{
    public class ContactFormModel
    {
        [Required(ErrorMessage = "A név megadása kötelező.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Az email megadása kötelező.")]
        [EmailAddress(ErrorMessage = "Érvényes email címet adjon meg.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Az üzenet megadása kötelező.")]
        public string Message { get; set; }

        [Required(ErrorMessage = "Az adatkezelési nyilatkozat elfogadása kötelező.")]
        [Display(Name = "Adatkezelési nyilatkozat")]
        public bool AcceptPrivacyPolicy { get; set; }
    }
}
