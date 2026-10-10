using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RestaurantOrdering.ViewModels.Admin;

public class ProductViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã món")]
    [StringLength(30)]
    [Display(Name = "Mã món")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên món")]
    [StringLength(150)]
    [Display(Name = "Tên món")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn danh mục")]
    [Display(Name = "Danh mục")]
    public int CategoryId { get; set; }

    [Required]
    [Range(0, 100_000_000, ErrorMessage = "Giá phải ≥ 0")]
    [Display(Name = "Giá (₫)")]
    public decimal Price { get; set; }

    [Display(Name = "Nhóm ra món")]
    [Range(1, 3)]
    public byte CourseGroup { get; set; } = 2;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Còn hàng")]
    public bool IsAvailable { get; set; } = true;

    [Display(Name = "Thứ tự")]
    public int SortOrder { get; set; }

    // Upload ảnh
    [Display(Name = "Ảnh món")]
    public IFormFile? ImageFile { get; set; }
    public string? ImageUrl { get; set; }

    // Display
    public string? CategoryName { get; set; }
    public List<SelectListItem> Categories { get; set; } = new();
    public List<ProductOptionViewModel> Options { get; set; } = new();
}

public class ProductOptionViewModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên tùy chọn")]
    [StringLength(100)]
    [Display(Name = "Tên tùy chọn")]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Nhóm")]
    public string? GroupName { get; set; }

    [Range(0, 10_000_000)]
    [Display(Name = "Phụ thu (₫)")]
    public decimal ExtraPrice { get; set; }

    [Display(Name = "Còn hàng")]
    public bool IsAvailable { get; set; } = true;

    [Display(Name = "Thứ tự")]
    public int SortOrder { get; set; }
}
