namespace RestaurantOrdering.ViewModels.Admin;

public class AuditLogListVm
{
    public long Id { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedTime { get; set; }

    /// Action hiển thị tiếng Việt
    public string ActionDisplay => Action switch
    {
        "CANCEL_ITEM" => "Hủy món",
        "EDIT_ITEM" => "Sửa món",
        "UNDO_STATUS" => "Hoàn tác",
        "MOVE_TABLE" => "Chuyển bàn",
        "MERGE_TABLE" => "Gộp bàn",
        "CHANGE_PRICE" => "Đổi giá",
        "ENABLE_ITEM" => "Bật món",
        "DISABLE_ITEM" => "Tắt món",
        "LOGIN" => "Đăng nhập",
        "CHANGE_ROLE" => "Đổi quyền",
        "CREATE" => "Tạo mới",
        "UPDATE" => "Cập nhật",
        "DELETE" => "Xóa",
        _ => Action
    };
}

public class AuditLogFilterVm
{
    public int? UserId { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
}
