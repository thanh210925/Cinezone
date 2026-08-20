namespace CINEMA.Models
{
    public class Position
    {
        public int? PositionId { get; set; }

        public string? PositionName { get; set; } 

        public string? Description { get; set; }

        public bool IsActive { get; set; }

        public virtual ICollection<Admin> Admins { get; set; }
            = new List<Admin>();

        // Hệ số lương
        public decimal? SalaryCoefficient { get; set; } = 1.0m;
    }
}