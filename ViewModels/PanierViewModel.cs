using RevendTout.Models;

namespace RevendTout.ViewModels
{
    public class PanierViewModel
    {
        public int PanierId { get; set; }

        public Dictionary<Produit, int> Produits { get; set; } = new Dictionary<Produit , int>();

        public int TotalArticles { get; set; }
        public decimal TotalPrix { get; set; }
        public decimal TotalReduction { get; set; }

        public Utilisateur? Utilisateur { get; set; }



    }
}
