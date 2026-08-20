using System.ComponentModel.DataAnnotations;

namespace CINEMA.DTOs
{
    public class CreateReviewDto
    {
        [Range(1, 5, ErrorMessage = "Điểm đánh giá từ 1 đến 5")]
        public int Rating { get; set; }

        [MaxLength(1000, ErrorMessage = "Bình luận tối đa 1000 ký tự")]
        public string? Comment { get; set; }
    }
}
