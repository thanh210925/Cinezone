using System.ComponentModel.DataAnnotations;

namespace CINEMA.Models
{
    public class WorkSchedule
    {
        [Key]
        public int ScheduleId { get; set; }
        public int AdminId { get; set; }
        public int ShiftId { get; set; }
        [DataType(DataType.Date)]
        public DateTime WorkDate { get; set; }

        // Thuộc tính điều hướng (Navigation Properties) để liên kết
        public virtual Admin Admin { get; set; }
        public virtual Shift Shift { get; set; }
    }
}
