using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.Models.Entities;
using RestaurantOrdering.Services;
using RestaurantOrdering.ViewModels.Waiter;

namespace RestaurantOrdering.Areas.Waiter.Controllers;

[Area("Waiter")]
[Authorize(Policy = "CanManageOrders")]
public class OrderController(
    AppDbContext db,
    SessionCartService sessionCart,
    UserManager<ApplicationUser> userManager,
    IRealtimeNotifier notifier,
    IAuditService auditService) : Controller
{
    // ── GET /Waiter/Order/{tableId} ───────────────────────────────────────
    public async Task<IActionResult> Index(int tableId)
    {
        var table = await db.DiningTables
            .Include(t => t.Area)
            .Include(t => t.TableStatus)
            .FirstOrDefaultAsync(t => t.Id == tableId);

        if (table == null) return NotFound();
        if (table.StatusId != 2 && table.StatusId != 3) // không phải OCCUPIED/WAITING_PAYMENT
            return RedirectToAction("Index", "Tables");

        // Tìm cart đang mở của bàn này
        var cart = await db.Carts
            .Include(c => c.Waiter)
            .Include(c => c.UsedTables.Where(u => u.EndTime == null))
            .FirstOrDefaultAsync(c =>
                c.UsedTables.Any(u => u.TableId == tableId && u.EndTime == null)
                && (c.StatusId == 1 || c.StatusId == 2));

        if (cart == null)
            return RedirectToAction("Index", "Tables");

        // Danh mục + món
        var categories = await db.Categories
            .OrderBy(c => c.SortOrder)
            .Include(c => c.Products.Where(p => !p.IsDeleted))
            .ToListAsync();

        // Tùy chọn của từng món
        var productIds = categories.SelectMany(c => c.Products).Select(p => p.Id).ToList();
        var options = await db.ProductItemOptions
            .Where(o => productIds.Contains(o.ProductId))
            .OrderBy(o => o.SortOrder)
            .ToListAsync();

        // CartItems đã gửi bếp (không phải DRAFT)
        var sentItems = await db.CartItems
            .Include(ci => ci.CartItemOptions)
            .Include(ci => ci.CartItemStatus)
            .Where(ci => ci.CartId == cart.Id && ci.StatusId != 1) // không phải DRAFT
            .OrderBy(ci => ci.RoundNo).ThenBy(ci => ci.ConfirmedTime)
            .ToListAsync();

        // Nhân viên phụ trách
        var user = await userManager.GetUserAsync(User);

        ViewData["Title"] = $"Bàn {table.Code}";
        ViewData["AreaTitle"] = "Phục vụ";
        ViewBag.Table = table;
        ViewBag.Cart = cart;
        ViewBag.Categories = categories;
        ViewBag.OptionsMap = options.GroupBy(o => o.ProductId).ToDictionary(g => g.Key, g => g.ToList());
        ViewBag.SentItems = sentItems;
        ViewBag.SentByRound = sentItems.GroupBy(ci => ci.RoundNo ?? 0).OrderBy(g => g.Key).ToList();
        ViewBag.CurrentUserId = user?.Id;
        ViewBag.DraftItems = sessionCart.Get(tableId);
        return View();
    }

    // ── POST /Waiter/Order/AddToCart ─────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest req)
    {
        var product = await db.Products
            .Include(p => p.ProductItemOptions)
            .FirstOrDefaultAsync(p => p.Id == req.ProductId);

        if (product == null || !product.IsAvailable)
            return Json(new { success = false, message = "Món không còn phục vụ." });

        // Build options
        var opts = new List<CartDraftOption>();
        foreach (var optId in req.OptionIds ?? [])
        {
            var opt = product.ProductItemOptions.FirstOrDefault(o => o.Id == optId);
            if (opt != null)
                opts.Add(new CartDraftOption { OptionId = opt.Id, OptionName = opt.Name, ExtraPrice = opt.ExtraPrice });
        }

        var item = new CartDraftItem
        {
            LocalId = Guid.NewGuid(),
            ProductId = product.Id,
            ProductName = product.Name,
            UnitPrice = product.Price,
            Quantity = Math.Max(1, req.Quantity),
            Note = req.Note?.Trim(),
            Options = opts
        };

        sessionCart.AddOrUpdate(req.TableId, item);
        var draft = sessionCart.Get(req.TableId);
        return Json(new { success = true, cartCount = draft.Count, totalPrice = draft.Sum(i => i.TotalPrice) });
    }

    // ── POST /Waiter/Order/UpdateDraftItem ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateDraftItem([FromBody] UpdateDraftRequest req)
    {
        var item = sessionCart.FindItem(req.TableId, req.LocalId);
        if (item == null) return Json(new { success = false });
        item.Quantity = Math.Max(1, req.Quantity);
        item.Note = req.Note?.Trim();
        sessionCart.AddOrUpdate(req.TableId, item);
        var draft = sessionCart.Get(req.TableId);
        return Json(new { success = true, totalPrice = draft.Sum(i => i.TotalPrice), itemTotal = item.TotalPrice });
    }

    // ── POST /Waiter/Order/RemoveDraftItem ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveDraftItem([FromBody] RemoveDraftRequest req)
    {
        sessionCart.Remove(req.TableId, req.LocalId);
        var draft = sessionCart.Get(req.TableId);
        return Json(new { success = true, cartCount = draft.Count, totalPrice = draft.Sum(i => i.TotalPrice) });
    }

    // ── GET /Waiter/Order/CartSummary?tableId=  (AJAX partial) ──────────
    [HttpGet]
    public IActionResult CartSummary(int tableId)
    {
        ViewBag.DraftItems = sessionCart.Get(tableId);
        ViewBag.TableId = tableId;
        return PartialView("_CartSummary");
    }

    // ── POST /Waiter/Order/Submit ────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int tableId)
    {
        var draft = sessionCart.Get(tableId);
        if (draft.Count == 0)
            return Json(new { success = false, message = "Giỏ nháp trống." });

        var cart = await db.Carts
            .Include(c => c.UsedTables.Where(u => u.EndTime == null))
            .Include(c => c.CartItems.Where(ci => ci.StatusId != 1)) // CONFIRMED trở lên
            .FirstOrDefaultAsync(c =>
                c.UsedTables.Any(u => u.TableId == tableId && u.EndTime == null)
                && (c.StatusId == 1 || c.StatusId == 2));

        if (cart == null)
            return Json(new { success = false, message = "Không tìm thấy đơn hàng đang mở cho bàn này." });

        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Tính RoundNo mới
        var maxRound = cart.CartItems.Count > 0
            ? cart.CartItems.Max(ci => ci.RoundNo ?? 0)
            : 0;
        var nextRound = maxRound + 1;

        // Kiểm tra lại món còn hàng, tách món hết ra
        var unavailable = new List<string>();
        var toAdd = new List<CartDraftItem>();
        var productIds = draft.Select(d => d.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var d in draft)
        {
            if (!products.TryGetValue(d.ProductId, out var prod) || !prod.IsAvailable)
                unavailable.Add(d.ProductName);
            else
                toAdd.Add(d);
        }

        if (toAdd.Count == 0)
            return Json(new { success = false, message = "Tất cả món trong giỏ đã hết hàng." });

        var now = DateTime.UtcNow;

        foreach (var d in toAdd)
        {
            var cartItem = new CartItem
            {
                CartId = cart.Id,
                ProductId = d.ProductId,
                ProductName = d.ProductName,
                UnitPrice = d.UnitPrice,
                Quantity = d.Quantity,
                Note = d.Note,
                StatusId = 2, // CONFIRMED
                RoundNo = nextRound,
                ConfirmedTime = now,
                ConfirmedBy = user.Id
            };
            db.CartItems.Add(cartItem);
            await db.SaveChangesAsync(); // cần Id trước khi thêm options + history

            // Thêm options
            foreach (var opt in d.Options)
            {
                db.CartItemOptions.Add(new CartItemOption
                {
                    CartItemId = cartItem.Id,
                    ProductItemOptionId = opt.OptionId,
                    OptionName = opt.OptionName,
                    ExtraPrice = opt.ExtraPrice
                });
            }

            // Ghi lịch sử trạng thái
            db.CartItemStatusHistories.Add(new CartItemStatusHistory
            {
                CartItemId = cartItem.Id,
                FromStatusId = null,
                ToStatusId = 2, // CONFIRMED
                ChangedBy = user.Id,
                ChangedTime = now
            });
        }

        await db.SaveChangesAsync();

        // Xóa toàn giỏ nháp (cả món đã hết lẫn đã gửi)
        sessionCart.Clear(tableId);

        // Lấy payload để gửi realtime cho bếp
        var confirmedItems = await db.CartItems
            .Include(ci => ci.CartItemOptions)
            .Where(ci => ci.CartId == cart.Id && ci.RoundNo == nextRound)
            .ToListAsync();

        var tableCodes = cart.UsedTables.Select(u => u.DiningTable?.Code ?? "?").ToList();
        if (!tableCodes.Any())
        {
            var tableCodesFromDb = await db.UsedTables
                .Include(u => u.DiningTable)
                .Where(u => u.CartId == cart.Id && u.EndTime == null)
                .Select(u => u.DiningTable!.Code)
                .ToListAsync();
            tableCodes = tableCodesFromDb;
        }

        var roundPayload = new
        {
            cartId = cart.Id,
            roundNo = nextRound,
            tableCode = string.Join(" + ", tableCodes),
            waiterId = user.Id,
            items = confirmedItems.Select(ci => new
            {
                id = ci.Id,
                productName = ci.ProductName,
                quantity = ci.Quantity,
                note = ci.Note,
                options = ci.CartItemOptions.Select(o => o.OptionName).ToList(),
                confirmedTime = ci.ConfirmedTime
            })
        };
        await notifier.OrderSubmittedAsync(roundPayload);

        var msg = unavailable.Count > 0
            ? $"Gửi bếp đợt {nextRound} thành công. Món hết hàng bị bỏ qua: {string.Join(", ", unavailable)}."
            : $"Gửi bếp đợt {nextRound} thành công!";

        return Json(new
        {
            success = true,
            roundNo = nextRound,
            sentCount = toAdd.Count,
            unavailableCount = unavailable.Count,
            unavailableNames = unavailable,
            message = msg
        });
    }

    // ── POST /Waiter/Order/CancelItem ─────────────────────────────────────
    // ORD-02: chỉ hủy được khi CONFIRMED; dùng RowVersion chống xung đột
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelItem([FromBody] CancelItemRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Reason))
            return Json(new { success = false, message = "Vui lòng nhập lý do hủy." });

        var item = await db.CartItems
            .Include(ci => ci.Cart)
                .ThenInclude(c => c.UsedTables.Where(u => u.EndTime == null))
                    .ThenInclude(u => u.DiningTable)
            .Include(ci => ci.CartItemOptions)
            .FirstOrDefaultAsync(ci => ci.Id == req.CartItemId);

        if (item == null)
            return Json(new { success = false, message = "Không tìm thấy món." });

        // Chỉ hủy được khi CONFIRMED (ORD-02)
        if (item.StatusId != 2)
        {
            var statusMsg = item.StatusId switch
            {
                3 => "Món đã được bếp nhận (Đang làm), không thể hủy.",
                4 => "Món đã xong, không thể hủy.",
                5 => "Món đã phục vụ, không thể hủy.",
                6 => "Món đã được hủy trước đó.",
                7 => "Món đã bị bếp từ chối.",
                _ => "Không thể hủy món ở trạng thái này."
            };
            return Json(new { success = false, message = statusMsg });
        }

        var user = await userManager.GetUserAsync(User);

        try
        {
            item.StatusId = 6; // CANCELLED
            item.CancelReason = req.Reason.Trim();
            // Gắn RowVersion để phát hiện race condition (bếp vừa bấm "Đang làm" cùng lúc)
            if (req.RowVersion != null)
                db.Entry(item).Property(x => x.RowVersion).OriginalValue = req.RowVersion;

            db.CartItemStatusHistories.Add(new CartItemStatusHistory
            {
                CartItemId = item.Id,
                FromStatusId = 2, ToStatusId = 6,
                ChangedBy = user!.Id, ChangedTime = DateTime.UtcNow,
                Reason = req.Reason.Trim()
            });
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // ORD-02: bếp vừa đổi sang COOKING trước
            await db.Entry(item).ReloadAsync();
            return Json(new
            {
                success = false,
                conflict = true,
                message = $"Món '{item.ProductName}' vừa được bếp nhận ({item.StatusId switch { 3 => "Đang làm", 4 => "Đã xong", _ => "đã đổi trạng thái" }}). Không thể hủy."
            });
        }

        // Ghi audit log hủy món
        await auditService.LogAsync("CANCEL_ITEM", "CartItem", item.Id.ToString(),
            new { item.ProductName, item.Quantity, statusId = 2 },
            new { item.ProductName, item.Quantity, statusId = 6, reason = req.Reason.Trim() });

        // Phát sự kiện realtime cho bếp
        var payload = new
        {
            id = item.Id,
            productName = item.ProductName,
            quantity = item.Quantity,
            statusId = 6,
            tableCode = string.Join(" + ", item.Cart.UsedTables
                .Where(u => u.EndTime == null)
                .Select(u => u.DiningTable?.Code ?? "?"))
        };
        await notifier.OrderItemCancelledAsync(payload);

        return Json(new { success = true, message = $"Đã hủy món '{item.ProductName}'." });
    }

    // ── POST /Waiter/Order/EditItem ───────────────────────────────────────
    // ORD-03: sửa số lượng/ghi chú khi CONFIRMED
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditItem([FromBody] EditItemRequest req)
    {
        if (req.Quantity < 1)
            return Json(new { success = false, message = "Số lượng phải ít nhất 1." });

        var item = await db.CartItems
            .Include(ci => ci.Cart)
                .ThenInclude(c => c.UsedTables.Where(u => u.EndTime == null))
                    .ThenInclude(u => u.DiningTable)
            .Include(ci => ci.CartItemOptions)
            .FirstOrDefaultAsync(ci => ci.Id == req.CartItemId);

        if (item == null)
            return Json(new { success = false, message = "Không tìm thấy món." });

        if (item.StatusId != 2)
            return Json(new { success = false, message = "Chỉ sửa được món đang Chờ làm." });

        var user = await userManager.GetUserAsync(User);

        // Ghi lại giá trị cũ để hiện thị "Đã chỉnh sửa" trên KDS
        var oldQty = item.Quantity;
        var oldNote = item.Note;

        try
        {
            item.Quantity = req.Quantity;
            item.Note = req.Note?.Trim();
            item.IsEdited = true;

            if (req.RowVersion != null)
                db.Entry(item).Property(x => x.RowVersion).OriginalValue = req.RowVersion;

            db.CartItemStatusHistories.Add(new CartItemStatusHistory
            {
                CartItemId = item.Id,
                FromStatusId = 2, ToStatusId = 2, // trạng thái không đổi nhưng ghi log
                ChangedBy = user!.Id, ChangedTime = DateTime.UtcNow,
                Reason = $"Sửa: SL {oldQty}→{req.Quantity}" + (oldNote != req.Note ? $", Ghi chú đổi" : "")
            });
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await db.Entry(item).ReloadAsync();
            return Json(new
            {
                success = false,
                conflict = true,
                message = $"Không thể sửa: món '{item.ProductName}' vừa đổi trạng thái."
            });
        }

        // Ghi audit log sửa món
        await auditService.LogAsync("EDIT_ITEM", "CartItem", item.Id.ToString(),
            new { item.ProductName, quantity = oldQty, note = oldNote },
            new { item.ProductName, quantity = req.Quantity, note = req.Note?.Trim() });

        // Phát realtime cho bếp (hiện nhãn "Đã chỉnh sửa")
        var payload = new
        {
            id = item.Id,
            productName = item.ProductName,
            quantity = item.Quantity,
            note = item.Note,
            statusId = item.StatusId,
            isEdited = true,
            oldQty,
            oldNote,
            tableCode = string.Join(" + ", item.Cart.UsedTables
                .Where(u => u.EndTime == null)
                .Select(u => u.DiningTable?.Code ?? "?"))
        };
        await notifier.OrderItemEditedAsync(payload);

        return Json(new { success = true, message = $"Đã cập nhật món '{item.ProductName}'." });
    }

    // ── POST /Waiter/Order/MarkServed ─────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkServed(long cartItemId)
    {
        var item = await db.CartItems.FindAsync(cartItemId);
        if (item == null) return Json(new { success = false, message = "Không tìm thấy món." });
        if (item.StatusId != 4) // chỉ READY mới đánh dấu SERVED
            return Json(new { success = false, message = "Món chưa xong, không thể đánh dấu đã phục vụ." });

        var user = await userManager.GetUserAsync(User);
        item.StatusId = 5; // SERVED
        item.ServedTime = DateTime.UtcNow;

        db.CartItemStatusHistories.Add(new CartItemStatusHistory
        {
            CartItemId = item.Id,
            FromStatusId = 4,
            ToStatusId = 5,
            ChangedBy = user!.Id,
            ChangedTime = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Gửi realtime cho phục vụ và bếp
        var payload = new { id = item.Id, statusId = 5, statusName = "Đã phục vụ" };
        await notifier.ItemStatusChangedAsync(payload);

        return Json(new { success = true });
    }

    // ── POST /Waiter/Order/RequestPayment ────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestPayment(int tableId)
    {
        var cart = await db.Carts
            .Include(c => c.UsedTables.Where(u => u.EndTime == null))
            .FirstOrDefaultAsync(c =>
                c.UsedTables.Any(u => u.TableId == tableId && u.EndTime == null)
                && c.StatusId == 1);

        if (cart == null)
            return Json(new { success = false, message = "Không tìm thấy đơn hàng đang mở." });

        // Chuyển cart sang WAITING_PAYMENT
        cart.StatusId = 2; // WAITING_PAYMENT

        // Chuyển bàn sang WAITING_PAYMENT
        var table = await db.DiningTables.FindAsync(tableId);
        if (table != null) table.StatusId = 3; // WAITING_PAYMENT

        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Đã yêu cầu thanh toán." });
    }

    // ── GET /Waiter/Order/GetSentRounds (AJAX reload) ─────────────────────
    [HttpGet]
    public async Task<IActionResult> GetSentRounds(int tableId)
    {
        var cart = await db.Carts
            .Include(c => c.UsedTables.Where(u => u.EndTime == null))
            .FirstOrDefaultAsync(c =>
                c.UsedTables.Any(u => u.TableId == tableId && u.EndTime == null)
                && (c.StatusId == 1 || c.StatusId == 2));

        if (cart == null) return Json(new List<object>());

        var sentItems = await db.CartItems
            .Include(ci => ci.CartItemOptions)
            .Include(ci => ci.CartItemStatus)
            .Where(ci => ci.CartId == cart.Id && ci.StatusId != 1)
            .OrderBy(ci => ci.RoundNo).ThenBy(ci => ci.ConfirmedTime)
            .ToListAsync();

        var result = sentItems.GroupBy(ci => ci.RoundNo ?? 0)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                roundNo = g.Key,
                items = g.Select(ci => new
                {
                    id = ci.Id,
                    productName = ci.ProductName,
                    quantity = ci.Quantity,
                    unitPrice = ci.UnitPrice,
                    note = ci.Note,
                    statusId = ci.StatusId,
                    statusName = ci.CartItemStatus?.Name ?? "",
                    statusColor = ci.CartItemStatus?.ColorHex ?? "#999",
                    isEdited = ci.IsEdited,
                    cancelReason = ci.CancelReason,
                    options = ci.CartItemOptions.Select(o => o.OptionName).ToList()
                })
            }).ToList();

        return Json(result);
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public class AddToCartRequest
{
    public int TableId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Note { get; set; }
    public List<int>? OptionIds { get; set; }
}

public class UpdateDraftRequest
{
    public int TableId { get; set; }
    public Guid LocalId { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
}

public class RemoveDraftRequest
{
    public int TableId { get; set; }
    public Guid LocalId { get; set; }
}

public class CancelItemRequest
{
    public long CartItemId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public byte[]? RowVersion { get; set; }
}

public class EditItemRequest
{
    public long CartItemId { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public byte[]? RowVersion { get; set; }
}
