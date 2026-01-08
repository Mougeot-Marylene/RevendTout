using Microsoft.AspNetCore.Mvc;

namespace RevendTout.Controllers
{
    public class ProduitController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
