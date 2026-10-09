namespace RestaurantOrdering.Models.Entities;

public class UsedTable : BaseEntity
{
    public long Id { get; set; }
    public long CartId { get; set; }
    public int TableId { get; set; }
    public bool IsPrimary { get; set; } = false;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }

    // Navigation
    public Cart Cart { get; set; } = null!;
    public DiningTable DiningTable { get; set; } = null!;
}
