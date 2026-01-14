using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RevendTout.Models;

namespace RevendTout.Controllers
{
    public class UtilisateurController : Controller
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
        public UtilisateurController(IConfiguration configuration)
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
            string query = "SELECT * FROM Utilisateurs";
            List<Utilisateur> utilisateur;

             using (var connexion = new NpgsqlConnection(_connexionString))
            {
                utilisateur = connexion.Query<Utilisateur>(query).ToList();
            }
            
             return View(utilisateur);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Detail(int id)
        {
            string query = @"SELECT 
                              u.id,u.nom,u.prenom,u.email,
                               a.id,a.numero_rue, a.nom_rue, a.ville, a.code_postal,a.pays
                              FROM Utilisateurs u
                            LEFT JOIN adresses a ON u.adresse_id = a.id
                           WHERE u.id=@identifiant";
            Utilisateur? utilisateurs;

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                utilisateurs = connexion.Query<Utilisateur, Adresse, Utilisateur>(query, (utilisateur, adresse) =>
                    {
                        utilisateur.Adresse = adresse;
                        return utilisateur;
                    },
                    new { identifiant = id },
                    splitOn: "id"
                ).FirstOrDefault();
            }

            return View(utilisateurs);
        }

    }
}
