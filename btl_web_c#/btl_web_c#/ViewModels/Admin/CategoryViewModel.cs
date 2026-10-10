using System.ComponentModel.DataAnnotations;

namespace RestaurantOrdering.ViewModels.Admin;

public class CategoryViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên danh mục")]
    [StringLength(100, ErrorMessage = "Tên tối đa 100 ký tự")]
    [Display(Name = "Tên danh mục")]
    public string Name { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Mô tả tối đa 255 ký tự")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Thứ tự")]
    [Range(0, 999)]
    public int SortOrder { get; set; }
}
