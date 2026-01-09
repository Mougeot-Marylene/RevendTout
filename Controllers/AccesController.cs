using Microsoft.AspNetCore.Mvc;
using Dapper;
using Npgsql;
using RevendTout.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using RevendTout.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;
using System.Text;
using System.Net.Mail;
using System.Net;


namespace RevendTout.Controllers
{
    public class AccesController : Controller
    {// attribut stockant la chaîne de connexion à la base de données
        private readonly string _connexionString;

        // instance de PasswordHasher pour le hachage des mots de passe
        private static PasswordHasher<string> PH = new PasswordHasher<string>();
        // Clé de chiffrement pour les tokens (doit être stockée de manière sécurisée)
        private byte[] _tokenKey;
        // Vecteur d'initialisation pour le chiffrement (doit être stocké de manière sécurisée)
        private byte[] _initializationVector;
        // Addresse IP du serveur SMTP pour l'envoi des emails
        private string _SmtpServerIp;
        // Port du serveur SMTP
        private int _SmtpServerPort;
        // Mot De passe du serveur SMTP
        private string _SmtpServerPassword;
        // URL du serveur de l'application
        private string _ApplicationUrl;
        // Port du serveur de l'application
        private int _ApplicationPort = 5203;
        // Adresse email de l'expéditeur des emails
        private string _SenderEmail;

        // Clé pour les messages de validation
        private const string ValidateMessageKey = "ValidateMessage";

        /// <summary>
        /// Constructeur de AccessController
        /// </summary>
        /// <param name="configuration">configuration de l'application</param>
        /// <exception cref="Exception"></exception>
        public AccesController(IConfiguration configuration)
        {
            // récupération de la chaîne de connexion dans la configuration
            _connexionString = configuration.GetConnectionString("RevendTout")!;
            // si la chaîne de connexionn'a pas été trouvé => déclenche une exception => code http 500 retourné
            if (_connexionString == null)
            {
                throw new InvalidOperationException("Error : Connexion string not found ! ");
            }
            // Récupération de la clé de chiffrement et du vecteur d'initialisation depuis la configuration
            try
            {
                _tokenKey = Encoding.ASCII.GetBytes(configuration.GetValue<string>("TokenKey"));
            }
            catch (ArgumentNullException)
            {
                throw new InvalidOperationException("Error : Token key not found ! ");
            }
            try
            {
                _initializationVector = Encoding.ASCII.GetBytes(configuration.GetValue<string>("InitializationVector"));
            }
            catch (ArgumentNullException)
            {
                throw new InvalidOperationException("Error : Initialization vector not found ! ");
            }
            // Récupération des paramètres du serveur SMTP depuis la configuration
            _SmtpServerIp = configuration.GetValue<string>("SmtpServer:Ip");
            _SmtpServerPort = configuration.GetValue<int>("SmtpServer:Port");
            _SmtpServerPassword = configuration.GetValue<string>("SmtpServer:Password");
            if (_SmtpServerIp == null || _SmtpServerPort == 0 || _SmtpServerPassword == null)
            {
                throw new InvalidOperationException("Error : SMTP server configuration not found ! ");
            }
            // Récupération des paramètres de l'application depuis la configuration
            _ApplicationUrl = configuration.GetValue<string>("Application:Url");
            _ApplicationPort = configuration.GetValue<int>("Application:Port");
            _SenderEmail = configuration.GetValue<string>("Application:Email");
            if (_ApplicationUrl == null || _ApplicationPort == 0 || _SenderEmail == null)
            {
                throw new InvalidOperationException("Error : Application configuration not found ! ");
            }
        }

        public IActionResult Connexion()
        {
            var model = new InscriptionViewModel();
            return View(model);
        }

        /// <summary>
        /// Retourne le formulaire d'inscription
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Inscription()
        {
            var model = new InscriptionViewModel();
            return View(model);
        }

        /// <summary>
        /// Traite le formulaire d'inscription
        /// </summary>
        /// <param name="utilisateur"></param>
        /// <returns></returns>
        [HttpPost]
        public IActionResult Inscription([FromForm] InscriptionViewModel utilisateur)
        {
            // Vérifie si le modèle est valide
            if (!ModelState.IsValid)
            {
                return View(utilisateur); // Retourne la vue avec le modèle en cas d'erreur
            }


            // Requête pour compter le nombre d'utilisateurs avec l'email fourni
            string query = "SELECT COUNT(*) FROM Utilisateurs WHERE email = @email";
            using (var connexion = new NpgsqlConnection(_connexionString))
            {
                connexion.Open();
                using (var transaction = connexion.BeginTransaction())
                {

                    // Exécute la requête et récupère le nombre d'utilisateurs
                    int nbUtilisateurs = connexion.QuerySingle<int>(query, new { email = utilisateur.Email });

                    // Vérifie si l'email est déjà utilisé
                    if (nbUtilisateurs > 0)
                    {
                        ModelState.AddModelError("Email", "Email déjà utilisé"); // Ajoute une erreur au modèle
                        return View(); // Retourne la vue avec l'erreur
                    }
                    else
                    {
                        // Requête pour insérer un nouvel utilisateur
                        string insertQuery = "INSERT INTO Utilisateurs (nom,prenom,email,mdp,emailverificationtoken) VALUES (@nom,@prenom,@email,@password,@token)";
                        // Génère un token de vérification d'email 
                        byte[] time = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());// on ajoute la date aujourd'hui à l'adresse mail pour être sur que le token soit unique
                        byte[] key = Guid.NewGuid().ToByteArray();
                        string token = Convert.ToBase64String(time.Concat(key).ToArray()); // on transforme le token en chaîne de caractère
                        string encryptedToken; // chiffre le token
                        using (Aes myAes = Aes.Create())
                        {
                            myAes.Key = _tokenKey;
                            myAes.IV = _initializationVector;

                            // EncryptStringToBytes_Aes => methode qui chiffre le token
                            encryptedToken = Convert.ToBase64String(EncryptStringToBytes_Aes(token, myAes.Key, myAes.IV)); // on recupere le token chiffre
                        }
                        // Hache le mot de passe de l'utilisateur
                        string HashedPassword = PH.HashPassword(utilisateur.Email, utilisateur.MotDePasse); // on recup le mdp haché

                        // Exécute la requête d'insertion et récupère le nombre de lignes affectées
                        int RowsAffected = connexion.Execute(insertQuery, new { nom = utilisateur.Nom, prenom = utilisateur.Prenom, email = utilisateur.Email, password = HashedPassword, token = encryptedToken }, transaction: transaction);
                        if (RowsAffected == 1)
                        {
                            // Création du lien de confirmation d'email
                            UriBuilder builder = new UriBuilder();
                            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                            {
                                builder.Scheme = "http";
                            }
                            else
                            {
                                builder.Scheme = "https";
                            }
                            builder.Host = _ApplicationUrl;
                            builder.Port = _ApplicationPort;
                            builder.Path = $"/Acces/ConfirmEmail";
                            builder.Query = $"email={Uri.EscapeDataString(utilisateur.Email)}&token={Uri.EscapeDataString(encryptedToken)}";

                            // Envoi de l'email de confirmation
                            var mail = new MailMessage();
                            mail.From = new MailAddress(_SenderEmail);
                            mail.To.Add(new MailAddress(utilisateur.Email));
                            mail.Subject = "Confirmation de votre adresse email";
                            mail.Body = "<a href=\"" + builder.Uri.ToString() + "\">Confirmer votre email</a>";
                            mail.IsBodyHtml = true;

                            using (var smtp = new SmtpClient(_SmtpServerIp, _SmtpServerPort))
                            {
                                //Credentials => se connecter au server SMTP
                                smtp.Credentials = new NetworkCredential(_SenderEmail, _SmtpServerPassword);
                                // EnableSsl => ce qui permet de chiffrer 
                                smtp.EnableSsl = false;
                                // try => on essaye envoyer le mail
                                try
                                {
                                    smtp.Send(mail);
                                }
                                catch (Exception e)
                                {
                                    transaction.Rollback();
                                    ViewData[ValidateMessageKey] = "Erreur lors du processus d'inscription, veuillez réessayer.";
                                    return View(utilisateur); // Retourne la vue avec l'erreur
                                }
                            }
                            transaction.Commit();
                            // Si l'insertion réussit, affiche un message de succès
                            TempData[ValidateMessageKey] = "Votre inscription est réussie. Veuillez vérifier votre email pour activer votre compte.";
                            return RedirectToAction("Connexion"); // Redirige vers la page de connexion
                        }
                        else
                        {
                            transaction.Rollback();
                            // Si l'insertion échoue, affiche un message d'erreur
                            ViewData[ValidateMessageKey] = "Erreur lors du processus d'inscription, veuillez réessayer.";
                            return View(); // Retourne la vue avec l'erreur
                        }
                    }
                }
            }
        }

        static byte[] EncryptStringToBytes_Aes(string plainText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException("plainText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("IV");
            byte[] encrypted;

            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create an encryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            //Write all data to the stream.
                            swEncrypt.Write(plainText);
                        }
                    }

                    encrypted = msEncrypt.ToArray();
                }
            }

            // Return the encrypted bytes from the memory stream.
            return encrypted;
        }
    }
}
