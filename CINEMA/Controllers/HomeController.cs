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

        public HomeController(ILogger<HomeController> logger, CinemaContext context, RecommendationEngine recommendationEngine, Services.GeminiService geminiService)
        {
            _logger = logger;
            _context = context;
            _recommendationEngine = recommendationEngine;
            _geminiService = geminiService;
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
                    m.ReleaseDate.HasValue &&
                    m.ReleaseDate.Value <= today &&
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
                // Lấy toàn bộ danh sách phim đang hoạt động để làm ngữ cảnh cho AI
                var activeMovies = _context.Movies
                    .Include(m => m.Genres)
                    .Where(m => m.IsActive == true && m.ReleaseDate.HasValue && m.ReleaseDate <= today)
                    .ToList();

                var moviesStr = string.Join("\n", activeMovies.Select(m => $"{m.MovieId} - {m.Title} ({string.Join(", ", m.Genres.Select(g => g.Name))})"));

                string prompt = $@"
Khách hàng đang tìm kiếm phim bằng từ khóa: '{keyword}' nhưng hệ thống rạp phim của chúng tôi không có kết quả chính xác nào.
Dưới đây là danh sách các phim hiện đang hoạt động tại rạp của chúng tôi:
{moviesStr}

Hãy phân tích từ khóa tìm kiếm '{keyword}' (về mặt ngữ nghĩa, thể loại, hoặc các phim tương tự) và chọn ra tối đa 4 bộ phim phù hợp nhất từ danh sách trên để gợi ý cho khách hàng.
Trả về kết quả duy nhất dưới dạng một mảng JSON chứa các MovieId được chọn, ví dụ: [3037, 3039]
Chỉ trả về JSON, không giải thích gì thêm.";

                var fallbackList = new List<Movie>();
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
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi gọi Gemini AI trong Search: " + ex.Message);
                }

                // Nếu gọi AI lỗi hoặc không có gợi ý phù hợp nào, mặc định gợi ý các phim mới nhất
                if (!fallbackList.Any())
                {
                    fallbackList = activeMovies
                        .OrderByDescending(m => m.ReleaseDate)
                        .Take(4)
                        .ToList();
                }

                ViewBag.SuggestMovies = fallbackList;
            }

            ViewBag.Keyword = keyword;

            return View(movies);
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
        // ======================= ĐẶT VÉ =======================
        // =====================================================

        [HttpGet]
        public IActionResult BookTicket(int id, int? showtimeId, bool isRecommend = false)
        {
            var movie = _context.Movies
                .Include(m => m.Genres)
                .FirstOrDefault(m => m.MovieId == id && m.IsActive == true);

            if (movie == null)
                return NotFound("Không tìm thấy phim.");

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

            var now = DateTime.Now;
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


            var customerId = GetCurrentCustomerId();
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

            decimal ticketTotal = seatList.Length * (showtime.BasePrice ?? 0);
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
                    _context.Tickets.Add(new Ticket
                    {
                        ShowtimeId = showtimeId,
                        SeatId = seatObj.SeatId,
                        OrderId = order.OrderId,
                        Price = showtime.BasePrice
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
    }
}