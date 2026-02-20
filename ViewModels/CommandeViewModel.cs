using Microsoft.VisualBasic;
using RevendTout.Models;

namespace RevendTout.ViewModels
{
    public class CommandeViewModel
    {
        public string Numero { get; set; }
        public string Statut { get; set; }
        public DateTime Date { get; set; }
        public int NombreArticles { get; set; }
        public decimal Montant { get; set; }

    
    }
}
