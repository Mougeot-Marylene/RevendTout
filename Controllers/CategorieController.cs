using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RevendTout.Models;
using RevendTout.ViewModels;

namespace RevendTout.Controllers
{
    public class CategorieController : Controller
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
            return View(model);
        }

        [HttpPost]
        public IActionResult Nouveau([FromForm] EditionCategorieViewModel categorie)
        {
            // lorsque l'on renvoie le formulaire, on récupères les informations rentrées précédement
            if (!ModelState.IsValid)
            {
                return View(categorie);
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
                        throw new InvalidOperationException("L'insertion du produit à échoué. Veuillez réessayer plus tard.");
                    }
                }
                catch (InvalidOperationException c)
                {
                    ViewData["ValidateMessage"] = c.Message;
                }

            }

            return View(categorie);
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
    }
}
