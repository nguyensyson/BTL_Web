using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.Models.Entities;

namespace RestaurantOrdering.Areas.Waiter.Controllers;

[Area("Waiter")]
[Authorize(Policy = "CanManageOrders")]
public class ReservationsController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    // ── GET /Waiter/Reservations ─────────────────────────────────
    public async Task<IActionResult> Index(DateTime? date)
    {
        var targetDate = date ?? DateTime.Today;
        var nextDate = targetDate.AddDays(1);

        var reservations = await db.Reservations
            .Include(r => r.ReservationTables)
                .ThenInclude(rt => rt.DiningTable)
                    .ThenInclude(t => t!.Area)
            .Where(r => r.CreatedTime >= targetDate && r.CreatedTime < nextDate)
            .OrderBy(r => r.ArrivalTime)
            .ToListAsync();

        // Lấy cấu hình
        var holdSetting = await db.SystemSettings
            .Where(s => s.SettingKey == "ReservationHoldMinutes")
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync();
        var holdMinutes = int.TryParse(holdSetting, out var hm) ? hm : 20;

        // Tính thời gian đến gần và quá giờ
        var now = DateTime.UtcNow;
        foreach (var r in reservations)
        {
            if (r.Status == 0 && r.HoldUntil.HasValue && r.HoldUntil < now)
                r.Status = 3; // Quá giờ
        }
        await db.SaveChangesAsync();

        ViewData["Title"] = "Đặt bàn";
        ViewBag.TargetDate = targetDate;
        ViewBag.HoldMinutes = holdMinutes;
        return View(reservations);
    }

    // ── GET /api/waiter/reservations/available-tables ────────────
    [HttpGet("/api/waiter/reservations/available-tables")]
    public async Task<IActionResult> GetAvailableTables(int? areaId, int guestCount)
    {
        var tables = await db.DiningTables
            .Include(t => t.Area)
            .Where(t => t.StatusId == 1) // AVAILABLE
            .Where(t => t.Capacity >= guestCount)
            .Where(t => areaId == null || t.AreaId == areaId.Value)
            .OrderBy(t => t.AreaId).ThenBy(t => t.SortOrder)
            .Select(t => new { t.Id, t.Code, AreaName = t.Area.Name, t.Capacity })
            .ToListAsync();

        return Json(tables);
    }

    // ── POST /api/waiter/reservations ────────────────────────────
    [HttpPost("/api/waiter/reservations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CustomerName))
            return BadRequest(new { message = "Vui lòng nhập tên khách." });
        if (string.IsNullOrWhiteSpace(req.Phone))
            return BadRequest(new { message = "Vui lòng nhập SĐT." });
        if (req.ArrivalTime <= DateTime.UtcNow)
            return BadRequest(new { message = "Giờ đến phải lớn hơn hiện tại." });
        if (req.GuestCount < 1)
            return BadRequest(new { message = "Số khách phải ít nhất 1." });

        var holdSetting = await db.SystemSettings
            .Where(s => s.SettingKey == "ReservationHoldMinutes")
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync();
        var holdMinutes = int.TryParse(holdSetting, out var hm2) ? hm2 : 20;

        var reservation = new Reservation
        {
            CustomerName = req.CustomerName.Trim(),
            Phone = req.Phone.Trim(),
            GuestCount = req.GuestCount,
            ArrivalTime = req.ArrivalTime.ToUniversalTime(),
            HoldUntil = req.ArrivalTime.AddMinutes(holdMinutes).ToUniversalTime(),
            Status = 0, // Chờ khách
            Note = req.Note?.Trim()
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        // Thêm bàn đặt
        foreach (var tableId in req.TableIds ?? [])
        {
            var table = await db.DiningTables.FindAsync(tableId);
            if (table != null && table.StatusId == 1) // vẫn trống
            {
                db.ReservationTables.Add(new ReservationTable
                {
                    ReservationId = reservation.Id,
                    TableId = tableId
                });
                table.StatusId = 4; // RESERVED
            }
        }
        await db.SaveChangesAsync();

        return Json(new { success = true, reservationId = reservation.Id, message = "Đặt bàn thành công!" });
    }

    // ── POST /api/waiter/reservations/{id}/seat ──────────────────
    // TBL-03: Khách đã đến → mở bàn từ đặt chỗ
    [HttpPost("/api/waiter/reservations/{id}/seat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SeatCustomer(int id)
    {
        var reservation = await db.Reservations
            .Include(r => r.ReservationTables)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
            return NotFound(new { message = "Không tìm thấy đặt chỗ." });

        if (reservation.Status == 1)
            return BadRequest(new { message = "Đặt chỗ đã được nhận bàn trước đó." });
        if (reservation.Status == 2)
            return BadRequest(new { message = "Đặt chỗ đã bị hủy." });

        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Mở cart cho từng bàn đặt
        var cartCode = $"C{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
        var cart = new Cart
        {
            CartCode = cartCode,
            StatusId = 1, // OPEN
            ReservationId = reservation.Id,
            GuestCount = reservation.GuestCount,
            WaiterId = user.Id,
            OpenedTime = DateTime.UtcNow
        };
        db.Carts.Add(cart);
        await db.SaveChangesAsync();

        foreach (var rt in reservation.ReservationTables)
        {
            db.UsedTables.Add(new UsedTable
            {
                CartId = cart.Id,
                TableId = rt.TableId,
                IsPrimary = true,
                StartTime = DateTime.UtcNow
            });
            var table = await db.DiningTables.FindAsync(rt.TableId);
            if (table != null)
                table.StatusId = 2; // OCCUPIED
        }

        reservation.Status = 1; // Đã nhận bàn

        await db.SaveChangesAsync();

        // Lấy bàn đầu tiên để redirect
        var firstTableId = reservation.ReservationTables.FirstOrDefault()?.TableId;
        return Json(new
        {
            success = true,
            message = $"Đã mở bàn cho {reservation.CustomerName}.",
            cartId = cart.Id,
            tableId = firstTableId
        });
    }

    // ── POST /api/waiter/reservations/{id}/cancel ─────────────────
    [HttpPost("/api/waiter/reservations/{id}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var reservation = await db.Reservations
            .Include(r => r.ReservationTables)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
            return NotFound(new { message = "Không tìm thấy đặt chỗ." });

        if (reservation.Status == 1)
            return BadRequest(new { message = "Đặt chỗ đã nhận bàn, không thể hủy." });
        if (reservation.Status == 2)
            return BadRequest(new { message = "Đặt chỗ đã được hủy trước đó." });

        // Giải phóng bàn
        foreach (var rt in reservation.ReservationTables)
        {
            var table = await db.DiningTables.FindAsync(rt.TableId);
            if (table != null && table.StatusId == 4)
                table.StatusId = 1; // AVAILABLE
        }

        reservation.Status = 2; // Hủy
        await db.SaveChangesAsync();

        return Json(new { success = true, message = "Đã hủy đặt bàn." });
    }

    // ── POST /api/waiter/reservations/{id}/release ────────────────
    // Nhả bàn (khi quá giờ giữ)
    [HttpPost("/api/waiter/reservations/{id}/release")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Release(int id)
    {
        var reservation = await db.Reservations
            .Include(r => r.ReservationTables)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
            return NotFound();

        // Giải phóng bàn
        foreach (var rt in reservation.ReservationTables)
        {
            var table = await db.DiningTables.FindAsync(rt.TableId);
            if (table != null && table.StatusId == 4)
                table.StatusId = 1;
        }

        reservation.Status = 3; // Quá giờ
        await db.SaveChangesAsync();

        return Json(new { success = true, message = "Đã nhả bàn đặt trước." });
    }
}

public class CreateReservationRequest
{
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public int GuestCount { get; set; } = 2;
    public DateTime ArrivalTime { get; set; }
    public string? Note { get; set; }
    public List<int>? TableIds { get; set; }
}
