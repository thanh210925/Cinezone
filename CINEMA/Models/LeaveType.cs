using System.ComponentModel.DataAnnotations;

namespace CINEMA.Models
{
    public class LeaveType
    {
        [Key]
        public int LeaveTypeId { get; set; }
        public string TypeName { get; set; } = string.Empty;
    }
}