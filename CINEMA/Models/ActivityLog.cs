using System.ComponentModel.DataAnnotations;

namespace CINEMA.Models
{
    public class ActivityLog
    {
        [Key]
        public int LogId { get; set; }

        public int AdminId { get; set; }

        [Required]
        public string Action { get; set; } // Ví dụ: "THÊM", "SỬA", "XÓA"

        [Required]
        public string Entity { get; set; } // Ví dụ: "Movies", "Showtimes"

        public int EntityId { get; set; }

        public DateTime LogDate { get; set; } = DateTime.Now;

        // Quan hệ với Admin
        public virtual Admin Admin { get; set; }
    }
}