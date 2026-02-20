using Dapper;
using RevendTout.Models;
using RevendTout.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Npgsql;

namespace RevendTout.ViewComponents
{
    public class RechercheViewComponent : ViewComponent
    {
        // Chaîne de connexion à la base de données
        private readonly string _connexionString;

        // Le constructeur injecte la configuration
        public RechercheViewComponent(IConfiguration configuration)
        {
            // Récupération de la chaîne de connexion depuis appsettings.json
            _connexionString = configuration.GetConnectionString("RevendTout")!;

            // Vérification que la chaîne de connexion existe
            if (_connexionString == null)
            {
                throw new Exception("Error : Connexion string not found !");
            }
        }

        // Méthode principale appelée lors du rendu du composant
        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Création d'une instance du ViewModel
            RechercheViewModel rechercheVM = new RechercheViewModel();

            // Appel à la méthode pour récupérer les catégories
            rechercheVM.Categories = GetCategories();

            // Retour de la vue avec le modèle
            return View(rechercheVM);
        }

        // Méthode privée pour récupérer les catégories de la base de données
        private List<SelectListItem> GetCategories()
        {
            string query = "SELECT id, nom FROM Categories";
            List<SelectListItem> categories;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                // Utilisation de Dapper pour exécuter la requête
                categories = connexion.Query<Categorie>(query)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.Nom
                    })
                    .ToList();
            }
            return categories;
        }
    }
}