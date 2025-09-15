using Microsoft.AspNetCore.Mvc;

namespace FUEN42Team3.Frontend.Controllers
{
    public class AboutController : Controller
    {
        public IActionResult Index()
        {
            return View();
            // The view will be located at Views/About/Index.cshtml
        }
    }
}
