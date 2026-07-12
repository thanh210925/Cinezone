using System.Diagnostics;
using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
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

        public HomeController(ILogger<HomeController> logger, CinemaContext context, RecommendationEngine recommendationEngine, Services.GeminiService geminiService, IEmailService emailService)
        {
            _logger = logger;
            _context = context;
            _recommendationEngine = recommendationEngine;
            _geminiService = geminiService;
            _emailService = emailService;
        }

        // =====================================================
        // ====================== TRANG CHỦ ====================
        // =====================================================
        [HttpGet]
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

        public IActionResult Index()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var now = DateTime.Now;

            // Tự động ngưng chiếu các phim có ngày kết thúc (EndDate) đã qua, hoặc toàn bộ suất chiếu đã kết thúc
            var activeMovies = _context.Movies
                .Include(m => m.Showtimes)
                .Where(m => m.IsActive == true)
                .ToList();

            var modified = false;
            foreach (var m in activeMovies)
            {
                // 1. Kiểm tra ngày kết thúc của phim (EndDate)
                if (m.EndDate.HasValue && m.EndDate.Value < today)
                {
                    m.IsActive = false;
                    modified = true;
                }
                // 2. Hoặc kiểm tra nếu tất cả các suất chiếu đã kết thúc
                else if (m.Showtimes.Any())
                {
                    bool allEnded = m.Showtimes.All(s => {
                        if (s.EndTime.HasValue) return s.EndTime < now;
                        if (s.StartTime.HasValue && m.Duration.HasValue)
                            return s.StartTime.Value.AddMinutes(m.Duration.Value) < now;
                        return s.StartTime < now;
                    });

                    if (allEnded)
                    {
                        m.IsActive = false;
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                _context.SaveChanges();
            }

            // Phim đang chiếu
            var movies = _context.Movies
                .Where(m => m.IsActive == true &&
                            m.ReleaseDate.HasValue &&
                            m.ReleaseDate <= today &&
                            (!m.EndDate.HasValue || m.EndDate >= today))
                .OrderByDescending(m => m.ReleaseDate)
                .ToList();

            // Phim sắp chiếu
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

            // 📌 THỐNG KÊ PHIM PHỔ BIẾN (Dựa trên tổng lượt click xem của tất cả khách hàng)
            var topMovieIds = _context.UserMovieViews
                .Where(v => v.Movie.IsActive == true 
                         && v.Movie.ReleaseDate.HasValue 
                         && v.Movie.ReleaseDate <= today)
                .GroupBy(v => v.MovieId)
                .Select(g => new {
                    MovieId = g.Key,
                    TotalViews = g.Sum(v => v.ViewCount)
                })
                .OrderByDescending(x => x.TotalViews)
                .Take(4)
                .ToList();

            var movieIds = topMovieIds.Select(x => x.MovieId).ToList();

            var popularMovies = _context.Movies
                .Where(m => m.IsActive == true && movieIds.Contains(m.MovieId))
                .ToList()
                .OrderBy(m => movieIds.IndexOf(m.MovieId))
                .ToList();

            if (!popularMovies.Any())
            {
                popularMovies = _context.Movies
                    .Where(m => m.IsActive == true 
                             && m.ReleaseDate.HasValue 
                             && m.ReleaseDate <= today)
                    .Take(4)
                    .ToList();
            }

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
        public IActionResult CheckVoucher(string code, decimal total)
        {
            if (string.IsNullOrEmpty(code))
                return Json(new { success = false, message = "Chưa nhập mã" });

            var voucher = _context.Vouchers
                .FirstOrDefault(v => v.Code != null &&
                                     v.Code.ToLower() == code.ToLower() &&
                                     v.IsActive);

            if (voucher == null)
                return Json(new { success = false, message = "Không tồn tại" });

            if (voucher.StartDate != null && voucher.StartDate > DateTime.Now)
                return Json(new { success = false, message = "Chưa đến thời gian" });

            if (voucher.EndDate != null && voucher.EndDate < DateTime.Now)
                return Json(new { success = false, message = "Hết hạn" });

            if (voucher.UsedCount >= voucher.Quantity)
                return Json(new { success = false, message = "Hết lượt" });

            if (total < voucher.MinOrderValue)
                return Json(new { success = false, message = "Chưa đủ điều kiện" });

            decimal discount = 0;

            if (voucher.DiscountPercent.HasValue)
                discount = total * (decimal)voucher.DiscountPercent.Value;

            if (voucher.DiscountAmount.HasValue)
                discount = voucher.DiscountAmount.Value;

            return Json(new
            {
                success = true,
                discount = discount
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

            // 1. Gợi ý phim từ RecommendationEngine (sử dụng Apriori + Cá nhân hóa phim mới + Gemini AI)
            var recommendedMovies = await _recommendationEngine.GetRecommendedMovies(customerId);

            // Lấy danh sách phim Khách hàng đã xem/đã mua để loại trừ (nếu đã đăng nhập)
            var viewedMovieIds = customerId.HasValue 
                ? _context.UserMovieViews
                    .Where(v => v.CustomerId == customerId.Value)
                    .Select(v => v.MovieId)
                    .ToList()
                : new List<int>();

            // Fallback: nếu danh sách gợi ý < 4 phim, bổ sung thêm các phim mới nhất đang chiếu
            if (recommendedMovies.Count < 4)
            {
                var currentRecommendedIds = recommendedMovies.Select(r => r.MovieId).ToList();
                var fallbackMovies = _context.Movies
                    .Where(m => m.IsActive == true
                             && !viewedMovieIds.Contains(m.MovieId)
                             && !currentRecommendedIds.Contains(m.MovieId)
                             && m.ReleaseDate.HasValue
                             && m.ReleaseDate <= today)
                    .OrderByDescending(m => m.ReleaseDate)
                    .Take(8 - recommendedMovies.Count)
                    .ToList();

                recommendedMovies.AddRange(fallbackMovies);
            }

            // =====================
            // Phim được xem nhiều
            // =====================

            var topMovieIds = _context.UserMovieViews
                .Where(v => v.Movie.IsActive == true 
                         && v.Movie.ReleaseDate.HasValue 
                         && v.Movie.ReleaseDate <= today)
                .GroupBy(v => v.MovieId)
                .Select(g => new
                {
                    MovieId = g.Key,
                    TotalViews = g.Sum(v => v.ViewCount)
                })
                .OrderByDescending(x => x.TotalViews)
                .Take(4)
                .ToList();

            var popularMovieIds = topMovieIds
                .Select(x => x.MovieId)
                .ToList();

            var popularMovies = _context.Movies
                .Where(m => m.IsActive == true && popularMovieIds.Contains(m.MovieId))
                .ToList()
                .OrderBy(m => popularMovieIds.IndexOf(m.MovieId))
                .ToList();

            // =====================
            // Gộp Popular + Recommend
            // =====================

            var finalMovies = popularMovies
                .Concat(recommendedMovies)
                .GroupBy(m => m.MovieId)
                .Select(g => g.First())
                .Take(12)
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

        [HttpPost]
        public IActionResult AddReview(int movieId, int rating, string comment, int? orderId)
        {
            var customerId = GetCurrentCustomerId();
            if (!customerId.HasValue)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập để đánh giá phim." });
            }

            if (rating < 1 || rating > 5)
            {
                return Json(new { success = false, message = "Đánh giá sao phải từ 1 đến 5." });
            }

            // MỞ ĐỂ TEST: Bất kỳ ai đăng nhập đều được bình luận
            var hasWatched = true;
            /*
            var now = DateTime.Now;
            var hasWatched = _context.Tickets
                .Include(t => t.Order)
                .Include(t => t.Showtime)
                .Any(t => 
                    t.Order != null && 
                    t.Order.CustomerId == customerId.Value &&
                    (t.Order.Status == "Paid" || t.Order.Status == "Completed" || t.Order.Status == "Đã thanh toán") &&
                    t.Showtime != null && 
                    t.Showtime.MovieId == movieId &&
                    t.Showtime.StartTime < now
                );
            */
            if (!hasWatched)
            {
                return Json(new { success = false, message = "Bạn cần xem phim (suất chiếu đã diễn ra) trước khi đánh giá." });
            }

            // Check if user already reviewed this movie
            var existingReview = _context.Reviews.FirstOrDefault(r => r.MovieId == movieId && r.CustomerId == customerId.Value);
            if (existingReview != null)
            {
                existingReview.Rating = rating;
                existingReview.Comment = comment;
                existingReview.CreatedAt = DateTime.Now;
                _context.SaveChanges();
                return Json(new { success = true, message = "Đã cập nhật đánh giá của bạn!" });
            }

            var review = new Review
            {
                MovieId = movieId,
                CustomerId = customerId.Value,
                OrderId = orderId,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.Now,
                IsHidden = false,
                LikesCount = 0
            };

            _context.Reviews.Add(review);
            _context.SaveChanges();

            return Json(new { success = true, message = "Cảm ơn bạn đã đánh giá phim!" });
        }

        [HttpPost]
        public IActionResult LikeReview(int reviewId)
        {
            var review = _context.Reviews.Find(reviewId);
            if (review == null) return Json(new { success = false, message = "Đánh giá không tồn tại." });

            review.LikesCount++;
            _context.SaveChanges();

            return Json(new { success = true, likesCount = review.LikesCount });
        }

        [HttpPost]
        public IActionResult ReportReview(int reviewId, string reason)
        {
            var review = _context.Reviews.Find(reviewId);
            if (review == null) return Json(new { success = false, message = "Đánh giá không tồn tại." });

            review.HasReport = true;
            review.ReportReason = string.IsNullOrEmpty(review.ReportReason) 
                ? reason 
                : review.ReportReason + "; " + reason;
            _context.SaveChanges();

            return Json(new { success = true, message = "Đã gửi báo cáo vi phạm thành công." });
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
                var voucher = _context.Vouchers.FirstOrDefault(v => v.Code.ToLower() == voucherCode.ToLower().Trim() && v.IsActive);
                if (voucher != null)
                {
                    if (voucher.StartDate.HasValue && DateTime.Now < voucher.StartDate.Value) { }
                    else if (voucher.EndDate.HasValue && DateTime.Now > voucher.EndDate.Value) { }
                    else if (voucher.UsedCount >= voucher.Quantity) { }
                    else if (baseTotal < voucher.MinOrderValue) { }
                    else
                    {
                        if (voucher.DiscountPercent.HasValue)
                            discount = baseTotal * (decimal)voucher.DiscountPercent.Value;
                        else if (voucher.DiscountAmount.HasValue)
                            discount = voucher.DiscountAmount.Value;
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