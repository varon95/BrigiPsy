using System.ComponentModel.DataAnnotations;

namespace BrigiPsy.Models
{
    public class ContactFormModel
    {
        [Required(ErrorMessage = "A név megadása kötelező.")]
        [StringLength(100, ErrorMessage = "A név legfeljebb 100 karakter lehet.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Az email megadása kötelező.")]
        [EmailAddress(ErrorMessage = "Érvényes email címet adjon meg.")]
        [StringLength(254, ErrorMessage = "Az email cím legfeljebb 254 karakter lehet.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Az üzenet megadása kötelező.")]
        [StringLength(4000, ErrorMessage = "Az üzenet legfeljebb 4000 karakter lehet.")]
        public string Üzenet { get; set; } = string.Empty;

        [Display(Name = "Adatkezelési nyilatkozat")]
        public bool AcceptPrivacyPolicy { get; set; }

        public string? Website { get; set; }
    }
}