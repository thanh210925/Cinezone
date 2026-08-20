namespace CINEMA.DTOs
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = null!;
        public string TokenType { get; set; } = "Bearer";
        public DateTime ExpiresAt { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiresAt { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string UserType { get; set; } = "Customer";
        public string? Role { get; set; }
        public string? Avatar { get; set; }
    }
}
