namespace RestaurantOrdering.Models.Entities;

public class CartItemStatusHistory : BaseEntity
{
    public long Id { get; set; }
    public long CartItemId { get; set; }
    public byte? FromStatusId { get; set; }
    public byte ToStatusId { get; set; }
    public int ChangedBy { get; set; }
    public DateTime ChangedTime { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }

    // Navigation
    public CartItem CartItem { get; set; } = null!;
}
