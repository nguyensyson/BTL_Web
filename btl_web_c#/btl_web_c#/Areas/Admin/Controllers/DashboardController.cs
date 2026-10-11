using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.ViewModels.Admin;

namespace RestaurantOrdering.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Tổng quan";
        ViewData["Breadcrumb"] = "Tổng quan";

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Doanh thu hôm nay (bill đã thanh toán, PaidTime trong ngày)
        var todayRevenue = await db.Bills
            .Where(b => b.Status == 1 && b.PaidTime >= today && b.PaidTime < tomorrow)
            .SumAsync(b => (decimal?)b.TotalAmount) ?? 0;

        var todayBillCount = await db.Bills
            .CountAsync(b => b.Status == 1 && b.PaidTime >= today && b.PaidTime < tomorrow);

        // Bàn đang phục vụ (StatusId = 2 OCCUPIED hoặc 3 WAITING_PAYMENT)
        var occupiedCount = await db.DiningTables
            .CountAsync(t => t.StatusId == 2 || t.StatusId == 3);
        var totalTableCount = await db.DiningTables.CountAsync();

        // Đọc SLA từ cấu hình (phút)
        var slaSettingStr = await db.SystemSettings
            .Where(s => s.SettingKey == "ItemSlaMinutes")
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync() ?? "20";
        var slaMins = int.TryParse(slaSettingStr, out var s) ? s : 20;
        var slaThreshold = DateTime.UtcNow.AddMinutes(-slaMins);

        // Món quá SLA: CONFIRMED hoặc COOKING, ConfirmedTime quá ngưỡng
        var overdueCount = await db.CartItems
            .CountAsync(ci => (ci.StatusId == 2 || ci.StatusId == 3)
                              && ci.ConfirmedTime != null
                              && ci.ConfirmedTime < slaThreshold);

        // Top 10 món bán chạy hôm nay (từ BillDetails của bill đã thanh toán hôm nay)
        var topProducts = await db.BillDetails
            .Include(bd => bd.Bill)
            .Where(bd => bd.Bill.Status == 1
                      && bd.Bill.PaidTime >= today
                      && bd.Bill.PaidTime < tomorrow)
            .GroupBy(bd => bd.ProductName)
            .Select(g => new TopProductVm
            {
                ProductName = g.Key,
                TotalQty = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.LineTotal)
            })
            .OrderByDescending(x => x.TotalQty)
            .Take(10)
            .ToListAsync();

        if (topProducts.Any())
        {
            var maxQty = topProducts.Max(x => x.TotalQty);
            foreach (var p in topProducts) p.MaxQty = maxQty;
        }

        // 10 bill gần nhất
        var recentBills = await db.Bills
            .OrderByDescending(b => b.CreatedTime)
            .Take(10)
            .Select(b => new RecentBillVm
            {
                BillId = b.Id,
                BillNo = b.BillNo,
                TableNames = b.TableNames ?? "",
                TotalAmount = b.TotalAmount,
                Status = b.Status,
                CreatedTime = b.CreatedTime
            })
            .ToListAsync();

        var vm = new DashboardViewModel
        {
            TodayRevenue = todayRevenue,
            TodayBillCount = todayBillCount,
            OccupiedTableCount = occupiedCount,
            TotalTableCount = totalTableCount,
            OverdueSlaCount = overdueCount,
            TopProducts = topProducts,
            RecentBills = recentBills
        };

        return View(vm);
    }
}
