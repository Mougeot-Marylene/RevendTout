using System.ComponentModel.DataAnnotations;

namespace RevendTout.ViewModels
{
    public class EditionCategorieViewModel
    {
        [Required(ErrorMessage = "Le nom est obligatoire.")]
        public string? Nom { get; set; }


        [Required(ErrorMessage = "La description est obligatoire.")]
        public string? Description { get; set; }
    }
}
