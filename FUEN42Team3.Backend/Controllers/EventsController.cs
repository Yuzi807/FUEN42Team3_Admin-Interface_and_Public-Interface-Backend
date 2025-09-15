using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUEN42Team3.Backend.Controllers
{
    [Authorize]
    public class EventsController : Controller
    {
        public IActionResult List()
        {
            return View();
        }
    }
}
