using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.ViewModels.Admin;

namespace RestaurantOrdering.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AuditLogsController(AppDbContext db) : Controller
{
    private const int PageSize = 20;

    // ── GET /Admin/AuditLogs ─────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AuditLogFilterVm filter)
    {
        var query = db.AuditLogs
            .Include(a => a.User)
            .AsQueryable();

        // Lọc theo người dùng
        if (filter.UserId.HasValue)
            query = query.Where(a => a.UserId == filter.UserId.Value);

        // Lọc theo action
        if (!string.IsNullOrWhiteSpace(filter.Action))
            query = query.Where(a => a.Action == filter.Action);

        // Lọc theo entity
        if (!string.IsNullOrWhiteSpace(filter.EntityName))
            query = query.Where(a => a.EntityName == filter.EntityName);

        // Lọc theo khoảng thời gian
        if (filter.FromDate.HasValue)
            query = query.Where(a => a.CreatedTime >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(a => a.CreatedTime <= filter.ToDate.Value);

        // Sắp xếp: mới nhất trước
        query = query.OrderByDescending(a => a.CreatedTime);

        // Tổng số bản ghi
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        // Phân trang
        var logs = await query
            .Skip((filter.Page - 1) * PageSize)
            .Take(PageSize)
            .Select(a => new AuditLogListVm
            {
                Id = a.Id,
                UserName = a.User != null ? a.User.FullName : "Hệ thống",
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                IpAddress = a.IpAddress,
                CreatedTime = a.CreatedTime
            })
            .ToListAsync();

        // Danh sách action & entity duy nhất cho bộ lọc
        var actionList = await db.AuditLogs
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        var entityList = await db.AuditLogs
            .Select(a => a.EntityName)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();

        var userList = await db.Users
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync();

        ViewData["Title"] = "Nhật ký hoạt động";
        ViewData["Breadcrumb"] = "Nhật ký hoạt động";
        ViewBag.Filter = filter;
        ViewBag.ActionList = actionList;
        ViewBag.EntityList = entityList;
        ViewBag.UserList = userList;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalPages;
        ViewBag.CurrentPage = filter.Page;

        return View(logs);
    }

    // ── GET /Admin/AuditLogs/Details/{id} ────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        var log = await db.AuditLogs
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (log == null)
            return NotFound();

        return View(log);
    }
}
