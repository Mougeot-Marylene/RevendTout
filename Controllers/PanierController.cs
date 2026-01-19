using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RevendTout.Models;
using RevendTout.ViewModels;
using System.Data;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace RevendTout.Controllers
{
    public class PanierController : Controller
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
        public PanierController(IConfiguration configuration)
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
            int id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                connexion.Open();

                using (var transaction = connexion.BeginTransaction())
                {
                    try
                    {
                        var panier = new PanierViewModel();

                        // récupère l'id de l'utlisateur connecté
                        string queryPanier = @"SELECT id FROM Paniers WHERE utilisateur_id = @id";
                        int panierId = connexion.QuerySingleOrDefault<int>(queryPanier, new { id }, transaction: transaction);

                        // si l'utilisateur n'a pas de panier, on en créer un
                        if (panierId == 0)
                        {
                            // créer un panier
                            string insertPanier = " INSERT INTO Paniers (utilisateur_id)VALUES (@id) RETURNING id";

                            panierId = connexion.ExecuteScalar<int>(insertPanier, new { id }, transaction: transaction);
                        }

                        /* ***************************************************** */

                        // 1. requête pour produits + quantités 
                        string query = @"
                                        SELECT prod.id, prod.nom, prod.prix, prod.reduction,
                                               pa.quantite
                                        FROM Produit_paniers pa
                                        LEFT JOIN Produits prod ON pa.produit_id = prod.id
                                        WHERE pa.panier_id = @panierId";

                        connexion.Query<Produit, int, Produit>(
                            query,
                            (produit, quantite) =>
                            {
                                if (!panier.Produits.ContainsKey(produit))
                                    panier.Produits.Add(produit, quantite);
                                return produit;
                            },
                            new { panierId },
                            splitOn: "quantite",
                            transaction: transaction
                        ).ToList();

                        // 2. Requête pour récupérer images des produits récupérés
                        var produitIds = panier.Produits.Keys.Select(p => p.Id).ToArray();

                        if (produitIds.Length > 0)
                        {
                            // ANY(@ids) => vérifier si une valeur est égale à au moins un élément d'un tableau.
                            string queryImages = "SELECT id, produit_id, url, description FROM Images WHERE produit_id = ANY(@ids)";

                            var images = connexion.Query<Image>(queryImages, new { ids = produitIds }, transaction: transaction).ToList();


                            // 3. Associer images aux produits du panier
                            foreach (var produit in panier.Produits.Keys)
                            {
                                produit.Images = images.Where(img => img.ProduitId == produit.Id).ToList();
                            }
                        }


                        // --- Calculs des totaux ---
                        int totalArticles = 0;
                        decimal totalPrix = 0m;
                        decimal TotalReduction = 0m;
                        decimal totalSansReduction = 0m;
                        decimal totalAvecReduction = 0m;
                        foreach (var item in panier.Produits)
                        {
                            int quantite = item.Value;
                            decimal? prix = item.Key.Prix;
                            decimal? reduction = item.Key.Reduction;

                            totalArticles += quantite;
                            totalSansReduction += (decimal)prix * quantite;
                            totalAvecReduction += ((decimal)prix - (decimal)reduction) * quantite;

                            totalPrix += ((decimal)prix - (decimal)reduction) * quantite;
                            TotalReduction += (totalSansReduction - totalAvecReduction);
                        }
                        panier.TotalArticles = totalArticles;
                        panier.TotalPrix = totalPrix;
                        panier.TotalReduction = TotalReduction;


                        transaction.Commit();

                        return View(panier);
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;  // Ou gérer l'exception selon ton besoin
                    }
                }
            }
        }


        [Authorize]
        public IActionResult DiminuerQuantite(int id)
        {
            // Récupère l'id utilisateur connecté
            int id_utilisateur = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            string queryPanier = "SELECT id FROM Paniers WHERE utilisateur_id = @id_utilisateur";

            string querySelect = "SELECT quantite FROM Produit_paniers WHERE produit_id = @produit_id AND panier_id = @panier_id";
            string queryUpdate = "UPDATE Produit_paniers SET quantite = @quantite WHERE produit_id = @produit_id AND panier_id = @panier_id";
            string queryDelete = "DELETE FROM Produit_paniers WHERE produit_id = @produit_id AND panier_id = @panier_id";

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                connexion.Open();

                using (var transaction = connexion.BeginTransaction())
                {
                    try
                    {
                        // Récupère le panier de l'utilisateur
                        int panierId = connexion.QuerySingleOrDefault<int>(queryPanier, new { id_utilisateur }, transaction);

                        if (panierId == 0)
                        {
                            // Pas de panier, on sort
                            transaction.Rollback();
                            return RedirectToAction("Index");
                        }

                        // Récupère la quantité actuelle
                        var quantiteActuelle = connexion.QuerySingleOrDefault<int?>(querySelect, new { produit_id = id, panier_id = panierId }, transaction);

                        if (quantiteActuelle == null)
                        {
                            // Produit non trouvé dans ce panier, rollback et redirection
                            transaction.Rollback();
                            return RedirectToAction("Index");
                        }

                        int nouvelleQuantite = quantiteActuelle.Value - 1;

                        if (nouvelleQuantite < 1)
                        {
                            // Supprime le produit du panier
                            connexion.Execute(queryDelete, new { produit_id = id, panier_id = panierId }, transaction);
                        }
                        else
                        {
                            // Met à jour la quantité
                            connexion.Execute(queryUpdate, new { quantite = nouvelleQuantite, produit_id = id, panier_id = panierId }, transaction);
                        }

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw new InvalidOperationException("Une erreur c'est produite. Veuillez réessayer plus tard.");
                    }
                }
            }

            return RedirectToAction("Index");
        }

        [Authorize]
        public IActionResult AugmenterQuantite(int id)
        {
            // Récupère l'id utilisateur connecté
            int id_utilisateur = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            string queryPanier = "SELECT id FROM Paniers WHERE utilisateur_id = @id_utilisateur";

            string querySelect = "SELECT quantite FROM Produit_paniers WHERE produit_id = @produit_id AND panier_id = @panier_id";
            string queryUpdate = "UPDATE Produit_paniers SET quantite = @quantite WHERE produit_id = @produit_id AND panier_id = @panier_id";
            string queryDelete = "DELETE FROM Produit_paniers WHERE produit_id = @produit_id AND panier_id = @panier_id";

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                connexion.Open();

                using (var transaction = connexion.BeginTransaction())
                {
                    try
                    {
                        // Récupère le panier de l'utilisateur
                        int panierId = connexion.QuerySingleOrDefault<int>(queryPanier, new { id_utilisateur }, transaction);

                        if (panierId == 0)
                        {
                            // Pas de panier, on sort
                            transaction.Rollback();
                            return RedirectToAction("Index");
                        }

                        // Récupère la quantité actuelle
                        var quantiteActuelle = connexion.QuerySingleOrDefault<int?>(querySelect, new { produit_id = id, panier_id = panierId }, transaction);

                        if (quantiteActuelle == null)
                        {
                            // Produit non trouvé 
                            transaction.Rollback();
                            return RedirectToAction("Index");
                        }

                        int nouvelleQuantite = quantiteActuelle.Value + 1;

                        // pn met à jour la quantité
                        connexion.Execute(queryUpdate, new { quantite = nouvelleQuantite, produit_id = id, panier_id = panierId }, transaction);

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw new InvalidOperationException("Une erreur c'est produite. Veuillez réessayer plus tard.");
                    }
                }
            }

            return RedirectToAction("Index");
        }

        [Authorize]
        public IActionResult AjouterAuPanier(int id)
        {
            // récup id de la personne connectée
            int id_utilisateur = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));


            string queryPanier = "SELECT id FROM Paniers WHERE utilisateur_id = @id_utilisateur";

            string querySelect = "SELECT quantite FROM Produit_paniers WHERE produit_id = @produit_id AND panier_id = @panier_id";
            string queryInsert = "INSERT INTO Produit_paniers (produit_id, panier_id, quantite) VALUES (@produit_id, @panier_id, @quantite)";
            string queryUpdate = "UPDATE Produit_paniers SET quantite = @quantite WHERE produit_id = @produit_id AND panier_id = @panier_id";


            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                connexion.Open();

                using (var transaction = connexion.BeginTransaction())
                {
                    try
                    {
                        var panier = new PanierViewModel();
                        panier.Produits = new Dictionary<Produit, int>();

                        //on selectionne le panier de la personne connectée
                        int panierId = connexion.QuerySingleOrDefault<int>(queryPanier, new { id_utilisateur }, transaction: transaction);

                        // si l'utilisateur na pas de panier on en créer un
                        if (panierId == 0)
                        {
                            string insertPanier = @" INSERT INTO Paniers (utilisateur_id)
                                VALUES (@id_utilisateur)
                                RETURNING id";

                            panierId = connexion.ExecuteScalar<int>(insertPanier, new { id_utilisateur }, transaction);
                        }


                        // Récupére la quantité actuelle pour ce produit 
                        var quantiteActuelle = connexion.QuerySingleOrDefault<int?>(querySelect, new { produit_id = id, panier_id = panierId }, transaction);



                        if (quantiteActuelle == null)
                        {
                            // Produit non présent dans le panier, on insère avec quantité 1
                            connexion.Execute(queryInsert, new { produit_id = id, panier_id = panierId, quantite = 1 }, transaction);
                        }
                        else
                        {
                            // Produit déjà présent, on incrémente la quantité
                            int nouvelleQuantite = quantiteActuelle.Value + 1;
                            connexion.Execute(queryUpdate, new { quantite = nouvelleQuantite, produit_id = id, panier_id = panierId }, transaction);
                        }


                        transaction.Commit();


                        return RedirectToAction("Index");
                    }
                    catch
                    {
                        transaction.Rollback();
                                throw new InvalidOperationException("Une erreur c'est produite. Veuillez réessayer plus tard.");
                    }
                }
            }
        }

    }
}
