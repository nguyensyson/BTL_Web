namespace RestaurantOrdering.ViewModels.Waiter;

/// <summary>Một món trong giỏ nháp lưu bằng Session trước khi gửi bếp.</summary>
public class CartDraftItem
{
    public Guid LocalId { get; set; } = Guid.NewGuid();
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }     // giá gốc
    public int Quantity { get; set; } = 1;
    public string? Note { get; set; }

    // Tùy chọn đã chọn
    public List<CartDraftOption> Options { get; set; } = new();

    public decimal TotalPrice => (UnitPrice + Options.Sum(o => o.ExtraPrice)) * Quantity;
}

public class CartDraftOption
{
    public int OptionId { get; set; }
    public string OptionName { get; set; } = string.Empty;
    public decimal ExtraPrice { get; set; }
}
