using Microsoft.AspNetCore.Mvc;

namespace AuthManagerSample.Mvc.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
