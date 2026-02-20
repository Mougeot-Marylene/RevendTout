using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Npgsql;
using RevendTout.Models;
using RevendTout.ViewModels;


namespace RevendTout.Controllers
{

    [AutoValidateAntiforgeryToken]
    public class ProduitController : Controller
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
        public ProduitController(IConfiguration configuration)
        {
            // récupération de la chaîne de connexion dans la configuration
            _connexionString = configuration.GetConnectionString("RevendTout")!;
            // si la chaîne de connexionn'a pas été trouvé => déclenche une exception => code http 500 retourné
            if (_connexionString == null)
            {
                throw new Exception("Error : Connexion string not found ! ");
            }
        }
        public IActionResult Index()
        {
            string query = @" SELECT 
                                p.id, p.nom, p.desc_courte, p.description, p.reduction, p.prix, p.archive,
                                i.id, i.produit_id, i.url, i.description
                            FROM Produits p
                            LEFT JOIN Images i ON p.id = i.produit_id
                            ORDER BY p.id";

            List<Produit> produits;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                var produitDict = new Dictionary<int, Produit>();

                var result = connexion.Query<Produit, Image, Produit>(
                    query,
                    (p, i) =>
                    {
                        if (!produitDict.TryGetValue(p.Id, out var prodEntry))
                        {
                            prodEntry = p;
                            prodEntry.Images = new List<Image>();
                            produitDict.Add(prodEntry.Id, prodEntry);
                        }
                        if (i != null)
                        {
                            prodEntry.Images.Add(i);
                        }
                        return prodEntry;
                    },
                    splitOn: "id"
                );

                produits = produitDict.Values.ToList();
            }

            return View(produits);
        }


        private List<SelectListItem> GetCategories()
        {
            string query = "SELECT id, nom FROM Categories";
            List<SelectListItem> categories;
            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                categories = connexion.Query<Categorie>(query)
                    /**
                     * .Select => LINQ = Language Integrated Query, C’est une façon d’écrire des requêtes directement en C# pour travailler sur :des listes, des tableaux, des résultats de base de données, des collections en général
                     * 
                     * Select les prends une par une et pour chacune d'entre elle ça retourne un nouveau selectListItem (1 par catégorie)
                     */
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(), // ce qui est choisi par l'utilisateur (on reçoit ID)
                        Text = c.Nom // c'est ce qui est affiché (texte)
                    })
                    .ToList();
            }
            return categories;

        }


        private List<SelectListItem> GetTailles()
        {
            string query = "SELECT id, nom FROM Tailles";
            List<SelectListItem> tailles;
            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                tailles = connexion.Query<Taille>(query)
                    /**
                     * .Select => LINQ = Language Integrated Query, C’est une façon d’écrire des requêtes directement en C# pour travailler sur :des listes, des tableaux, des résultats de base de données, des collections en général
                     * 
                     * Select les prends une par une et pour chacune d'entre elle ça retourne un nouveau selectListItem (1 par catégorie)
                     */
                    .Select(t => new SelectListItem
                    {
                        Value = t.Id.ToString(), // ce qui est choisi par l'utilisateur (on reçoit ID)
                        Text = t.Nom // c'est ce qui est affiché (texte)
                    })
                    .ToList();
            }
            return tailles;

        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Nouveau()
        {
            var model = new EditionProduitViewModel();

            model.Categories = GetCategories();
            model.Tailles = GetTailles();
            model.ActionType = "Nouveau";
            model.TitreAction = "Ajouter un nouveau produit";
            return View("Editer", model);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult Nouveau([FromForm] EditionProduitViewModel produit)
        {
            // lorsque l'on renvoie le formulaire, on récupères les informations rentrées prècédement
            if (!ModelState.IsValid)
            {
                produit.Categories = GetCategories(); // je remets les catégories dans le formulaire
                produit.Tailles = GetTailles(); // je remets les tailles dans le formulaire
                produit.ActionType = "Nouveau";
                produit.TitreAction = "Ajouter un nouveau produit";
                return View("Editer", produit);
            }

            // Création de requêtes d'insertion
            string queryProduit = "INSERT INTO Produits (nom, desc_courte, description, reduction, prix, quantite) VALUES (@Nom, @Desc_courte, @Description, @Reduction, @Prix, @Quantite) returning id";


            string queryCategorieProduit = "INSERT INTO Produit_categories (produit_id, categorie_id) VALUES(@produit_id, @categorie_id)";

            string queryTailleProduit = "INSERT INTO Produit_tailles (produit_id, taille_id) VALUES(@produit_id, @taille_id)";

            int idProduit;
            int nbCategories;
            int nbTaillles;

            using (var connexion = new NpgsqlConnection(_connexionString)) // ouvre connexion à la BDD
            {
                connexion.Open();

                using (var tran = connexion.BeginTransaction()) // ouvre une transaction
                {
                    try
                    {   // retourne qu'une seule case (1 seule ligne et 1 seule colonne)
                        idProduit = connexion.ExecuteScalar<int>(queryProduit, produit); // j'insere mon produit dans la BDD et je récupère son Id

                        // si l'id que je reçois est égale à 0, c'est pas normal donc on rollback
                        if (idProduit == 0)
                        {
                            throw new InvalidOperationException("L'insertion du produit à échoué. Veuillez réessayer plus tard.");
                        }

                        // permet de faire la requette d'ajout de catégorie autant de fois qu'on a sélectionner de nombre de catégorie
                        List<Object> parameters = new List<object>();
                        foreach (var categorieId in produit.CategorieIds)
                        {
                            parameters.Add(new { categorie_id = categorieId, produit_id = idProduit }); // new {...} => un pour chaque catégorie choisie
                        }
                       
                        // roduit.CategorieIds => id de la catégorie que j'ai reçu dans mon model (en paramètre dans la méthode), idProduit => id du produit que je viens de recevoir de la BDD                    
                        nbCategories = connexion.Execute(queryCategorieProduit, parameters);

                        // pour les tailles
                        // produit.TailleId => id de la taille que j'ai reçu dans mon model (en paramètre dans la méthode), idProduit => id du produit que je viens de recevoir de la BDD                    
                        nbTaillles = connexion.Execute(queryTailleProduit, new { taille_id = produit.TailleId, produit_id = idProduit });


                        // on vérifie que le nombre de catégorie qui ont été crées est bien égale à 1 (pour 1 catégorie)
                        if (nbCategories == produit.CategorieIds.Count && nbTaillles == 1) // produit.CategorieIds.Count => nb de catégorie choisie pour le produit, nbCategories => nb de catégorie ajouté
                        {
                            tran.Commit();
                            TempData["ValidateMessage"] = "Produit ajouté avec succès !";

                            return RedirectToAction("Admin_Index_Detail", new { id = idProduit });
                        }
                        else
                        {
                            throw new InvalidOperationException("L'insertion du produit à échoué. Veuillez réessayer plus tard.");
                        }
                    }

                    catch (PostgresException e) when (e.MessageText.Contains("produits_unique")) // violation de contrainte d'unicité sur le titre
                    {
                        tran.Rollback();
                        ModelState.AddModelError("Nom", "Ce nom est déjà référencé pour un produit dans la BDD.");
                    }
                    catch (InvalidOperationException c)
                    {
                        tran.Rollback();
                        ViewData["ValidateMessage"] = c.Message;
                    }
                }
            }

            // si tout ne s'est pas bien passé
            produit.Categories = GetCategories();
            produit.Tailles = GetTailles();

            produit.ActionType = "Nouveau";
            produit.TitreAction = "Ajouter un nouveau produit";
            return View("Editer", produit);
        }


        public IActionResult Detail(int id)
        {
            string query = @"
                            SELECT 
                                p.id, p.nom, p.desc_courte, p.description, p.reduction, p.prix,
                                i.id AS ImageId, i.produit_id AS ImageProduitId, i.url, i.description AS ImageDescription,
                                t.id AS TailleId, t.nom AS Nom
                            FROM Produits p
                            LEFT JOIN Images i ON p.id = i.produit_id
                            LEFT JOIN Produit_tailles pt ON p.id = pt.produit_id
                            LEFT JOIN Tailles t ON pt.taille_id = t.id
                            WHERE p.id = @identifiant";

            Produit? produit = null;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                // C’est une boîte de rangement
                // clé : Id du produit
                // valeur : le produit unique
                // Ça évite d’avoir le même produit plusieurs fois
                var produitDict = new Dictionary<int, Produit>();

                
                // je lis un Produit,  une Image et je retourne un Produit (la requête SQL fait un JOIN)
                var result = connexion.Query<Produit, Image, Taille, Produit>(
                    query,

                    // p = le produit de la ligne, i = l’image de la ligne (peut être null)
                    (p, i, t) =>
                    {
                        // Est-ce que ce produit est déjà dans le dictionnaire ?
                        if (!produitDict.TryGetValue(p.Id, out var prodEntry))
                        {
                            prodEntry = p; // On crée le produit
                            prodEntry.Images = new List<Image>(); // On initialise sa liste d’images
                            produitDict.Add(prodEntry.Id, prodEntry); // On le stocke
                        }

                        // Si la ligne contient une image, on l’ajoute à la liste Images
                        if (i != null)
                        {
                            prodEntry.Images!.Add(i);
                        }
                        if (t != null)
                        {
                            prodEntry.Tailles!.Add(t);
                        }

                        // Retourne le produit (obligatoire pour Dapper)
                        return prodEntry;
                    },
                    new { identifiant = id },
                    splitOn: "ImageId,TailleId" // correspond aux alias des colonnes dans la requête
                );

                // On récupère le produit avec toutes ses images
                produit = produitDict.Values.FirstOrDefault();
            }

            if (produit == null)
                return NotFound();

            return View(produit);
        }



        [Authorize(Roles = "Admin")]
        [HttpGet] //décorateur 
        public IActionResult Modifier([FromRoute] int id)
        {
            string queryProduit = "SELECT * FROM Produits WHERE id = @id";
            string queryCategories = "SELECT categorie_id FROM Produit_categories WHERE produit_id = @id";
            string queryTailles = "SELECT taille_id FROM Produit_tailles WHERE produit_id = @id";
            string queryImages = "SELECT * FROM Images WHERE produit_id = @id";

            Produit? produit;
            List<int> categorieIds;
            List<int> tailleIds;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                // Récupérer le produit
                produit = connexion.QueryFirstOrDefault<Produit>(queryProduit, new { id = id });
                if (produit == null)
                {
                    return NotFound();
                }

                // Récupérer les catégories associées
                categorieIds = connexion.Query<int>(queryCategories, new { id = id }).ToList();

                // réucup les tailles
                tailleIds = connexion.Query<int>(queryTailles, new { id = id }).ToList();

                // Récupérer les images associées
                var images = connexion.Query<Image>(queryImages, new { id = id }).ToList();

                // Affecter les images au produit
                produit.Images = images;
            }

            // Préparer le ViewModel avec les données du produit
            var model = new EditionProduitViewModel
            {
                id = id,
                Nom = produit.Nom,
                Desc_courte = produit.Desc_courte,
                Description = produit.Description,
                Prix = produit.Prix,
                Reduction = produit.Reduction,
                Quantite = produit.Quantite,
                CategorieIds = categorieIds,
                Categories = GetCategories(),
                Tailles = GetTailles(),
                TailleId = tailleIds.FirstOrDefault(),
                ActionType = "Modifier",
                TitreAction = "Modifier le produit : " + produit.Nom,
                Images = produit.Images
            };

            return View("Editer", model);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult Modifier([FromForm] EditionProduitViewModel produit)
        {
            //Verifier si le modèle est valide, si c'est pas le cas on renvoie le formulaire, on réuccpères les informations rentrées précédement
            if (!ModelState.IsValid)
            {
                produit.Categories = GetCategories(); // je remets les catégories dans le formulaire
                produit.Tailles = GetTailles();
                produit.ActionType = "Modifier";
                produit.TitreAction = "Modifier le produit : " + produit.Nom;
                return View("Editer", produit); // je retourne la vue Editer en lui donnant mon ViewModel
            }


            string queryProduit = "UPDATE Produits SET nom=@Nom, desc_courte=@Desc_courte, description=@Description, prix=@Prix, reduction=@Reduction, quantite=@Quantite WHERE id=@id; ";
            string queryNbCategoriesAvantUpdate = "SELECT COUNT(*) FROM produit_categories WHERE produit_id=@produit_id; ";
            string queryDeleteCatProduit = "DELETE FROM produit_categories WHERE produit_id=@produit_id;";
            string queryCategorieProduit = "INSERT INTO produit_categories (produit_id, categorie_id) VALUES(@produit_id, @categorie_id);";
            string queryNbTailleAvantUpdate = "SELECT COUNT(*) FROM produit_tailles WHERE produit_id=@produit_id; ";
            string queryDeleteTailleProduit = "DELETE FROM produit_tailles WHERE produit_id=@produit_id;";
            string queryTailleProduit = "INSERT INTO produit_tailles (produit_id, taille_id) VALUES(@produit_id, @taille_id);";

            int resUpdateProduit;
            int nbCategoriesASupprimer;
            int resDeleteCategorie;
            int resInsertCategorie;
            int nbTailleASupprimer;
            int resDeleteTaille;
            int resInsertTaille;


            using (var connexion = new NpgsqlConnection(_connexionString)) // ouvre connexion à la BDD
            {
                // on fait une transaction car on à plusieurs requettes, si on en avait qu'une il n'y en aurait pas besoin
                connexion.Open(); // j'ouvre la connexion de la transaction

                using (var tran = connexion.BeginTransaction()) // créer une transaction, (commence la transaction)
                {   // bloc try=> on essaye
                    try
                    {                      
                        /* update du produit */
                        resUpdateProduit = connexion.Execute(queryProduit, produit);

                        if (resUpdateProduit != 1)
                        {
                            throw new InvalidOperationException("La modification du produit à échoué. Veuillez réessayer plus tard.");
                        }

                        /* suppression des anciennes catégories */

                        //ExecuteScalar pour juste 1 case à récupérer
                        nbCategoriesASupprimer = connexion.ExecuteScalar<int>(queryNbCategoriesAvantUpdate, new { produit_id = produit.id });

                        resDeleteCategorie = connexion.Execute(queryDeleteCatProduit, new { produit_id = produit.id });

                        if (resDeleteCategorie != nbCategoriesASupprimer)
                        {

                            throw new InvalidOperationException("La modification du produit à échoué. Veuillez réessayer plus tard.");
                        }

                        /* suppression des anciennes tailles */
                        nbTailleASupprimer = connexion.ExecuteScalar<int>(queryNbTailleAvantUpdate, new { produit_id = produit.id });

                        resDeleteTaille = connexion.Execute(queryDeleteTailleProduit, new { produit_id = produit.id });

                        if (resDeleteTaille != nbTailleASupprimer)
                        {

                            throw new InvalidOperationException("La modification du produit à échoué. Veuillez réessayer plus tard.");
                        }

                        /* insersion des nouvelles catégories */

                        // permet de faire la requette d'ajout de catégorie autant de fois qu'on a séléctionner de nombre de catégorie
                        List<Object> parameters = new List<object>();
                        foreach (var categorieId in produit.CategorieIds)
                        {
                            parameters.Add(new { categorie_id = categorieId, produit_id = produit.id }); // new {...} => un pour chaque catégorie choisie
                        }


                        resInsertCategorie = connexion.Execute(queryCategorieProduit, parameters);

                        // Insérer la taille sélectionnée (UNE SEULE)
                         resInsertTaille = connexion.Execute(queryTailleProduit, new { produit_id = produit.id, taille_id = produit.TailleId });

                        // on vérifie que le nombre de catégorie qui ont été crées est bien égale au nb de categorie
                        if (resInsertCategorie == produit.CategorieIds.Count && resInsertTaille == 1) // produit.CategorieIds.Count => nb de catégorie choisie pour le produit, resInsertCategorie => nb de catégorie ajouté
                        {
                            tran.Commit();
                            TempData["ValidateMessage"] = "Produit modifié avec succès !";

                            return RedirectToAction("Admin_Index_Detail", new { id = produit.id });
                        }
                        else
                        {
                            throw new InvalidOperationException("La modification du produit à échoué. Veuillez réessayer plus tard.");
                        }
                    }
                    catch (PostgresException e) when (e.MessageText.Contains("produits_unique")) // violation de contrainte d'unicité sur le titre || _unique veut dire key primaire
                    {
                        tran.Rollback();
                        ModelState.AddModelError("Titre", "Ce titre est déjà utilisé par un autre produit dans la BDD.");
                    }
                    catch (InvalidOperationException e)
                    {
                        tran.Rollback();
                        ViewData["ValidateMessage"] = e.Message;
                    }

                    // si tout ne s'est pas bien passé
                    produit.Categories = GetCategories();
                    produit.ActionType = "Modifier";
                    produit.TitreAction = "Modifier le produit : " + produit.Nom;
                    return View("Editer", produit); // je retourne la vue Editer en lui donnant mon ViewModel
                }
            }
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Admin_Index()
        {
            string query = "SELECT * FROM Produits  ORDER BY date_creation ASC";
            List<Produit> produits;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                produits = connexion.Query<Produit>(query).ToList();
            }

            return View(produits);
        }

        [Authorize(Roles = "Admin")]

        public IActionResult Admin_Index_Detail(int id)
        {
            string query = @"
                            SELECT 
                                p.id, p.nom, p.desc_courte, p.description, p.reduction, p.prix, p.quantite, p.score_vente,
                                i.id AS ImageId, i.produit_id AS ImageProduitId, i.url, i.description AS ImageDescription,
                                t.id AS TailleId, t.nom AS Nom
                            FROM Produits p
                            LEFT JOIN Images i ON p.id = i.produit_id
                            LEFT JOIN Produit_tailles pt ON p.id = pt.produit_id
                            LEFT JOIN Tailles t ON pt.taille_id = t.id
                            WHERE p.id = @identifiant";

            Produit? produit = null;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                var produitDict = new Dictionary<int, Produit>();

                var result = connexion.Query<Produit, Image, Taille, Produit>(
                    query,
                    (p, i, t) =>
                    {
                        if (!produitDict.TryGetValue(p.Id, out var prodEntry))
                        {
                            prodEntry = p;
                            prodEntry.Images = new List<Image>();
                            prodEntry.Tailles = new List<Taille>(); // Initialise la liste des tailles ici !
                            produitDict.Add(prodEntry.Id, prodEntry);
                        }

                        if (i != null)
                        {
                            prodEntry.Images!.Add(i);
                        }
                        if (t != null)
                        {
                            prodEntry.Tailles!.Add(t);
                        }

                        return prodEntry;
                    },
                    new { identifiant = id },
                    splitOn: "ImageId,TailleId" // correspond aux alias des colonnes dans la requête
                );


                produit = produitDict.Values.FirstOrDefault();
            }

            if (produit == null)
                return NotFound();

            return View(produit);
        }


        public IActionResult Archiver(int id)
        {
            string queryUpdateProduitArchive = @"UPDATE Produits
                                SET archive = true
                                WHERE id = @id";

            int res;
            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                // créer une transaction 
                // bloc essaie
                try
                {
                    // suppression des categories
                    res = connexion.Execute(queryUpdateProduitArchive, new { id = id });

                    if (res == 1)
                    {
                        TempData["ValidateMessage"] = "Le prouit à été archivé avec succes";
                        return RedirectToAction("Admin_Index");
                    }
                    else
                    {
                        throw new InvalidOperationException("L'archive du produit à échouée. Veuillez réessayer plus tard.");
                    }

                }
                catch (Exception)
                {
                    throw new InvalidOperationException("L'archive du produit à échouée. Veuillez réessayer plus tard.");
                }

            }

        }

        [HttpGet]
        public IActionResult Recherche(string? termeRecherche, int? categorieId)
        {
            List<Produit> produits = new List<Produit>();

            string query = @"
                            SELECT DISTINCT p.*  
                            FROM Produits p
                            LEFT JOIN produit_categories pc ON p.id = pc.produit_id
                            WHERE 1=1 ";

            // Ajouter des filtres selon les paramètres
            if (!string.IsNullOrEmpty(termeRecherche))
            {
                query += $" AND p.nom LIKE '%{termeRecherche}%'";
            }

            if (categorieId.HasValue)
            {
                query += $" AND pc.categorie_id = {categorieId}";
            }

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                produits = connexion.Query<Produit>(query).ToList();
                // APRÈS avoir récupéré produits
                foreach (var produit in produits)
                {
                    produit.Images = connexion.Query<Image>(
                        "SELECT * FROM Images WHERE produit_id = @id ORDER BY id",
                        new { id = produit.Id }
                    ).ToList();
                }

            }

            return View("Index", produits);
        }
    }
}
