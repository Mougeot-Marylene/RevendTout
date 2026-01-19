using System.ComponentModel.DataAnnotations;

namespace RevendTout.ViewModels
{
    public class EditionTailleAdulteViewModel
    {

        //ActionType => permet de dire si je suis en nouveau ou en modifier
        public string ActionType { get; set; } = string.Empty; // il créer une chaine qui est vide (Empty)
        public string TitreAction { get; set; } = string.Empty; // il créer une chaine qui est vide (Empty)
        public int Id { get; set; }


        [Required(ErrorMessage = "La taille est obligatoire.")]
        public string? Taille { get; set; }
    }
}
