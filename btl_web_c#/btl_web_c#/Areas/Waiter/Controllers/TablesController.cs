using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.Models.Entities;
using RestaurantOrdering.Services;

namespace RestaurantOrdering.Areas.Waiter.Controllers;

[Area("Waiter")]
[Authorize(Policy = "CanManageOrders")]
public class TablesController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IRealtimeNotifier notifier) : Controller
{
    // ── GET /Waiter/Tables ────────────────────────────────────────────────
    public async Task<IActionResult> Index(int? areaId, byte? statusFilter)
    {
        ViewData["Title"] = "Sơ đồ bàn";
        ViewData["AreaTitle"] = "Phục vụ";

        var areas = await db.Areas.OrderBy(a => a.SortOrder).ToListAsync();
        var tables = await db.DiningTables
            .Include(t => t.Area)
            .Include(t => t.TableStatus)
            .OrderBy(t => t.AreaId).ThenBy(t => t.SortOrder)
            .ToListAsync();

        // Lấy cart đang mở của từng bàn (để hiện số món đang chờ)
        var activeCarts = await db.Carts
            .Where(c => c.StatusId == 1 || c.StatusId == 2) // OPEN hoặc WAITING_PAYMENT
            .Include(c => c.UsedTables.Where(u => u.EndTime == null))
            .Include(c => c.CartItems.Where(ci => ci.StatusId != 6 && ci.StatusId != 7)) // bỏ hủy/từ chối
            .ToListAsync();

        // Map tableId → cart
        var tableCartMap = activeCarts
            .SelectMany(c => c.UsedTables, (c, ut) => new { ut.TableId, Cart = c })
            .ToDictionary(x => x.TableId, x => x.Cart);

        if (areaId.HasValue)
            tables = tables.Where(t => t.AreaId == areaId.Value).ToList();
        if (statusFilter.HasValue)
            tables = tables.Where(t => t.StatusId == statusFilter.Value).ToList();

        ViewBag.Areas = areas;
        ViewBag.SelectedAreaId = areaId;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.TableCartMap = tableCartMap;
        return View(tables);
    }

    // ── POST /Waiter/Tables/Open ──────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(int tableId, int guestCount)
    {
        if (guestCount < 1) guestCount = 1;

        // Dùng transaction + kiểm tra RowVersion để tránh 2 người mở cùng lúc
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var table = await db.DiningTables
                .FirstOrDefaultAsync(t => t.Id == tableId);

            if (table == null)
                return Json(new { success = false, message = "Bàn không tồn tại." });

            if (table.StatusId != 1) // 1 = AVAILABLE
                return Json(new { success = false, message = "Bàn đã được sử dụng hoặc đang chờ." });

            var user = await userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var cartCode = $"C{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
            var cart = new Cart
            {
                CartCode = cartCode,
                StatusId = 1, // OPEN
                GuestCount = guestCount,
                WaiterId = user.Id,
                OpenedTime = DateTime.UtcNow
            };
            db.Carts.Add(cart);
            await db.SaveChangesAsync();

            db.UsedTables.Add(new UsedTable
            {
                CartId = cart.Id,
                TableId = tableId,
                IsPrimary = true,
                StartTime = DateTime.UtcNow
            });

            table.StatusId = 2; // OCCUPIED
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Json(new { success = true, cartId = cart.Id, tableId });
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return Json(new { success = false, message = "Bàn vừa được mở bởi người khác. Vui lòng thử lại." });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ── POST /api/waiter/tables/{tableId}/move ──────────────────────────
    // TBL-01: Chuyển bàn
    [HttpPost("/api/waiter/tables/{tableId:int}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveTable(int tableId, int targetTableId)
    {
        if (tableId == targetTableId)
            return BadRequest(new { message = "Không thể chuyển bàn về chính nó." });

        var sourceTable = await db.DiningTables.FindAsync(tableId);
        var targetTable = await db.DiningTables.FindAsync(targetTableId);

        if (sourceTable == null || targetTable == null)
            return NotFound(new { message = "Bàn không tồn tại." });

        if (sourceTable.StatusId != 2) // phải đang OCCUPIED
            return BadRequest(new { message = "Chỉ chuyển được bàn đang phục vụ." });

        if (targetTable.StatusId != 1) // target phải AVAILABLE
            return BadRequest(new { message = "Bàn đích phải đang trống." });

        // Tìm UsedTable hiện tại
        var usedTable = await db.UsedTables
            .Include(u => u.Cart)
                .ThenInclude(c => c.UsedTables)
            .FirstOrDefaultAsync(u => u.TableId == tableId && u.EndTime == null);

        if (usedTable == null)
            return BadRequest(new { message = "Không tìm thấy thông tin sử dụng bàn." });

        var sourceCode = sourceTable.Code;
        var targetCode = targetTable.Code;

        // Đóng dòng UsedTable cũ
        usedTable.EndTime = DateTime.UtcNow;
        usedTable.IsPrimary = false;

        // Tạo dòng mới cho bàn đích
        db.UsedTables.Add(new UsedTable
        {
            CartId = usedTable.CartId,
            TableId = targetTableId,
            IsPrimary = true,
            StartTime = DateTime.UtcNow
        });

        // Cập nhật trạng thái bàn
        sourceTable.StatusId = 1; // AVAILABLE
        targetTable.StatusId = 2; // OCCUPIED

        await db.SaveChangesAsync();

        // Thông báo realtime cho bếp
        var cart = usedTable.Cart;
        var tableCodes = cart.UsedTables
            .Where(u => u.EndTime == null)
            .Select(u => u.DiningTable?.Code ?? "?")
            .ToList();
        var newTableCodes = string.Join(" + ", tableCodes);

        await notifier.TableMovedAsync(tableId, sourceCode, targetCode, newTableCodes, cart.Id);

        return Json(new
        {
            success = true,
            message = $"Đã chuyển bàn {sourceCode} → {targetCode}.",
            newTableCode = targetCode
        });
    }

    // ── POST /api/waiter/tables/merge ─────────────────────────────────────
    // TBL-02: Gộp bàn
    [HttpPost("/api/waiter/tables/merge")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MergeTables(int primaryTableId, int secondaryTableId)
    {
        if (primaryTableId == secondaryTableId)
            return BadRequest(new { message = "Không thể gộp bàn với chính nó." });

        var primaryTable = await db.DiningTables.FindAsync(primaryTableId);
        var secondaryTable = await db.DiningTables.FindAsync(secondaryTableId);

        if (primaryTable == null || secondaryTable == null)
            return NotFound(new { message = "Bàn không tồn tại." });

        if (secondaryTable.StatusId != 2) // bàn phụ phải đang OCCUPIED
            return BadRequest(new { message = "Chỉ gộp được bàn đang phục vụ." });

        // Tìm UsedTables của cả 2 bàn
        var primaryUsed = await db.UsedTables
            .Include(u => u.Cart)
            .FirstOrDefaultAsync(u => u.TableId == primaryTableId && u.EndTime == null);

        var secondaryUsed = await db.UsedTables
            .Include(u => u.Cart)
                .ThenInclude(c => c.CartItems.Where(ci => ci.StatusId >= 2 && ci.StatusId != 6 && ci.StatusId != 7))
            .FirstOrDefaultAsync(u => u.TableId == secondaryTableId && u.EndTime == null);

        if (primaryUsed == null)
            return BadRequest(new { message = "Bàn chính phải đang có khách." });

        if (secondaryUsed == null)
            return BadRequest(new { message = "Bàn phụ không tìm thấy." });

        var secondaryCart = secondaryUsed.Cart;
        var secondaryItems = secondaryCart.CartItems.ToList();

        // Chuyển món sang cart chính
        foreach (var item in secondaryItems)
        {
            item.CartId = primaryUsed.CartId;
        }

        // Đóng UsedTable bàn phụ
        secondaryUsed.EndTime = DateTime.UtcNow;
        secondaryUsed.IsPrimary = false;

        // Thêm UsedTable mới cho bàn phụ vào cart chính
        db.UsedTables.Add(new UsedTable
        {
            CartId = primaryUsed.CartId,
            TableId = secondaryTableId,
            IsPrimary = false,
            StartTime = DateTime.UtcNow
        });

        // Đóng cart cũ
        secondaryCart.StatusId = 4; // CANCELLED
        secondaryCart.ClosedTime = DateTime.UtcNow;

        // Bàn phụ về trống
        secondaryTable.StatusId = 1; // AVAILABLE

        await db.SaveChangesAsync();

        // Thông báo realtime
        var primaryCart = primaryUsed.Cart;
        var allTableCodes = primaryCart.UsedTables
            .Where(u => u.EndTime == null)
            .Select(u => u.DiningTable?.Code ?? "?")
            .ToList();
        allTableCodes.Add(secondaryTable.Code);
        var mergedCodes = string.Join(" + ", allTableCodes.Distinct());

        await notifier.TablesMergedAsync(secondaryTableId, primaryTable.Code, mergedCodes, primaryCart.Id);

        return Json(new
        {
            success = true,
            message = $"Đã gộp bàn {secondaryTable.Code} vào {primaryTable.Code}.",
            mergedTableCode = secondaryTable.Code,
            primaryTableCode = primaryTable.Code
        });
    }

    // ── GET /api/waiter/tables/available-for-move ────────────────────────
    [HttpGet("/api/waiter/tables/available-for-move")]
    public async Task<IActionResult> GetAvailableTablesForMove(int? excludeTableId)
    {
        var tables = await db.DiningTables
            .Include(t => t.Area)
            .Where(t => t.StatusId == 1 && t.Id != excludeTableId)
            .OrderBy(t => t.AreaId).ThenBy(t => t.SortOrder)
            .Select(t => new { t.Id, t.Code, AreaName = t.Area.Name, t.Capacity })
            .ToListAsync();

        return Json(tables);
    }
}
