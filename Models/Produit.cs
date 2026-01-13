namespace RevendTout.Models
{
    public class Produit
    {
        public int Id { get; set; }
        public string? Nom { get; set; }
        public string? Desc_courte { get; set; }
        public string? Description { get; set; }
        public decimal? Prix { get; set; }
        public decimal? Reduction { get; set; }
        public int? Quantite { get; set; }
        public int? ScoreVente { get; set; }
        public DateTime DateCreation { get; set; }
        public bool? Archive { get; set; }
    }
}




