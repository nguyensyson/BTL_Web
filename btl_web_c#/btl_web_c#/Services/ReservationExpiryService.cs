using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;

namespace RestaurantOrdering.Services;

/// <summary>
/// Background service: quét đặt bàn quá giờ giữ, tự động nhả bàn.
/// Chạy mỗi 1 phút.
/// </summary>
public class ReservationExpiryService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ReservationExpiryService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReleaseExpiredReservationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi nhả bàn đặt trước quá giờ.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ReleaseExpiredReservationsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        // Tìm các đặt chỗ đang chờ (Status=0) mà đã quá HoldUntil
        var expired = await db.Reservations
            .Include(r => r.ReservationTables)
                .ThenInclude(rt => rt.DiningTable)
            .Where(r => r.Status == 0 && r.HoldUntil != null && r.HoldUntil < now)
            .ToListAsync(ct);

        if (expired.Count == 0) return;

        foreach (var res in expired)
        {
            // Giải phóng bàn (RESERVED → AVAILABLE)
            foreach (var rt in res.ReservationTables)
            {
                if (rt.DiningTable != null && rt.DiningTable.StatusId == 4) // RESERVED
                {
                    rt.DiningTable.StatusId = 1; // AVAILABLE
                    logger.LogInformation("Đã nhả bàn {TableCode} từ đặt trước của {Customer} (quá giờ)",
                        rt.DiningTable.Code, res.CustomerName);
                }
            }

            res.Status = 3; // Quá giờ
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã nhả {Count} đặt bàn quá giờ.", expired.Count);
    }
}
