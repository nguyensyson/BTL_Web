namespace RestaurantOrdering.Models.Entities;

public class SystemSetting : BaseEntity
{
    public int Id { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string? Description { get; set; }
}
