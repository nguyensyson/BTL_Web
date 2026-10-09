namespace RestaurantOrdering.Models.Entities;

public class Product : BaseEntity
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }

    /// <summary>1=Khai vị, 2=Món chính, 3=Tráng miệng</summary>
    public byte CourseGroup { get; set; } = 2;
    public bool IsAvailable { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public byte[]? RowVersion { get; set; }

    // Navigation
    public Category Category { get; set; } = null!;
    public ICollection<ProductItemOption> ProductItemOptions { get; set; } = new List<ProductItemOption>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
