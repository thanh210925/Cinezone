using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CINEMA.Models
{
    public class Payroll
    {
        [Key]
        public int PayrollId { get; set; }

        [Required]
        public int AdminId { get; set; }

        [Required]
        public int Month { get; set; }

        [Required]
        public int Year { get; set; }

        // Số ngày đi làm thực tế (IsApproved = true trong bảng Attendance)
        public int WorkingDays { get; set; }

        // Số ngày nghỉ được duyệt có lương (ví dụ: Nghỉ phép năm)
        public int PaidLeaveDays { get; set; }

        // Hệ số lương tại thời điểm tính lương
        [Column(TypeName = "decimal(10, 2)")]
        public decimal SalaryCoefficient { get; set; } = 1.0m;

        // Lương cơ bản mỗi ngày công
        [Column(TypeName = "decimal(18, 2)")]
        public decimal BaseSalaryPerDay { get; set; }

        // Phụ cấp / Thưởng thêm
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Bonus { get; set; } = 0;

        // Khấu trừ / Phạt
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Deductions { get; set; } = 0;

        // Tổng lương thực nhận: (WorkingDays + PaidLeaveDays) * BaseSalaryPerDay * SalaryCoefficient + Bonus - Deductions
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalSalary { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Unpaid"; // Unpaid / Paid

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? PaidAt { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [ForeignKey("AdminId")]
        public virtual Admin? Admin { get; set; }
    }
}
