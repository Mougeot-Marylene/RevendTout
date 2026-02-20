namespace RevendTout.Models
{
    public class Produit_categorie
    {
        public int Produit_Id { get; set; }
        public int Categorie_Id { get; set; }
        public Produit? Produit { get; set; }
        public Categorie? Categorie { get; set; }
    }
}
