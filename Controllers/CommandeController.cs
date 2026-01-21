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

            List<CommandeViewModel> commandes;

            /* COALESCE => permet de combiner des colonnes, 
                 au lieu de me retouner plusieurs ligne je fusionne les colone pour en retourner qu'une 
                    comment ? => en comptant le nombre total de produits pour cette commande_id COALESCE(SUM(cp.quantite), 0) et le montant total de tous les produits ( COALESCE(SUM(p.prix * cp.quantite), 0) 
             */
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

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                try
                {
                  commandes = connexion.Query<CommandeViewModel>(queryCommandes, new { id }).ToList();


                    return View(commandes);
                }
                catch
                {
                    throw new InvalidOperationException("L'affichage des commandes à échoué. Veuillez réessayer plus tard.");
                }
            }

        }
    }
}
