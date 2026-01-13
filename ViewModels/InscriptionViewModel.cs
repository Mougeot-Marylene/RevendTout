using RevendTout.Models;
using System.ComponentModel.DataAnnotations;

namespace RevendTout.ViewModels
{
    public class InscriptionViewModel
    {
        [Required(ErrorMessage = "Le nom est requis.")]
        public string? Nom { get; set; }

        [Required(ErrorMessage = "Le prénom est requis.")]
        public string? Prenom { get; set; }

        [Required(ErrorMessage = "L'email est requis.")]
        [EmailAddress(ErrorMessage = "L'email n'est pas valide.")]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Le mot de passe est requis.")]
        [StringLength(100, ErrorMessage = "Le mot de passe doit contenir au moins {2} caractères.", MinimumLength = 6)]
        [Display(Name = "Mot de passe")]
        public string? MotDePasse { get; set; }

        [Required(ErrorMessage = "Le mot de passe est requis.")]
        [StringLength(100, ErrorMessage = "Le mot de passe doit contenir au moins {2} caractères.", MinimumLength = 6)]
        [Compare("MotDePasse", ErrorMessage = "Les mots de passe ne correspondent pas.")]
        [Display(Name = "Répéter mot de passe")]
        public string? ConfirmMotDePasse { get; set; }

        public Adresse? Adresse { get; set; }

    }
}
