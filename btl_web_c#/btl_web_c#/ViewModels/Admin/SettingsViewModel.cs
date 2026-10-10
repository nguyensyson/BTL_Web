using System.ComponentModel.DataAnnotations;

namespace RestaurantOrdering.ViewModels.Admin;

public class SettingsViewModel
{
    [Range(0, 1)]
    [Display(Name = "Thuế VAT (%)")]
    public decimal TaxRate { get; set; }

    [Range(0, 1)]
    [Display(Name = "Phí dịch vụ (%)")]
    public decimal ServiceFeeRate { get; set; }

    [Range(1, 120)]
    [Display(Name = "Ngưỡng SLA món ăn (phút)")]
    public int ItemSlaMinutes { get; set; }

    [Range(5, 120)]
    [Display(Name = "Thời gian giữ bàn đặt trước (phút)")]
    public int ReservationHoldMinutes { get; set; }

    [StringLength(200)]
    [Display(Name = "Tên nhà hàng")]
    public string RestaurantName { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Địa chỉ")]
    public string RestaurantAddress { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Số điện thoại")]
    public string RestaurantPhone { get; set; } = string.Empty;
}
