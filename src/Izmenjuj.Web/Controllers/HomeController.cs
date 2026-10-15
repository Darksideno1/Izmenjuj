using Microsoft.AspNetCore.Mvc;

namespace Izmenjuj.Web.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Error() => View("Error");
}
