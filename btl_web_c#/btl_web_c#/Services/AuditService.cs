using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.Models.Entities;
using System.Security.Claims;
using System.Text.Json;

namespace RestaurantOrdering.Services;

/// <summary>
/// Ghi audit log cho các hành động: hủy/sửa món, hoàn tác, chuyển/gộp bàn,
/// đổi giá, bật/tắt món, đăng nhập, đổi quyền.
/// </summary>
public interface IAuditService
{
    Task LogAsync(string action, string entityName, string entityId,
        object? oldValues = null, object? newValues = null);

    Task LogLoginAsync(int userId, string ipAddress);

    Task LogChangePriceAsync(int productId, string productName,
        decimal oldPrice, decimal newPrice, int userId);

    Task LogToggleAvailabilityAsync(int productId, string productName,
        bool oldValue, bool newValue, int userId);
}

public class AuditService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor) : IAuditService
{
    private int? GetUserId()
    {
        var claim = httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private string? GetIpAddress()
    {
        return httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }

    public async Task LogAsync(string action, string entityName, string entityId,
        object? oldValues = null, object? newValues = null)
    {
        var log = new AuditLog
        {
            UserId = GetUserId(),
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
            IpAddress = GetIpAddress()
        };

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }

    public async Task LogLoginAsync(int userId, string ipAddress)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = "LOGIN",
            EntityName = "User",
            EntityId = userId.ToString(),
            IpAddress = ipAddress
        };

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }

    public async Task LogChangePriceAsync(int productId, string productName,
        decimal oldPrice, decimal newPrice, int userId)
    {
        await LogAsync("CHANGE_PRICE", "Product", productId.ToString(),
            new { productName, price = oldPrice },
            new { productName, price = newPrice });
    }

    public async Task LogToggleAvailabilityAsync(int productId, string productName,
        bool oldValue, bool newValue, int userId)
    {
        await LogAsync(newValue ? "ENABLE_ITEM" : "DISABLE_ITEM",
            "Product", productId.ToString(),
            new { productName, isAvailable = oldValue },
            new { productName, isAvailable = newValue });
    }
}
