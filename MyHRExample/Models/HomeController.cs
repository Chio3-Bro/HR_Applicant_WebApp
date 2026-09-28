using Microsoft.AspNetCore.Mvc;

namespace MyHRExample.Models
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
