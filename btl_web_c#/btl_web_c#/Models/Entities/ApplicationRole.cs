using Microsoft.AspNetCore.Identity;

namespace RestaurantOrdering.Models.Entities;

public class ApplicationRole : IdentityRole<int>
{
    public ApplicationRole() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}
