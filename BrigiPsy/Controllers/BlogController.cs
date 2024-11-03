using Microsoft.AspNetCore.Mvc;

namespace BrigiPsy.Controllers
{
    public class BlogController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
