using System.ComponentModel.DataAnnotations;

namespace RevendTout.Models
{
    public class Image
    {
        public IFormFile? FichierImage { get; set; } // fichier de l'image
        public string? Url { get; set; }  // pour l'affichage

        public string? Description { get; set; }

        public DateTime? Date_creation { get; set; }
    }
}
