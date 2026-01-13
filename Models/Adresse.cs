using System.ComponentModel.DataAnnotations;

namespace RevendTout.Models
{
    public class Adresse
    {
        [Required(ErrorMessage = "Le numéro de rue est requis.")]
        [StringLength(10, ErrorMessage = "Le  numéro de rue doit contenir minimum 1 carctères et maximmum 10 caractères", MinimumLength = 1)]
        [Display(Name = "Numéro de rue")]
        public string? NumeroRue { get; set; }

        [Required(ErrorMessage = "Le nom de rue est requis.")]
        [StringLength(200, ErrorMessage = "Le  nom de rue doit contenir minimum 1 carctères et maximmum 200 caractères", MinimumLength = 1)]
        [Display(Name = "Nom de rue")]
        public string? NomRue { get; set; }

        [Required(ErrorMessage = "La ville est requis.")]
        [StringLength(100, ErrorMessage = "La ville doit contenir minimum 1 carctères et maximmum 100 caractères", MinimumLength = 1)]
        [Display(Name = "Ville")]
        public string? Ville { get; set; }

        [Required(ErrorMessage = "Le code postal est requis.")]
        [StringLength(20, ErrorMessage = "Le code postal doit contenir minimum 1 carctères et maximmum 20 caractères", MinimumLength = 1)]
        [Display(Name = "Code postal")]
        public string? CodePostal { get; set; }

        [Required(ErrorMessage = "Le pays est requis.")]
        [StringLength(20, ErrorMessage = "Le pays doit contenir minimum 1 carctères et maximmum 20 caractères", MinimumLength = 1)]
        [Display(Name = "Pays")]
        public string? Pays { get; set; }

    }
}
