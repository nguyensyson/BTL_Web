namespace RestaurantOrdering.Models.Entities;

public class CartItemStatus : BaseEntity
{
    public byte Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
    public bool ShowOnKitchen { get; set; } = false;

    // Navigation
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
