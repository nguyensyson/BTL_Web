namespace RestaurantOrdering.Models.Entities;

public class ReservationTable : BaseEntity
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int TableId { get; set; }

    // Navigation
    public Reservation Reservation { get; set; } = null!;
    public DiningTable DiningTable { get; set; } = null!;
}
