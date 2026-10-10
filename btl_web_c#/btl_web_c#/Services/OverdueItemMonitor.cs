using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;

namespace RestaurantOrdering.Services;

/// <summary>
/// Background service: quét món chờ quá SLA, phát cảnh báo.
/// Chạy mỗi 30 giây.
/// </summary>
public class OverdueItemMonitor(
    IServiceScopeFactory scopeFactory,
    ILogger<OverdueItemMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OverdueItemMonitor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOverdueItemsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi kiểm tra món quá SLA.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task CheckOverdueItemsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var slaSetting = await db.SystemSettings
            .Where(s => s.SettingKey == "ItemSlaMinutes")
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(ct);

        var slaMinutes = int.TryParse(slaSetting, out var m) ? m : 15;

        var now = DateTime.UtcNow;

        // Tìm món CONFIRMED/COOKING đang chờ quá SLA
        var overdueItems = await db.CartItems
            .Include(ci => ci.Cart)
                .ThenInclude(c => c.UsedTables.Where(u => u.EndTime == null))
            .Where(ci => (ci.StatusId == 2 || ci.StatusId == 3) && ci.IsDeleted == false)
            .Where(ci => ci.ConfirmedTime.HasValue &&
                EF.Functions.DateDiffMinute(ci.ConfirmedTime.Value, now) >= slaMinutes)
            .ToListAsync(ct);

        if (overdueItems.Count == 0) return;

        foreach (var item in overdueItems)
        {
            var waiterId = item.Cart?.WaiterId ?? 0;
            var tableCodes = item.Cart?.UsedTables
                .Select(u => u.DiningTable?.Code ?? "?")
                .ToList() ?? new List<string>();

            var payload = new
            {
                id = item.Id,
                productName = item.ProductName,
                tableCode = string.Join(" + ", tableCodes),
                waiterId,
                waitMinutes = slaMinutes,
                statusId = item.StatusId
            };

            // Phát sự kiện ItemOverdue tới bếp và phục vụ
            var notifier = scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>();
            await notifier.ItemOverdueAsync(payload);

            logger.LogWarning("Item {ItemId} ({Product}) đã chờ {Minutes} phút (SLA: {Sla})",
                item.Id, item.ProductName, slaMinutes, slaMinutes);
        }
    }
}
