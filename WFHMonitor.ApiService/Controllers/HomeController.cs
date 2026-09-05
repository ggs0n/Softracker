using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Models;

namespace WFHMonitor.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return Redirect("/app/");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
