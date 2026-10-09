namespace RestaurantOrdering.Models.Entities;

public class Payment : BaseEntity
{
    public long Id { get; set; }
    public long BillId { get; set; }
    public byte PaymentMethodId { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceNo { get; set; }
    public DateTime PaidTime { get; set; } = DateTime.UtcNow;
    public int CashierId { get; set; }

    // Navigation
    public Bill Bill { get; set; } = null!;
    public PaymentMethod PaymentMethod { get; set; } = null!;
    public ApplicationUser Cashier { get; set; } = null!;
}
