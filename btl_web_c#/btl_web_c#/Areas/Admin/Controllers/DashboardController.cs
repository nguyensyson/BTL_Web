using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantOrdering.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Tổng quan";
        ViewData["Breadcrumb"] = "Tổng quan";
        return View();
    }
}
