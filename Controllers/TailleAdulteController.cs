using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RevendTout.Models;
using RevendTout.ViewModels;

namespace RevendTout.Controllers
{
    public class TailleAdulteController : Controller
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
        public TailleAdulteController(IConfiguration configuration)
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
            string query = "SELECT * FROM taille_adultes";
            List<TailleAdulte> taille_adultes;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                taille_adultes = connexion.Query<TailleAdulte>(query).ToList();
            }

            return View(taille_adultes);
        }

        [HttpGet]
        public IActionResult Nouveau()
        {
            var model = new EditionTailleAdulteViewModel();

            model.ActionType = "Nouveau";
            model.TitreAction = "Ajouter une taille";
            return View("Editer", model);
        }

        [HttpPost]
        public IActionResult Nouveau([FromForm] EditionTailleAdulteViewModel tailleAdulte)
        {
            // lorsque l'on renvoie le formulaire, on récupères les informations rentrées précédement
            if (!ModelState.IsValid)
            {
                tailleAdulte.ActionType = "Nouveau";
                tailleAdulte.TitreAction = "Ajouter une tailleAdulte";
                return View("Editer", tailleAdulte);
            }

            int res;
            string queryTailleAdulte = @"INSERT INTO taille_adultes (taille) VALUES (@taille)";


            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                try
                {

                    res = connexion.Execute(queryTailleAdulte, tailleAdulte);


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

            tailleAdulte.ActionType = "Nouveau";
            tailleAdulte.TitreAction = "Ajouter une tailleAdulte";
            return View("Editer", tailleAdulte);
        }


        public IActionResult Detail(int id)
        {
            string query = @"SELECT *
                              FROM taille_adultes 
                           WHERE id=@identifiant";

            TailleAdulte taileAdultes;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                try
                {
                    taileAdultes = connexion.QuerySingle<TailleAdulte>(query, new { identifiant = id });
                }
                catch (System.Exception)
                {
                    return NotFound();
                }

            }
            return View(taileAdultes);
        }


        [HttpGet] //décorateur 
        public IActionResult Modifier([FromRoute] int id)
        {
            // récupération de la taille à modifier
            string query = "SELECT * FROM taille_adultes WHERE id = @id";

            TailleAdulte tailleAdulte; // je vais récupèrer une catégorie


            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                tailleAdulte = connexion.QueryFirstOrDefault<TailleAdulte>(query, new { id = id }); // j'ai ma tailleAdulte               
            }

            // si l'utilisateur veut modifier une tailleAdulte qui n'existe pas, on aura null
            if (tailleAdulte == null)
            {
                return NotFound(); // erreur 404
            }

            var model = new EditionTailleAdulteViewModel();
            // je met les données de ma tailleAdulte dans mon viewModel
            model.Taille = tailleAdulte.Taille;


            model.ActionType = "Modifier";
            model.TitreAction = "Modifier la taille : " + model.Taille;
            return View("Editer", model); // je retourne la vue Editer en lui donnant mon ViewModel
        }


        [HttpPost]
        public IActionResult Modifier([FromForm] EditionTailleAdulteViewModel tailleAdulte)
        {
            //Verifier si le modèle est valide, si c'est pas le cas on renvoie le formulaire, on réuccpères les informations rentrées précédement
            if (!ModelState.IsValid)
            {
                tailleAdulte.ActionType = "Modifier";
                tailleAdulte.TitreAction = "Modifier la taille : " + tailleAdulte.Taille;
                return View("Editer", tailleAdulte); // je retourne la vue Editer en lui donnant mon ViewModel
            }


            string queryProduit = "UPDATE taille_adultes SET taille=@taille WHERE id=@id; ";

            int resUpdateTaille;

            using (var connexion = new NpgsqlConnection(_connexionString)) // ouvre connexion à la BDD
            {
                // on fait une transaction car on à plusieurs requettes, si on en avait qu'une il n'y en aurait pas besoin
                connexion.Open(); // j'ouvre la connexion de la transaction

                using (var tran = connexion.BeginTransaction()) // créer une transaction, (commence la transaction)
                {   // bloc try=> on essaye
                    try
                    {
                        /* update de la categorie */
                        resUpdateTaille = connexion.Execute(queryProduit, tailleAdulte);

                        if (resUpdateTaille != 1)
                        {
                            throw new InvalidOperationException("La modification de la taille à échoué. Veuillez réessayer plus tard.");
                        }
                        else
                        {
                            tran.Commit();
                            TempData["ValidateMessage"] = "taille modifié avec succès !";

                            return RedirectToAction("Detail", new { id = tailleAdulte.Id });
                        }
                    }
                    catch (PostgresException e) when (e.MessageText.Contains("taille_adultes_unique")) // violation de contrainte d'unicité sur le titre || _unique veut dire key primaire
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

                    tailleAdulte.ActionType = "Modifier";
                    tailleAdulte.TitreAction = "Modifier la taille adulte : " + tailleAdulte.Taille;
                    return View("Editer", tailleAdulte); // je retourne la vue Editer en lui donnant mon ViewModel
                }
            }
        }

        public IActionResult Supprimer(int id)
        {
            string queryDeleteProduitTaille = "DELETE FROM produit_tailles_adultes WHERE taille_adultes = @id";
            string queryDeleteTaille = "DELETE FROM taille_adultes WHERE id = @id";
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
