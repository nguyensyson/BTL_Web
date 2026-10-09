namespace RestaurantOrdering.Models.Entities;

public class CartItem : BaseEntity
{
    public long Id { get; set; }
    public long CartId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;  // Snapshot
    public decimal UnitPrice { get; set; }                    // Snapshot
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public byte StatusId { get; set; }
    public int? RoundNo { get; set; }
    public DateTime? ConfirmedTime { get; set; }
    public int? ConfirmedBy { get; set; }
    public DateTime? CookingStartTime { get; set; }
    public DateTime? ReadyTime { get; set; }
    public DateTime? ServedTime { get; set; }
    public bool IsPriority { get; set; } = false;
    public bool IsEdited { get; set; } = false;
    public string? CancelReason { get; set; }
    public byte[]? RowVersion { get; set; }

    // Navigation
    public Cart Cart { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public CartItemStatus CartItemStatus { get; set; } = null!;
    public ICollection<CartItemOption> CartItemOptions { get; set; } = new List<CartItemOption>();
    public ICollection<CartItemStatusHistory> CartItemStatusHistories { get; set; } = new List<CartItemStatusHistory>();
    public BillDetail? BillDetail { get; set; }
}
