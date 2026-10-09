namespace RestaurantOrdering.Models.Entities;

public class Bill : BaseEntity
{
    public long Id { get; set; }
    public string BillNo { get; set; } = string.Empty;
    public long CartId { get; set; }
    public string? TableNames { get; set; }
    public decimal SubTotal { get; set; }
    public decimal ServiceFee { get; set; } = 0;
    public decimal TaxAmount { get; set; } = 0;
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TotalAmount { get; set; }

    /// <summary>0=Chưa TT, 1=Đã TT, 2=Đã hủy</summary>
    public byte Status { get; set; } = 0;
    public DateTime? PaidTime { get; set; }
    public int? CashierId { get; set; }

    // Navigation
    public Cart Cart { get; set; } = null!;
    public ApplicationUser? Cashier { get; set; }
    public ICollection<BillDetail> BillDetails { get; set; } = new List<BillDetail>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
