using System.ComponentModel.DataAnnotations;

namespace CINEMA.Models
{
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Token { get; set; } = null!;

        public int UserId { get; set; }

        public string UserType { get; set; } = "Customer"; // Customer hoặc Admin

        public DateTime ExpiresAt { get; set; }

        public bool IsRevoked { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
