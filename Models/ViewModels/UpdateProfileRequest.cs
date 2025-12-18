using System.ComponentModel.DataAnnotations;

namespace MusicWeb.Models.ViewModels;

public class UpdateProfileRequest
{
    [Required(ErrorMessage = "Tên hiển thị không được để trống")]
    [StringLength(50, ErrorMessage = "Tên hiển thị không quá 50 ký tự")]
    public string DisplayName { get; set; } = string.Empty;

    [Url(ErrorMessage = "Đường dẫn ảnh không hợp lệ")]
    public string? AvatarUrl { get; set; }

    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
}
