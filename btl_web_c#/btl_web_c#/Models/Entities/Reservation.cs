namespace RestaurantOrdering.Models.Entities;

public class Reservation : BaseEntity
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public DateTime ArrivalTime { get; set; }
    public DateTime? HoldUntil { get; set; }

    /// <summary>0=Chờ khách, 1=Đã nhận bàn, 2=Đã hủy, 3=Quá giờ</summary>
    public byte Status { get; set; } = 0;
    public string? Note { get; set; }

    // Navigation
    public ICollection<ReservationTable> ReservationTables { get; set; } = new List<ReservationTable>();
    public ICollection<Cart> Carts { get; set; } = new List<Cart>();
}
