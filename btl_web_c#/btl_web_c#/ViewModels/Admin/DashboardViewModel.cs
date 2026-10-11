namespace RestaurantOrdering.ViewModels.Admin;

public class DashboardViewModel
{
    // Thẻ số liệu
    public decimal TodayRevenue { get; set; }
    public int TodayBillCount { get; set; }
    public int OccupiedTableCount { get; set; }
    public int TotalTableCount { get; set; }
    public int OverdueSlaCount { get; set; }

    // Bảng top 5 món bán chạy hôm nay
    public List<TopProductVm> TopProducts { get; set; } = new();

    // Bảng 10 bill gần nhất
    public List<RecentBillVm> RecentBills { get; set; } = new();
}

public class TopProductVm
{
    public string ProductName { get; set; } = "";
    public int TotalQty { get; set; }
    public decimal TotalRevenue { get; set; }
    public int MaxQty { get; set; } // để vẽ thanh ngang CSS
}

public class RecentBillVm
{
    public long BillId { get; set; }
    public string BillNo { get; set; } = "";
    public string TableNames { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public byte Status { get; set; }
    public string StatusText => Status switch { 0 => "Chưa TT", 1 => "Đã TT", 2 => "Đã hủy", _ => "?" };
    public string StatusBadge => Status switch { 0 => "warning", 1 => "success", 2 => "danger", _ => "secondary" };
    public DateTime CreatedTime { get; set; }
}
