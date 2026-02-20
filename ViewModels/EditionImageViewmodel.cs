using Microsoft.AspNetCore.Mvc.Rendering;
using RevendTout.Models;
using System.ComponentModel.DataAnnotations;

namespace RevendTout.ViewModels
{
    public class EditionImageViewmodel
    {
        public int IdProduit { get; set; }

        
        public IFormFile? FichierImage { get; set; } // fichier de l'image
        


        [Required(ErrorMessage = "Le lien est obligatoire.")]
        [StringLength(100, ErrorMessage = "Le lien doit contenir minimum 5 carctères et maximmum 100 caractères", MinimumLength = 1)]
        public string? Description { get; set; }


    }
}
