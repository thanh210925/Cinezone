using CINEMA.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CINEMA.Helpers
{
    public static class MovieSeeder
    {
        public static void Seed(CinemaContext context)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var sampleMovies = new List<Movie>
            {
                // 🎬 Phim đang chiếu
                new Movie { Title = "Deadpool & Wolverine", Duration = 132, AgeRating = "18+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(-15), EndDate = today.AddDays(60), IsActive = true, Description = "Sự kết hợp bùng nổ giữa Deadpool và Wolverine trong hành trình đa vũ trụ đầy hài hước và hành động.", PosterUrl = "https://images.unsplash.com/photo-1536440136628-849c177e76a1?w=500&q=80" },
                new Movie { Title = "Kẻ Trộm Mặt Trăng 4", Duration = 95, AgeRating = "P", Country = "Mỹ", Language = "Lồng tiếng", ReleaseDate = today.AddDays(-10), EndDate = today.AddDays(60), IsActive = true, Description = "Gru và gia đình đón thành viên mới cùng các Minions đối mặt với kẻ thù nguy hiểm Maxime Le Mal.", PosterUrl = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=500&q=80" },
                new Movie { Title = "Inside Out 2 (Những Mảnh Ghép Cảm Xúc 2)", Duration = 96, AgeRating = "P", Country = "Mỹ", Language = "Lồng tiếng", ReleaseDate = today.AddDays(-12), EndDate = today.AddDays(60), IsActive = true, Description = "Riley bước vào tuổi dậy thì với sự xuất hiện của cảm xúc mới: Lo Âu (Anxiety).", PosterUrl = "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?w=500&q=80" },
                new Movie { Title = "Godzilla x Kong: Đế Chế Mới", Duration = 115, AgeRating = "13+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(-20), EndDate = today.AddDays(60), IsActive = true, Description = "Godzilla và Kong phải liên minh chống lại hiểm họa sinh tồn dưới lòng Trái Đất.", PosterUrl = "https://images.unsplash.com/photo-1568605117036-5fe5e7bab0b7?w=500&q=80" },
                new Movie { Title = "Thư Tình Gửi Ngoại", Duration = 127, AgeRating = "P", Country = "Thái Lan", Language = "Phụ đề", ReleaseDate = today.AddDays(-8), EndDate = today.AddDays(60), IsActive = true, Description = "Cháu trai chăm sóc bà ngoại bị bệnh với hy vọng thừa kế căn nhà, nhưng tìm thấy tình cảm gia đình ấm áp.", PosterUrl = "https://images.unsplash.com/photo-1518709268805-4e9042af9f23?w=500&q=80" },
                new Movie { Title = "Dune: Hành Tinh Cát - Phần 2", Duration = 166, AgeRating = "13+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(-25), EndDate = today.AddDays(60), IsActive = true, Description = "Paul Atreides hợp lực với Chani và người Fremen trả thù những kẻ hủy diệt gia tộc.", PosterUrl = "https://images.unsplash.com/photo-1534447677768-be436bb09401?w=500&q=80" },
                new Movie { Title = "Conan: Ngôi Sao 5 Cánh 1 Triệu Đô", Duration = 110, AgeRating = "P", Country = "Nhật Bản", Language = "Lồng tiếng", ReleaseDate = today.AddDays(-5), EndDate = today.AddDays(60), IsActive = true, Description = "Conan và Kaito Kid đụng độ tại Hakodate tìm kiếm thanh kiếm bí mật thời Bakumatsu.", PosterUrl = "https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=500&q=80" },
                new Movie { Title = "Alien: Romulus", Duration = 119, AgeRating = "18+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(-7), EndDate = today.AddDays(60), IsActive = true, Description = "Một nhóm thanh niên khám phá trạm không gian hoang phế và đối mặt với sinh vật tàn bạo nhất vũ trụ.", PosterUrl = "https://images.unsplash.com/photo-1509198397868-475647b2a1e5?w=500&q=80" },

                // 🚀 10 Phim Sắp Chiếu (ReleaseDate > today)
                new Movie { Title = "Avatar 3: Lửa Và Tro Tàn", Duration = 190, AgeRating = "13+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(15), EndDate = today.AddDays(75), IsActive = true, Description = "Hành trình tiếp theo trên hành tinh Pandora, khám phá bộ tộc Tro Tàn đầy tàn bạo và bí ẩn.", PosterUrl = "https://images.unsplash.com/photo-1518709268805-4e9042af9f23?w=500&q=80" },
                new Movie { Title = "Captain America: Thế Giới Mới", Duration = 135, AgeRating = "13+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(20), EndDate = today.AddDays(80), IsActive = true, Description = "Sam Wilson chính thức khoác lên mình danh xưng Captain America và đối mặt với âm mưu chính trị toàn cầu.", PosterUrl = "https://images.unsplash.com/photo-1568605117036-5fe5e7bab0b7?w=500&q=80" },
                new Movie { Title = "Mufasa: Vua Sư Tử", Duration = 120, AgeRating = "P", Country = "Mỹ", Language = "Lồng tiếng", ReleaseDate = today.AddDays(10), EndDate = today.AddDays(70), IsActive = true, Description = "Câu chuyện về nguồn gốc thời trẻ của Mufasa từ chú sư tử mồ côi trở thành vị vua huyền thoại.", PosterUrl = "https://images.unsplash.com/photo-1534447677768-be436bb09401?w=500&q=80" },
                new Movie { Title = "Nhiệm Vụ Bất Khả Thi 8", Duration = 165, AgeRating = "16+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(25), EndDate = today.AddDays(85), IsActive = true, Description = "Ethan Hunt và biệt đội IMF bước vào trận chiến sinh tử cuối cùng chống lại trí tuệ nhân tạo Thực Thể.", PosterUrl = "https://images.unsplash.com/photo-1509198397868-475647b2a1e5?w=500&q=80" },
                new Movie { Title = "Bộ Tứ Siêu Đẳng: Bước Khởi Đầu", Duration = 130, AgeRating = "13+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(30), EndDate = today.AddDays(90), IsActive = true, Description = "Bộ bốn siêu anh hùng gia đình đầu tiên của Marvel tái xuất trong thế giới retro-futuristic.", PosterUrl = "https://images.unsplash.com/photo-1635805737707-575885ab0820?w=500&q=80" },
                new Movie { Title = "Doraemon: Bản Tình Ca Kính Vạn Hoa", Duration = 105, AgeRating = "P", Country = "Nhật Bản", Language = "Lồng tiếng", ReleaseDate = today.AddDays(8), EndDate = today.AddDays(68), IsActive = true, Description = "Nobita và Doraemon du hành vào thế giới âm nhạc phép thuật để giải cứu hành tinh.", PosterUrl = "https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=500&q=80" },
                new Movie { Title = "Kính Vạn Hoa Điện Ảnh", Duration = 112, AgeRating = "P", Country = "Việt Nam", Language = "Tiếng Việt", ReleaseDate = today.AddDays(12), EndDate = today.AddDays(72), IsActive = true, Description = "Bộ ba Quý rốm, Hạnh cận và Tiểu Long tái xuất trong vụ án bí ẩn tại vùng quê mùa hè.", PosterUrl = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=500&q=80" },
                new Movie { Title = "Fast & Furious 11: Cú Nốc Ao", Duration = 145, AgeRating = "16+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(40), EndDate = today.AddDays(100), IsActive = true, Description = "Dominic Toretto cùng gia đình đối mặt với kẻ thù nguy hiểm nhất Dante Reyes.", PosterUrl = "https://images.unsplash.com/photo-1568605117036-5fe5e7bab0b7?w=500&q=80" },
                new Movie { Title = "Spider-Man: Beyond the Spider-Verse", Duration = 148, AgeRating = "P", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(35), EndDate = today.AddDays(95), IsActive = true, Description = "Miles Morales đối mặt với bản sao đen tối của chính mình tại Vũ trụ 42.", PosterUrl = "https://images.unsplash.com/photo-1635805737707-575885ab0820?w=500&q=80" },
                new Movie { Title = "Lật Mặt 8: Vòng Xoay Định Mệnh", Duration = 125, AgeRating = "16+", Country = "Việt Nam", Language = "Tiếng Việt", ReleaseDate = today.AddDays(18), EndDate = today.AddDays(78), IsActive = true, Description = "Tác phẩm điện ảnh hành động kịch tính của đạo diễn Lý Hải về tình anh em.", PosterUrl = "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?w=500&q=80" }
            };

            bool changes = false;
            foreach (var m in sampleMovies)
            {
                var existing = context.Movies.FirstOrDefault(x => x.Title == m.Title);
                if (existing == null)
                {
                    context.Movies.Add(m);
                    changes = true;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(existing.PosterUrl) || existing.PosterUrl.Contains("tmdb.org"))
                    {
                        existing.PosterUrl = m.PosterUrl;
                        changes = true;
                    }
                    if (m.ReleaseDate > today && (existing.ReleaseDate == null || existing.ReleaseDate <= today))
                    {
                        existing.ReleaseDate = m.ReleaseDate;
                        existing.EndDate = m.EndDate;
                        existing.IsActive = true;
                        changes = true;
                    }
                }
            }

            if (changes)
            {
                context.SaveChanges();
            }
        }
    }
}
