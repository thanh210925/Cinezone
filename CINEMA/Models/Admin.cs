using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CINEMA.Models
{
    public partial class Admin
    {
        [Key]
        public int? AdminId { get; set; }

        

        [Required(ErrorMessage = "Họ tên không được để trống")]
        [StringLength(100)]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [DataType(DataType.Password)]
        public string? PasswordHash { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string? Phone { get; set; }

        // Giới tính
        [StringLength(10)]
        public string? Gender { get; set; } // Male / Female / Other

        // CCCD
        [StringLength(50)]
        public string? CitizenId { get; set; }


        // Thông tin công việc
        [StringLength(255)]
        public string? JobInfo { get; set; }

        // Ngày vào làm
        public DateTime? HireDate { get; set; }

        [StringLength(20)]
        public string? Role { get; set; } = "Staff"; // Mặc định là Staff

        public bool IsActive { get; set; } = true;

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastLogin { get; set; }

        // Mở rộng: Liên kết trực tiếp với bảng Employee (nếu bạn tách ra sau này)
        // [ForeignKey("Employee")]
        // public int? EmployeeId { get; set; }
        // public virtual Employee? Employee { get; set; }

        public int? BranchId { get; set; }

        public int? PositionId { get; set; }

        public DateTime? BirthDate { get; set; }

        public string? Address { get; set; }

        public string? Avatar { get; set; }

        // Mã nhân viên
        [StringLength(50)]
        public string? EmployeeCode { get; set; }

        public virtual Branch? Branch { get; set; }

        public virtual Position? Position { get; set; }


        public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
        public virtual ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
        public virtual ICollection<Payroll> Payrolls { get; set; } = new List<Payroll>();
    }
}