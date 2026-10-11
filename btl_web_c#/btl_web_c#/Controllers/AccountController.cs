using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RestaurantOrdering.Models.Entities;
using RestaurantOrdering.Services;
using RestaurantOrdering.ViewModels;

namespace RestaurantOrdering.Controllers;

public class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAuditService auditService) : Controller
{
    // ── GET /Account/Login ───────────────────────────────────────────────
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleHome();

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // ── POST /Account/Login ──────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        // Kiểm tra user tồn tại và chưa bị xóa mềm / vô hiệu hóa
        var user = await userManager.FindByNameAsync(model.UserName);
        if (user == null || user.IsDeleted || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            model.UserName, model.Password,
            model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Cập nhật thời gian đăng nhập
            user.LastLoginTime = DateTime.UtcNow;
            await userManager.UpdateAsync(user);

            // Ghi audit log đăng nhập
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            await auditService.LogLoginAsync(user.Id, ip);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToRoleHome();
        }

        if (result.IsLockedOut)
        {
            var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
            var minutes = lockoutEnd.HasValue
                ? (int)Math.Ceiling((lockoutEnd.Value - DateTimeOffset.UtcNow).TotalMinutes)
                : 15;
            ModelState.AddModelError(string.Empty,
                $"Tài khoản tạm khóa. Vui lòng thử lại sau {minutes} phút.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
        return View(model);
    }

    // ── POST /Account/Logout ─────────────────────────────────────────────
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    // ── GET /Account/ChangePassword ──────────────────────────────────────
    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword() => View();

    // ── POST /Account/ChangePassword ─────────────────────────────────────
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login");

        var result = await userManager.ChangePasswordAsync(
            user, model.CurrentPassword, model.NewPassword);

        if (result.Succeeded)
        {
            await signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Đổi mật khẩu thành công.";
            return RedirectToAction("ChangePassword");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    // ── Helper: redirect theo vai trò ────────────────────────────────────
    private IActionResult RedirectToRoleHome()
    {
        if (User.IsInRole("Admin"))
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        if (User.IsInRole("Kitchen"))
            return RedirectToAction("Index", "Board", new { area = "Kitchen" });
        if (User.IsInRole("Cashier"))
            return RedirectToAction("Index", "Bills", new { area = "Cashier" });
        // Waiter (default)
        return RedirectToAction("Index", "Tables", new { area = "Waiter" });
    }
}
