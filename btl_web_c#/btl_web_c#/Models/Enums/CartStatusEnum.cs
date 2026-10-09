namespace RestaurantOrdering.Models.Enums;

public enum CartStatusEnum : byte
{
    Open = 1,
    WaitingPayment = 2,
    Paid = 3,
    Cancelled = 4
}
