using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CINEMA.Models
{
    public class Attendance
    {
        [Key]
        public int AttendanceId { get; set; }

        public int AdminId { get; set; }

        [Column(TypeName = "date")]
        public DateTime Date { get; set; }

        public DateTime? CheckInTime { get; set; }

        public DateTime? CheckOutTime { get; set; }

        [StringLength(500)]
        public string? CheckInPhoto { get; set; }

        [StringLength(500)]
        public string? CheckOutPhoto { get; set; }

        [ForeignKey("AdminId")]
        public virtual Admin? Admin { get; set; }

        public bool IsApproved { get; set; } = false; // Mặc định chưa duyệt
    }
}