using Microsoft.AspNetCore.Mvc;

namespace BizPulse.AI.POC.Controllers;

public class DatabaseSchemaController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}