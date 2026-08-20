using System.ComponentModel.DataAnnotations;

namespace CINEMA.DTOs
{
    public class RegisterFaceDto
    {
        public int? AdminId { get; set; }

        [Required(ErrorMessage = "Vector khuôn mặt không được để trống")]
        public List<float> FaceDescriptor { get; set; } = new();

        public string? FacePhotoUrl { get; set; }
    }
}
