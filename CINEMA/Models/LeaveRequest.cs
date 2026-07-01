using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CINEMA.Models
{
    public class LeaveRequest
    {
        [Key]
        public int RequestId { get; set; }
        public int AdminId { get; set; }
        public int LeaveTypeId { get; set; } // Thêm dòng này

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Reason { get; set; }
        public string Status { get; set; } = "Chờ duyệt";
        public DateTime CreatedAt { get; set; }

        [ForeignKey("AdminId")]
        public virtual Admin? Admin { get; set; }

        // Thêm quan hệ này để EF hiểu LeaveType
        [ForeignKey("LeaveTypeId")]
        public virtual LeaveType? LeaveType { get; set; }
    }
}