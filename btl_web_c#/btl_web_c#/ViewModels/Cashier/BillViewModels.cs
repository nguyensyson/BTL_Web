namespace RestaurantOrdering.ViewModels.Cashier;

public class BillListItemVm
{
    public long CartId { get; set; }
    public string CartCode { get; set; } = "";
    public string TableNames { get; set; } = "";
    public string AreaName { get; set; } = "";
    public int GuestCount { get; set; }
    public string WaiterName { get; set; } = "";
    public DateTime OpenedTime { get; set; }
    public decimal SubTotal { get; set; }
    public bool HasBill { get; set; }
    public byte BillStatus { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount => SubTotal - PaidAmount;
    public int UnpaidMinutes => (int)(DateTime.Now - OpenedTime).TotalMinutes;
}

public class PayBillVm
{
    public long CartId { get; set; }
    public string CartCode { get; set; } = "";
    public string TableNames { get; set; } = "";
    public string AreaName { get; set; } = "";
    public int GuestCount { get; set; }
    public string WaiterName { get; set; } = "";
    public DateTime OpenedTime { get; set; }
    public List<BillItemVm> Items { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal ServiceFeeRate { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public bool IsPaid { get; set; }
    public string? BillNo { get; set; }
    public List<PaymentRecordVm> Payments { get; set; } = new();
    public string RestaurantName { get; set; } = "";
    public string RestaurantAddress { get; set; } = "";
    public string RestaurantPhone { get; set; } = "";
}

public class BillItemVm
{
    public long CartItemId { get; set; }
    public string ProductName { get; set; } = "";
    public string? OptionsText { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public byte StatusId { get; set; }
    public string StatusName { get; set; } = "";
}

public class PaymentRecordVm
{
    public long PaymentId { get; set; }
    public string MethodName { get; set; } = "";
    public decimal Amount { get; set; }
    public string? ReferenceNo { get; set; }
    public DateTime PaidTime { get; set; }
}
