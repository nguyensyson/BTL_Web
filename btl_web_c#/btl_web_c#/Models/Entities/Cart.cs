namespace RestaurantOrdering.Models.Entities;

public class Cart : BaseEntity
{
    public long Id { get; set; }
    public string CartCode { get; set; } = string.Empty;
    public byte StatusId { get; set; }
    public int? ReservationId { get; set; }
    public int GuestCount { get; set; }
    public int WaiterId { get; set; }
    public DateTime OpenedTime { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedTime { get; set; }
    public string? Note { get; set; }
    public byte[]? RowVersion { get; set; }

    // Navigation
    public CartStatus CartStatus { get; set; } = null!;
    public Reservation? Reservation { get; set; }
    public ApplicationUser Waiter { get; set; } = null!;
    public ICollection<UsedTable> UsedTables { get; set; } = new List<UsedTable>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public Bill? Bill { get; set; }
}
