using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RevendTout.ViewModels;

namespace RevendTout.Controllers
{

    [AutoValidateAntiforgeryToken]
    public class ImageController : Controller
    {
        // attribut stockant la chaîne de connexion à la base de données
        private readonly string _connexionString;

        private static readonly string[] _permittedExtensions =
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };


        /// <summary>
        /// Constructeur de CategoriesController
        /// </summary>
        /// <param name="configuration">configuration de l'application</param>
        /// <exception cref="Exception"></exception>
        /// 
        /// configuration on recup ce qu'il  ya dans appsetting.json
        public ImageController(IConfiguration configuration)
        {
            // récupération de la chaîne de connexion dans la configuration
            _connexionString = configuration.GetConnectionString("RevendTout")!;
            // si la chaîne de connexionn'a pas été trouvé => déclenche une exception => code http 500 retourné
            if (_connexionString == null)
            {
                throw new Exception("Error : Connexion string not found ! ");
            }
        }


        [HttpGet]
        public IActionResult Ajouter()
        {
            var model = new EditionImageViewmodel();

            return View(model);
        }

        [HttpPost]
        public IActionResult Ajouter([FromForm] EditionImageViewmodel image)
        {
            // validation fichier Image
            if (image.FichierImage != null)
            {
                var ext = Path.GetExtension(image.FichierImage.FileName).ToLowerInvariant(); // créer le path (chemin de l'image), on le met en miniscule avec ToLowerInvariant

                // si l'extention n'existe pas ou si le fichier ne contient pas d'extention
                if (string.IsNullOrEmpty(ext) || !_permittedExtensions.Contains(ext))
                {
                    ModelState.AddModelError("Image", "Ce type de fichier n'est pas accepté.");
                    return View("Editer", image);
                }
            }

            int res;
            string queryImage = @"INSERT INTO Images (produit_id, url, description)  VALUES (@IdProduit, @url, @Description )";

            // gestion du lien du fichier
            string? filePath = null;

            // si l'image n'est pas null est que le fichier existe (qu'il a une taille plus grande que 0)
            if (image.FichierImage != null && image.FichierImage.Length>0)
            {
                // creation d'un nom de fichier unique
                filePath = Path.Combine("/images/Produits/",
                    Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + Path.GetExtension(image.FichierImage.FileName)).ToString();
                using (var stream = System.IO.File.Create("wwwroot" + filePath))
                {
                    image.FichierImage.CopyTo(stream);
                }
               
            }

            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                try
                {

                    res = connexion.Execute(queryImage, new {IdProduit=image.IdProduit, url = filePath, Description=image.Description});


                    if (res == 1)
                    {
                        TempData["ValidateMessage"] = "L'image à bien été ajoutée";

                        return RedirectToRoute(new
                        {
                            controller = "produit",
                            action = "Admin_Index_Detail",
                            id =  image.IdProduit
                        });

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

            return RedirectToRoute(new
            {
                controller = "produit",
                action = "Admin_Index_Detail",
                id = image.IdProduit
            });
        }


    }
}
