using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.Models.Entities;
using RestaurantOrdering.Services;
using RestaurantOrdering.ViewModels.Admin;

namespace RestaurantOrdering.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class MenuController(AppDbContext db, IWebHostEnvironment env, IRealtimeNotifier notifier) : Controller
{
    private const int PageSize = 12;
    private static readonly string[] AllowedExt = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxImageSize = 2 * 1024 * 1024; // 2 MB

    // ── Index ──────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index(string? q, int? categoryId, bool? available, int page = 1)
    {
        var query = db.Products.Include(p => p.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Name.Contains(q) || p.Code.Contains(q));
        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);
        if (available.HasValue)
            query = query.Where(p => p.IsAvailable == available.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.CategoryId).ThenBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync();

        ViewBag.Q = q; ViewBag.CategoryId = categoryId; ViewBag.Available = available;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)PageSize);
        ViewBag.Total = total;
        ViewBag.Categories = await db.Categories.OrderBy(c => c.SortOrder)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();
        return View(items);
    }

    // ── Form (GET) ─────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Form(int id = 0)
    {
        var categories = await db.Categories.OrderBy(c => c.SortOrder)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();

        if (id == 0)
            return PartialView("_ProductForm", new ProductViewModel { Categories = categories });

        var p = await db.Products.Include(p => p.ProductItemOptions)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (p == null) return NotFound();

        return PartialView("_ProductForm", new ProductViewModel
        {
            Id = p.Id, Code = p.Code, Name = p.Name, CategoryId = p.CategoryId,
            Price = p.Price, CourseGroup = p.CourseGroup, Description = p.Description,
            IsAvailable = p.IsAvailable, SortOrder = p.SortOrder, ImageUrl = p.ImageUrl,
            Categories = categories,
            Options = p.ProductItemOptions.OrderBy(o => o.SortOrder).Select(o => new ProductOptionViewModel
            {
                Id = o.Id, Name = o.Name, GroupName = o.GroupName,
                ExtraPrice = o.ExtraPrice, IsAvailable = o.IsAvailable, SortOrder = o.SortOrder
            }).ToList()
        });
    }

    // ── Save (POST) ────────────────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ProductViewModel vm)
    {
        vm.Categories = await db.Categories.OrderBy(c => c.SortOrder)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();

        // Validate ảnh
        if (vm.ImageFile != null)
        {
            var ext = Path.GetExtension(vm.ImageFile.FileName).ToLower();
            if (!AllowedExt.Contains(ext))
                ModelState.AddModelError("ImageFile", "Chỉ chấp nhận ảnh .jpg, .jpeg, .png, .webp");
            if (vm.ImageFile.Length > MaxImageSize)
                ModelState.AddModelError("ImageFile", "Ảnh tối đa 2 MB.");
        }

        if (!ModelState.IsValid) return PartialView("_ProductForm", vm);

        if (await db.Products.AnyAsync(p => p.Code == vm.Code && p.Id != vm.Id))
        {
            ModelState.AddModelError("Code", "Mã món đã tồn tại.");
            return PartialView("_ProductForm", vm);
        }

        // Upload ảnh
        string? imageUrl = vm.ImageUrl;
        if (vm.ImageFile != null && vm.ImageFile.Length > 0)
        {
            var folder = Path.Combine(env.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(folder);
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(vm.ImageFile.FileName).ToLower()}";
            await using var stream = System.IO.File.Create(Path.Combine(folder, fileName));
            await vm.ImageFile.CopyToAsync(stream);
            imageUrl = $"/uploads/products/{fileName}";
        }

        if (vm.Id == 0)
        {
            var product = new Product
            {
                Code = vm.Code, Name = vm.Name, CategoryId = vm.CategoryId,
                Price = vm.Price, CourseGroup = vm.CourseGroup,
                Description = vm.Description, IsAvailable = vm.IsAvailable,
                SortOrder = vm.SortOrder, ImageUrl = imageUrl
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            // Lưu options
            await SaveOptions(product.Id, vm.Options);
        }
        else
        {
            var product = await db.Products.Include(p => p.ProductItemOptions)
                .FirstOrDefaultAsync(p => p.Id == vm.Id);
            if (product == null) return NotFound();
            product.Code = vm.Code; product.Name = vm.Name; product.CategoryId = vm.CategoryId;
            product.Price = vm.Price; product.CourseGroup = vm.CourseGroup;
            product.Description = vm.Description; product.IsAvailable = vm.IsAvailable;
            product.SortOrder = vm.SortOrder;
            if (imageUrl != null) product.ImageUrl = imageUrl;
            await db.SaveChangesAsync();
            await SaveOptions(product.Id, vm.Options);
        }
        return Json(new { success = true, message = vm.Id == 0 ? "Thêm món thành công." : "Cập nhật món thành công." });
    }

    // ── Toggle Availability ────────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAvailability(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product == null) return Json(new { success = false });
        product.IsAvailable = !product.IsAvailable;
        await db.SaveChangesAsync();
        // Phát realtime cho phục vụ và bếp
        await notifier.MenuItemAvailabilityChangedAsync(product.Id, product.IsAvailable, product.Name);
        return Json(new { success = true, isAvailable = product.IsAvailable });
    }

    // ── Delete ─────────────────────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product == null) return Json(new { success = false, message = "Không tìm thấy món." });
        db.Products.Remove(product);
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Đã ẩn món ăn khỏi hệ thống." });
    }

    // ── DeleteOption ───────────────────────────────────────────────────────
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOption(int id)
    {
        var opt = await db.ProductItemOptions.FindAsync(id);
        if (opt == null) return Json(new { success = false });
        db.ProductItemOptions.Remove(opt);
        await db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ── Helper ─────────────────────────────────────────────────────────────
    private async Task SaveOptions(int productId, List<ProductOptionViewModel> options)
    {
        var existingIds = options.Where(o => o.Id > 0).Select(o => o.Id).ToList();
        // Xóa options không còn trong danh sách
        var toDelete = await db.ProductItemOptions
            .Where(o => o.ProductId == productId && !existingIds.Contains(o.Id)).ToListAsync();
        db.ProductItemOptions.RemoveRange(toDelete);

        foreach (var opt in options)
        {
            if (opt.Id == 0)
            {
                db.ProductItemOptions.Add(new ProductItemOption
                {
                    ProductId = productId, Name = opt.Name, GroupName = opt.GroupName,
                    ExtraPrice = opt.ExtraPrice, IsAvailable = opt.IsAvailable, SortOrder = opt.SortOrder
                });
            }
            else
            {
                var existing = await db.ProductItemOptions.FindAsync(opt.Id);
                if (existing == null) continue;
                existing.Name = opt.Name; existing.GroupName = opt.GroupName;
                existing.ExtraPrice = opt.ExtraPrice; existing.IsAvailable = opt.IsAvailable;
                existing.SortOrder = opt.SortOrder;
            }
        }
        await db.SaveChangesAsync();
    }
}
