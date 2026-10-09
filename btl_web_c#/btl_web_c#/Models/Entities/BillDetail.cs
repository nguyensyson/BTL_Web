namespace RestaurantOrdering.Models.Entities;

public class BillDetail : BaseEntity
{
    public long Id { get; set; }
    public long BillId { get; set; }
    public long CartItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;  // Snapshot
    public string? OptionsText { get; set; }                  // Snapshot
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }

    // Navigation
    public Bill Bill { get; set; } = null!;
    public CartItem CartItem { get; set; } = null!;
}
