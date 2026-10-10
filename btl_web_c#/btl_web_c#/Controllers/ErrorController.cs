using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantOrdering.Controllers;

public class ErrorController : Controller
{
    [HttpGet("/Error/{code:int}")]
    public IActionResult Index(int code)
    {
        ViewData["ErrorCode"] = code;
        return View("Error");
    }

    [HttpGet("/Error/500")]
    public IActionResult ServerError()
    {
        var exFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        ViewData["ErrorCode"] = 500;
        ViewData["ExceptionMessage"] = exFeature?.Error?.Message;
        return View("Error");
    }
}
