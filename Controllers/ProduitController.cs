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
            _connexionString = configuration.GetConnectionString("GestionBibliotheque")!;
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
            return View(model);
        }

        [HttpPost]
        public IActionResult Nouveau([FromForm] EditionProduitViewModel produit)
        {
            // lorsque l'on renvoie le formulaire, on récupères les informations rentrées prècédement
            if (!ModelState.IsValid)
            {
                produit.Categories = GetCategories(); // je remets les catégories dans le formulaire
                return View(produit);
            }

            // On affecte la date actuelle ici, avant l'insertion
            produit.DateCreation = DateTime.Now;

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

                            return RedirectToAction("Nouveau");
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

            return View(produit);
        }


        public IActionResult Detail(int id)
        {
            string query = @"SELECT *
                              FROM Produits p
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
    }
}
