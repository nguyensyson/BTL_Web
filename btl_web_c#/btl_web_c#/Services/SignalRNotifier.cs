using Microsoft.AspNetCore.SignalR;
using RestaurantOrdering.Hubs;

namespace RestaurantOrdering.Services;

/// <summary>
/// Triển khai IRealtimeNotifier bằng SignalR IHubContext.
/// Chỉ phát sự kiện sau khi SaveChanges thành công.
/// </summary>
public class SignalRNotifier(IHubContext<KitchenHub> hub) : IRealtimeNotifier
{
    // Bếp nhận đợt món mới
    public Task OrderSubmittedAsync(object roundPayload)
        => hub.Clients.Group("kitchen").SendAsync("OrderSubmitted", roundPayload);

    // Ai cũng có thể cần biết trạng thái đổi (bếp, phục vụ)
    public Task ItemStatusChangedAsync(object itemPayload)
        => hub.Clients.Groups("kitchen", "waiters").SendAsync("ItemStatusChanged", itemPayload);

    // Âm báo + toast riêng cho phục vụ phụ trách bàn
    public Task ItemReadyAsync(int waiterId, object itemPayload)
        => hub.Clients.Group($"waiter-{waiterId}").SendAsync("ItemReady", itemPayload);

    // Bếp từ chối
    public Task ItemRejectedAsync(int waiterId, object itemPayload)
        => hub.Clients.Group($"waiter-{waiterId}").SendAsync("ItemRejected", itemPayload);

    // Bật/tắt món trên menu
    public Task MenuItemAvailabilityChangedAsync(int productId, bool isAvailable, string productName)
        => hub.Clients.Groups("waiters", "kitchen")
               .SendAsync("MenuItemAvailabilityChanged", new { productId, isAvailable, productName });

    // Phục vụ sửa/hủy → bếp
    public Task OrderItemEditedAsync(object itemPayload)
        => hub.Clients.Group("kitchen").SendAsync("OrderItemEdited", itemPayload);

    public Task OrderItemCancelledAsync(object itemPayload)
        => hub.Clients.Group("kitchen").SendAsync("OrderItemCancelled", itemPayload);

    // TBL-01: Chuyển bàn
    public Task TableMovedAsync(int oldTableId, string oldTableCode, string newTableCode, string allTableCodes, long cartId)
    {
        var payload = new { oldTableId, oldTableCode, newTableCode, allTableCodes, cartId };
        return hub.Clients.Groups("kitchen").SendAsync("TableMoved", payload);
    }

    // TBL-02: Gộp bàn
    public Task TablesMergedAsync(int mergedTableId, string primaryTableCode, string allTableCodes, long cartId)
    {
        var payload = new { mergedTableId, primaryTableCode, allTableCodes, cartId };
        return hub.Clients.Groups("kitchen").SendAsync("TablesMerged", payload);
    }

    // KIT-03: Món quá SLA
    public Task ItemOverdueAsync(object itemPayload)
    {
        // Gửi tới bếp
        return hub.Clients.Group("kitchen").SendAsync("ItemOverdue", itemPayload);
    }

    // KIT-06: Hoàn tác trạng thái → bếp + waiter phụ trách
    public Task ItemStatusRevertedAsync(int waiterId, object itemPayload)
    {
        return hub.Clients.Groups("kitchen", $"waiter-{waiterId}")
               .SendAsync("ItemStatusReverted", itemPayload);
    }
}
