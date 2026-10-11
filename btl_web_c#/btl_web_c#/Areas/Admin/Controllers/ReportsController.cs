using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.ViewModels.Admin;
using System.Text;

namespace RestaurantOrdering.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ReportsController(AppDbContext db) : Controller
{
    // ── GET /Admin/Reports ──────────────────────────────────────────
    public IActionResult Index()
    {
        ViewData["Title"] = "Báo cáo";
        ViewData["Breadcrumb"] = "Báo cáo";
        return View();
    }

    // ── GET /Admin/Reports/Revenue ──────────────────────────────────
    public async Task<IActionResult> Revenue([FromQuery] ReportFilterVm filter)
    {
        var from = filter.FromDate.Date;
        var to = filter.ToDate.Date.AddDays(1);

        var bills = await db.Bills
            .Where(b => b.Status == 1 && b.PaidTime >= from && b.PaidTime < to)
            .Select(b => new { b.TotalAmount, b.PaidTime })
            .ToListAsync();

        // Doanh thu theo từng ngày
        var daily = bills
            .GroupBy(b => DateOnly.FromDateTime(b.PaidTime!.Value.ToLocalTime()))
            .Select(g => new DailyRevenueVm
            {
                Date = g.Key,
                BillCount = g.Count(),
                TotalRevenue = g.Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.Date)
            .ToList();

        // Điền các ngày không có doanh thu với 0
        for (var d = filter.FromDate.Date; d <= filter.ToDate.Date; d = d.AddDays(1))
        {
            var key = DateOnly.FromDateTime(d);
            if (!daily.Any(x => x.Date == key))
                daily.Add(new DailyRevenueVm { Date = key, BillCount = 0, TotalRevenue = 0 });
        }
        daily = daily.OrderBy(x => x.Date).ToList();

        var maxRev = daily.Any() ? (int)daily.Max(x => x.TotalRevenue) : 1;
        foreach (var r in daily) r.MaxRevenue = maxRev;

        var vm = new RevenueReportVm
        {
            Filter = filter,
            TotalRevenue = bills.Sum(b => b.TotalAmount),
            TotalBills = bills.Count,
            AvgBillValue = bills.Count > 0 ? bills.Sum(b => b.TotalAmount) / bills.Count : 0,
            Daily = daily
        };

        ViewData["Title"] = "Báo cáo doanh thu";
        ViewData["Breadcrumb"] = "Báo cáo > Doanh thu";
        return View(vm);
    }

    // ── GET /Admin/Reports/TopProducts ─────────────────────────────
    public async Task<IActionResult> TopProducts([FromQuery] ReportFilterVm filter)
    {
        var from = filter.FromDate.Date;
        var to = filter.ToDate.Date.AddDays(1);

        var items = await db.BillDetails
            .Include(bd => bd.Bill)
            .Include(bd => bd.CartItem).ThenInclude(ci => ci!.Product).ThenInclude(p => p!.Category)
            .Where(bd => bd.Bill.Status == 1
                      && bd.Bill.PaidTime >= from
                      && bd.Bill.PaidTime < to)
            .GroupBy(bd => new
            {
                bd.ProductName,
                CategoryName = bd.CartItem != null && bd.CartItem.Product != null && bd.CartItem.Product.Category != null
                    ? bd.CartItem.Product.Category.Name
                    : "Khác"
            })
            .Select(g => new TopProductItemVm
            {
                ProductName = g.Key.ProductName,
                CategoryName = g.Key.CategoryName,
                TotalQty = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.LineTotal)
            })
            .OrderByDescending(x => x.TotalQty)
            .Take(20)
            .ToListAsync();

        if (items.Any())
        {
            var max = items.Max(x => x.TotalQty);
            foreach (var i in items) i.MaxQty = max;
        }

        var vm = new TopProductReportVm { Filter = filter, Items = items };
        ViewData["Title"] = "Món bán chạy";
        ViewData["Breadcrumb"] = "Báo cáo > Món bán chạy";
        return View(vm);
    }

    // ── GET /Admin/Reports/PeakHours ────────────────────────────────
    public async Task<IActionResult> PeakHours([FromQuery] ReportFilterVm filter)
    {
        var from = filter.FromDate.Date;
        var to = filter.ToDate.Date.AddDays(1);

        var bills = await db.Bills
            .Where(b => b.Status == 1 && b.PaidTime >= from && b.PaidTime < to)
            .Select(b => new { b.TotalAmount, b.PaidTime })
            .ToListAsync();

        var byHour = bills
            .GroupBy(b => b.PaidTime!.Value.ToLocalTime().Hour)
            .Select(g => new PeakHourItemVm
            {
                Hour = g.Key,
                BillCount = g.Count(),
                Revenue = g.Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.Hour)
            .ToList();

        // Điền các giờ 0-23 còn thiếu
        for (var h = 0; h < 24; h++)
        {
            if (!byHour.Any(x => x.Hour == h))
                byHour.Add(new PeakHourItemVm { Hour = h, BillCount = 0, Revenue = 0 });
        }
        byHour = byHour.OrderBy(x => x.Hour).ToList();

        var maxBills = byHour.Any() ? byHour.Max(x => x.BillCount) : 1;
        foreach (var h in byHour) h.MaxBills = maxBills;

        var vm = new PeakHourReportVm { Filter = filter, Items = byHour };
        ViewData["Title"] = "Giờ cao điểm";
        ViewData["Breadcrumb"] = "Báo cáo > Giờ cao điểm";
        return View(vm);
    }

    // ── GET /Admin/Reports/Kitchen ──────────────────────────────────
    public async Task<IActionResult> Kitchen([FromQuery] ReportFilterVm filter)
    {
        var from = filter.FromDate.Date;
        var to = filter.ToDate.Date.AddDays(1);

        // Lấy các CartItem có đủ cả ConfirmedTime và ReadyTime trong khoảng
        var items = await db.CartItems
            .Where(ci => ci.ConfirmedTime >= from && ci.ConfirmedTime < to
                      && ci.ReadyTime != null)
            .Select(ci => new
            {
                ci.ProductName,
                CookingMinutes = EF.Functions.DateDiffMinute(ci.ConfirmedTime!.Value, ci.ReadyTime!.Value),
                ci.StatusId
            })
            .ToListAsync();

        var servedCount = await db.CartItems
            .CountAsync(ci => ci.StatusId == 5 && ci.ConfirmedTime >= from && ci.ConfirmedTime < to);
        var cancelledCount = await db.CartItems
            .CountAsync(ci => ci.StatusId == 6 && ci.ConfirmedTime >= from && ci.ConfirmedTime < to);
        var rejectedCount = await db.CartItems
            .CountAsync(ci => ci.StatusId == 7 && ci.ConfirmedTime >= from && ci.ConfirmedTime < to);

        var avgMinutes = items.Any() ? items.Average(x => x.CookingMinutes) : 0;

        var byProduct = items
            .GroupBy(i => i.ProductName)
            .Select(g => new ProductCookingTimeVm
            {
                ProductName = g.Key,
                ItemCount = g.Count(),
                AvgMinutes = g.Average(x => x.CookingMinutes)
            })
            .OrderByDescending(x => x.AvgMinutes)
            .Take(15)
            .ToList();

        var vm = new KitchenPerformanceReportVm
        {
            Filter = filter,
            AvgCookingMinutes = avgMinutes,
            TotalServedItems = servedCount,
            TotalCancelledItems = cancelledCount,
            TotalRejectedItems = rejectedCount,
            ProductTimes = byProduct
        };

        ViewData["Title"] = "Hiệu suất bếp";
        ViewData["Breadcrumb"] = "Báo cáo > Hiệu suất bếp";
        return View(vm);
    }

    // ── GET /Admin/Reports/ExportRevenueCsv ─────────────────────────
    [HttpGet]
    public async Task<IActionResult> ExportRevenueCsv([FromQuery] ReportFilterVm filter)
    {
        var from = filter.FromDate.Date;
        var to = filter.ToDate.Date.AddDays(1);

        var bills = await db.Bills
            .Where(b => b.Status == 1 && b.PaidTime >= from && b.PaidTime < to)
            .OrderBy(b => b.PaidTime)
            .Select(b => new { b.BillNo, b.TableNames, b.SubTotal, b.ServiceFee, b.TaxAmount, b.DiscountAmount, b.TotalAmount, b.PaidTime })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Mã HĐ,Bàn,Tạm tính,Phí DV,Thuế,Giảm giá,Tổng tiền,Thời gian TT");
        foreach (var b in bills)
        {
            sb.AppendLine($"{b.BillNo},{b.TableNames},{b.SubTotal},{b.ServiceFee},{b.TaxAmount},{b.DiscountAmount},{b.TotalAmount},{b.PaidTime?.ToLocalTime():dd/MM/yyyy HH:mm}");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"doanh-thu-{filter.FromDate:yyyyMMdd}-{filter.ToDate:yyyyMMdd}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }
}
