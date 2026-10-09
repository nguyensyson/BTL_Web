namespace RestaurantOrdering.Models.Entities;

public class ProductItemOption : BaseEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string? GroupName { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal ExtraPrice { get; set; } = 0;
    public bool IsAvailable { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    // Navigation
    public Product Product { get; set; } = null!;
    public ICollection<CartItemOption> CartItemOptions { get; set; } = new List<CartItemOption>();
}
