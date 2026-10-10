using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.Models.Entities;
using RestaurantOrdering.Services;
using RestaurantOrdering.ViewModels.Cashier;

namespace RestaurantOrdering.Areas.Cashier.Controllers;

[Area("Cashier")]
[Authorize(Policy = "CanPay")]
public class BillsController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IRealtimeNotifier notifier) : Controller
{
    // ── GET /Cashier ──────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        // Lấy các cart đang WAITING_PAYMENT hoặc đã có bill chưa thanh toán
        var carts = await db.Carts
            .Include(c => c.UsedTables.Where(u => u.EndTime == null))
                .ThenInclude(u => u.DiningTable)
                    .ThenInclude(t => t!.Area)
            .Include(c => c.Waiter)
            .Include(c => c.Bill)
            .Where(c => c.StatusId == 2 || (c.StatusId == 3 && c.Bill != null && c.Bill.Status == 0))
            .OrderBy(c => c.OpenedTime)
            .ToListAsync();

        var vm = carts.Select(c =>
        {
            // Tính tạm tính từ CartItems hợp lệ (không phải DRAFT/CANCELLED/REJECTED)
            var validItems = c.CartItems
                .Where(ci => ci.StatusId >= 2 && ci.StatusId <= 5 && ci.StatusId != 6 && ci.StatusId != 7)
                .ToList();

            var subTotal = validItems.Sum(ci =>
            {
                var basePrice = ci.UnitPrice * ci.Quantity;
                var optionPrice = ci.CartItemOptions.Sum(o => o.ExtraPrice) * ci.Quantity;
                return basePrice + optionPrice;
            });

            var tables = c.UsedTables
                .Where(u => u.EndTime == null)
                .Select(u => u.DiningTable?.Code ?? "?")
                .ToList();

            return new BillListItemVm
            {
                CartId = c.Id,
                CartCode = c.CartCode,
                TableNames = string.Join(", ", tables),
                AreaName = c.UsedTables.FirstOrDefault()?.DiningTable?.Area?.Name ?? "",
                GuestCount = c.GuestCount,
                WaiterName = c.Waiter.FullName,
                OpenedTime = c.OpenedTime,
                SubTotal = subTotal,
                HasBill = c.Bill != null,
                BillStatus = c.Bill?.Status ?? (byte)0,
                PaidAmount = c.Bill?.Payments.Sum(p => p.Amount) ?? 0
            };
        }).ToList();

        ViewData["Title"] = "Thanh toán";
        ViewData["AreaTitle"] = "Thu ngân";
        return View(vm);
    }

    // ── GET /Cashier/Bills/Pay/{cartId} ────────────────────────────
    public async Task<IActionResult> Pay(long cartId)
    {
        var cart = await db.Carts
            .Include(c => c.UsedTables.Where(u => u.EndTime == null))
                .ThenInclude(u => u.DiningTable)
                    .ThenInclude(t => t!.Area)
            .Include(c => c.Waiter)
            .Include(c => c.CartItems.Where(ci => ci.StatusId != 1)) // loại DRAFT
                .ThenInclude(ci => ci.CartItemOptions)
            .Include(c => c.Bill)
                .ThenInclude(b => b!.Payments)
                    .ThenInclude(p => p.PaymentMethod)
            .Include(c => c.Bill)
                .ThenInclude(b => b!.BillDetails)
            .FirstOrDefaultAsync(c => c.Id == cartId);

        if (cart == null) return NotFound();

        // Kiểm tra trạng thái cart
        if (cart.StatusId != 2 && cart.StatusId != 3)
            return RedirectToAction("Index");

        // Load cấu hình
        var settings = await db.SystemSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
        var taxRate = decimal.TryParse(settings.GetValueOrDefault("TaxRate", "0.08"), out var tr) ? tr : 0.08m;
        var serviceFeeRate = decimal.TryParse(settings.GetValueOrDefault("ServiceFeeRate", "0.05"), out var sfr) ? sfr : 0.05m;
        var restaurantName = settings.GetValueOrDefault("RestaurantName", "Nhà hàng");
        var restaurantAddress = settings.GetValueOrDefault("RestaurantAddress", "");
        var restaurantPhone = settings.GetValueOrDefault("RestaurantPhone", "");

        // Tính tổng từ CartItems hợp lệ
        var items = cart.CartItems
            .Where(ci => ci.StatusId >= 2 && ci.StatusId != 6 && ci.StatusId != 7)
            .Select(ci =>
            {
                var baseTotal = ci.UnitPrice * ci.Quantity;
                var optionTotal = ci.CartItemOptions.Sum(o => o.ExtraPrice) * ci.Quantity;
                var lineTotal = baseTotal + optionTotal;
                return new BillItemVm
                {
                    CartItemId = ci.Id,
                    ProductName = ci.ProductName,
                    OptionsText = ci.CartItemOptions.Any()
                        ? string.Join(", ", ci.CartItemOptions.Select(o => o.OptionName))
                        : null,
                    UnitPrice = ci.UnitPrice + ci.CartItemOptions.Sum(o => o.ExtraPrice),
                    Quantity = ci.Quantity,
                    LineTotal = lineTotal,
                    StatusId = ci.StatusId,
                    StatusName = ci.StatusId switch
                    {
                        2 => "Chờ làm",
                        3 => "Đang làm",
                        4 => "Đã xong",
                        5 => "Đã phục vụ",
                        _ => "?"
                    }
                };
            }).ToList();

        var subTotal = items.Sum(i => i.LineTotal);
        var serviceFee = Math.Round(subTotal * serviceFeeRate, 0);
        var taxAmount = Math.Round((subTotal + serviceFee) * taxRate, 0);
        var totalAmount = subTotal + serviceFee + taxAmount;

        // Bill hiện có (nếu đã tạo)
        var bill = cart.Bill;
        var paidAmount = bill?.Payments.Sum(p => p.Amount) ?? 0;
        var remainingAmount = totalAmount - paidAmount;

        var tables = cart.UsedTables
            .Where(u => u.EndTime == null)
            .Select(u => u.DiningTable?.Code ?? "?")
            .ToList();

        var vm = new PayBillVm
        {
            CartId = cart.Id,
            CartCode = cart.CartCode,
            TableNames = string.Join(", ", tables),
            AreaName = cart.UsedTables.FirstOrDefault()?.DiningTable?.Area?.Name ?? "",
            GuestCount = cart.GuestCount,
            WaiterName = cart.Waiter.FullName,
            OpenedTime = cart.OpenedTime,
            Items = items,
            SubTotal = subTotal,
            ServiceFeeRate = serviceFeeRate,
            ServiceFee = serviceFee,
            TaxRate = taxRate,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
            PaidAmount = paidAmount,
            RemainingAmount = remainingAmount,
            IsPaid = bill?.Status == 1,
            BillNo = bill?.BillNo,
            Payments = bill?.Payments.Select(p => new PaymentRecordVm
            {
                PaymentId = p.Id,
                MethodName = p.PaymentMethod.Name,
                Amount = p.Amount,
                ReferenceNo = p.ReferenceNo,
                PaidTime = p.PaidTime
            }).ToList() ?? new(),
            RestaurantName = restaurantName,
            RestaurantAddress = restaurantAddress,
            RestaurantPhone = restaurantPhone
        };

        ViewData["Title"] = $"Thanh toán – {cart.CartCode}";
        return View(vm);
    }

    // ── GET /api/cashier/bills/{cartId}/preview ────────────────────
    [HttpGet("/api/cashier/bills/{cartId:long}/preview")]
    public async Task<IActionResult> GetBillPreview(long cartId)
    {
        var cart = await db.Carts
            .Include(c => c.CartItems.Where(ci => ci.StatusId != 1))
                .ThenInclude(ci => ci.CartItemOptions)
            .Include(c => c.Bill).ThenInclude(b => b!.Payments)
            .FirstOrDefaultAsync(c => c.Id == cartId);

        if (cart == null) return NotFound();

        var settings = await db.SystemSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
        var taxRate = decimal.TryParse(settings.GetValueOrDefault("TaxRate", "0.08"), out var tr) ? tr : 0.08m;
        var serviceFeeRate = decimal.TryParse(settings.GetValueOrDefault("ServiceFeeRate", "0.05"), out var sfr) ? sfr : 0.05m;

        var items = cart.CartItems
            .Where(ci => ci.StatusId >= 2 && ci.StatusId != 6 && ci.StatusId != 7)
            .Select(ci => new
            {
                cartItemId = ci.Id,
                productName = ci.ProductName,
                optionsText = ci.CartItemOptions.Any()
                    ? string.Join(", ", ci.CartItemOptions.Select(o => o.OptionName))
                    : null,
                unitPrice = ci.UnitPrice + ci.CartItemOptions.Sum(o => o.ExtraPrice),
                quantity = ci.Quantity,
                lineTotal = (ci.UnitPrice + ci.CartItemOptions.Sum(o => o.ExtraPrice)) * ci.Quantity,
                statusId = ci.StatusId,
                statusName = ci.StatusId switch { 2 => "Chờ làm", 3 => "Đang làm", 4 => "Đã xong", 5 => "Đã phục vụ", _ => "?" }
            }).ToList();

        var subTotal = items.Sum(i => i.lineTotal);
        var serviceFee = Math.Round(subTotal * serviceFeeRate, 0);
        var taxAmount = Math.Round((subTotal + serviceFee) * taxRate, 0);
        var totalAmount = subTotal + serviceFee + taxAmount;
        var paidAmount = cart.Bill?.Payments.Sum(p => p.Amount) ?? 0;

        return Json(new
        {
            cartId,
            items,
            subTotal,
            serviceFee,
            taxAmount,
            totalAmount,
            paidAmount,
            remainingAmount = totalAmount - paidAmount,
            unpaidItemCount = items.Count(i => i.statusId < 5) // chưa SERVED
        });
    }

    // ── POST /api/cashier/bills/{cartId}/pay ───────────────────────
    [HttpPost("/api/cashier/bills/{cartId:long}/pay")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessPayment(long cartId, [FromBody] ProcessPaymentRequest req)
    {
        if (req.Amount <= 0)
            return BadRequest(new { message = "Số tiền phải lớn hơn 0." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var cart = await db.Carts
            .Include(c => c.UsedTables.Where(u => u.EndTime == null))
            .Include(c => c.CartItems.Where(ci => ci.StatusId != 1))
                .ThenInclude(ci => ci.CartItemOptions)
            .Include(c => c.Bill)
                .ThenInclude(b => b!.Payments)
            .FirstOrDefaultAsync(c => c.Id == cartId);

        if (cart == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });

        // Lấy cấu hình
        var settings = await db.SystemSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
        var taxRate = decimal.TryParse(settings.GetValueOrDefault("TaxRate", "0.08"), out var tr) ? tr : 0.08m;
        var serviceFeeRate = decimal.TryParse(settings.GetValueOrDefault("ServiceFeeRate", "0.05"), out var sfr) ? sfr : 0.05m;

        // Tính tổng
        var validItems = cart.CartItems
            .Where(ci => ci.StatusId >= 2 && ci.StatusId != 6 && ci.StatusId != 7)
            .ToList();

        var subTotal = validItems.Sum(ci =>
            (ci.UnitPrice + ci.CartItemOptions.Sum(o => o.ExtraPrice)) * ci.Quantity);
        var serviceFee = Math.Round(subTotal * serviceFeeRate, 0);
        var taxAmount = Math.Round((subTotal + serviceFee) * taxRate, 0);
        var totalAmount = subTotal + serviceFee + taxAmount;

        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Tạo bill nếu chưa có
        var bill = cart.Bill;
        if (bill == null)
        {
            bill = new Bill
            {
                BillNo = GenerateBillNo(),
                CartId = cart.Id,
                TableNames = string.Join(" + ", cart.UsedTables
                    .Where(u => u.EndTime == null)
                    .Select(u => u.DiningTable?.Code ?? "?")),
                SubTotal = subTotal,
                ServiceFee = serviceFee,
                TaxAmount = taxAmount,
                DiscountAmount = 0,
                TotalAmount = totalAmount,
                Status = 0,
                CashierId = user.Id
            };
            db.Bills.Add(bill);
            await db.SaveChangesAsync();

            // Tạo BillDetails (chỉ món hợp lệ)
            foreach (var ci in validItems)
            {
                db.BillDetails.Add(new BillDetail
                {
                    BillId = bill.Id,
                    CartItemId = ci.Id,
                    ProductName = ci.ProductName,
                    OptionsText = ci.CartItemOptions.Any()
                        ? string.Join(", ", ci.CartItemOptions.Select(o => o.OptionName))
                        : null,
                    UnitPrice = ci.UnitPrice + ci.CartItemOptions.Sum(o => o.ExtraPrice),
                    Quantity = ci.Quantity,
                    LineTotal = (ci.UnitPrice + ci.CartItemOptions.Sum(o => o.ExtraPrice)) * ci.Quantity
                });
            }
            await db.SaveChangesAsync();
        }

        // Thêm payment
        var payment = new Payment
        {
            BillId = bill.Id,
            PaymentMethodId = req.PaymentMethodId,
            Amount = req.Amount,
            ReferenceNo = req.ReferenceNo?.Trim(),
            PaidTime = DateTime.UtcNow,
            CashierId = user.Id
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        // Tính tổng đã trả
        var totalPaid = await db.Payments.Where(p => p.BillId == bill.Id).SumAsync(p => p.Amount);

        // Kiểm tra đã đủ chưa
        if (totalPaid >= totalAmount)
        {
            // Đủ tiền → thanh toán thành công
            bill.Status = 1; // Đã thanh toán
            bill.PaidTime = DateTime.UtcNow;
            bill.CashierId = user.Id;
            cart.StatusId = 3; // PAID
            cart.ClosedTime = DateTime.UtcNow;

            // Đóng các UsedTables
            foreach (var ut in cart.UsedTables.Where(u => u.EndTime == null))
            {
                ut.EndTime = DateTime.UtcNow;
            }

            // Cập nhật trạng thái bàn về AVAILABLE
            var tableIds = cart.UsedTables.Select(u => u.TableId).ToList();
            var tables = await db.DiningTables.Where(t => tableIds.Contains(t.Id)).ToListAsync();
            foreach (var t in tables)
            {
                t.StatusId = 1; // AVAILABLE
            }

            await db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                paid = true,
                totalPaid,
                totalAmount,
                change = totalPaid - totalAmount,
                billNo = bill.BillNo,
                message = $"Thanh toán thành công! Mã bill: {bill.BillNo}"
            });
        }

        // Chưa đủ
        return Json(new
        {
            success = true,
            paid = false,
            totalPaid,
            totalAmount,
            remainingAmount = totalAmount - totalPaid,
            paymentMethod = req.PaymentMethodId,
            message = $"Đã nhận {totalPaid:N0} ₫. Còn thiếu {(totalAmount - totalPaid):N0} ₫"
        });
    }

    // ── GET /Cashier/Bills/Print/{cartId} ──────────────────────────
    public async Task<IActionResult> Print(long cartId)
    {
        var bill = await db.Bills
            .Include(b => b.Cart)
                .ThenInclude(c => c.Waiter)
            .Include(b => b.Cashier)
            .Include(b => b.BillDetails)
            .Include(b => b.Payments)
                .ThenInclude(p => p.PaymentMethod)
            .FirstOrDefaultAsync(b => b.CartId == cartId);

        if (bill == null) return NotFound();

        var settings = await db.SystemSettings.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

        ViewData["Title"] = $"Hóa đơn {bill.BillNo}";
        ViewData["Layout"] = null; // Print layout
        ViewBag.Bill = bill;
        ViewBag.Settings = settings;
        return View();
    }

    // ── Helpers ─────────────────────────────────────────────────────
    private static string GenerateBillNo()
    {
        return $"HD{DateTime.Now:yyyyMMdd}{DateTime.Now:HHmmss}";
    }
}

public class ProcessPaymentRequest
{
    public byte PaymentMethodId { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceNo { get; set; }
}
