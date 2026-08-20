using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CINEMA.Models
{
    public class ActivityLog
    {
        [Key]
        public int LogId { get; set; }

        [ForeignKey("Admin")]
        public int AdminId { get; set; }

        public virtual Admin Admin { get; set; }

        [Required]
        public string Action { get; set; }

        [Required]
        public string Entity { get; set; }

        public int EntityId { get; set; }

        public DateTime LogDate { get; set; } = DateTime.Now;


    }
}