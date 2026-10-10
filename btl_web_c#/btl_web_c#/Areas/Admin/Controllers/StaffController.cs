using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.Models.Entities;
using RestaurantOrdering.ViewModels.Admin;

namespace RestaurantOrdering.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class StaffController(AppDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    private const int PageSize = 10;
    private static readonly string[] Roles = ["Admin", "Waiter", "Kitchen", "Cashier"];

    // ── Index ──────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        // Lấy user kèm roles qua Identity
        var usersQuery = db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            usersQuery = usersQuery.Where(u => u.UserName!.Contains(q) || u.FullName.Contains(q));

        var total = await usersQuery.CountAsync();
        var users = await usersQuery
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync();

        // Lấy roles cho từng user
        var staffList = new List<StaffViewModel>();
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            staffList.Add(new StaffViewModel
            {
                Id = u.Id, UserName = u.UserName ?? "", FullName = u.FullName,
                Phone = u.PhoneNumber, Email = u.Email, Role = string.Join(", ", roles),
                IsActive = u.IsActive, LastLoginTime = u.LastLoginTime,
                IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow
            });
        }

        ViewBag.Q = q; ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)PageSize);
        ViewBag.Roles = Roles;
        return View(staffList);
    }

    // ── Form (GET) ─────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Form(int id = 0)
    {
        if (id == 0)
            return PartialView("_StaffForm", new StaffViewModel());

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();
        var roles = await userManager.GetRolesAsync(user);
        return PartialView("_StaffForm", new StaffViewModel
        {
            Id = user.Id, UserName = user.UserName ?? "", FullName = user.FullName,
            Phone = user.PhoneNumber, Email = user.Email,
            Role = roles.FirstOrDefault() ?? "", IsActive = user.IsActive
        });
    }

    // ── Save (POST) ────────────────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(StaffViewModel vm)
    {
        if (vm.Id == 0 && string.IsNullOrWhiteSpace(vm.Password))
            ModelState.AddModelError("Password", "Mật khẩu là bắt buộc khi tạo mới.");

        if (!ModelState.IsValid) return PartialView("_StaffForm", vm);

        if (vm.Id == 0)
        {
            // Kiểm tra trùng username
            if (await userManager.FindByNameAsync(vm.UserName) != null)
            {
                ModelState.AddModelError("UserName", "Tên đăng nhập đã tồn tại.");
                return PartialView("_StaffForm", vm);
            }
            var user = new ApplicationUser
            {
                UserName = vm.UserName, FullName = vm.FullName,
                PhoneNumber = vm.Phone, Email = vm.Email,
                EmailConfirmed = true, IsActive = vm.IsActive
            };
            var result = await userManager.CreateAsync(user, vm.Password!);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
                return PartialView("_StaffForm", vm);
            }
            await userManager.AddToRoleAsync(user, vm.Role);
        }
        else
        {
            var user = await userManager.FindByIdAsync(vm.Id.ToString());
            if (user == null) return NotFound();
            user.FullName = vm.FullName; user.PhoneNumber = vm.Phone;
            user.Email = vm.Email; user.IsActive = vm.IsActive;
            await userManager.UpdateAsync(user);

            // Cập nhật role
            var currentRoles = await userManager.GetRolesAsync(user);
            await userManager.RemoveFromRolesAsync(user, currentRoles);
            await userManager.AddToRoleAsync(user, vm.Role);
        }
        return Json(new { success = true, message = vm.Id == 0 ? "Tạo nhân viên thành công." : "Cập nhật nhân viên thành công." });
    }

    // ── Toggle Active ──────────────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null) return Json(new { success = false });
        // Không cho tắt chính mình
        if (user.UserName == User.Identity!.Name)
            return Json(new { success = false, message = "Không thể vô hiệu hóa tài khoản đang đăng nhập." });
        user.IsActive = !user.IsActive;
        await userManager.UpdateAsync(user);
        return Json(new { success = true, isActive = user.IsActive });
    }

    // ── Unlock ─────────────────────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(int id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null) return Json(new { success = false });
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        return Json(new { success = true, message = "Đã mở khóa tài khoản." });
    }

    // ── Reset Password ─────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ResetPasswordForm(int id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();
        return PartialView("_ResetPasswordForm", new ResetPasswordViewModel { UserId = id });
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
    {
        if (!ModelState.IsValid) return PartialView("_ResetPasswordForm", vm);
        var user = await userManager.FindByIdAsync(vm.UserId.ToString());
        if (user == null) return NotFound();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, vm.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return PartialView("_ResetPasswordForm", vm);
        }
        return Json(new { success = true, message = "Đặt lại mật khẩu thành công." });
    }

    // ── Delete (soft) ──────────────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null) return Json(new { success = false, message = "Không tìm thấy nhân viên." });
        if (user.UserName == User.Identity!.Name)
            return Json(new { success = false, message = "Không thể xóa tài khoản đang đăng nhập." });
        user.IsDeleted = true; user.IsActive = false;
        user.UpdatedTime = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        return Json(new { success = true, message = "Đã ẩn nhân viên khỏi hệ thống." });
    }
}
