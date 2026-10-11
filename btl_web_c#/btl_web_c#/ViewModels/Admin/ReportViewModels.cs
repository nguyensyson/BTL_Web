namespace RestaurantOrdering.ViewModels.Admin;

public class ReportFilterVm
{
    public DateTime FromDate { get; set; } = DateTime.Today.AddDays(-29);
    public DateTime ToDate { get; set; } = DateTime.Today;
}

// Doanh thu theo ngày
public class DailyRevenueVm
{
    public DateOnly Date { get; set; }
    public int BillCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public int MaxRevenue { get; set; } // cho thanh ngang CSS
}

// Báo cáo tổng hợp doanh thu
public class RevenueReportVm
{
    public ReportFilterVm Filter { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public int TotalBills { get; set; }
    public decimal AvgBillValue { get; set; }
    public List<DailyRevenueVm> Daily { get; set; } = new();
}

// Món bán chạy
public class TopProductReportVm
{
    public ReportFilterVm Filter { get; set; } = new();
    public List<TopProductItemVm> Items { get; set; } = new();
}

public class TopProductItemVm
{
    public string ProductName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public int TotalQty { get; set; }
    public decimal TotalRevenue { get; set; }
    public int MaxQty { get; set; }
}

// Giờ cao điểm
public class PeakHourReportVm
{
    public ReportFilterVm Filter { get; set; } = new();
    public List<PeakHourItemVm> Items { get; set; } = new();
}

public class PeakHourItemVm
{
    public int Hour { get; set; }
    public int BillCount { get; set; }
    public decimal Revenue { get; set; }
    public int MaxBills { get; set; }
}

// Hiệu suất bếp
public class KitchenPerformanceReportVm
{
    public ReportFilterVm Filter { get; set; } = new();
    public double AvgCookingMinutes { get; set; }
    public int TotalServedItems { get; set; }
    public int TotalCancelledItems { get; set; }
    public int TotalRejectedItems { get; set; }
    public List<ProductCookingTimeVm> ProductTimes { get; set; } = new();
}

public class ProductCookingTimeVm
{
    public string ProductName { get; set; } = "";
    public int ItemCount { get; set; }
    public double AvgMinutes { get; set; }
}
