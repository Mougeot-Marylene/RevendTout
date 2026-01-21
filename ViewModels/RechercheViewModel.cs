using Microsoft.AspNetCore.Mvc.Rendering;

namespace RevendTout.ViewModels
{
    public class RechercheViewModel
    {
        // Propriété pour le terme de recherche saisi par l'utilisateur
        public string? TermeRecherche { get; set; }
        // Liste des catégories affichées dans un dropdown (SelectListItem est utilisé pour créer une liste déroulante en Razor)
        public List<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
        // ID de la catégorie sélectionnée
        public int? CategorieId { get; set; }
    }
}
