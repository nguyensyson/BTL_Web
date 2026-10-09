namespace RestaurantOrdering.Models.Entities;

public class CartStatus : BaseEntity
{
    public byte Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Navigation
    public ICollection<Cart> Carts { get; set; } = new List<Cart>();
}
