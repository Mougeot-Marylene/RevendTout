using System.ComponentModel.DataAnnotations;

namespace RevendTout.Models
{
    public class Image
    {
        public int Id { get; set; }  // id de l'image en base
        public int ProduitId { get; set; }  // clé étrangère vers Produit

        public IFormFile? FichierImage { get; set; } // fichier upload, non en base

        public string? Url { get; set; }  // url de l'image (en base)

        public string? Description { get; set; }

        public DateTime? Date_creation { get; set; }
    }
}
