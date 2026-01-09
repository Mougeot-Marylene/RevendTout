using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RevendTout.Models;
using RevendTout.ViewModels;
using System.Reflection;

namespace RevendTout.Controllers
{
    public class CategorieController : Controller
    {
        // attribut stockant la chaîne de connexion à la base de données
        private readonly string _connexionString;

        /// <summary>
        /// Constructeur de CategoriesController
        /// </summary>
        /// <param name="configuration">configuration de l'application</param>
        /// <exception cref="Exception"></exception>
        /// 
        /// configuration on recup ce qu'il  ya dans appsetting.json
        public CategorieController(IConfiguration configuration)
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
            string query = "SELECT * FROM Categories";
            List<Categorie> categories;

             using (var connexion = new NpgsqlConnection(_connexionString))
            {
                categories = connexion.Query<Categorie>(query).ToList();
            }
            
             return View(categories);
        }

        [HttpGet]
        public IActionResult Nouveau()
        {
            var model =  new EditionCategorieViewModel();

            model.ActionType = "Nouveau";
            model.TitreAction = "Ajouter une categorie";
            return View("Editer", model);
        }

        [HttpPost]
        public IActionResult Nouveau([FromForm] EditionCategorieViewModel categorie)
        {
            // lorsque l'on renvoie le formulaire, on récupères les informations rentrées précédement
            if (!ModelState.IsValid)
            {
                categorie.ActionType = "Nouveau";
                categorie.TitreAction = "Ajouter une categorie";
                return View("Editer", categorie);
            }

            int res;
            string queryCategorie = @"INSERT INTO Categories (nom, description)  VALUES (@Nom, @Description )";

         
            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                try
                {

                    res = connexion.Execute(queryCategorie, categorie);


                    if (res == 1)
                    {
                        TempData["ValidateMessage"] = "La catégorie à bien été ajoutée";
                        return RedirectToAction("Nouveau");
                    }
                    else
                    {
                        throw new InvalidOperationException("L'insertion de la catégorie à échoué. Veuillez réessayer plus tard.");
                    }
                }
                catch (InvalidOperationException c)
                {
                    ViewData["ValidateMessage"] = c.Message;
                }

            }

            categorie.ActionType = "Nouveau";
            categorie.TitreAction = "Ajouter une categorie";
            return View("Editer", categorie);
        }
    
    
        public IActionResult Detail(int id)
        {
            string query = @"SELECT *
                              FROM Categories c
                           WHERE id=@identifiant";

            Categorie categories;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                try
                {
                    categories = connexion.QuerySingle<Categorie>(query, new { identifiant = id });
                }
                catch (System.Exception)
                {
                    return NotFound();
                }

            }
            return View(categories);
        }


        [HttpGet] //décorateur 
        public IActionResult Modifier([FromRoute] int id)
        {
            // récupération de la categorie à modifier
            string query = "SELECT * FROM Categories WHERE id = @id";

            Categorie categorie; // je vais récupèrer une catégorie

                      
            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                categorie = connexion.QueryFirstOrDefault<Categorie>(query, new { id = id }); // j'ai ma categorie               
            }

            // si l'utilisateur veut modifier une categorie qui n'existe pas, on aura null
            if (categorie == null)
            {
                return NotFound(); // erreur 404
            }

            var model = new EditionCategorieViewModel();
            // je met les données de ma categorie dans mon viewModel
            model.Nom = categorie.Nom;
            model.Description = categorie.Description;


            model.ActionType = "Modifier";
            model.TitreAction = "Modifier la categorie : " + model.Nom;
            return View("Editer", model); // je retourne la vue Editer en lui donnant mon ViewModel
        }

        [HttpPost]
        public IActionResult Modifier([FromForm] EditionCategorieViewModel categorie)
        {
            //Verifier si le modèle est valide, si c'est pas le cas on renvoie le formulaire, on réuccpères les informations rentrées précédement
            if (!ModelState.IsValid)
            {
                categorie.ActionType = "Modifier";
                categorie.TitreAction = "Modifier le categorie : " + categorie.Nom;
                return View("Editer", categorie); // je retourne la vue Editer en lui donnant mon ViewModel
            }


            string queryProduit = "UPDATE Categories SET nom=@Nom,  description=@Description WHERE id=@id; ";

            int resUpdateCategorie;

            using (var connexion = new NpgsqlConnection(_connexionString)) // ouvre connexion à la BDD
            {
                // on fait une transaction car on à plusieurs requettes, si on en avait qu'une il n'y en aurait pas besoin
                connexion.Open(); // j'ouvre la connexion de la transaction

                using (var tran = connexion.BeginTransaction()) // créer une transaction, (commence la transaction)
                {   // bloc try=> on essaye
                    try
                    {
                        /* update de la categorie */
                        resUpdateCategorie = connexion.Execute(queryProduit, categorie);

                        if (resUpdateCategorie != 1)
                        {
                            throw new InvalidOperationException("La modification de la categorie à échoué. Veuillez réessayer plus tard.");
                        }
                        else
                        {
                            tran.Commit();
                            TempData["ValidateMessage"] = "Catégorie modifié avec succès !";
                        }
                    }
                    catch (PostgresException e) when (e.MessageText.Contains("categories_unique")) // violation de contrainte d'unicité sur le titre || _unique veut dire key primaire
                    {
                        tran.Rollback();
                        ModelState.AddModelError("Titre", "Ce nom est déjà utilisé par une autre categorie dans la BDD.");
                    }
                    catch (InvalidOperationException e)
                    {
                        tran.Rollback();
                        ViewData["ValidateMessage"] = e.Message;
                    }

                    // si tout ne s'est pas bien passé

                    categorie.ActionType = "Modifier";
                    categorie.TitreAction = "Modifier le categorie : " + categorie.Nom;
                    return View("Editer", categorie); // je retourne la vue Editer en lui donnant mon ViewModel
                }
            }
        }



    }
}
