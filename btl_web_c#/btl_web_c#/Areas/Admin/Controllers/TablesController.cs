using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.Models.Entities;
using RestaurantOrdering.ViewModels.Admin;

namespace RestaurantOrdering.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class TablesController(AppDbContext db) : Controller
{
    private const int PageSize = 15;

    public async Task<IActionResult> Index(string? q, int? areaId, int page = 1)
    {
        var query = db.DiningTables.Include(t => t.Area).Include(t => t.TableStatus).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(t => t.Code.Contains(q));
        if (areaId.HasValue)
            query = query.Where(t => t.AreaId == areaId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(t => t.AreaId).ThenBy(t => t.SortOrder).ThenBy(t => t.Code)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync();

        ViewBag.Q = q; ViewBag.AreaId = areaId; ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)PageSize);
        ViewBag.Total = total;
        ViewBag.Areas = await db.Areas.OrderBy(a => a.SortOrder).Select(a => new SelectListItem(a.Name, a.Id.ToString())).ToListAsync();
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int id = 0)
    {
        var areas = await db.Areas.OrderBy(a => a.SortOrder)
            .Select(a => new SelectListItem(a.Name, a.Id.ToString())).ToListAsync();
        if (id == 0)
            return PartialView("_TableForm", new DiningTableViewModel { Areas = areas });

        var tbl = await db.DiningTables.FindAsync(id);
        if (tbl == null) return NotFound();
        return PartialView("_TableForm", new DiningTableViewModel
        {
            Id = tbl.Id, Code = tbl.Code, AreaId = tbl.AreaId,
            Capacity = tbl.Capacity, SortOrder = tbl.SortOrder, Areas = areas
        });
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(DiningTableViewModel vm)
    {
        vm.Areas = await db.Areas.OrderBy(a => a.SortOrder)
            .Select(a => new SelectListItem(a.Name, a.Id.ToString())).ToListAsync();

        if (!ModelState.IsValid) return PartialView("_TableForm", vm);
        if (await db.DiningTables.AnyAsync(t => t.Code == vm.Code && t.Id != vm.Id))
        {
            ModelState.AddModelError("Code", "Mã bàn đã tồn tại.");
            return PartialView("_TableForm", vm);
        }
        if (vm.Id == 0)
        {
            db.DiningTables.Add(new DiningTable
            {
                Code = vm.Code, AreaId = vm.AreaId,
                Capacity = vm.Capacity, StatusId = 1, SortOrder = vm.SortOrder
            });
        }
        else
        {
            var tbl = await db.DiningTables.FindAsync(vm.Id);
            if (tbl == null) return NotFound();
            tbl.Code = vm.Code; tbl.AreaId = vm.AreaId;
            tbl.Capacity = vm.Capacity; tbl.SortOrder = vm.SortOrder;
        }
        await db.SaveChangesAsync();
        return Json(new { success = true, message = vm.Id == 0 ? "Thêm bàn thành công." : "Cập nhật bàn thành công." });
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var tbl = await db.DiningTables.FindAsync(id);
        if (tbl == null) return Json(new { success = false, message = "Không tìm thấy bàn." });
        // Kiểm tra bàn đang phục vụ (StatusId != 1 = AVAILABLE)
        if (tbl.StatusId != 1)
            return Json(new { success = false, message = "Bàn đang được sử dụng, không thể xóa." });
        // Kiểm tra có lịch sử giao dịch (UsedTables)
        var hasHistory = await db.UsedTables.IgnoreQueryFilters().AnyAsync(ut => ut.TableId == id);
        if (hasHistory)
            return Json(new { success = false, message = "Bàn đã có lịch sử giao dịch. Dữ liệu đã được ẩn khỏi vận hành." });
        db.DiningTables.Remove(tbl);
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Đã xóa bàn." });
    }
}
