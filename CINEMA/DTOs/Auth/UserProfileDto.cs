namespace CINEMA.DTOs
{
    public class UserProfileDto
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public string? Avatar { get; set; }
        public decimal TotalSpent { get; set; }
        public string MembershipLevel { get; set; } = "Đồng";
        public double ReputationScore { get; set; }
    }
}
