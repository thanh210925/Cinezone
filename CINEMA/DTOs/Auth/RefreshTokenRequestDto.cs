using System.ComponentModel.DataAnnotations;

namespace CINEMA.DTOs
{
    public class RefreshTokenRequestDto
    {
        [Required(ErrorMessage = "RefreshToken không được để trống")]
        public string RefreshToken { get; set; } = null!;
    }
}
