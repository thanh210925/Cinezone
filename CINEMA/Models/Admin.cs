using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CINEMA.Models
{
    public partial class Admin
    {
        [Key]
        public int AdminId { get; set; }

        [Required(ErrorMessage = "Họ tên không được để trống")]
        [StringLength(100)]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [DataType(DataType.Password)]
        public string PasswordHash { get; set; } = null!;

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string? Phone { get; set; }

        [StringLength(20)]
        public string? Role { get; set; } = "Staff"; // Mặc định là Staff

        public bool IsActive { get; set; } = true;

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastLogin { get; set; }

        // Mở rộng: Liên kết trực tiếp với bảng Employee (nếu bạn tách ra sau này)
        // [ForeignKey("Employee")]
        // public int? EmployeeId { get; set; }
        // public virtual Employee? Employee { get; set; }
    }
}