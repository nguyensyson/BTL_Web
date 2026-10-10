using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RestaurantOrdering.ViewModels.Admin;

public class DiningTableViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã bàn")]
    [StringLength(20, ErrorMessage = "Mã bàn tối đa 20 ký tự")]
    [Display(Name = "Mã bàn")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn khu vực")]
    [Display(Name = "Khu vực")]
    public int AreaId { get; set; }

    [Required]
    [Range(1, 50, ErrorMessage = "Sức chứa từ 1 đến 50")]
    [Display(Name = "Sức chứa")]
    public int Capacity { get; set; }

    [Display(Name = "Thứ tự")]
    [Range(0, 999)]
    public int SortOrder { get; set; }

    // Display only
    public string? AreaName { get; set; }
    public string? StatusName { get; set; }
    public string? StatusColorHex { get; set; }
    public List<SelectListItem> Areas { get; set; } = new();
}
