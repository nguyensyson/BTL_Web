namespace RestaurantOrdering.Models.Entities;

public abstract class BaseEntity
{
    public bool IsDeleted { get; set; } = false;
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedTime { get; set; }
}
