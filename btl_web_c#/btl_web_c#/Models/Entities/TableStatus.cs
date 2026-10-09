namespace RestaurantOrdering.Models.Entities;

public class TableStatus : BaseEntity
{
    public byte Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ColorHex { get; set; }

    // Navigation
    public ICollection<DiningTable> DiningTables { get; set; } = new List<DiningTable>();
}
