using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RevendTout.Models;
using RevendTout.ViewModels;

namespace RevendTout.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class TailleController : Controller
    {
        // attribut stockant la chaîne de connexion à la base de données
        private readonly string _connexionString;

        /// <summary>
        /// Constructeur de TailleAdulteController
        /// </summary>
        /// <param name="configuration">configuration de l'application</param>
        /// <exception cref="Exception"></exception>
        /// 
        /// configuration on recup ce qu'il  ya dans appsetting.json
        public TailleController(IConfiguration configuration)
        {
            // récupération de la chaîne de connexion dans la configuration
            _connexionString = configuration.GetConnectionString("RevendTout")!;
            // si la chaîne de connexionn'a pas été trouvé => déclenche une exception => code http 500 retourné
            if (_connexionString == null)
            {
                throw new Exception("Error : Connexion string not found ! ");
            }
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Index()
        {
            string query = "SELECT * FROM Tailles";
            List<Taille> tailles;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                tailles = connexion.Query<Taille>(query).ToList();
            }

            return View(tailles);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Nouveau()
        {
            var model = new EditionTailleViewModel();

            model.ActionType = "Nouveau";
            model.TitreAction = "Ajouter une taille";
            return View("Editer", model);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult Nouveau([FromForm] EditionTailleViewModel taille)
        {
            // lorsque l'on renvoie le formulaire, on récupères les informations rentrées précédement
            if (!ModelState.IsValid)
            {
                taille.ActionType = "Nouveau";
                taille.TitreAction = "Ajouter une taille Enfant";
                return View("Editer", taille);
            }

            int res;
            string queryTaille = @"INSERT INTO Tailles (nom) VALUES (@nom)";


            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                try
                {

                    res = connexion.Execute(queryTaille, taille);


                    if (res == 1)
                    {
                        TempData["ValidateMessage"] = "La taille à bien été ajoutée";

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        throw new InvalidOperationException("L'insertion de la taille à échoué. Veuillez réessayer plus tard.");
                    }
                }
                catch (InvalidOperationException c)
                {
                    ViewData["ValidateMessage"] = c.Message;
                }

            }

            taille.ActionType = "Nouveau";
            taille.TitreAction = "Ajouter une taille Enfant";
            return View("Editer", taille);
        }


        [Authorize(Roles = "Admin")]
        public IActionResult Detail(int id)
        {
            string query = "SELECT * FROM tailles WHERE id=@identifiant";

            Taille tailleEnfant;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                try
                {
                    tailleEnfant = connexion.QuerySingle<Taille>(query, new { identifiant = id });
                }
                catch (System.Exception)
                {
                    return NotFound();
                }

            }
            return View(tailleEnfant);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet] //décorateur 
        public IActionResult Modifier([FromRoute] int id)
        {
            // récupération de la taille à modifier
            string query = "SELECT * FROM tailles WHERE id = @id";

            Taille taille; // je vais récupèrer une catégorie


            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                taille = connexion.QueryFirstOrDefault<Taille>(query, new { id = id }); // j'ai ma taille               
            }

            // si l'utilisateur veut modifier une taille qui n'existe pas, on aura null
            if (taille == null)
            {
                return NotFound(); // erreur 404
            }

            var model = new EditionTailleViewModel();
            // je met les données de ma taille dans mon viewModel
            model.Nom = taille.Nom;


            model.ActionType = "Modifier";
            model.TitreAction = "Modifier la taille : " + model.Nom;
            return View("Editer", model); // je retourne la vue Editer en lui donnant mon ViewModel
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult Modifier([FromForm] EditionTailleViewModel taille)
        {
            //Verifier si le modèle est valide, si c'est pas le cas on renvoie le formulaire, on réuccpères les informations rentrées précédement
            if (!ModelState.IsValid)
            {
                taille.ActionType = "Modifier";
                taille.TitreAction = "Modifier la taille : " + taille.Nom;
                return View("Editer", taille); // je retourne la vue Editer en lui donnant mon ViewModel
            }


            string queryProduit = "UPDATE tailles SET nom=@nom WHERE id=@id; ";

            int resUpdateTaille;

            using (var connexion = new NpgsqlConnection(_connexionString)) // ouvre connexion à la BDD
            {
                // on fait une transaction car on à plusieurs requettes, si on en avait qu'une il n'y en aurait pas besoin
                connexion.Open(); // j'ouvre la connexion de la transaction

                using (var tran = connexion.BeginTransaction()) // créer une transaction, (commence la transaction)
                {   // bloc try=> on essaye
                    try
                    {
                        /* update de la taille */
                        resUpdateTaille = connexion.Execute(queryProduit, taille);

                        if (resUpdateTaille != 1)
                        {
                            throw new InvalidOperationException("La modification de la taille à échoué. Veuillez réessayer plus tard.");
                        }
                        else
                        {
                            tran.Commit();
                            TempData["ValidateMessage"] = "taille modifié avec succès !";

                            return RedirectToAction("Detail", new { id = taille.Id });
                        }
                    }
                    catch (PostgresException e) when (e.MessageText.Contains("taille_unique")) // violation de contrainte d'unicité sur le titre || _unique veut dire key primaire
                    {
                        tran.Rollback();
                        ModelState.AddModelError("Titre", "Cette taille est déjà utilisée.");
                    }
                    catch (InvalidOperationException e)
                    {
                        tran.Rollback();
                        ViewData["ValidateMessage"] = e.Message;
                    }

                    // si tout ne s'est pas bien passé

                    taille.ActionType = "Modifier";
                    taille.TitreAction = "Modifier la taille adulte : " + taille.Nom;
                    return View("Editer", taille); // je retourne la vue Editer en lui donnant mon ViewModel
                }
            }
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Supprimer(int id)
        {
            string queryDeleteProduitTaille = "DELETE FROM produit_tailles WHERE taille_id = @id";
            string queryDeleteTaille = "DELETE FROM tailles WHERE id = @id";
            int res;
            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                // créer une transaction 
                connexion.Open();
                // ouvre une connexion pour la transaction
                using (var tran = connexion.BeginTransaction())
                {
                    // bloc essaie
                    try
                    {
                        // suppresion des produit_tailles_adultes
                        connexion.Execute(queryDeleteProduitTaille, new { id = id });
                        // suppression des taille_adultes
                        res = connexion.Execute(queryDeleteTaille, new { id = id });

                        if (res == 1)
                        {
                            tran.Commit();
                            TempData["ValidateMessage"] = "La taille à été suppprimée avec succes";
                            return RedirectToAction("Index");
                        }
                        else
                        {
                            tran.Rollback();
                            return NotFound();
                        }

                    }
                    catch (Exception)
                    {
                        tran.Rollback();
                        throw new InvalidOperationException("La suppression de la taille à échouée. Veuillez réessayer plus tard.");
                    }
                }

            }

        }

    }
}

