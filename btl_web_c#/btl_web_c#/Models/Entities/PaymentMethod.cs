namespace RestaurantOrdering.Models.Entities;

public class PaymentMethod : BaseEntity
{
    public byte Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Navigation
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
