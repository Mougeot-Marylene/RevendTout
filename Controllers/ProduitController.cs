using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Npgsql;
using RevendTout.Models;
using RevendTout.ViewModels;
using static System.Net.Mime.MediaTypeNames;


namespace RevendTout.Controllers
{
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
            string query = "SELECT * FROM Produits";
            List<Produit> produits;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                produits = connexion.Query<Produit>(query).ToList();
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

        [HttpGet]
        public IActionResult Nouveau()
        {
            var model = new EditionProduitViewModel();
            model.Categories = GetCategories();
            model.ActionType = "Nouveau";
            model.TitreAction = "Ajouter un nouveau produit";
            return View("Editer", model);
        }

        [HttpPost]
        public IActionResult Nouveau([FromForm] EditionProduitViewModel produit)
        {
            // lorsque l'on renvoie le formulaire, on récupères les informations rentrées prècédement
            if (!ModelState.IsValid)
            {
                produit.Categories = GetCategories(); // je remets les catégories dans le formulaire
                produit.ActionType = "Nouveau";
                produit.TitreAction = "Ajouter un nouveau produit";
                return View("Editer", produit);
            }

            string queryProduit = "INSERT INTO Produits (nom, desc_courte, description, reduction, prix, quantite, date_creation) VALUES (@Nom, @Desc_courte, @Description, @Reduction, @Prix, @Quantite, @DateCreation) returning id";


            string queryCategorieProduit = "INSERT INTO Produit_categories (produit_id, categorie_id) VALUES(@produit_id, @categorie_id)";

            int idProduit;
            int nbCategories;

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

                        // on vérifie que le nombre de catégorie qui ont été crées est bien égale à 1 (pour 1 catégorie)
                        if (nbCategories == produit.CategorieIds.Count) // produit.CategorieIds.Count => nb de catégorie choisie pour le produit, nbCategories => nb de catégorie ajouté
                        {
                            tran.Commit();
                            TempData["ValidateMessage"] = "Produit ajouté avec succès !";

                            return RedirectToAction("Detail", new { id = produit.id });
                        }
                        else
                        {
                            throw new InvalidOperationException("L'insertion du produit à échoué. Veuillez réessayer plus tard.");
                        }
                    }
                    catch (InvalidOperationException c)
                    {
                        tran.Rollback();
                        ViewData["ValidateMessage"] = c.Message;
                    }
                }
            }

            produit.Categories = GetCategories();

            produit.ActionType = "Nouveau";
            produit.TitreAction = "Ajouter un nouveau produit";
            return View("Editer", produit);
        }


        public IActionResult Detail(int id)
        {
            string query = @"SELECT *
                              FROM Produits
                           WHERE id=@identifiant";

            Produit produits;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                try
                {
                    produits = connexion.QuerySingle<Produit>(query, new { identifiant = id });
                }
                catch (System.Exception)
                {
                    return NotFound();
                }

            }
            return View(produits);
        }


        /* Partie admin */
        public IActionResult Admin_Index()
        {
            string query = "SELECT * FROM Produits";
            List<Produit> produits;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                produits = connexion.Query<Produit>(query).ToList();
            }

            return View(produits);
        }


        [HttpGet] //décorateur 
        public IActionResult Modifier([FromRoute] int id)
        {
            // récupération du produit à modifier
            string query = "SELECT * FROM Produits WHERE id = @id";
            string queryCategories = "SELECT categorie_id FROM Produit_categories WHERE produit_id = @id";

            Produit produit; // je vais récupèrer un produit

            List<int> categorieIds; // je créer une liste entier qui sont mes id de catégories            
            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                produit = connexion.QueryFirstOrDefault<Produit>(query, new { id = id }); // j'ai mon produit
                categorieIds = connexion.Query<int>(queryCategories, new { id = id }).ToList(); // j'interoge ma bdd et je récupère une liste d'entiers qui sont les id de catégorie de ce produit la (produit), je lui donne l'id qui m'interesse et je le transforme en liste
            }

            // si l'utilisateur veut modifier un produit qui n'existe pas, on aura null
            if (produit == null)
            {
                return NotFound(); // erreur 404
            }

            var model = new EditionProduitViewModel();
            // je met les données de mon produit dans mon viewModel
            model.Nom = produit.Nom;
            model.Desc_courte = produit.Desc_courte;
            model.Description = produit.Description;
            model.Prix = produit.Prix;
            model.Reduction = produit.Reduction;
            model.Quantite = produit.Quantite;
            model.CategorieIds = categorieIds;

            // Gérer les catégories déjà sélectionnées
            model.Categories = GetCategories();

            model.ActionType = "Modifier";
            model.TitreAction = "Modifier le produit : " + model.Nom;
            return View("Editer", model); // je retourne la vue Editer en lui donnant mon ViewModel
        }

        [HttpPost]
        public IActionResult Modifier([FromForm] EditionProduitViewModel produit)
        {
            //Verifier si le modèle est valide, si c'est pas le cas on renvoie le formulaire, on réuccpères les informations rentrées précédement
            if (!ModelState.IsValid)
            {
                produit.Categories = GetCategories(); // je remets les catégories dans le formulaire
                produit.ActionType = "Modifier";
                produit.TitreAction = "Modifier le produit : " + produit.Nom;
                return View("Editer", produit); // je retourne la vue Editer en lui donnant mon ViewModel
            }


            string queryProduit = "UPDATE Produits SET nom=@Nom, desc_courte=@Desc_courte, description=@Description, prix=@Prix, reduction=@Reduction, quantite=@Quantite WHERE id=@id; ";
            string queryNbCategoriesAvantUpdate = "SELECT COUNT(*) FROM produit_categories WHERE produit_id=@produit_id; ";
            string queryDeleteCatProduit = "DELETE FROM produit_categories WHERE produit_id=@produit_id;";
            string queryCategorieProduit = "INSERT INTO produit_categories (produit_id, categorie_id) VALUES(@produit_id, @categorie_id);";

            int resUpdateProduit;
            int nbCategoriesASupprimer;
            int resDeleteCategorie;
            int resInsertCategorie;


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

                        /* insersion des nouvelles catégories */

                        // permet de faire la requette d'ajout de catégorie autant de fois qu'on a séléctionner de nombre de catégorie
                        List<Object> parameters = new List<object>();
                        foreach (var categorieId in produit.CategorieIds)
                        {
                            parameters.Add(new { categorie_id = categorieId, produit_id = produit.id }); // new {...} => un pour chaque catégorie choisie
                        }

                        resInsertCategorie = connexion.Execute(queryCategorieProduit, parameters);

                        // on vérifie que le nombre de catégorie qui ont été crées est bien égale au nb de categorie
                        if (resInsertCategorie == produit.CategorieIds.Count) // produit.CategorieIds.Count => nb de catégorie choisie pour le produit, resInsertCategorie => nb de catégorie ajouté
                        {
                            tran.Commit();
                            TempData["ValidateMessage"] = "Produit modifié avec succès !";

                            return RedirectToAction("Detail", new { id = produit.id });
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


    }
}
