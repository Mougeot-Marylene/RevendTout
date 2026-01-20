using Microsoft.AspNetCore.Mvc.Rendering;
using RevendTout.Models;
using System.ComponentModel.DataAnnotations;

namespace RevendTout.ViewModels
{
    public class EditionProduitViewModel
    {
        //ActionType => permet de dire si je suis en nouveau ou en modifier
        public string ActionType { get; set; } = string.Empty; // il créer une chaine qui est vide (Empty)
        public string TitreAction { get; set; } = string.Empty; // il créer une chaine qui est vide (Empty)
        public int id { get; set; }

        [Required(ErrorMessage = "Le nom est obligatoire.")]
        [StringLength(255, ErrorMessage = "Le nom doit contenir minimum 5 carctères et maximmum 255 caractères", MinimumLength = 5)]
        public string? Nom { get; set; }

        [Required(ErrorMessage = "La description courte est obligatoire.")]
        [StringLength(250, ErrorMessage = "Le nom doit contenir minimum 5 carctères et maximmum 250 caractères", MinimumLength = 5)]
        [Display(Name = "Description courte")] // display => ca valeur Description courte va venir se mettre dans la vue au niveau du model 
        public string? Desc_courte { get; set; }

        [Required(ErrorMessage = "La description est obligatoire.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Le prix est obligatoire.")]
        [Range(0.01, 999999, ErrorMessage = "Le prix doit être supérieur à 0.")]
        public decimal? Prix { get; set; }

        [Required(ErrorMessage = "La réduction est obligatoire.")]
        public decimal? Reduction { get; set; }

        [Required(ErrorMessage = "La quantité est obligatoire.")]
        public int? Quantite { get; set; }

        [Display(Name = "Catégorie")] // display => ca valeur catégorie va venir se mettre dans la vue au niveau du model 
        public List<int> CategorieIds { get; set; } = new List<int>(); // pour plusieurs catégories
        public List<SelectListItem> Categories { get; set; } = new List<SelectListItem>(); // l'id de la catégorie de produit sera dynamique


        public Image? Image { get; set; }
        public List<Image>? Images { get; set; } = new List<Image>();

        [Display(Name = "Taille")]
        public int TailleId { get; set; }
        public List<SelectListItem> Tailles { get; set; } = new List<SelectListItem>();


    }
}
