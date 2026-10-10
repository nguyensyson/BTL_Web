using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantOrdering.Controllers;

public class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        if (User.IsInRole("Admin"))
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        if (User.IsInRole("Kitchen"))
            return RedirectToAction("Index", "Board", new { area = "Kitchen" });
        if (User.IsInRole("Cashier"))
            return RedirectToAction("Index", "Bills", new { area = "Cashier" });

        return RedirectToAction("Index", "Tables", new { area = "Waiter" });
    }
}
