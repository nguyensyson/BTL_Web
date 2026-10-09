namespace RestaurantOrdering.Models.Entities;

public class CartItemOption : BaseEntity
{
    public long Id { get; set; }
    public long CartItemId { get; set; }
    public int ProductItemOptionId { get; set; }
    public string OptionName { get; set; } = string.Empty;  // Snapshot
    public decimal ExtraPrice { get; set; }                  // Snapshot

    // Navigation
    public CartItem CartItem { get; set; } = null!;
    public ProductItemOption ProductItemOption { get; set; } = null!;
}
