using System.Diagnostics;
using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CINEMA.Services;

namespace CINEMA.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly CinemaContext _context;
        private readonly RecommendationEngine _recommendationEngine;
        private readonly Services.GeminiService _geminiService;
        private readonly IEmailService _emailService;
        private IMovieService _movieService;

        public HomeController(
            ILogger<HomeController> logger,
            CinemaContext context,
            RecommendationEngine recommendationEngine,
            Services.GeminiService geminiService,
            IEmailService emailService,
            IMovieService movieService)
        {
            _logger = logger;
            _context = context;
            _recommendationEngine = recommendationEngine;
            _geminiService = geminiService;
            _emailService = emailService;
            _movieService = movieService;
        }

        // =====================================================
        // ====================== TRANG CHỦ ====================
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return RedirectToAction("Index");

            var today = DateOnly.FromDateTime(DateTime.Today);

            var keywordNoSign = RemoveDiacritics(keyword);

            var movies = _context.Movies
                .Include(m => m.Genres)
                .AsEnumerable()
                .Where(m =>
                    m.IsActive == true &&
                    m.ReleaseDate.HasValue &&
                    m.ReleaseDate.Value <= today &&
                    (!m.EndDate.HasValue || m.EndDate >= today) &&
                    RemoveDiacritics(m.Title ?? "")
                        .Contains(keywordNoSign))
                .ToList();

            // 📌 GHI LOG TÌM KIẾM
            _context.UserSearchLogs.Add(new UserSearchLog
            {
                CustomerId = GetCurrentCustomerId(),
                Keyword = keyword,
                ResultCount = movies.Count,
                CreatedAt = DateTime.Now
            });
            _context.SaveChanges();

            // 📌 LƯU TƯƠNG TÁC TÌM KIẾM VÀO SESSION ĐỂ GỢI Ý PHIM CÙNG THỂ LOẠI
            HttpContext.Session.SetString("LatestInteraction", "Search");
            HttpContext.Session.SetString("LatestSearchKeyword", keyword);

            // Ghi log tìm kiếm vào UserActivityLogs để cá nhân hóa gợi ý
            int? searchMovieId = movies.FirstOrDefault()?.MovieId;
            int? searchGenreId = movies.FirstOrDefault()?.Genres?.FirstOrDefault()?.GenreId;
            LogActivity("SEARCH", movieId: searchMovieId, genreId: searchGenreId, metadata: keyword);

            if (!movies.Any())
            {
                // Lấy toàn bộ danh sách phim đang chiếu (đang hoạt động, đã phát hành và chưa kết thúc) để làm ngữ cảnh cho AI
                var activeMovies = _context.Movies
                    .Include(m => m.Genres)
                    .Where(m => m.IsActive == true && 
                                m.ReleaseDate.HasValue && 
                                m.ReleaseDate <= today &&
                                (!m.EndDate.HasValue || m.EndDate >= today))
                    .ToList();

                var moviesStr = string.Join("\n", activeMovies.Select(m => $"{m.MovieId} - {m.Title} ({string.Join(", ", m.Genres.Select(g => g.Name))})"));

                string prompt = $@"
Khách hàng đang tìm kiếm phim bằng từ khóa: '{keyword}' nhưng hệ thống rạp phim của chúng tôi không có kết quả chính xác nào.
Dưới đây là danh sách các phim hiện đang hoạt động tại rạp của chúng tôi:
{moviesStr}

Nhiệm vụ của bạn:
1. Hãy xác định thể loại phim (Genre) mà từ khóa '{keyword}' hướng tới (Ví dụ: 'ghost' hướng tới phim Kinh dị/Bí ẩn; 'sieu nhan' hướng tới phim Hành động/Viễn tưởng; 'tình yêu' hướng tới phim Tình cảm/Lãng mạn, v.v.).
2. Chọn ra tối đa 4 bộ phim từ danh sách hoạt động ở trên có **CÙNG THỂ LOẠI hoặc THỂ LOẠI TƯƠNG ĐỒNG NHẤT** với thể loại của từ khóa tìm kiếm đó để đề xuất cho khách hàng.
3. Trả về kết quả duy nhất dưới dạng một mảng JSON chứa các MovieId được chọn, ví dụ: [3037, 3039]
Chỉ trả về mảng JSON, không giải thích gì thêm.";

                var fallbackList = new List<Movie>();
                var isAIRecommended = false;
                var reason = "";

                // Chỉ gọi Gemini API khi API Key hợp lệ và không phải là keyAPI
                bool hasValidKey = _geminiService.HasValidKey;

                if (hasValidKey)
                {
                    try
                    {
                        var rawResponse = await _geminiService.Ask(prompt);
                        
                        var cleanJson = "";
                        int startIdx = rawResponse.IndexOf('[');
                        int endIdx = rawResponse.LastIndexOf(']');
                        if (startIdx >= 0 && endIdx > startIdx)
                        {
                            cleanJson = rawResponse.Substring(startIdx, endIdx - startIdx + 1);
                        }
                        else
                        {
                            // Thử parse candidates
                            dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(rawResponse);
                            string text = jsonObj.candidates[0].content.parts[0].text;
                            text = text.Trim();
                            int s = text.IndexOf('[');
                            int e = text.LastIndexOf(']');
                            if (s >= 0 && e > s) cleanJson = text.Substring(s, e - s + 1);
                        }

                        if (!string.IsNullOrEmpty(cleanJson))
                        {
                            var ids = Newtonsoft.Json.JsonConvert.DeserializeObject<List<int>>(cleanJson);
                            if (ids != null && ids.Any())
                            {
                                fallbackList = activeMovies.Where(m => ids.Contains(m.MovieId)).ToList();
                                if (fallbackList.Any())
                                {
                                    isAIRecommended = true;
                                    reason = $"Hệ thống phân tích thông minh đề xuất các phim có thể bạn quan tâm dựa trên từ khóa '{keyword}'.";
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Lỗi gọi Gemini AI trong Search: " + ex.Message);
                    }
                }

                // Nếu Gemini API không khả dụng hoặc không có gợi ý phù hợp, thực hiện Thuật toán Gợi ý thể loại cục bộ (Local Rule-based Matching)
                if (!fallbackList.Any())
                {
                    var dbGenres = _context.Genres.ToList();
                    var matchedGenres = new List<Genre>();

                    // 1. So khớp trực tiếp tên thể loại
                    foreach (var genre in dbGenres)
                    {
                        var genreNorm = RemoveDiacritics(genre.Name ?? "").ToLower();
                        if (genreNorm.Contains(keywordNoSign) || keywordNoSign.Contains(genreNorm))
                        {
                            matchedGenres.Add(genre);
                        }
                    }

                    // 2. So khớp qua từ đồng nghĩa thông dụng
                    var synonymMapping = new Dictionary<string, string[]>
                    {
                        { "Kinh dị", new[] { "ma", "quy", "kinh hoang", "dang so", "giat gan", "u am", "horror", "kinh di" } },
                        { "Tình cảm", new[] { "yeu", "tinh cam", "lang man", "hen ho", "dam my", "ngon tinh", "romance", "tinh yeu" } },
                        { "Hành động", new[] { "danh nhau", "vo thuat", "ban sung", "dua xe", "nghet tho", "chien tranh", "action", "kich tinh" } },
                        { "Hài", new[] { "hai", "cuoi", "vui ve", "hai huoc", "comedy" } },
                        { "Hoạt hình", new[] { "hoat hinh", "anime", "tre em", "thieu nhi", "cartoon", "animation" } },
                        { "Viễn tưởng", new[] { "vien tuong", "robot", "quai vat", "khong gian", "sieu anh hung", "sieu nhan", "sci-fi" } },
                        { "Tâm lý", new[] { "tam ly", "kich tinh", "drama", "cuoc song" } },
                        { "Phiêu lưu", new[] { "phieu luu", "kham pha", "du ngoan", "adventure" } }
                    };

                    foreach (var kvp in synonymMapping)
                    {
                        if (kvp.Value.Any(syn => keywordNoSign.Contains(syn)))
                        {
                            var matchedGenre = dbGenres.FirstOrDefault(g => 
                                RemoveDiacritics(g.Name ?? "").ToLower().Contains(RemoveDiacritics(kvp.Key).ToLower())
                            );
                            if (matchedGenre != null && !matchedGenres.Contains(matchedGenre))
                            {
                                matchedGenres.Add(matchedGenre);
                            }
                        }
                    }

                    if (matchedGenres.Any())
                    {
                        var matchedIds = matchedGenres.Select(g => g.GenreId).ToList();
                        fallbackList = activeMovies
                            .Where(m => m.Genres.Any(g => matchedIds.Contains(g.GenreId)))
                            .Take(4)
                            .ToList();

                        if (fallbackList.Any())
                        {
                            isAIRecommended = true;
                            reason = $"Không tìm thấy phim phù hợp với từ khóa '{keyword}'. Chúng tôi gợi ý các phim thuộc thể loại {string.Join(", ", matchedGenres.Select(g => g.Name))} có tính chất tương đồng:";
                        }
                    }
                }

                // Nếu vẫn chưa tìm thấy phim gợi ý nào, mặc định hiển thị phim mới nhất
                if (!fallbackList.Any())
                {
                    fallbackList = activeMovies
                        .OrderByDescending(m => m.ReleaseDate)
                        .Take(4)
                        .ToList();
                    reason = $"Không tìm thấy phim phù hợp với từ khóa '{keyword}'. Dưới đây là một số bộ phim đang chiếu nổi bật:";
                }

                ViewBag.SuggestMovies = fallbackList;
                ViewBag.AIRecommendationReason = reason;
                ViewBag.IsAIRecommended = isAIRecommended;
            }

            ViewBag.Keyword = keyword;

            return View(movies);
        }

        public IActionResult TermsAndPolicies()
        {
            return View();
        }

        public async Task<IActionResult> Theaters()
        {
            EnsureSampleTheaters();

            var theaters = await _context.Theaters
                .Include(t => t.Auditoria)
                .Where(t => t.IsActive != false)
                .ToListAsync();

            return View(theaters);
        }

        private void EnsureSampleTheaters()
        {
            if (!_context.Theaters.Any())
            {
                var sampleTheaters = new List<Theater>
                {
                    new Theater { Name = "CineZone Landmark 81 VIP", Address = "Tầng 3-4, Vinhomes Landmark 81, 720A Điện Biên Phủ, Phường 22, Bình Thạnh, TP. Hồ Chí Minh", Phone = "028 7300 8888", GoogleMapUrl = "https://maps.google.com/?q=Landmark+81", IsActive = true, CreatedAt = DateTime.Now },
                    new Theater { Name = "CineZone Nguyễn Huệ Central", Address = "98 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh", Phone = "028 7300 9999", GoogleMapUrl = "https://maps.google.com/?q=Nguyen+Hue+Walkway", IsActive = true, CreatedAt = DateTime.Now },
                    new Theater { Name = "CineZone Bà Triệu Vincom", Address = "Tầng 6 Vincom Center, 191 Bà Triệu, Phường Lê Đại Hành, Hai Bà Trưng, Hà Nội", Phone = "024 7300 7777", GoogleMapUrl = "https://maps.google.com/?q=Vincom+Ba+Trieu", IsActive = true, CreatedAt = DateTime.Now },
                    new Theater { Name = "CineZone Dragon Bridge", Address = "Số 1 Cầu Rồng, Phước Ninh, Hải Châu, Đà Nẵng", Phone = "0236 730 6666", GoogleMapUrl = "https://maps.google.com/?q=Dragon+Bridge+Danang", IsActive = true, CreatedAt = DateTime.Now },
                    new Theater { Name = "CineZone Ninh Kiều Park", Address = "Đại Lộ Hòa Bình, Phường Tân An, Ninh Kiều, Cần Thơ", Phone = "0292 730 5555", GoogleMapUrl = "https://maps.google.com/?q=Ninh+Kieu+Can+Tho", IsActive = true, CreatedAt = DateTime.Now }
                };

                _context.Theaters.AddRange(sampleTheaters);
                _context.SaveChanges();

                foreach (var t in sampleTheaters)
                {
                    _context.Branches.Add(new Branch
                    {
                        TheaterId = t.TheaterId,
                        BranchCode = $"CN{t.TheaterId:D3}",
                        BranchName = t.Name,
                        Address = t.Address,
                        Phone = t.Phone,
                        Email = "contact@cinezone.vn",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    });

                    for (int i = 1; i <= 5; i++)
                    {
                        _context.Auditoriums.Add(new Auditorium
                        {
                            TheaterId = t.TheaterId,
                            Name = $"Phòng chiếu {i}",
                            SeatRows = 10,
                            SeatCols = 12,
                            ScreenType = i == 1 ? "IMAX 3D" : "Dolby Atmos",
                            IsActive = true
                        });
                    }
                }
                _context.SaveChanges();
            }
        }

        private void Ensure20SampleMovies()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var sampleMovies = new List<Movie>
            {
                new Movie { Title = "Deadpool & Wolverine", Duration = 132, AgeRating = "18+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(-15), EndDate = today.AddDays(60), IsActive = true, Description = "Sự kết hợp bùng nổ giữa Deadpool và Wolverine trong hành trình đa vũ trụ đầy hài hước và hành động.", PosterUrl = "https://image.tmdb.org/t/p/w500/8cdWjvZQUExWVZE2KlCHSDUapxF.jpg" },
                new Movie { Title = "Kẻ Trộm Mặt Trăng 4", Duration = 95, AgeRating = "P", Country = "Mỹ", Language = "Lồng tiếng", ReleaseDate = today.AddDays(-10), EndDate = today.AddDays(60), IsActive = true, Description = "Gru và gia đình đón thành viên mới cùng các Minions đối mặt với kẻ thù nguy hiểm Maxime Le Mal.", PosterUrl = "https://image.tmdb.org/t/p/w500/wWba3TaojhK7T44jVScEFiIOyPh.jpg" },
                new Movie { Title = "Inside Out 2 (Những Mảnh Ghép Cảm Xúc 2)", Duration = 96, AgeRating = "P", Country = "Mỹ", Language = "Lồng tiếng", ReleaseDate = today.AddDays(-12), EndDate = today.AddDays(60), IsActive = true, Description = "Riley bước vào tuổi dậy thì với sự xuất hiện của cảm xúc mới: Lo Âu (Anxiety).", PosterUrl = "https://image.tmdb.org/t/p/w500/vpnVM9B6NMmQpEZZaOf8i21LI8a.jpg" },
                new Movie { Title = "Godzilla x Kong: Đế Chế Mới", Duration = 115, AgeRating = "13+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(-20), EndDate = today.AddDays(60), IsActive = true, Description = "Godzilla và Kong phải liên minh chống lại hiểm họa sinh tồn dưới lòng Trái Đất.", PosterUrl = "https://image.tmdb.org/t/p/w500/z1y5O22iYwUv2jY7vL5k0j.jpg" },
                new Movie { Title = "Thư Tình Gửi Ngoại", Duration = 127, AgeRating = "P", Country = "Thái Lan", Language = "Phụ đề", ReleaseDate = today.AddDays(-8), EndDate = today.AddDays(60), IsActive = true, Description = "Cháu trai chăm sóc bà ngoại bị bệnh với hy vọng thừa kế căn nhà, nhưng tìm thấy tình cảm gia đình ấm áp.", PosterUrl = "https://image.tmdb.org/t/p/w500/7aT9i7k1B3H0.jpg" },
                new Movie { Title = "Dune: Hành Tinh Cát - Phần 2", Duration = 166, AgeRating = "13+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(-25), EndDate = today.AddDays(60), IsActive = true, Description = "Paul Atreides hợp lực với Chani và người Fremen trả thù những kẻ hủy diệt gia tộc.", PosterUrl = "https://image.tmdb.org/t/p/w500/1pdfLPoLZeYhMHu2NVhKHrmYyfl.jpg" },
                new Movie { Title = "Conan: Ngôi Sao 5 Cánh 1 Triệu Đô", Duration = 110, AgeRating = "P", Country = "Nhật Bản", Language = "Lồng tiếng", ReleaseDate = today.AddDays(-5), EndDate = today.AddDays(60), IsActive = true, Description = "Conan và Kaito Kid đụng độ tại Hakodate tìm kiếm thanh kiếm bí mật thời Bakumatsu.", PosterUrl = "https://image.tmdb.org/t/p/w500/9b2.jpg" },
                new Movie { Title = "Alien: Romulus", Duration = 119, AgeRating = "18+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(-7), EndDate = today.AddDays(60), IsActive = true, Description = "Một nhóm thanh niên khám phá trạm không gian hoang phế và đối mặt với sinh vật tàn bạo nhất vũ trụ.", PosterUrl = "https://image.tmdb.org/t/p/w500/b33nnV9K2SpD.jpg" },
                new Movie { Title = "Kung Fu Panda 4", Duration = 94, AgeRating = "P", Country = "Mỹ", Language = "Lồng tiếng", ReleaseDate = today.AddDays(-18), EndDate = today.AddDays(60), IsActive = true, Description = "Po tìm kiếm người kế vị Thần Long Đại Hiệp và đối đầu với Tắc Kè Bông biến hình.", PosterUrl = "https://image.tmdb.org/t/p/w500/kDp1vUBuSpE.jpg" },
                new Movie { Title = "Linh Miêu: Quỷ Nhập Tràng", Duration = 118, AgeRating = "18+", Country = "Việt Nam", Language = "Tiếng Việt", ReleaseDate = today.AddDays(-6), EndDate = today.AddDays(60), IsActive = true, Description = "Câu chuyện kinh dị về linh miêu và nghi án linh hồn hồi sinh gia tộc làm gốm cổ truyền.", PosterUrl = "https://image.tmdb.org/t/p/w500/linhmieu.jpg" },
                new Movie { Title = "Spider-Man: Across the Spider-Verse", Duration = 140, AgeRating = "P", Country = "Mỹ", Language = "Lồng tiếng", ReleaseDate = today.AddDays(-14), EndDate = today.AddDays(60), IsActive = true, Description = "Miles Morales du hành qua đa vũ trụ Nhện và đụng độ với Người Nhện 2099.", PosterUrl = "https://image.tmdb.org/t/p/w500/8Vt6mWEReuy4Of61Lnj5Xj7sR4.jpg" },
                new Movie { Title = "Transformers One", Duration = 104, AgeRating = "P", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(-9), EndDate = today.AddDays(60), IsActive = true, Description = "Hành trình nguồn gốc tình bạn giữa Orion Pax (Optimus Prime) và D-16 (Megatron).", PosterUrl = "https://image.tmdb.org/t/p/w500/iS9W3D.jpg" },
                new Movie { Title = "Venom: Kèo Cuối", Duration = 109, AgeRating = "13+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(-4), EndDate = today.AddDays(60), IsActive = true, Description = "Eddie Brock và Venom trốn chạy sự truy đuổi từ cả hai thế giới Trái Đất và hành tinh Klyntar.", PosterUrl = "https://image.tmdb.org/t/p/w500/v9.jpg" },
                new Movie { Title = "Cám (Tấm Cám Dị Truyện)", Duration = 122, AgeRating = "18+", Country = "Việt Nam", Language = "Tiếng Việt", ReleaseDate = today.AddDays(-11), EndDate = today.AddDays(60), IsActive = true, Description = "Dị bản kinh dị đen tối về câu chuyện Cám và Tấm cùng những bí mật gia tộc đầy u uẩn.", PosterUrl = "https://image.tmdb.org/t/p/w500/cam.jpg" },
                new Movie { Title = "Gặp Lại Chị Bầu", Duration = 114, AgeRating = "13+", Country = "Việt Nam", Language = "Tiếng Việt", ReleaseDate = today.AddDays(-13), EndDate = today.AddDays(60), IsActive = true, Description = "Câu chuyện xuyên không ấm áp về tình mẹ con và nhóm bạn trẻ đam mê nghệ thuật thập niên 90.", PosterUrl = "https://image.tmdb.org/t/p/w500/gaplaichibau.jpg" },
                new Movie { Title = "Moana 2", Duration = 100, AgeRating = "P", Country = "Mỹ", Language = "Lồng tiếng", ReleaseDate = today.AddDays(-3), EndDate = today.AddDays(60), IsActive = true, Description = "Moana và Maui tái hợp trên đại dương xa xôi theo lời gọi của tổ tiên.", PosterUrl = "https://image.tmdb.org/t/p/w500/moana2.jpg" },
                new Movie { Title = "Wicked", Duration = 160, AgeRating = "P", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(-2), EndDate = today.AddDays(60), IsActive = true, Description = "Hành trình số phận giữa Elphaba (Phù thủy xứ Oz) và Glinda tại đại học Shiz.", PosterUrl = "https://image.tmdb.org/t/p/w500/wicked.jpg" },
                new Movie { Title = "Joker: Folie à Deux", Duration = 138, AgeRating = "18+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(-16), EndDate = today.AddDays(60), IsActive = true, Description = "Arthur Fleck gặp gỡ Harley Quinn tại bệnh viện tâm thần Arkham trong vũ điệu điên loạn.", PosterUrl = "https://image.tmdb.org/t/p/w500/joker2.jpg" },
                new Movie { Title = "Cú Nhảy Sinh Tử", Duration = 107, AgeRating = "16+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(-17), EndDate = today.AddDays(60), IsActive = true, Description = "Hai người bạn mắc kẹt trên đỉnh tháp truyền hình 600m cô lập giữa sa mạc.", PosterUrl = "https://image.tmdb.org/t/p/w500/fall.jpg" },
                new Movie { Title = "Thám Tử Lừng Danh Hàn Quốc", Duration = 105, AgeRating = "16+", Country = "Hàn Quốc", Language = "Phụ đề", ReleaseDate = today.AddDays(-1), EndDate = today.AddDays(60), IsActive = true, Description = "Vụ án bí ẩn được phá giải bằng trí tuệ và sự phối hợp hài hước giữa các thám tử.", PosterUrl = "https://image.tmdb.org/t/p/w500/detective.jpg" }
            };

            foreach (var m in sampleMovies)
            {
                if (!_context.Movies.Any(x => x.Title == m.Title))
                {
                    _context.Movies.Add(m);
                }
            }
            _context.SaveChanges();

            Ensure10ComingSoonMovies();
        }

        private void Ensure10ComingSoonMovies()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var comingSoonMovies = new List<Movie>
            {
                new Movie { Title = "Avatar 3: Lửa Và Tro Tàn", Duration = 190, AgeRating = "13+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(15), EndDate = today.AddDays(75), IsActive = true, Description = "Hành trình tiếp theo trên hành tinh Pandora, khám phá bộ tộc Tro Tàn đầy tàn bạo và bí ẩn.", PosterUrl = "https://image.tmdb.org/t/p/w500/8cdWjvZQUExWVZE2KlCHSDUapxF.jpg" },
                new Movie { Title = "Captain America: Thế Giới Mới", Duration = 135, AgeRating = "13+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(20), EndDate = today.AddDays(80), IsActive = true, Description = "Sam Wilson chính thức khoác lên mình danh xưng Captain America và đối mặt với âm mưu chính trị toàn cầu.", PosterUrl = "https://image.tmdb.org/t/p/w500/z1y5O22iYwUv2jY7vL5k0j.jpg" },
                new Movie { Title = "Mufasa: Vua Sư Tử", Duration = 120, AgeRating = "P", Country = "Mỹ", Language = "Lồng tiếng", ReleaseDate = today.AddDays(10), EndDate = today.AddDays(70), IsActive = true, Description = "Câu chuyện về nguồn gốc thời trẻ của Mufasa từ chú sư tử mồ côi trở thành vị vua huyền thoại.", PosterUrl = "https://image.tmdb.org/t/p/w500/vpnVM9B6NMmQpEZZaOf8i21LI8a.jpg" },
                new Movie { Title = "Nhiệm Vụ Bất Khả Thi 8", Duration = 165, AgeRating = "16+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(25), EndDate = today.AddDays(85), IsActive = true, Description = "Ethan Hunt và biệt đội IMF bước vào trận chiến sinh tử cuối cùng chống lại trí tuệ nhân tạo Thực Thể.", PosterUrl = "https://image.tmdb.org/t/p/w500/1pdfLPoLZeYhMHu2NVhKHrmYyfl.jpg" },
                new Movie { Title = "Bộ Tứ Siêu Đẳng: Bước Khởi Đầu", Duration = 130, AgeRating = "13+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(30), EndDate = today.AddDays(90), IsActive = true, Description = "Bộ bốn siêu anh hùng gia đình đầu tiên của Marvel tái xuất trong thế giới retro-futuristic.", PosterUrl = "https://image.tmdb.org/t/p/w500/wWba3TaojhK7T44jVScEFiIOyPh.jpg" },
                new Movie { Title = "Doraemon: Bản Tình Ca Kính Vạn Hoa", Duration = 105, AgeRating = "P", Country = "Nhật Bản", Language = "Lồng tiếng", ReleaseDate = today.AddDays(8), EndDate = today.AddDays(68), IsActive = true, Description = "Nobita và Doraemon du hành vào thế giới âm nhạc phép thuật để giải cứu hành tinh.", PosterUrl = "https://image.tmdb.org/t/p/w500/9b2.jpg" },
                new Movie { Title = "Kính Vạn Hoa Điện Ảnh", Duration = 112, AgeRating = "P", Country = "Việt Nam", Language = "Tiếng Việt", ReleaseDate = today.AddDays(12), EndDate = today.AddDays(72), IsActive = true, Description = "Bộ ba Quý rốm, Hạnh cận và Tiểu Long tái xuất trong vụ án bí ẩn tại vùng quê mùa hè.", PosterUrl = "https://image.tmdb.org/t/p/w500/gaplaichibau.jpg" },
                new Movie { Title = "Fast & Furious 11: Cú Nốc Ao", Duration = 145, AgeRating = "16+", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(40), EndDate = today.AddDays(100), IsActive = true, Description = "Dominic Toretto cùng gia đình đối mặt với kẻ thù nguy hiểm nhất Dante Reyes.", PosterUrl = "https://image.tmdb.org/t/p/w500/v9.jpg" },
                new Movie { Title = "Spider-Man: Beyond the Spider-Verse", Duration = 148, AgeRating = "P", Country = "Mỹ", Language = "Phụ đề", ReleaseDate = today.AddDays(35), EndDate = today.AddDays(95), IsActive = true, Description = "Miles Morales đối mặt với bản sao đen tối của chính mình tại Vũ trụ 42.", PosterUrl = "https://image.tmdb.org/t/p/w500/8Vt6mWEReuy4Of61Lnj5Xj7sR4.jpg" },
                new Movie { Title = "Lật Mặt 8: Vòng Xoay Định Mệnh", Duration = 125, AgeRating = "16+", Country = "Việt Nam", Language = "Tiếng Việt", ReleaseDate = today.AddDays(18), EndDate = today.AddDays(78), IsActive = true, Description = "Tác phẩm điện ảnh hành động kịch tính của đạo diễn Lý Hải về tình anh em.", PosterUrl = "https://image.tmdb.org/t/p/w500/cam.jpg" }
            };

            foreach (var m in comingSoonMovies)
            {
                if (!_context.Movies.Any(x => x.Title == m.Title))
                {
                    _context.Movies.Add(m);
                }
            }
            _context.SaveChanges();
        }

        public IActionResult Index()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var now = DateTime.Now;

            // Tự động thêm 20 phim đang chiếu & 10 phim sắp chiếu vào CSDL
            CINEMA.Helpers.MovieSeeder.Seed(_context);

            // Phim đang chiếu (ReleaseDate <= hôm nay và chưa hết hạn)
            var movies = _context.Movies
                .Where(m => m.IsActive == true &&
                            m.ReleaseDate.HasValue &&
                            m.ReleaseDate <= today &&
                            (!m.EndDate.HasValue || m.EndDate >= today))
                .OrderByDescending(m => m.ReleaseDate)
                .ToList();

            // Phim sắp chiếu (ReleaseDate > hôm nay)
            var comingSoon = _context.Movies
                .Where(m => m.IsActive == true &&
                            m.ReleaseDate.HasValue &&
                            m.ReleaseDate > today)
                .OrderBy(m => m.ReleaseDate)
                .ToList();

            // Rạp
            var theaters = _context.Theaters
                .Where(t => t.IsActive == true)
                .OrderBy(t => t.Name)
                .ToList();

            // Popup quảng cáo
            var popup = _context.Popups
                .FirstOrDefault(p => p.IsActive == true);

            // 📌 THỐNG KÊ THỂ LOẠI PHỔ BIẾN (Dựa trên lượng click và xem của toàn bộ khách hàng)
            var popularGenres = _context.UserActivityLogs
                .Include(l => l.Movie)
                    .ThenInclude(m => m.Genres)
                .Include(l => l.Genre)
                .AsEnumerable()
                .SelectMany(l => {
                    var list = new List<Genre>();
                    if (l.Genre != null) list.Add(l.Genre);
                    if (l.Movie?.Genres != null) list.AddRange(l.Movie.Genres);
                    return list.GroupBy(g => g.GenreId).Select(g => g.First());
                })
                .GroupBy(g => g.GenreId)
                .Select(group => new {
                    Genre = group.First(),
                    Count = group.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(3)
                .Select(x => x.Genre)
                .ToList();

            if (!popularGenres.Any())
            {
                popularGenres = _context.Genres.Take(3).ToList();
            }

            // 📌 THỐNG KÊ COMBO PHỔ BIẾN (Dựa trên số lượng bán được từ các đơn hàng thành công)
            var topComboIds = _context.OrderCombos
                .Where(oc => oc.ComboId.HasValue && 
                             (oc.Order.Status == "Paid" || oc.Order.Status == "Completed" || oc.Order.PaidAt != null))
                .GroupBy(oc => oc.ComboId)
                .Select(g => new {
                    ComboId = g.Key,
                    TotalQty = g.Sum(oc => oc.Quantity ?? 0)
                })
                .OrderByDescending(x => x.TotalQty)
                .Take(4)
                .ToList();

            var comboIds = topComboIds
                .Where(x => x.ComboId.HasValue)
                .Select(x => x.ComboId.Value)
                .ToList();

            var popularCombos = _context.Combos
                .Where(c => c.IsActive == true && comboIds.Contains(c.ComboId))
                .ToList()
                .OrderBy(c => comboIds.IndexOf(c.ComboId))
                .ToList();

            if (!popularCombos.Any())
            {
                popularCombos = _context.Combos.Where(c => c.IsActive == true).Take(4).ToList();
            }

            // 📌 LẤY TOÀN BỘ PHIM ĐANG CHIẾU VÀ ĐẶT PHIM PHỔ BIẾN LÊN ĐẦU
            var allActiveMovies = _context.Movies
                .Where(m => m.IsActive == true)
                .ToList();

            var movieViewsDict = _context.UserMovieViews
                .Where(v => v.Movie != null && v.Movie.IsActive == true)
                .GroupBy(v => v.MovieId)
                .ToDictionary(g => g.Key, g => g.Sum(v => v.ViewCount));

            var popularMovies = allActiveMovies
                .OrderByDescending(m => movieViewsDict.ContainsKey(m.MovieId) ? movieViewsDict[m.MovieId] : 0)
                .ThenByDescending(m => m.ReleaseDate)
                .Take(10)
                .ToList();

            ViewBag.ComingSoon = comingSoon;
            ViewBag.Theaters = theaters;
            ViewBag.Popup = popup;
            ViewBag.PopularGenres = popularGenres;
            ViewBag.PopularMovies = popularMovies;
            ViewBag.PopularCombos = popularCombos;

            return View(movies);
        }

        // =====================================================
        // ================== API QUICK BOOKING =================
        // =====================================================

        // Lấy phim theo rạp
        [HttpGet]
        public IActionResult GetMoviesByTheater(int theaterId)
        {
            var now = DateTime.Now; // Lấy thời gian hiện tại
            var movies = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                .Where(s => s.Auditorium.TheaterId == theaterId &&
                            s.IsActive == true &&
                            s.Movie.IsActive == true &&
                            s.StartTime > now) // Thay >= bằng > để loại bỏ các suất vừa đúng giờ này
                .Select(s => new { s.Movie.MovieId, s.Movie.Title })
                .Distinct()
                .OrderBy(m => m.Title)
                .ToList();

            return Json(movies);
        }

        // Lấy suất chiếu
        [HttpGet]
        public IActionResult GetShowtimes(int theaterId, int movieId)
        {
            var now = DateTime.Now;
            var showtimes = _context.Showtimes
                .Include(s => s.Auditorium)
                .Where(s => s.Auditorium.TheaterId == theaterId &&
                            s.MovieId == movieId &&
                            s.IsActive == true &&
                            s.StartTime > now) // Chỉ lấy các suất chưa diễn ra
                .OrderBy(s => s.StartTime)
                .Select(s => new {
                    s.ShowtimeId,
                    Date = s.StartTime!.Value.ToString("yyyy-MM-dd"),
                    Time = s.StartTime!.Value.ToString("HH:mm"),
                    Price = s.BasePrice ?? 0
                })
                .ToList();

            return Json(showtimes);
        }


        // =====================================================
        // =================== CHI TIẾT PHIM ===================
        // =====================================================
        [HttpGet]
        public IActionResult MovieDetails(int id)
        {
            var movie = _context.Movies
                .Include(m => m.Genres)
                .FirstOrDefault(m => m.MovieId == id && m.IsActive == true);

            if (movie == null)
                return NotFound("Không tìm thấy phim.");

            // 📌 GHI LOG XEM CHI TIẾT
            LogActivity("VIEW_DETAILS", movieId: id);
            TrackMovieView(id);

            // ── QUẢN LÝ ĐÁNH GIÁ & BÌNH LUẬN ──
            var now = DateTime.Now;
            var customerId = GetCurrentCustomerId();
            bool canComment = false;
            int? validOrderId = null;

            if (customerId.HasValue)
            {
                // MỞ ĐỂ TEST: Bất kỳ ai đăng nhập đều được bình luận
                canComment = true;
                /*
                var pastTicket = _context.Tickets
                    .Include(t => t.Order)
                    .Include(t => t.Showtime)
                    .FirstOrDefault(t => 
                        t.Order != null && 
                        t.Order.CustomerId == customerId.Value &&
                        (t.Order.Status == "Paid" || t.Order.Status == "Completed" || t.Order.Status == "Đã thanh toán") &&
                        t.Showtime != null && 
                        t.Showtime.MovieId == id &&
                        t.Showtime.StartTime < now
                    );
                if (pastTicket != null)
                {
                    canComment = true;
                    validOrderId = pastTicket.OrderId;
                }
                */
            }

            var reviews = _context.Reviews
                .Include(r => r.Customer)
                .Where(r => r.MovieId == id && r.IsHidden == false)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            double averageRating = 0;
            int ratingsCount = reviews.Count;
            if (ratingsCount > 0)
            {
                averageRating = Math.Round(reviews.Average(r => r.Rating), 1);
            }

            // Gợi ý phim cùng thể loại (ví dụ 4 phim)
            var today = DateOnly.FromDateTime(DateTime.Today);
            var genreIds = movie.Genres?.Select(g => g.GenreId).ToList() ?? new List<int>();
            var relatedMovies = _context.Movies
                .Include(m => m.Genres)
                .Where(m => m.IsActive == true && m.MovieId != id && m.Genres.Any(g => genreIds.Contains(g.GenreId)))
                .Take(4)
                .ToList();

            if (relatedMovies.Count < 4)
            {
                var needed = 4 - relatedMovies.Count;
                var existingIds = relatedMovies.Select(rm => rm.MovieId).Concat(new[] { id }).ToList();
                var fallbackMovies = _context.Movies
                    .Include(m => m.Genres)
                    .Where(m => m.IsActive == true && !existingIds.Contains(m.MovieId))
                    .Take(needed)
                    .ToList();
                relatedMovies.AddRange(fallbackMovies);
            }

            ViewBag.CanComment = canComment;
            ViewBag.ValidOrderId = validOrderId;
            ViewBag.Reviews = reviews;
            ViewBag.AverageRating = averageRating;
            ViewBag.RatingsCount = ratingsCount;
            ViewBag.RelatedMovies = relatedMovies;

            return View(movie);
        }

        [HttpGet]
        public IActionResult BookTicket(int id, int? showtimeId, bool isRecommend = false)
        {
            var movie = _context.Movies
                .Include(m => m.Genres)
                .FirstOrDefault(m => m.MovieId == id && m.IsActive == true);

            if (movie == null)
                return NotFound("Không tìm thấy phim.");

            // ── QUẢN LÝ ĐÁNH GIÁ & BÌNH LUẬN ──
            var now = DateTime.Now;
            var customerId = GetCurrentCustomerId();
            bool canComment = false;
            int? validOrderId = null;

            if (customerId.HasValue)
            {
                var pastTicket = _context.Tickets
                    .Include(t => t.Order)
                    .Include(t => t.Showtime)
                    .FirstOrDefault(t => 
                        t.Order != null && 
                        t.Order.CustomerId == customerId.Value &&
                        (t.Order.Status == "Paid" || t.Order.Status == "Completed" || t.Order.Status == "Đã thanh toán") &&
                        t.Showtime != null && 
                        t.Showtime.MovieId == id &&
                        t.Showtime.StartTime < now
                    );
                if (pastTicket != null)
                {
                    canComment = true;
                    validOrderId = pastTicket.OrderId;
                }
            }

            var reviews = _context.Reviews
                .Include(r => r.Customer)
                .Where(r => r.MovieId == id && r.IsHidden == false)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            double averageRating = 0;
            int ratingsCount = reviews.Count;
            if (ratingsCount > 0)
            {
                averageRating = Math.Round(reviews.Average(r => r.Rating), 1);
            }

            ViewBag.CanComment = canComment;
            ViewBag.ValidOrderId = validOrderId;
            ViewBag.Reviews = reviews;
            ViewBag.AverageRating = averageRating;
            ViewBag.RatingsCount = ratingsCount;

            // 📌 GHI LOG XEM PHIM (LƯU VẾT CLICK GỢI Ý NẾU CÓ)
            if (isRecommend)
            {
                LogActivity("CLICK_RECOMMEND", movieId: id);
            }
            else
            {
                LogActivity("VIEW_MOVIE", movieId: id);
            }
            TrackMovieView(id);

            // 📌 LƯU TƯƠNG TÁC CLICK PHIM VÀO SESSION ĐỂ GỢI Ý PHIM CÙNG THỂ LOẠI
            HttpContext.Session.SetString("LatestInteraction", "Click");
            HttpContext.Session.SetInt32("LatestClickedMovieId", id);

            // Lấy tất cả suất chiếu trước
            var showtimes = _context.Showtimes
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .Where(s =>
                       s.MovieId == id &&
                       s.IsActive == true &&
                       s.StartTime >= DateTime.Now)
                .OrderBy(s => s.StartTime)
                .ToList();

            // ❗ Nếu không có suất chiếu
            if (!showtimes.Any())
            {
                ViewBag.Message = "Phim này hiện chưa có lịch chiếu.";
                ViewBag.Showtimes = new List<Showtime>();
                return View(movie); // hoặc redirect trang khác
            }

            // Nếu chưa chọn thì lấy suất đầu
            if (!showtimeId.HasValue)
            {
                showtimeId = showtimes.First().ShowtimeId;
            }

            var showtime = showtimes.FirstOrDefault(s => s.ShowtimeId == showtimeId);

            if (showtime == null)
                return NotFound("Suất chiếu không tồn tại.");

            // Ghế
            var seats = _context.Seats
                .Where(s => s.AuditoriumId == showtime.AuditoriumId && s.IsActive == true)
                .OrderBy(s => s.RowLabel)
                .ThenBy(s => s.SeatNumber)
                .ToList();

            var bookedSeats = _context.Tickets
                .Include(t => t.Seat)
                .Include(t => t.Order)
                .Where(t => t.ShowtimeId == showtime.ShowtimeId
                    && t.Order != null
                    && (t.Order.Status == "Đã thanh toán" 
                        || ((t.Order.Status == "Chờ thanh toán" || t.Order.Status == "Đang chờ thanh toán") 
                            && t.Order.ExpiredAt > now)))
                .Select(t => t.Seat.RowLabel + t.Seat.SeatNumber)
                .ToList();


            var genreIds = movie.Genres?.Select(g => g.GenreId).ToList() ?? new List<int>();
            var recommendedCombos = _recommendationEngine.GetRecommendedCombos(id, genreIds, customerId);

            ViewBag.Showtime = showtime;
            ViewBag.Showtimes = showtimes;
            ViewBag.Seats = seats;
            ViewBag.BookedSeats = bookedSeats;
            ViewBag.Combos = recommendedCombos;

            return View(movie);
        }

        // POST từ Quick Booking
        [HttpPost]
        public IActionResult BookTicket(int movieId, int showtimeId)
        {
            return RedirectToAction("BookTicket", new
            {
                id = movieId,
                showtimeId = showtimeId
            });
        }

        // =====================================================
        // ======================= LỊCH CHIẾU ===================
        // =====================================================

        [HttpGet]
        public IActionResult Schedule(DateTime? date)
        {
            var today = DateTime.Today;
            var maxDate = today.AddDays(29); // Cho phép chọn bấm xem lịch cả tháng
            
            var selectedDate = date?.Date ?? today;
            if (selectedDate < today || selectedDate > maxDate)
            {
                selectedDate = today;
            }
            
            var now = DateTime.Now;
            var isRestricted = selectedDate > today.AddDays(1); // Chỉ cho phép xem suất chiếu 2 ngày gần nhất
            
            List<Movie> movies = new List<Movie>();

            if (!isRestricted)
            {
                movies = _context.Movies
                    .Include(m => m.Genres)
                    .Include(m => m.Showtimes.Where(s =>
                        s.StartTime.HasValue &&
                        ((selectedDate > DateTime.Today) || (s.StartTime > now))
                    ))
                    .ThenInclude(s => s.Auditorium)
                        .ThenInclude(a => a.Theater)
                    .Where(m =>
                        m.IsActive == true &&
                        m.Showtimes.Any(s =>
                            s.StartTime.HasValue &&
                            s.StartTime.Value.Date == selectedDate &&
                            ((selectedDate > DateTime.Today) || (s.StartTime > now))
                        ))
                    .OrderBy(m => m.Title)
                    .ToList();
            }

            ViewBag.SelectedDate = selectedDate;
            ViewBag.IsRestricted = isRestricted;

            return View(movies);
        }
        // =====================================================
        // ======================= THANH TOÁN ===================
        // =====================================================

        [HttpPost]
        public IActionResult GoToPayment(int movieId, int showtimeId, string selectedSeats, int comboId)
        {
            // ❌ CHƯA CHỌN GHẾ
            if (string.IsNullOrEmpty(selectedSeats))
            {
                TempData["Error"] = "Vui lòng chọn ít nhất 1 ghế!";
                return RedirectToAction("BookTicket", new
                {
                    id = movieId,
                    showtimeId = showtimeId
                });
            }

            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == movieId);
            var showtime = _context.Showtimes
                .Include(s => s.Auditorium)
                .ThenInclude(a => a.Theater)
                .FirstOrDefault(s => s.ShowtimeId == showtimeId);

            var combo = _context.Combos.FirstOrDefault(c => c.ComboId == comboId);

            if (movie == null || showtime == null)
                return NotFound("Phim hoặc suất chiếu không tồn tại.");

            int seatCount = selectedSeats
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Length;

            decimal ticketPrice = (showtime.BasePrice ?? 0) * seatCount;
            decimal comboPrice = combo?.Price ?? 0;
            decimal total = ticketPrice + comboPrice;

            ViewBag.Movie = movie;
            ViewBag.Showtime = showtime;
            ViewBag.SelectedSeats = selectedSeats;
            ViewBag.Combo = combo;
            ViewBag.Total = total;

            return View("Payment");
        }

        // =====================================================
        // =================== PRIVACY / ERROR ==================
        // =====================================================

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        [HttpPost]
        public IActionResult ConfirmPayment(int showtimeId, string selectedSeats, int comboId)
        {
            // ⚠️ Lấy user hiện tại (an toàn hơn, không crash nếu chưa đăng nhập)
            var customerId = GetCurrentCustomerId();
            if (customerId == null)
            {
                TempData["Error"] = "Vui lòng đăng nhập để đặt vé.";
                return RedirectToAction("Login", "Account"); // đổi lại route đăng nhập thực tế của bạn
            }

            var showtime = _context.Showtimes.Find(showtimeId);
            var combo = _context.Combos.FirstOrDefault(c => c.ComboId == comboId);

            if (showtime == null)
                return NotFound();

            var seatList = selectedSeats.Split(',', StringSplitOptions.RemoveEmptyEntries);

            decimal seatSurchargeTotal = 0m;
            foreach (var seatStr in seatList)
            {
                if (seatStr.Length >= 2)
                {
                    string row = seatStr.Substring(0, 1);
                    if (int.TryParse(seatStr.Substring(1), out int number))
                    {
                        var seatObj = _context.Seats.FirstOrDefault(s =>
                            s.RowLabel == row &&
                            s.SeatNumber == number &&
                            s.AuditoriumId == showtime.AuditoriumId);
                        if (seatObj != null)
                        {
                            if (seatObj.SeatType == "VIP")
                                seatSurchargeTotal += 30000m;
                            else if (seatObj.SeatType == "Couple")
                                seatSurchargeTotal += 100000m;
                        }
                    }
                }
            }

            decimal ticketTotal = seatList.Length * (showtime.BasePrice ?? 0) + seatSurchargeTotal;
            decimal comboPrice = combo?.Price ?? 0;
            decimal totalAmount = ticketTotal + comboPrice;

            // 🧾 Tạo Order
            var order = new Order
            {
                CustomerId = customerId.Value,
                CreatedAt = DateTime.Now,
                Status = "Đã thanh toán",
                TotalAmount = totalAmount
            };

            _context.Orders.Add(order);
            _context.SaveChanges();

            // 🎟️ Tạo Ticket
            foreach (var seat in seatList)
            {
                string row = seat.Substring(0, 1);
                int number = int.Parse(seat.Substring(1));

                var seatObj = _context.Seats.FirstOrDefault(s =>
                    s.RowLabel == row &&
                    s.SeatNumber == number &&
                    s.AuditoriumId == showtime.AuditoriumId);

                if (seatObj != null)
                {
                    decimal seatSurcharge = 0m;
                    if (seatObj.SeatType == "VIP")
                        seatSurcharge = 30000m;
                    else if (seatObj.SeatType == "Couple")
                        seatSurcharge = 100000m;

                    _context.Tickets.Add(new Ticket
                    {
                        ShowtimeId = showtimeId,
                        SeatId = seatObj.SeatId,
                        OrderId = order.OrderId,
                        Price = (showtime.BasePrice ?? 0) + seatSurcharge
                    });
                }
            }

            _context.SaveChanges();

            // 📌 GHI LOG ĐẶT VÉ THÀNH CÔNG (tín hiệu quan trọng nhất cho gợi ý)
            LogActivity("BOOK_TICKET", movieId: showtime.MovieId);

            // 💎 UPDATE MEMBERSHIP
            var customer = _context.Customers.Find(customerId.Value);
            if (customer != null)
            {
                customer.TotalSpent += totalAmount;
                _context.SaveChanges();
            }

            return RedirectToAction("Index"); // hoặc trang success
        }

        [HttpGet]
        public IActionResult CheckVoucher(string code, decimal total, decimal comboTotal = 0)
        {
            if (string.IsNullOrEmpty(code))
                return Json(new { success = false, message = "Chưa nhập mã voucher" });

            var voucher = _context.Vouchers
                .Include(v => v.VoucherCondition)
                    .ThenInclude(vc => vc.Rules)
                .FirstOrDefault(v => v.Code != null &&
                                     v.Code.ToLower() == code.ToLower() &&
                                     v.IsActive);

            if (voucher == null)
                return Json(new { success = false, message = "Mã voucher không tồn tại" });

            if (voucher.StartDate != null && voucher.StartDate > DateTime.Now)
                return Json(new { success = false, message = "Chưa đến thời gian áp dụng" });

            if (voucher.EndDate != null && voucher.EndDate < DateTime.Now)
                return Json(new { success = false, message = "Mã voucher đã hết hạn" });

            if (voucher.UsedCount >= voucher.Quantity)
                return Json(new { success = false, message = "Mã voucher đã hết lượt sử dụng" });

            if (total < voucher.MinOrderValue)
                return Json(new { success = false, message = "Đơn hàng chưa đủ điều kiện áp dụng mã này" });

            // =============== TÍCH HỢP KIỂM TRA PHẠM VI ÁP DỤNG ===============
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            decimal mDiscountPercent = 0m;
            if (customerId != null)
            {
                var customer = _context.Customers.Find(customerId.Value);
                if (customer != null)
                {
                    string membershipLevel = customer.MembershipLevel ?? "Đồng";
                    if (membershipLevel == "Kim cương")
                        mDiscountPercent = 0.10m;
                    else if (membershipLevel == "Bạc")
                        mDiscountPercent = 0.05m;
                }
            }

            // Thu thập thông tin cho Evaluation Context
            int ticketQuantity = 0;
            int comboQuantity = 0;
            decimal comboTotalVal = comboTotal * (1 - mDiscountPercent);
            decimal ticketTotal = total - comboTotalVal;
            if (ticketTotal < 0) ticketTotal = 0;

            bool isGroupBooking = HttpContext.Session.GetString("GroupBooking_RoomId") != null;
            int groupMemberCount = 0;
            string dayOfWeek = DateTime.Now.DayOfWeek.ToString();
            int showtimeHour = DateTime.Now.Hour;

            // Đọc thêm từ Session
            var showtimeIdOpt = HttpContext.Session.GetInt32("Booking_ShowtimeId");
            if (showtimeIdOpt.HasValue)
            {
                var showtime = _context.Showtimes
                    .Include(s => s.Movie)
                    .FirstOrDefault(s => s.ShowtimeId == showtimeIdOpt.Value);
                if (showtime != null && showtime.StartTime.HasValue)
                {
                    dayOfWeek = showtime.StartTime.Value.DayOfWeek.ToString();
                    showtimeHour = showtime.StartTime.Value.Hour;
                }
            }

            var adultTk = HttpContext.Session.GetInt32("Booking_AdultTickets") ?? 0;
            var childTk = HttpContext.Session.GetInt32("Booking_ChildTickets") ?? 0;
            var studentTk = HttpContext.Session.GetInt32("Booking_StudentTickets") ?? 0;
            ticketQuantity = adultTk + childTk + studentTk;

            var combosJson = HttpContext.Session.GetString("Booking_Combos");
            if (!string.IsNullOrEmpty(combosJson))
            {
                try
                {
                    var comboDict = JsonSerializer.Deserialize<Dictionary<string, string>>(combosJson);
                    if (comboDict != null)
                    {
                        foreach (var kvp in comboDict)
                        {
                            if (int.TryParse(kvp.Value, out int q))
                            {
                                comboQuantity += q;
                            }
                        }
                    }
                }
                catch { }
            }

            if (isGroupBooking)
            {
                string roomId = HttpContext.Session.GetString("GroupBooking_RoomId");
                var room = _context.GroupBookingRooms
                    .Include(r => r.Members)
                    .FirstOrDefault(r => r.RoomId == roomId);
                if (room != null)
                {
                    groupMemberCount = room.Members.Count;
                }
            }

            // Đánh giá các quy tắc động
            if (voucher.VoucherCondition != null && voucher.VoucherCondition.Rules != null && voucher.VoucherCondition.Rules.Any())
            {
                var evalContext = new Helpers.VoucherEvaluationContext
                {
                    TicketQuantity = ticketQuantity,
                    ComboQuantity = comboQuantity,
                    TicketTotal = ticketTotal,
                    ComboTotal = comboTotalVal,
                    TotalPrice = total,
                    IsGroupBooking = isGroupBooking,
                    GroupMemberCount = groupMemberCount,
                    DayOfWeek = dayOfWeek,
                    ShowtimeHour = showtimeHour
                };

                foreach (var rule in voucher.VoucherCondition.Rules)
                {
                    if (!Helpers.VoucherRuleEvaluator.Evaluate(rule, evalContext, out string ruleError))
                    {
                        return Json(new { success = false, message = $"Voucher không hợp lệ: {ruleError}" });
                    }
                }
            }

            // ===================================================================

            // Tính số tiền được giảm theo điều kiện phạm vi áp dụng
            decimal discountableAmount = total;
            if (voucher.VoucherCondition != null && voucher.VoucherCondition.Rules != null && voucher.VoucherCondition.Rules.Any())
            {
                bool hasTicketRules = voucher.VoucherCondition.Rules.Any(r => r.Field == "TicketQuantity" || r.Field == "TicketTotal");
                bool hasComboRules = voucher.VoucherCondition.Rules.Any(r => r.Field == "ComboQuantity" || r.Field == "ComboTotal");

                if (hasTicketRules && !hasComboRules)
                {
                    discountableAmount = ticketTotal;
                }
                else if (hasComboRules && !hasTicketRules)
                {
                    discountableAmount = comboTotalVal;
                }
            }

            // Nếu qua hết các điều kiện trên thì mới áp dụng tính tiền
            decimal discount = 0;

            if (voucher.DiscountPercent.HasValue)
                discount = discountableAmount * (decimal)(voucher.DiscountPercent.Value / 100.0);
            else if (voucher.DiscountAmount.HasValue)
                discount = voucher.DiscountAmount.Value;

            if (discount > discountableAmount)
                discount = discountableAmount;

            return Json(new
            {
                success = true,
                discount = discount,
                terms = voucher.VoucherCondition?.Description ?? "Áp dụng cho toàn bộ đơn hàng"
            });
        }

        private string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c)
                    != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString()
                     .Replace('đ', 'd')
                     .Replace('Đ', 'D')
                     .ToLower();
        }

        // =====================================================
        // ================ TRACKING HELPERS ====================
        // =====================================================

        private int? GetCurrentCustomerId()
        {
            var idStr = HttpContext.Session.GetString("CustomerId");
            return HttpContext.Session.GetInt32("CustomerId");
        }

        private void LogActivity(string activityType, int? movieId = null, int? genreId = null, string? metadata = null)
        {
            var log = new UserActivityLog
            {
                CustomerId = GetCurrentCustomerId(),
                SessionId = HttpContext.Session.Id,
                ActivityType = activityType,
                MovieId = movieId,
                GenreId = genreId,
                Metadata = metadata,
                DeviceType = Request.Headers["User-Agent"].ToString().Contains("Mobile") ? "mobile" : "web",
                CreatedAt = DateTime.Now
            };
            _context.UserActivityLogs.Add(log);
            _context.SaveChanges();
        }

        private void TrackMovieView(int movieId)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null) return; // chỉ track khách đã đăng nhập cho bảng này

            var view = _context.UserMovieViews
                .FirstOrDefault(v => v.CustomerId == customerId && v.MovieId == movieId);

            if (view != null)
            {
                view.ViewCount += 1;
                view.LastViewedAt = DateTime.Now;
            }
            else
            {
                _context.UserMovieViews.Add(new UserMovieView
                {
                    CustomerId = customerId.Value,
                    MovieId = movieId,
                    ViewCount = 1,
                    LastViewedAt = DateTime.Now
                });
            }
            _context.SaveChanges();
        }

        // =====================================================
        // ================ GỢI Ý PHIM (RECOMMEND) ===============
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Recommend()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var customerId = GetCurrentCustomerId();

            // 1. Gợi ý phim từ RecommendationEngine (Apriori + Cá nhân hóa + Gemini AI)
            var recommendedMovies = await _recommendationEngine.GetRecommendedMovies(customerId);

            // Khôi phục IsActive = true cho toàn bộ phim nếu cần
            var disabledMoviesInApi = _context.Movies.Where(m => m.IsActive != true).ToList();
            if (disabledMoviesInApi.Any())
            {
                foreach (var m in disabledMoviesInApi)
                {
                    m.IsActive = true;
                }
                _context.SaveChanges();
            }

            // 2. Lấy toàn bộ danh sách phim đang chiếu
            var allActiveMovies = _context.Movies
                .Where(m => m.IsActive == true)
                .ToList();

            var movieViewsDict = _context.UserMovieViews
                .Where(v => v.Movie != null && v.Movie.IsActive == true)
                .GroupBy(v => v.MovieId)
                .ToDictionary(g => g.Key, g => g.Sum(v => v.ViewCount));

            var popularMovies = allActiveMovies
                .OrderByDescending(m => movieViewsDict.ContainsKey(m.MovieId) ? movieViewsDict[m.MovieId] : 0)
                .ThenByDescending(m => m.ReleaseDate)
                .ToList();

            // =====================
            // Gộp Gợi ý + Popular + Phim còn lại (Tối đa 10 phim cho lưới trang chủ)
            // =====================
            var finalMovies = recommendedMovies
                .Concat(popularMovies)
                .Concat(allActiveMovies)
                .GroupBy(m => m.MovieId)
                .Select(g => g.First())
                .Take(10)
                .Select(m => new
                {
                    movieId = m.MovieId,
                    title = m.Title,
                    posterUrl = m.PosterUrl,
                    ageRating = m.AgeRating,
                    duration = m.Duration
                })
                .ToList();

            return Json(finalMovies);
        }

        // =====================================================
        // ============= TRANG PHIM ĐANG CHIẾU (MỚI) ============
        // =====================================================
        [HttpGet]
        public IActionResult NowShowing(int? genreId, string search)
        {
            CINEMA.Helpers.MovieSeeder.Seed(_context);

            var today = DateOnly.FromDateTime(DateTime.Today);
            var query = _context.Movies
                .Include(m => m.Genres)
                .Where(m => m.IsActive == true &&
                            m.ReleaseDate.HasValue &&
                            m.ReleaseDate <= today &&
                            (!m.EndDate.HasValue || m.EndDate >= today));

            if (genreId.HasValue && genreId.Value > 0)
            {
                query = query.Where(m => m.Genres.Any(g => g.GenreId == genreId.Value));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLower();
                query = query.Where(m => m.Title != null && m.Title.ToLower().Contains(searchLower));
            }

            var movies = query
                .OrderByDescending(m => m.ReleaseDate)
                .ToList();

            ViewBag.Genres = _context.Genres.OrderBy(g => g.Name).ToList();
            ViewBag.SelectedGenreId = genreId;
            ViewBag.SearchKeyword = search;

            return View(movies);
        }

        // =====================================================
        // ============= TRANG PHIM SẮP CHIẾU (MỚI) ============
        // =====================================================
        [HttpGet]
        public IActionResult ComingSoon(int? genreId, string search)
        {
            CINEMA.Helpers.MovieSeeder.Seed(_context);

            var today = DateOnly.FromDateTime(DateTime.Today);
            var query = _context.Movies
                .Include(m => m.Genres)
                .Where(m => m.IsActive == true &&
                            m.ReleaseDate.HasValue &&
                            m.ReleaseDate > today);

            if (genreId.HasValue && genreId.Value > 0)
            {
                query = query.Where(m => m.Genres.Any(g => g.GenreId == genreId.Value));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLower();
                query = query.Where(m => m.Title != null && m.Title.ToLower().Contains(searchLower));
            }

            var movies = query
                .OrderBy(m => m.ReleaseDate)
                .ToList();

            ViewBag.Genres = _context.Genres.OrderBy(g => g.Name).ToList();
            ViewBag.SelectedGenreId = genreId;
            ViewBag.SearchKeyword = search;

            return View(movies);
        }
        [HttpGet]
        public IActionResult MoviesByGenre(int genreId)
        {
            // 1. Tìm thể loại trước để lấy tên
            var genre = _context.Genres.FirstOrDefault(g => g.GenreId == genreId);

            if (genre == null)
                return NotFound("Thể loại không tồn tại.");

            // 2. Ghi log: Truyền genre.Name vào tham số metadata
            LogActivity("VIEW_GENRE", genreId: genreId, metadata: "Thể loại: " + genre.Name);

            // 3. Lấy danh sách phim như cũ
            var movies = _context.Movies
                .Include(m => m.Genres)
                .Where(m => m.IsActive == true && m.Genres.Any(g => g.GenreId == genreId))
                .OrderByDescending(m => m.ReleaseDate)
                .ToList();

            ViewBag.GenreName = genre.Name;
            return View(movies);
        }

        // =====================================================
        // ================ QUẢN LÝ BÌNH LUẬN & ĐÁNH GIÁ ====================
        // =====================================================
        // =====================================================
        // ================ QUẢN LÝ BÌNH LUẬN & ĐÁNH GIÁ ====================
        // =====================================================

        // =====================================================
        // ================ QUẢN LÝ BÌNH LUẬN & ĐÁNH GIÁ ====================
        // =====================================================

        // 📝 THÊM ĐÁNH GIÁ MỚI (Luôn luôn tạo mới, cho phép bình luận nhiều lần)
        [HttpPost]
        public async Task<IActionResult> AddReview(int movieId, int rating, string comment, int? orderId)
        {
            // 1. Xử lý ID người dùng (Đã đăng nhập hoặc Khách ẩn danh)
            var customerId = GetCurrentCustomerId();
            int finalCustomerId;
            bool isGuest = !customerId.HasValue;

            if (isGuest)
            {
                // Nếu chưa đăng nhập: Gom vào tài khoản "Khách ẩn danh" chung
                var guestAccount = _context.Customers.FirstOrDefault(c => c.Email == "anonymous@cinezone.com");
                if (guestAccount == null)
                {
                    guestAccount = new Customer
                    {
                        FullName = "Khách ẩn danh",
                        Email = "anonymous@cinezone.com",
                        PasswordHash = "GUEST_NONE",
                        CreatedAt = DateTime.Now,
                        MembershipLevel = "Đồng",
                        TotalSpent = 0
                    };
                    _context.Customers.Add(guestAccount);
                    await _context.SaveChangesAsync();
                }
                finalCustomerId = guestAccount.CustomerId;
            }
            else
            {
                finalCustomerId = customerId.Value;
            }

            // 2. Kiểm tra số sao hợp lệ
            if (rating < 1 || rating > 5)
            {
                return Json(new { success = false, message = "Đánh giá sao phải từ 1 đến 5." });
            }

            // 3. LUÔN LUÔN TẠO MỚI (Đã xóa bỏ hoàn toàn đoạn logic "existingReview" cũ)
            var review = new Review
            {
                MovieId = movieId,
                CustomerId = finalCustomerId,
                OrderId = orderId,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.Now,
                IsHidden = false,
                LikesCount = 0
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // 4. Tính gộp lượt vote mới vào điểm Bayesian hệ thống
            await _movieService.UpdateMovieBayesianRatingAsync(movieId);

            return Json(new { success = true, message = "Đã gửi đánh giá mới thành công!" });
        }

        // 🚩 BÁO CÁO VI PHẠM
        [HttpPost]
        public async Task<IActionResult> ReportReview(int reviewId, string reason)
        {
            var review = await _context.Reviews.FindAsync(reviewId);
            if (review == null)
            {
                return Json(new { success = false, message = "Bình luận này không tồn tại hoặc đã bị xóa." });
            }

            review.HasReport = true;
            review.ReportReason = string.IsNullOrEmpty(review.ReportReason)
                ? reason
                : review.ReportReason + "; " + reason;

            await _context.SaveChangesAsync();

            // Cập nhật uy tín người dùng sau khi bị báo cáo (bị trừ 5%)
            await _movieService.UpdateUserReputationAsync(review.CustomerId);

            return Json(new { success = true, message = "Đã gửi báo cáo vi phạm thành công. Ban quản lý sẽ sớm kiểm duyệt." });
        }
        // =====================================================
        // 🍿 ĐẶT BẮP NƯỚC LẺ (STANDALONE CONCESSIONS)
        // =====================================================
        [HttpGet]
        public IActionResult Concessions()
        {
            var combos = _context.Combos.Where(c => c.IsActive == true).ToList();
            ViewBag.Theaters = _context.Theaters.Where(t => t.IsActive == true).ToList();
            return View(combos);
        }

        [HttpPost]
        public IActionResult ConcessionsCheckout(List<int> comboIds, List<int> quantities, int theaterId)
        {
            if (theaterId <= 0)
            {
                TempData["Error"] = "Vui lòng chọn rạp chiếu để nhận bắp nước!";
                return RedirectToAction("Concessions");
            }

            var theater = _context.Theaters.FirstOrDefault(t => t.TheaterId == theaterId && t.IsActive == true);
            if (theater == null)
            {
                TempData["Error"] = "Rạp chiếu không tồn tại hoặc đã ngừng hoạt động!";
                return RedirectToAction("Concessions");
            }

            if (comboIds == null || quantities == null || comboIds.Count != quantities.Count)
            {
                TempData["Error"] = "Dữ liệu gửi lên không hợp lệ!";
                return RedirectToAction("Concessions");
            }

            decimal totalAmount = 0m;
            var checkoutList = new List<dynamic>();

            for (int i = 0; i < comboIds.Count; i++)
            {
                int comboId = comboIds[i];
                int qty = quantities[i];

                if (qty <= 0) continue;

                var combo = _context.Combos.FirstOrDefault(c => c.ComboId == comboId && c.IsActive == true);
                if (combo != null)
                {
                    decimal price = combo.Price ?? 0;
                    totalAmount += price * qty;

                    checkoutList.Add(new {
                        ComboId = comboId,
                        Name = combo.Name,
                        Quantity = qty,
                        UnitPrice = price,
                        ImageUrl = combo.ImageUrl,
                        Total = price * qty
                    });
                }
            }

            if (!checkoutList.Any())
            {
                TempData["Error"] = "Vui lòng chọn ít nhất 1 sản phẩm bắp nước!";
                return RedirectToAction("Concessions");
            }

            ViewBag.Theater = theater;
            ViewBag.Total = totalAmount;
            ViewBag.CheckoutItems = checkoutList;
            ViewBag.ComboIds = comboIds;
            ViewBag.Quantities = quantities;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProcessConcessionsPayment(
            string fullName, 
            string phone, 
            string email, 
            string voucherCode, 
            int theaterId, 
            List<int> comboIds, 
            List<int> quantities, 
            string paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Vui lòng điền đầy đủ Họ tên, Số điện thoại và Email!";
                return RedirectToAction("Concessions");
            }

            var theater = _context.Theaters.FirstOrDefault(t => t.TheaterId == theaterId);
            if (theater == null)
            {
                TempData["Error"] = "Rạp chiếu không hợp lệ!";
                return RedirectToAction("Concessions");
            }

            // 1. Tìm hoặc Tạo tài khoản khách lẻ (Guest account)
            var customer = _context.Customers.FirstOrDefault(c => c.Email.ToLower() == email.ToLower().Trim());
            if (customer == null)
            {
                customer = new Customer
                {
                    FullName = fullName.Trim(),
                    Email = email.ToLower().Trim(),
                    Phone = phone.Trim(),
                    PasswordHash = "GUEST_" + Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTime.Now,
                    MembershipLevel = "Đồng",
                    TotalSpent = 0
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            // Lưu session cho khách lẻ (cần thiết cho PaymentController xử lý VNPAY)
            HttpContext.Session.SetInt32("CustomerId", customer.CustomerId);
            HttpContext.Session.SetString("CustomerName", customer.FullName);
            HttpContext.Session.SetString("CustomerEmail", customer.Email);

            // 2. Tính toán tiền bắp nước
            decimal baseTotal = 0m;
            var orderCombosList = new List<OrderCombo>();

            for (int i = 0; i < comboIds.Count; i++)
            {
                int comboId = comboIds[i];
                int qty = quantities[i];

                if (qty <= 0) continue;

                var combo = _context.Combos.FirstOrDefault(c => c.ComboId == comboId && c.IsActive == true);
                if (combo != null)
                {
                    decimal price = combo.Price ?? 0;
                    baseTotal += price * qty;

                    orderCombosList.Add(new OrderCombo
                    {
                        ComboId = comboId,
                        Quantity = qty,
                        UnitPrice = price
                    });
                }
            }

            if (!orderCombosList.Any())
            {
                TempData["Error"] = "Vui lòng chọn ít nhất 1 sản phẩm bắp nước!";
                return RedirectToAction("Concessions");
            }

            // 3. Áp dụng Voucher (nếu có)
            decimal discount = 0m;
            if (!string.IsNullOrEmpty(voucherCode))
            {
                var voucher = _context.Vouchers
                    .Include(v => v.VoucherCondition)
                        .ThenInclude(vc => vc.Rules)
                    .FirstOrDefault(v => v.Code.ToLower() == voucherCode.ToLower().Trim() && v.IsActive);
                if (voucher != null)
                {
                    bool isVoucherValid = true;
                    if (voucher.VoucherCondition != null && voucher.VoucherCondition.Rules != null && voucher.VoucherCondition.Rules.Any())
                    {
                        int comboQuantity = 0;
                        for (int i = 0; i < comboIds.Count; i++)
                        {
                            if (quantities[i] > 0)
                            {
                                var combo = _context.Combos.FirstOrDefault(c => c.ComboId == comboIds[i] && c.IsActive == true);
                                if (combo != null) comboQuantity += quantities[i];
                            }
                        }

                        var evalContext = new Helpers.VoucherEvaluationContext
                        {
                            TicketQuantity = 0,
                            ComboQuantity = comboQuantity,
                            TicketTotal = 0,
                            ComboTotal = baseTotal,
                            TotalPrice = baseTotal,
                            IsGroupBooking = false,
                            GroupMemberCount = 0,
                            DayOfWeek = DateTime.Now.DayOfWeek.ToString(),
                            ShowtimeHour = DateTime.Now.Hour
                        };

                        foreach (var rule in voucher.VoucherCondition.Rules)
                        {
                            if (!Helpers.VoucherRuleEvaluator.Evaluate(rule, evalContext, out _))
                            {
                                isVoucherValid = false;
                                break;
                            }
                        }
                    }

                    if (voucher.StartDate.HasValue && DateTime.Now < voucher.StartDate.Value) { }
                    else if (voucher.EndDate.HasValue && DateTime.Now > voucher.EndDate.Value) { }
                    else if (voucher.UsedCount >= voucher.Quantity) { }
                    else if (baseTotal < voucher.MinOrderValue) { }
                    else if (!isVoucherValid) { }
                    else
                    {
                        if (voucher.DiscountPercent.HasValue)
                            discount = baseTotal * (decimal)(voucher.DiscountPercent.Value / 100.0);
                        else if (voucher.DiscountAmount.HasValue)
                            discount = voucher.DiscountAmount.Value;

                        if (discount > baseTotal)
                            discount = baseTotal;
                    }
                }
            }

            decimal totalAmount = baseTotal - discount;
            if (totalAmount < 0) totalAmount = 0;

            // 4. Tạo Order
            var order = new Order
            {
                CustomerId = customer.CustomerId,
                CreatedAt = DateTime.Now,
                Status = paymentMethod == "VNPAY" ? "Chờ thanh toán" : "Đã thanh toán",
                TotalAmount = totalAmount,
                ComboTotal = totalAmount,
                TicketTotal = 0,
                VoucherCode = string.IsNullOrEmpty(voucherCode) ? null : voucherCode.Trim(),
                DiscountAmount = discount,
                PaymentMethod = paymentMethod + " (Nhận tại: " + theater.Name + ")"
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // 5. Lưu OrderCombo
            foreach (var oc in orderCombosList)
            {
                oc.OrderId = order.OrderId;
                _context.OrderCombos.Add(oc);
            }
            await _context.SaveChangesAsync();

            // Cập nhật Voucher đã dùng
            if (!string.IsNullOrEmpty(voucherCode) && discount > 0)
            {
                var voucher = _context.Vouchers.FirstOrDefault(v => v.Code.ToLower() == voucherCode.ToLower().Trim());
                if (voucher != null)
                {
                    voucher.UsedCount++;
                }
            }

            // Cập nhật chi tiêu khách hàng
            customer.TotalSpent += totalAmount;
            customer.MembershipLevel = customer.CalculateMembershipLevel();
            await _context.SaveChangesAsync();

            // 6. Xử lý thanh toán
            if (paymentMethod == "VNPAY")
            {
                // Chuyển sang VNPAY của PaymentController
                return RedirectToAction("CreatePayment", "Payment", new { orderId = order.OrderId });
            }
            else
            {
                // Thanh toán trực tiếp tại quầy / COD giả lập thành công lập tức
                string reqBaseUrl = $"{Request.Scheme}://{Request.Host}";
                try
                {
                    await _emailService.SendOrderSuccessEmailAsync(order.OrderId, reqBaseUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send guest concessions success email.");
                }

                // Thiết lập các thuộc tính ViewBag để hiển thị trang Success.cshtml tương tự vé
                ViewBag.PaymentStatus = "Đã thanh toán";
                ViewBag.BookingCode = $"CZ{order.OrderId:D6}";
                ViewBag.CustomerName = customer.FullName;
                ViewBag.CustomerPhone = customer.Phone;
                ViewBag.PaymentMethod = "Thanh toán trực tiếp tại quầy";
                ViewBag.Total = totalAmount;

                var combosToShow = _context.OrderCombos
                    .Where(oc => oc.OrderId == order.OrderId)
                    .Include(oc => oc.Combo)
                    .Select(oc => new CINEMA.ViewModels.ComboViewModel
                    {
                        ComboName = oc.Combo != null ? oc.Combo.Name : "Combo",
                        Quantity = oc.Quantity ?? 0,
                        Price = oc.UnitPrice ?? 0
                    }).ToList();

                ViewBag.Combos = combosToShow;
                ViewBag.Message = "Hóa đơn đặt bắp nước đã được gửi tới hòm thư của bạn. Vui lòng xuất trình mã đơn hàng tại quầy để nhận bắp nước.";

                return View("~/Views/Payment/Success.cshtml");
            }
        }
    }
}