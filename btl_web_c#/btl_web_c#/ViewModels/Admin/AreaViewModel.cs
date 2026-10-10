using System.ComponentModel.DataAnnotations;

namespace RestaurantOrdering.ViewModels.Admin;

public class AreaViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên khu vực")]
    [StringLength(100, ErrorMessage = "Tên tối đa 100 ký tự")]
    [Display(Name = "Tên khu vực")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Thứ tự")]
    [Range(0, 999)]
    public int SortOrder { get; set; }
}
