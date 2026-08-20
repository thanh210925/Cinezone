using System.ComponentModel.DataAnnotations;

namespace CINEMA.DTOs
{
    public class FaceCheckinDto
    {
        [Required(ErrorMessage = "Vector khuôn mặt từ camera không được để trống")]
        public List<float> FaceDescriptor { get; set; } = new();

        public string? CheckInPhotoBase64 { get; set; }
    }
}
