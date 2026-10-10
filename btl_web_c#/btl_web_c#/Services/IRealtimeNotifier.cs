namespace RestaurantOrdering.Services;

/// <summary>
/// Interface gửi sự kiện realtime — tách biệt business logic khỏi Hub.
/// </summary>
public interface IRealtimeNotifier
{
    // Waiter gửi bếp → bếp nhận đợt mới
    Task OrderSubmittedAsync(object roundPayload);

    // Bếp/Phục vụ đổi trạng thái món
    Task ItemStatusChangedAsync(object itemPayload);

    // Bếp báo xong (READY) → phục vụ bàn đó nhận âm báo
    Task ItemReadyAsync(int waiterId, object itemPayload);

    // Bếp từ chối món (REJECTED)
    Task ItemRejectedAsync(int waiterId, object itemPayload);

    // Bếp/Admin bật/tắt món trên menu
    Task MenuItemAvailabilityChangedAsync(int productId, bool isAvailable, string productName);

    // Phục vụ sửa/hủy món → bếp nhận
    Task OrderItemEditedAsync(object itemPayload);
    Task OrderItemCancelledAsync(object itemPayload);

    // TBL-01: Chuyển bàn → bếp cập nhật nhãn
    Task TableMovedAsync(int oldTableId, string oldTableCode, string newTableCode, string allTableCodes, long cartId);

    // TBL-02: Gộp bàn → bếp cập nhật nhãn
    Task TablesMergedAsync(int mergedTableId, string primaryTableCode, string allTableCodes, long cartId);

    // KIT-03: Món quá SLA → bếp + phục vụ phụ trách
    Task ItemOverdueAsync(object itemPayload);

    // KIT-06: Hoàn tác trạng thái món
    Task ItemStatusRevertedAsync(int waiterId, object itemPayload);
}
