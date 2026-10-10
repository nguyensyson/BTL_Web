using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.Models.Entities;
using RestaurantOrdering.ViewModels.Admin;

namespace RestaurantOrdering.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AreasController(AppDbContext db) : Controller
{
    private const int PageSize = 10;

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var query = db.Areas.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(a => a.Name.Contains(q));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Name)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(a => new { a.Id, a.Name, a.SortOrder, TableCount = a.DiningTables.Count(t => !t.IsDeleted) })
            .ToListAsync();

        ViewBag.Q = q; ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)PageSize);
        ViewBag.Total = total;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int id = 0)
    {
        if (id == 0) return PartialView("_AreaForm", new AreaViewModel());
        var area = await db.Areas.FindAsync(id);
        if (area == null) return NotFound();
        return PartialView("_AreaForm", new AreaViewModel { Id = area.Id, Name = area.Name, SortOrder = area.SortOrder });
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AreaViewModel vm)
    {
        if (!ModelState.IsValid) return PartialView("_AreaForm", vm);
        if (await db.Areas.AnyAsync(a => a.Name == vm.Name && a.Id != vm.Id))
        {
            ModelState.AddModelError("Name", "Tên khu vực đã tồn tại.");
            return PartialView("_AreaForm", vm);
        }
        if (vm.Id == 0)
            db.Areas.Add(new Area { Name = vm.Name, SortOrder = vm.SortOrder });
        else
        {
            var area = await db.Areas.FindAsync(vm.Id);
            if (area == null) return NotFound();
            area.Name = vm.Name; area.SortOrder = vm.SortOrder;
        }
        await db.SaveChangesAsync();
        return Json(new { success = true, message = vm.Id == 0 ? "Thêm khu vực thành công." : "Cập nhật khu vực thành công." });
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var area = await db.Areas.Include(a => a.DiningTables).FirstOrDefaultAsync(a => a.Id == id);
        if (area == null) return Json(new { success = false, message = "Không tìm thấy khu vực." });
        if (area.DiningTables.Any(t => !t.IsDeleted))
            return Json(new { success = false, message = "Khu vực còn bàn, không thể xóa." });
        db.Areas.Remove(area);
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Đã xóa khu vực." });
    }
}
