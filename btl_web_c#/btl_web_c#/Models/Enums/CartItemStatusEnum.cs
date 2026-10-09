namespace RestaurantOrdering.Models.Enums;

public enum CartItemStatusEnum : byte
{
    Draft = 1,
    Confirmed = 2,
    Cooking = 3,
    Ready = 4,
    Served = 5,
    Cancelled = 6,
    Rejected = 7
}
