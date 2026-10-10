using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.ViewModels.Admin;

namespace RestaurantOrdering.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class SettingsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Cấu hình hệ thống";
        ViewData["Breadcrumb"] = "Cấu hình";

        var settings = await db.SystemSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
        var vm = new SettingsViewModel
        {
            TaxRate = Parse(settings, "TaxRate", 0.08m),
            ServiceFeeRate = Parse(settings, "ServiceFeeRate", 0.05m),
            ItemSlaMinutes = (int)Parse(settings, "ItemSlaMinutes", 15m),
            ReservationHoldMinutes = (int)Parse(settings, "ReservationHoldMinutes", 20m),
            RestaurantName = settings.GetValueOrDefault("RestaurantName", "Nhà hàng"),
            RestaurantAddress = settings.GetValueOrDefault("RestaurantAddress", ""),
            RestaurantPhone = settings.GetValueOrDefault("RestaurantPhone", "")
        };
        return View(vm);
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SettingsViewModel vm)
    {
        ViewData["Title"] = "Cấu hình hệ thống";
        ViewData["Breadcrumb"] = "Cấu hình";
        if (!ModelState.IsValid) return View(vm);

        await SetAsync("TaxRate", vm.TaxRate.ToString("F4"));
        await SetAsync("ServiceFeeRate", vm.ServiceFeeRate.ToString("F4"));
        await SetAsync("ItemSlaMinutes", vm.ItemSlaMinutes.ToString());
        await SetAsync("ReservationHoldMinutes", vm.ReservationHoldMinutes.ToString());
        await SetAsync("RestaurantName", vm.RestaurantName);
        await SetAsync("RestaurantAddress", vm.RestaurantAddress);
        await SetAsync("RestaurantPhone", vm.RestaurantPhone);

        TempData["Success"] = "Lưu cấu hình thành công.";
        return RedirectToAction("Index");
    }

    private static decimal Parse(Dictionary<string, string> d, string key, decimal def)
        => d.TryGetValue(key, out var v) && decimal.TryParse(v, out var r) ? r : def;

    private async Task SetAsync(string key, string value)
    {
        var s = await db.SystemSettings.FirstOrDefaultAsync(x => x.SettingKey == key);
        if (s == null) db.SystemSettings.Add(new Models.Entities.SystemSetting { SettingKey = key, SettingValue = value });
        else s.SettingValue = value;
        await db.SaveChangesAsync();
    }
}
