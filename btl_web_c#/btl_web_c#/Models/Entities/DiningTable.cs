namespace RestaurantOrdering.Models.Entities;

public class DiningTable : BaseEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int AreaId { get; set; }
    public int Capacity { get; set; }
    public byte StatusId { get; set; }
    public int SortOrder { get; set; } = 0;
    public byte[]? RowVersion { get; set; }

    // Navigation
    public Area Area { get; set; } = null!;
    public TableStatus TableStatus { get; set; } = null!;
    public ICollection<UsedTable> UsedTables { get; set; } = new List<UsedTable>();
    public ICollection<ReservationTable> ReservationTables { get; set; } = new List<ReservationTable>();
}
