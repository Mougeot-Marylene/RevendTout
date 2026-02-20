using System.ComponentModel.DataAnnotations;

namespace RevendTout.ViewModels
{
    public class ConnexionViewModel
    {
        [Required(ErrorMessage = "L'email est requis.")]
        public string? Email { get; set; }
        [Required(ErrorMessage = "Le mot de passe est requis.")]
        public string? MotDePasse { get; set; }
    }
}
