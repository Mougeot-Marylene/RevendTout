using System.Data;

namespace RevendTout.Models
{
    public class Utilisateur
    {
        public int Id { get; set; }
        public string? Nom { get; set; }
        public string? Prenom { get; set; }
        public string? Email { get; set; }
        public string? Mdp { get; set; }
        public bool Admin { get; set; } = false;

        public Adresse? Adresse { get; set; }
    }
}
