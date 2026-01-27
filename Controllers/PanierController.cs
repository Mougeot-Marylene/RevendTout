using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RevendTout.Models;
using RevendTout.ViewModels;
using System.Security.Claims;


namespace RevendTout.Controllers
{

    [AutoValidateAntiforgeryToken]
    public class PanierController : Controller
    {
        // attribut stockant la chaîne de connexion à la base de données
        private readonly string _connexionString;
        private object _dbContext;

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
                        // 1. Récupérer ou créer panier
                        string queryPanier = @"SELECT id FROM Paniers WHERE utilisateur_id = @id";
                        int panierId = connexion.QuerySingleOrDefault<int>(queryPanier, new { id }, transaction: transaction);

                        if (panierId == 0)
                        {
                            string insertPanier = "INSERT INTO Paniers (utilisateur_id) VALUES (@id) RETURNING id";
                            panierId = connexion.ExecuteScalar<int>(insertPanier, new { id }, transaction: transaction);
                        }

                        // 2. Récupérer produits + images + quantités en une seule requête avec LEFT JOIN
                        string query = @"
                    SELECT p.id, p.nom, p.prix, p.reduction,
                           i.id, i.produit_id, i.url, i.description,
                           pa.quantite
                    FROM Produit_paniers pa
                    LEFT JOIN Produits p ON pa.produit_id = p.id
                    LEFT JOIN Images i ON p.id = i.produit_id
                    WHERE pa.panier_id = @panierId
                    ORDER BY p.id";

                        var produitDict = new Dictionary<int, Produit>();
                        var quantiteDict = new Dictionary<int, int>(); // Pour stocker quantités par produit

                        var result = connexion.Query<Produit, Image, int, Produit>(
                            query,
                            (p, i, quantite) =>
                            {
                                if (!produitDict.TryGetValue(p.Id, out var prodEntry))
                                {
                                    prodEntry = p;
                                    prodEntry.Images = new List<Image>();
                                    produitDict.Add(p.Id, prodEntry);
                                    quantiteDict[p.Id] = quantite;
                                }
                                if (i != null)
                                    prodEntry.Images.Add(i);
                                return prodEntry;
                            },
                            new { panierId },
                            splitOn: "id,quantite",
                            transaction: transaction
                        );

                        // 3. Construire le ViewModel panier avec produits et quantités
                        var panier = new PanierViewModel();
                        panier.Produits = new Dictionary<Produit, int>();

                        foreach (var prod in produitDict.Values)
                        {
                            panier.Produits.Add(prod, quantiteDict[prod.Id]);
                        }

                        // 4. Calculs totaux comme avant
                        int totalArticles = 0;
                        decimal totalPrix = 0m;
                        decimal totalReduction = 0m;
                        decimal totalSansReduction = 0m;
                        decimal totalAvecReduction = 0m;

                        foreach (var item in panier.Produits)
                        {
                            int quantite = item.Value;
                            decimal prix = item.Key.Prix ?? 0;
                            decimal reduction = item.Key.Reduction ?? 0;

                            totalArticles += quantite;
                            totalSansReduction += prix * quantite;
                            totalAvecReduction += (prix - reduction) * quantite;

                            totalPrix += (prix - reduction) * quantite;
                        }

                        panier.TotalArticles = totalArticles;
                        panier.TotalPrix = totalPrix;
                        panier.TotalReduction = totalSansReduction - totalAvecReduction;

                        transaction.Commit();

                        return View(panier);
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
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

        [Authorize]
        public IActionResult Paiement()
        {
            int id_utilisateur = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                connexion.Open();

                try
                {
                    // 1. Récupérer l'id du panier de l'utilisateur
                    var panierId = connexion.QuerySingleOrDefault<int>(
                        "SELECT id FROM Paniers WHERE utilisateur_id = @id_utilisateur",
                        new { id_utilisateur });

                    // 2. Récupérer produits + images + quantités
                    string queryProduits = @"
                SELECT p.id, p.nom, p.prix, p.reduction,
                       i.id, i.produit_id, i.url, i.description,
                       pa.quantite
                FROM Produit_paniers pa
                LEFT JOIN Produits p ON pa.produit_id = p.id
                LEFT JOIN Images i ON p.id = i.produit_id
                WHERE pa.panier_id = @panierId
                ORDER BY p.id";

                    var produitDict = new Dictionary<int, Produit>();
                    var quantiteDict = new Dictionary<int, int>();

                    var result = connexion.Query<Produit, Image, int, Produit>(
                        queryProduits,
                        (p, i, quantite) =>
                        {
                            if (!produitDict.TryGetValue(p.Id, out var prodEntry))
                            {
                                prodEntry = p;
                                prodEntry.Images = new List<Image>();
                                produitDict.Add(p.Id, prodEntry);
                                quantiteDict[p.Id] = quantite;
                            }
                            if (i != null)
                                prodEntry.Images.Add(i);
                            return prodEntry;
                        },
                        new { panierId },
                        splitOn: "id,id,quantite"
                    );

                    // 3. Récupérer l'utilisateur et son adresse
                    string queryUtilisateur = @"
                SELECT u.id, u.nom, u.prenom, u.adresse_id,
                       a.id, a.numero_rue, a.nom_rue, a.code_postal, a.ville, a.pays
                FROM Utilisateurs u
                LEFT JOIN Adresses a ON u.adresse_id = a.id
                WHERE u.id = @id_utilisateur";

                    var utilisateur = connexion.Query<Utilisateur, Adresse, Utilisateur>(
                        queryUtilisateur,
                        (u, a) =>
                        {
                            u.Adresse = a;
                            return u;
                        },
                        new { id_utilisateur },
                        splitOn: "id"
                    ).First();

                    // 4. Construire le ViewModel
                    var panier = new PanierViewModel
                    {
                        Utilisateur = utilisateur,
                        Produits = new Dictionary<Produit, int>()
                    };

                    foreach (var prod in produitDict.Values)
                    {
                        panier.Produits.Add(prod, quantiteDict[prod.Id]);
                    }

                    // 5. Calcul des totaux
                    int totalArticles = 0;
                    decimal totalPrix = 0m;
                    decimal totalReduction = 0m;
                    decimal totalSansReduction = 0m;
                    decimal totalAvecReduction = 0m;

                    foreach (var item in panier.Produits)
                    {
                        int quantite = item.Value;
                        decimal prix = item.Key.Prix ?? 0;
                        decimal reduction = item.Key.Reduction ?? 0;

                        totalArticles += quantite;
                        totalSansReduction += prix * quantite;
                        totalAvecReduction += (prix - reduction) * quantite;

                        totalPrix += (prix - reduction) * quantite;
                    }

                    panier.TotalArticles = totalArticles;
                    panier.TotalPrix = totalPrix;
                    panier.TotalReduction = totalSansReduction - totalAvecReduction;

                    return View(panier);
                }
                catch
                {
                    throw;
                }
            }
        }


        [Authorize]
        public IActionResult Paiementverif(string NumCB, int code_cb)
        {
            int utilisateur_id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            string queryPanierId = @"SELECT id FROM Paniers WHERE utilisateur_id=@utilisateur_id";

            int panierId;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                connexion.Open();

                using (var transaction = connexion.BeginTransaction())
                {
                    try
                    {
                        // resultat requête qui récupère id du panier panier
                        panierId = connexion.QuerySingle<int>(queryPanierId, new { utilisateur_id });


                        // requêtte qui récupère les produit du panier
                        string queryProduit = @"SELECT 
                                                    pa.id AS PanierId,
                                                    prod.id AS Id,
                                                    prod.nom,
                                                    prod.score_vente,
                                                    prod.quantite AS Quantite,  
                                                    pp.quantite AS QuantiteDansPanier 
                                                FROM Paniers pa
                                                LEFT JOIN Produit_paniers pp ON pa.id = pp.panier_id
                                                LEFT JOIN Produits prod ON pp.produit_id = prod.id
                                                WHERE pa.utilisateur_id=@utilisateur_id";

                        // Dictionnaire pour regrouper les produits par panier
                        var dictPanier = new Dictionary<int, PanierViewModel>();

                        // Requête pour récupérer les produits (mapping)
                        var result = connexion.Query<PanierViewModel, Produit, int, PanierViewModel>(
                            queryProduit,
                            (panier, produit, quantiteDansPanier) =>
                            {
                                if (!dictPanier.TryGetValue(panier.PanierId, out var panierVm))
                                {
                                    panierVm = panier;
                                    panierVm.Produits = new Dictionary<Produit, int>();
                                    dictPanier.Add(panierVm.PanierId, panierVm);
                                }

                                if (produit != null)
                                {
                                    panierVm.Produits[produit] = quantiteDansPanier;
                                }

                                // verifier que le nombre de quantite est suffisant poru comamnder le produit 
                                if (quantiteDansPanier > produit.Quantite)
                                {
                                    throw new InvalidOperationException($"La quantité de produit {produit.Nom} pour et manquante.");
                                }

                                return panierVm;
                            },
                            new { utilisateur_id },
                            splitOn: "Id,QuantiteDansPanier"
                        ).Distinct().ToList();


                        /* ============================= */
                        /* Carte bancaire  */
                        /* ============================= */

                        // Convertir en tableau de caractères pour pouvoir modifier mes doénnes
                        char[] tabNumCB = NumCB.ToCharArray();

                        for (int index = tabNumCB.Length - 2; index >= 0; index -= 2)
                        {
                            int num = int.Parse(tabNumCB[index].ToString());
                            int nouvNum = num * 2;

                            if (nouvNum > 9)
                            {
                                nouvNum -= 9;
                            }

                            // On remplace le num de l'iindex par le nouveau chiffre
                            tabNumCB[index] = nouvNum.ToString()[0];
                            
                        }

                        // Reconstruire la chaîne modifiée
                        string nouvelleCB = new string(tabNumCB);

                        int somme = 0;
                        // faire la somme de la nouvelle chaine
                        for (int i = 0; i < nouvelleCB.Length; i++)
                        {
                            somme = somme + int.Parse(nouvelleCB[i].ToString());
                        }

                        // reucpère le dernier chiffre de la somme
                        string str = somme.ToString();

                        string dernierChiffre = str.Substring(str.Length - 1);


                        if (dernierChiffre != "0")
                        {
                            throw new InvalidOperationException($"Votre carte bancaire n'est pas valide, veuillez ressayer.");
                        }
                      
                        // compte le nombre de numero de la carte
                        int CountNumCB = (tabNumCB.Length);
                        //l'index commence à 0 donc ça fait 15 chiffres
                        if (CountNumCB != 16)
                        {
                            throw new InvalidOperationException("Le numéro de carte bancaire doit contenir exactement 16 chiffres.");
                        }

                        // partie cvv (3 numero derriere la carte)
                        string myStringCVV = code_cb.ToString();

                        code_cb = (myStringCVV.Length);

                        if (code_cb != 3)
                        {
                            throw new InvalidOperationException("Le CVV de carte bancaire doit contenir exactement 3 chiffres.");
                        }


                        /* ============================= */
                        /* Recuperation produit et quantite  */
                        /* ============================= */

                        // 3. Construire une liste des produits + quantités à utiliser plus tard
                        var produitsCommande = new List<(int produitId, int quantite)>();
                        //  on vérifie les quantités et on fait les updates
                        foreach (var panierVm in dictPanier.Values)
                        {
                            foreach (var kvp in panierVm.Produits)
                            {
                                var produit = kvp.Key;
                                int quantiteDansPanier = kvp.Value;

                                if (quantiteDansPanier > produit.Quantite)
                                {
                                    throw new InvalidOperationException($"La quantité de produit {produit.Nom} est insuffisante.");
                                }

                                // Mettre à jour la liste produitsCommande
                                produitsCommande.Add((produit.Id, quantiteDansPanier));

                                // Diminuer la quantite au produit  
                                int? nouvelleQuant = produit.Quantite - quantiteDansPanier;
                                // Augmenter le score de vente 
                                int? nouvScorVente = produit.Score_vente + quantiteDansPanier;

                                var parametresUpdate = new { nouvelleQuant, nouvScorVente, id = produit.Id };

                                int resUpdate = connexion.Execute(
                                    "UPDATE Produits SET quantite = @nouvelleQuant , score_vente = @nouvScorVente WHERE id = @id",
                                    parametresUpdate,
                                    transaction: transaction);

                                if (resUpdate != 1)
                                {
                                    throw new InvalidOperationException("Une erreur est survenue lors de la mise à jour du produit.");
                                }

                            }
                        }


                        /* ============================= */
                        /* Commande  */
                        /* ============================= */

                        string queryComande = "INSERT INTO Commandes (utilisateur_id, statut_commandes_id) VALUES (@utilisateur_id, 1) returning id";

                        int resCom = connexion.ExecuteScalar<int>(queryComande, new { utilisateur_id });

                        if (resCom <= 0)
                        {
                            throw new InvalidOperationException("Une erreur est survenue lors de la mise à jour du produit.");
                        }


                        /* ============================= */
                        /* Commande_produit */
                        /* ============================= */

                        string queryComandePROD = "INSERT INTO Commande_produit (commande_id, produit_id, quantite) VALUES (@commandeId, @produitId, @quantite)";
                        if (produitsCommande.Count == 0)
                        {
                            throw new InvalidOperationException("La liste des produits à commander est vide.");
                        }
                        foreach (var item in produitsCommande)
                        {
                            int resCommProd = connexion.Execute(queryComandePROD, new
                            {
                                commandeId = resCom,
                                produitId = item.produitId,
                                quantite = item.quantite
                            }, transaction);

                            if (resCommProd != 1)
                            {
                                throw new InvalidOperationException("Une erreur est survenue lors de l'insertion des produits de la commande.");
                            }
                        }

                        /* ============================= */
                        /* Panier  */
                        /* ============================= */
                        // compter le nom de ligne dans produit_panier par rapport a l'id du panier
                        string nbCount = "SELECT COUNT(*) FROM Produit_paniers WHERE panier_id=@panierId";

                        int resCount = connexion.ExecuteScalar<int>(nbCount, new { panierId });

                        // supprime le panier dans produit_panier
                        string supPanierProd = "DELETE FROM Produit_paniers WHERE panier_id=@panierId ";
                        int resSupPanProd = connexion.Execute(supPanierProd, new { panierId });
                        if (resSupPanProd != resCount)
                        {
                            throw new InvalidOperationException("Une erreur est survenue lors de la mise à jour du produit.");
                        }
                        // supprime panier
                        string supPanier = "DELETE FROM Paniers WHERE id=@panierId ";
                        int resSupPan = connexion.Execute(supPanier, new { panierId });

                        if (resSupPan != 1)
                        {
                            throw new InvalidOperationException("Une erreur est survenue lors de la mise à jour du produit.");
                        }


                        transaction.Commit();
                        TempData["ValidateMessage"] = "Paiement Validé !";
                        return RedirectToRoute(new
                        {
                            controller = "Commande",
                            action = "Index",
                        });

                    }

                    catch (InvalidOperationException e)
                    {
                        transaction.Rollback();
                        TempData["ValidateMessage"] = e.Message;
                        return RedirectToAction("Index");

                    }

                }


            }


        }


    }
}
