using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.Models.Entities;
using RestaurantOrdering.Services;

namespace RestaurantOrdering.Areas.Kitchen.Controllers;

[Area("Kitchen")]
[Authorize(Roles = "Kitchen,Admin")]
public class BoardController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IRealtimeNotifier notifier) : Controller
{
    // ── GET /Kitchen/Board ────────────────────────────────────────────────
    public IActionResult Index()
    {
        ViewData["Title"] = "Màn hình bếp";
        return View();
    }

    // ── GET /api/kitchen/board  (KDS load board — KIT-02) ─────────────────
    [HttpGet("/api/kitchen/board")]
    public async Task<IActionResult> GetBoard()
    {
        // Lấy tất cả món CONFIRMED(2), COOKING(3), READY(4)
        var items = await db.CartItems
            .Include(ci => ci.Cart)
                .ThenInclude(c => c.UsedTables.Where(u => u.EndTime == null))
                    .ThenInclude(u => u.DiningTable)
            .Include(ci => ci.CartItemOptions)
            .Where(ci => ci.StatusId == 2 || ci.StatusId == 3 || ci.StatusId == 4)
            .OrderByDescending(ci => ci.IsPriority)
            .ThenBy(ci => ci.ConfirmedTime)
            .ToListAsync();

        var result = items.Select(ci => MapItem(ci));
        return Json(result);
    }

    // ── POST /api/kitchen/items/{id}/start  (CONFIRMED → COOKING) ─────────
    [HttpPost("/api/kitchen/items/{id:long}/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartCooking(long id)
    {
        var item = await db.CartItems
            .Include(ci => ci.Cart)
                .ThenInclude(c => c.UsedTables.Where(u => u.EndTime == null))
                    .ThenInclude(u => u.DiningTable)
            .Include(ci => ci.CartItemOptions)
            .FirstOrDefaultAsync(ci => ci.Id == id);

        if (item == null) return NotFound(new { message = "Không tìm thấy món." });
        if (item.StatusId != 2) // phải là CONFIRMED
            return Conflict(new { message = "Món không ở trạng thái Chờ làm." });

        var user = await userManager.GetUserAsync(User);

        item.StatusId = 3; // COOKING
        item.CookingStartTime = DateTime.UtcNow;

        db.CartItemStatusHistories.Add(new CartItemStatusHistory
        {
            CartItemId = item.Id,
            FromStatusId = 2, ToStatusId = 3,
            ChangedBy = user!.Id, ChangedTime = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var payload = MapItem(item);
        await notifier.ItemStatusChangedAsync(payload);

        return Ok(payload);
    }

    // ── POST /api/kitchen/items/{id}/done  (COOKING → READY) ─────────────
    [HttpPost("/api/kitchen/items/{id:long}/done")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDone(long id)
    {
        var item = await db.CartItems
            .Include(ci => ci.Cart)
                .ThenInclude(c => c.UsedTables.Where(u => u.EndTime == null))
                    .ThenInclude(u => u.DiningTable)
            .Include(ci => ci.CartItemOptions)
            .FirstOrDefaultAsync(ci => ci.Id == id);

        if (item == null) return NotFound(new { message = "Không tìm thấy món." });
        if (item.StatusId != 3)
            return Conflict(new { message = "Món không ở trạng thái Đang làm." });

        var user = await userManager.GetUserAsync(User);

        item.StatusId = 4; // READY
        item.ReadyTime = DateTime.UtcNow;

        db.CartItemStatusHistories.Add(new CartItemStatusHistory
        {
            CartItemId = item.Id,
            FromStatusId = 3, ToStatusId = 4,
            ChangedBy = user!.Id, ChangedTime = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var payload = MapItem(item);
        await notifier.ItemStatusChangedAsync(payload);
        // Gửi âm báo cho phục vụ phụ trách bàn
        await notifier.ItemReadyAsync(item.Cart.WaiterId, payload);

        return Ok(payload);
    }

    // ── POST /api/kitchen/products/{id}/toggle-availability (bếp báo hết món)
    [HttpPost("/api/kitchen/products/{id:int}/toggle-availability")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "CanUpdateCooking")]
    public async Task<IActionResult> ToggleProductAvailability(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product == null) return NotFound(new { message = "Không tìm thấy món." });
        product.IsAvailable = !product.IsAvailable;
        await db.SaveChangesAsync();
        await notifier.MenuItemAvailabilityChangedAsync(product.Id, product.IsAvailable, product.Name);
        return Ok(new { success = true, isAvailable = product.IsAvailable, productName = product.Name });
    }

    // ── POST /api/kitchen/items/{id}/reject  (bếp từ chối) ───────────────
    [HttpPost("/api/kitchen/items/{id:long}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(long id, [FromBody] RejectRequest req)
    {
        var item = await db.CartItems
            .Include(ci => ci.Cart)
                .ThenInclude(c => c.UsedTables.Where(u => u.EndTime == null))
                    .ThenInclude(u => u.DiningTable)
            .Include(ci => ci.CartItemOptions)
            .Include(ci => ci.Product)
            .FirstOrDefaultAsync(ci => ci.Id == id);

        if (item == null) return NotFound(new { message = "Không tìm thấy món." });
        if (item.StatusId > 3)
            return Conflict(new { message = "Không thể từ chối món đã xong." });

        var user = await userManager.GetUserAsync(User);
        var fromStatus = item.StatusId;

        item.StatusId = 7; // REJECTED
        item.CancelReason = req.Reason;

        db.CartItemStatusHistories.Add(new CartItemStatusHistory
        {
            CartItemId = item.Id,
            FromStatusId = fromStatus, ToStatusId = 7,
            ChangedBy = user!.Id, ChangedTime = DateTime.UtcNow,
            Reason = req.Reason
        });
        await db.SaveChangesAsync();

        var payload = MapItem(item);
        await notifier.ItemStatusChangedAsync(payload);
        await notifier.ItemRejectedAsync(item.Cart.WaiterId, payload);

        return Ok(payload);
    }

    // ── POST /api/kitchen/items/{id}/undo  (KIT-06: hoàn tác trạng thái) ─
    [HttpPost("/api/kitchen/items/{id:long}/undo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UndoStatus(long id, [FromBody] UndoRequest req)
    {
        var item = await db.CartItems
            .Include(ci => ci.Cart)
                .ThenInclude(c => c.UsedTables.Where(u => u.EndTime == null))
                    .ThenInclude(u => u.DiningTable)
            .Include(ci => ci.CartItemOptions)
            .FirstOrDefaultAsync(ci => ci.Id == id);

        if (item == null) return NotFound(new { message = "Không tìm thấy món." });

        // Chỉ cho phép undo COOKING → CONFIRMED hoặc READY → COOKING
        // Không cho undo SERVED, CANCELLED, REJECTED
        byte? newStatus = item.StatusId switch
        {
            3 => (byte)2, // COOKING → CONFIRMED
            4 => (byte)3, // READY → COOKING
            _ => null
        };

        if (newStatus == null)
            return Conflict(new { message = "Không thể hoàn tác trạng thái này." });

        // Kiểm tra bill đã thanh toán chưa
        var hasPaidBill = await db.Bills
            .AnyAsync(b => b.CartId == item.CartId && b.Status == 1);
        if (hasPaidBill)
            return Conflict(new { message = "Bill đã thanh toán, không thể hoàn tác." });

        var user = await userManager.GetUserAsync(User);
        var fromStatus = item.StatusId;

        item.StatusId = newStatus.Value;
        // Xóa thời gian của bước kế tiếp
        if (newStatus == 2) { item.CookingStartTime = null; item.ReadyTime = null; }
        if (newStatus == 3) item.ReadyTime = null;

        db.CartItemStatusHistories.Add(new CartItemStatusHistory
        {
            CartItemId = item.Id,
            FromStatusId = fromStatus,
            ToStatusId = newStatus.Value,
            ChangedBy = user!.Id,
            ChangedTime = DateTime.UtcNow,
            Reason = req.Reason ?? "Hoàn tác"
        });
        await db.SaveChangesAsync();

        var payload = MapItem(item);
        await notifier.ItemStatusChangedAsync(payload);
        await notifier.ItemStatusRevertedAsync(item.Cart.WaiterId, payload);

        return Ok(payload);
    }

    // ── GET /api/kitchen/board  (KIT-04: thứ tự ra món) ─────────────────
    // Route trùng với GetBoard() ở trên — chỉ giữ 1 action duy nhất
    [HttpGet("/api/kitchen/board")]
    public async Task<IActionResult> GetBoard()
    {
        // Lấy tất cả món CONFIRMED(2), COOKING(3), READY(4)
        var items = await db.CartItems
            .Include(ci => ci.Cart)
                .ThenInclude(c => c.UsedTables.Where(u => u.EndTime == null))
                    .ThenInclude(u => u.DiningTable)
            .Include(ci => ci.CartItemOptions)
            .Include(ci => ci.Product)
            .Where(ci => ci.StatusId == 2 || ci.StatusId == 3 || ci.StatusId == 4)
            .ToListAsync();

        // KIT-04: sắp xếp theo ưu tiên, rồi CourseGroup, rồi thời gian
        var ordered = items
            .OrderByDescending(ci => ci.IsPriority)
            .ThenBy(ci => ci.Product?.CourseGroup ?? 2)
            .ThenBy(ci => ci.ConfirmedTime)
            .ToList();

        var result = ordered.Select(ci => MapItem(ci));
        return Json(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static object MapItem(CartItem ci)
    {
        var tableCodes = ci.Cart?.UsedTables
            .Where(u => u.EndTime == null)
            .Select(u => u.DiningTable?.Code ?? "?")
            .ToList() ?? new List<string>();

        var waitMinutes = ci.ConfirmedTime.HasValue
            ? (int)(DateTime.UtcNow - ci.ConfirmedTime.Value).TotalMinutes : 0;

        return new
        {
            id = ci.Id,
            cartId = ci.CartId,
            productName = ci.ProductName,
            quantity = ci.Quantity,
            note = ci.Note,
            statusId = ci.StatusId,
            roundNo = ci.RoundNo,
            tableCode = string.Join(" + ", tableCodes),
            waiterId = ci.Cart?.WaiterId ?? 0,
            confirmedTime = ci.ConfirmedTime,
            cookingStartTime = ci.CookingStartTime,
            readyTime = ci.ReadyTime,
            isPriority = ci.IsPriority,
            isEdited = ci.IsEdited,
            cancelReason = ci.CancelReason,
            waitMinutes,
            courseGroup = ci.Product?.CourseGroup ?? 1,
            options = ci.CartItemOptions.Select(o => o.OptionName).ToList()
        };
    }
}

public class RejectRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class UndoRequest
{
    public string? Reason { get; set; }
}
