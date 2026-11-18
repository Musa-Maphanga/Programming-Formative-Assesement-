using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : Controller
{
    /// <summary>
    /// The front door sends each role where it needs to be: an organiser to
    /// their dashboard, a participant to the race listing, and a visitor who is
    /// not signed in to the same public listing.
    /// </summary>
    public IActionResult Index()
    {
        var session = HttpContext.Session;

        if (session.IsOrganiser()) return RedirectToAction("Index", "Organiser");

        return RedirectToAction("Index", "Events");
    }

    [HttpGet("/Home/StatusCode")]
    public IActionResult StatusCodePage(int code)
    {
        ViewData["Code"] = code;
        ViewData["Title"] = code == 404 ? "Page not found" : "Something went wrong";
        return View("StatusCode");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
        => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
