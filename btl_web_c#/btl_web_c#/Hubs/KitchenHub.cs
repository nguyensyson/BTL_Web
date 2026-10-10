using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RestaurantOrdering.Hubs;

/// <summary>
/// Hub duy nhất — giao tiếp realtime giữa Waiter, Kitchen, Cashier.
/// Client kết nối qua /hubs/kitchen
/// Groups:
///   "kitchen"        → tất cả nhân viên bếp (đang kết nối)
///   "waiters"        → tất cả phục vụ
///   "waiter-{id}"   → từng phục vụ riêng (nhận thông báo món của bàn mình)
/// </summary>
[Authorize]
public class KitchenHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user == null) return;

        if (user.IsInRole("Kitchen"))
            await Groups.AddToGroupAsync(Context.ConnectionId, "kitchen");

        if (user.IsInRole("Waiter") || user.IsInRole("Admin"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "waiters");
            // Group riêng theo userId để nhận thông báo của bàn mình phụ trách
            var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"waiter-{userId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
