using Microsoft.AspNetCore.Identity;

namespace RestaurantOrdering.Models.Entities;

public class ApplicationUser : IdentityUser<int>
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginTime { get; set; }

    // BaseEntity fields
    public bool IsDeleted { get; set; } = false;
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedTime { get; set; }

    // Navigation
    public ICollection<Cart> Carts { get; set; } = new List<Cart>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
