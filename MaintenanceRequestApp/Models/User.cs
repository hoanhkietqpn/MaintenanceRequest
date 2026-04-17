using System.ComponentModel.DataAnnotations;

namespace MaintenanceRequestApp.Models
{
    public class User
    {
        [Key]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Tên đăng nhập không được để trống.")]
        [StringLength(100)]
        public string Username { get; set; }

        [Required(ErrorMessage = "Tên không được để trống.")]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Họ không được để trống.")]
        [StringLength(100)]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; }

        [StringLength(50)]
        public string Role { get; set; } = "NhanVienKyThuat";
    }
}
