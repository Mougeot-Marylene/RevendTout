using System.ComponentModel.DataAnnotations;

namespace RevendTout.ViewModels
{
    public class EditionCategorieViewModel
    {
        //ActionType => permet de dire si je suis en nouveau ou en modifier
        public string ActionType { get; set; } = string.Empty; // il créer une chaine qui est vide (Empty)
        public string TitreAction { get; set; } = string.Empty; // il créer une chaine qui est vide (Empty)
        public int id { get; set; }


        [Required(ErrorMessage = "Le nom est obligatoire.")]
        [StringLength(100, ErrorMessage = "Le nom doit contenir minimum 5 carctères et maximmum 100 caractères", MinimumLength = 5)]
        public string? Nom { get; set; }

        [Required(ErrorMessage = "La description est obligatoire.")]
        public string? Description { get; set; }
    }
}
