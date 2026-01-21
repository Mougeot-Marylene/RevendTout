using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RevendTout.ViewModels;
using System.Security.Claims;

namespace RevendTout.Controllers
{
    public class CommandeController : Controller
    {
        // attribut stockant la chaîne de connexion à la base de données
        private readonly string _connexionString;

        /// <summary>
        /// Constructeur de ProduitsController
        /// </summary>
        /// <param name="configuration">configuration de l'application</param>
        /// <exception cref="Exception"></exception>
        /// 
        /// configuration on recup ce qu'il  ya dans appsetting.json
        public CommandeController(IConfiguration configuration)
        {
            // récupération de la chaîne de connexion dans la configuration
            _connexionString = configuration.GetConnectionString("RevendTout")!;
            // si la chaîne de connexionn'a pas été trouvé => déclenche une exception => code http 500 retourné
            if (_connexionString == null)
            {
                throw new Exception("Error : Connexion string not found ! ");
            }
        }


        [Authorize]
        public IActionResult Index()
        {
            // récupèration de l'id personne connectée
            int id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // Je déclare une liste de CommandeViewModel qui contiendra les commandes de l'utilisateur
            List<CommandeViewModel> commandes;

            // Requête SQL pour récupérer les données des commandes de l'utilisateur
            string queryCommandes =
                @"SELECT 
                    c.id AS Numero,
                    c.date_creation AS Date,
                    st.label AS Statut,
                    COALESCE(SUM(cp.quantite)) AS NombreArticles,
                    COALESCE(SUM(p.prix * cp.quantite)) AS Montant
                FROM Commandes c
                LEFT JOIN Commande_produit cp ON c.id = cp.commande_id
                LEFT JOIN Produits p ON cp.produit_id = p.id
                LEFT JOIN Statut_commandes st ON c.statut_commandes_id = st.id
                WHERE c.utilisateur_id = @id
                GROUP BY c.id, c.statut_commandes_id, c.date_creation, st.label";

            // j'ouvre une connexion à la base de données dans un bloc using (qui se ferme à al fin de l'execution)
            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                // bloc d'essaie pour executer le code suivant
                try
                {
                    // Ouvre la connexion, exécute la requête, transforme  (mapper) chaque ligne de résultat en objet CommandeViewModel, et récupère la liste des commandes de l’utilisateur.
                    commandes = connexion.Query<CommandeViewModel>(queryCommandes, new { id }).ToList();

                    // Retourne la vue avec la liste des commandes
                    return View(commandes);
                }
                catch
                {
                    // si il y a un problème on affiche le message
                    throw new InvalidOperationException("L'affichage des commandes à échoué. Veuillez réessayer plus tard.");
                }
            }

        }
    }
}
