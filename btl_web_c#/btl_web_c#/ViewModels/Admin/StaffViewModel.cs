using System.ComponentModel.DataAnnotations;

namespace RestaurantOrdering.ViewModels.Admin;

public class StaffViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [StringLength(50, MinimumLength = 3)]
    [Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [StringLength(100)]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [EmailAddress]
    [StringLength(150)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Display(Name = "Vai trò")]
    [Required(ErrorMessage = "Vui lòng chọn vai trò")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Kích hoạt")]
    public bool IsActive { get; set; } = true;

    // Chỉ dùng khi tạo mới
    [StringLength(100, MinimumLength = 6)]
    [Display(Name = "Mật khẩu")]
    public string? Password { get; set; }

    // Display only
    public DateTime? LastLoginTime { get; set; }
    public bool IsLockedOut { get; set; }
}

public class ResetPasswordViewModel
{
    public int UserId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
    [StringLength(100, MinimumLength = 6)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;
}
