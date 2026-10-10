using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.Models.Entities;
using RestaurantOrdering.ViewModels.Admin;

namespace RestaurantOrdering.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CategoriesController(AppDbContext db) : Controller
{
    private const int PageSize = 10;

    // GET /Admin/Categories
    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var query = db.Categories.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.Name.Contains(q));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(c => new { c.Id, c.Name, c.Description, c.SortOrder,
                               ProductCount = c.Products.Count(p => !p.IsDeleted) })
            .ToListAsync();

        ViewBag.Q = q;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)PageSize);
        ViewBag.Total = total;
        return View(items);
    }

    // GET /Admin/Categories/Form?id=0  (AJAX partial)
    [HttpGet]
    public async Task<IActionResult> Form(int id = 0)
    {
        CategoryViewModel vm;
        if (id == 0)
            vm = new CategoryViewModel();
        else
        {
            var cat = await db.Categories.FindAsync(id);
            if (cat == null) return NotFound();
            vm = new CategoryViewModel { Id = cat.Id, Name = cat.Name, Description = cat.Description, SortOrder = cat.SortOrder };
        }
        return PartialView("_CategoryForm", vm);
    }

    // POST /Admin/Categories/Save
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CategoryViewModel vm)
    {
        if (!ModelState.IsValid)
            return PartialView("_CategoryForm", vm);

        // Kiểm tra trùng tên
        var duplicate = await db.Categories.AnyAsync(c => c.Name == vm.Name && c.Id != vm.Id);
        if (duplicate)
        {
            ModelState.AddModelError("Name", "Tên danh mục đã tồn tại.");
            return PartialView("_CategoryForm", vm);
        }

        if (vm.Id == 0)
        {
            db.Categories.Add(new Category { Name = vm.Name, Description = vm.Description, SortOrder = vm.SortOrder });
        }
        else
        {
            var cat = await db.Categories.FindAsync(vm.Id);
            if (cat == null) return NotFound();
            cat.Name = vm.Name;
            cat.Description = vm.Description;
            cat.SortOrder = vm.SortOrder;
        }
        await db.SaveChangesAsync();
        return Json(new { success = true, message = vm.Id == 0 ? "Thêm danh mục thành công." : "Cập nhật danh mục thành công." });
    }

    // POST /Admin/Categories/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await db.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);
        if (cat == null) return Json(new { success = false, message = "Không tìm thấy danh mục." });
        if (cat.Products.Any(p => !p.IsDeleted))
            return Json(new { success = false, message = "Danh mục còn món ăn, không thể xóa." });

        db.Categories.Remove(cat); // soft-delete qua interceptor
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Đã xóa danh mục." });
    }
}
