namespace RestaurantOrdering.Models.Enums;

public enum TableStatusEnum : byte
{
    Available = 1,
    Occupied = 2,
    WaitingPayment = 3,
    Reserved = 4
}
