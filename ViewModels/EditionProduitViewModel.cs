using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace RevendTout.ViewModels
{
    public class EditionProduitViewModel
    {
        [Required(ErrorMessage = "Le nom est obligatoire.")]
        public string? Nom { get; set; }

        [Required(ErrorMessage = "La description courte est obligatoire.")]
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

        public DateTime DateCreation { get; set; }

        [Display(Name = "Catégorie")] // display => ca valeur catégorie va venir se mettre dans la vue au niveau du model 
        public List<int> CategorieIds { get; set; } = new List<int>(); // pour plusieurs catégories
        public List<SelectListItem> Categories { get; set; } = new List<SelectListItem>(); // l'id de la catégorie de produit sera dynamique
    }
}
