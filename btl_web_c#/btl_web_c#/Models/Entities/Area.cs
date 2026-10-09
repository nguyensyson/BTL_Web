namespace RestaurantOrdering.Models.Entities;

public class Area : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;

    // Navigation
    public ICollection<DiningTable> DiningTables { get; set; } = new List<DiningTable>();
}
