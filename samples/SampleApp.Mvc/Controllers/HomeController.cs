using Microsoft.AspNetCore.Mvc;

namespace SampleApp.Mvc.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
