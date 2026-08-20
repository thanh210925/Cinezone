namespace CINEMA.Models
{
    public class Branch
    {
        public int BranchId { get; set; }

        // FK đến Theater
        public int TheaterId { get; set; }

        public string BranchCode { get; set; } = string.Empty;

        public string BranchName { get; set; } = string.Empty;

        public string? Address { get; set; }

        public string? Phone { get; set; }

        public string? Email { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Theater Theater { get; set; } = null!;

        public virtual ICollection<Admin> Admins { get; set; }
            = new List<Admin>();
    }
}