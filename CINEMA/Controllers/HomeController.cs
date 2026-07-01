using System.Diagnostics;
using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace CINEMA.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly CinemaContext _context;

        public HomeController(ILogger<HomeController> logger, CinemaContext context)
        {
            _logger = logger;
            _context = context;
        }

        // =====================================================
        // ====================== TRANG CHỦ ====================
        // =====================================================
        [HttpGet]
        public IActionResult Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return RedirectToAction("Index");

            var today = DateOnly.FromDateTime(DateTime.Today);

            var keywordNoSign = RemoveDiacritics(keyword);

            var movies = _context.Movies
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

            if (!movies.Any())
            {
                ViewBag.SuggestMovies = _context.Movies
                    .Where(m => m.IsActive == true)
                    .OrderByDescending(m => m.ReleaseDate)
                    .Take(4)
                    .ToList();
            }

            ViewBag.Keyword = keyword;

            return View(movies);
        }

        public IActionResult Index()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            // Phim đang chiếu
            var movies = _context.Movies
                .Where(m => m.IsActive == true)
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

            ViewBag.ComingSoon = comingSoon;
            ViewBag.Theaters = theaters;
            ViewBag.Popup = popup;

            return View(movies);
        }

        // =====================================================
        // ================== API QUICK BOOKING =================
        // =====================================================

        // Lấy phim theo rạp
        [HttpGet]
        public IActionResult GetMoviesByTheater(int theaterId)
        {
            var movies = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                .Where(s =>
                       s.Auditorium.TheaterId == theaterId &&
                       s.IsActive == true &&
                       s.Movie.IsActive == true &&
                       s.StartTime >= DateTime.Now)
                .Select(s => new
                {
                    s.Movie.MovieId,
                    s.Movie.Title
                })
                .Distinct()
                .OrderBy(m => m.Title)
                .ToList();

            return Json(movies);
        }

        // Lấy suất chiếu
        [HttpGet]
        public IActionResult GetShowtimes(int theaterId, int movieId)
        {
            var showtimes = _context.Showtimes
                .Include(s => s.Auditorium)
                 .Where(s =>
                       s.Auditorium.TheaterId == theaterId &&
                       s.MovieId == movieId &&
                       s.IsActive == true &&
                       s.StartTime >= DateTime.Now)
             .OrderBy(s => s.StartTime)
                .Select(s => new
                {
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
        public IActionResult BookTicket(int id, int? showtimeId)
        {
            var movie = _context.Movies
                .Include(m => m.Genres)
                .FirstOrDefault(m => m.MovieId == id && m.IsActive == true);

            if (movie == null)
                return NotFound("Không tìm thấy phim.");

            // 📌 GHI LOG XEM PHIM
            LogActivity("VIEW_MOVIE", movieId: id);
            TrackMovieView(id);

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


            var combos = _context.Combos
                .Where(c => c.IsActive == true)
                .ToList();

            ViewBag.Showtime = showtime;
            ViewBag.Showtimes = showtimes;
            ViewBag.Seats = seats;
            ViewBag.BookedSeats = bookedSeats;
            ViewBag.Combos = combos;

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
            var selectedDate = date?.Date ?? DateTime.Today;

            var movies = _context.Movies
                .Include(m => m.Genres)
                .Include(m => m.Showtimes)
                    .ThenInclude(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .Where(m =>
                       m.IsActive == true &&
                       m.Showtimes.Any(s =>
                           s.StartTime.HasValue &&
                           s.StartTime.Value.Date == selectedDate))
                .OrderBy(m => m.Title)
                .ToList();

            ViewBag.SelectedDate = selectedDate;

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
        public IActionResult Recommend()
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");

            if (customerId == null)
            {
                // Khách chưa đăng nhập -> gợi ý các phim mới nhất đang chiếu
                var guestRecommended = _context.Movies
                    .Where(m => m.IsActive == true)
                    .OrderByDescending(m => m.ReleaseDate)
                    .Take(8)
                    .Select(m => new { m.MovieId, m.Title, m.PosterUrl })
                    .ToList();
                return Json(guestRecommended);
            }

            // 1. Thống kê điểm số thể loại (Genre Score) từ lịch sử hoạt động
            // BOOK_TICKET = 3đ, VIEW_MOVIE = 1đ
            var logs = _context.UserActivityLogs
                .Where(l => l.CustomerId == customerId)
                .Include(l => l.Movie)
                    .ThenInclude(m => m.Genres)
                .AsEnumerable()
                .ToList();

            var genreScores = logs
                .SelectMany(l => {
                    var genresList = new List<int>();
                    if (l.GenreId.HasValue)
                    {
                        genresList.Add(l.GenreId.Value);
                    }
                    if (l.Movie != null && l.Movie.Genres != null)
                    {
                        genresList.AddRange(l.Movie.Genres.Select(g => g.GenreId));
                    }
                    int points = l.ActivityType == "BOOK_TICKET" ? 3 : 1;
                    return genresList.Distinct().Select(gid => new { GenreId = gid, Points = points });
                })
                .GroupBy(x => x.GenreId)
                .Select(g => new {
                    GenreId = g.Key,
                    Score = g.Sum(x => x.Points)
                })
                .OrderByDescending(x => x.Score)
                .Take(3)
                .ToList();

            var topGenreIds = genreScores.Select(x => x.GenreId).ToList();

            // 2. Lấy danh sách phim Khách hàng đã xem để loại trừ
            var viewedMovieIds = _context.UserMovieViews
                .Where(v => v.CustomerId == customerId)
                .Select(v => v.MovieId)
                .ToList();

            // 3. Tìm phim cùng thể loại ưa thích mà khách CHƯA XEM
            List<Movie> recommendedMovies = new List<Movie>();

            if (topGenreIds.Any())
            {
                recommendedMovies = _context.Movies
                    .Include(m => m.Genres)
                    .Where(m => m.IsActive == true
                             && m.Genres.Any(g => topGenreIds.Contains(g.GenreId))
                             && !viewedMovieIds.Contains(m.MovieId))
                    .OrderByDescending(m => m.ReleaseDate)
                    .Take(8)
                    .ToList();
            }

            // 4. Dự phòng (Fallback): Nếu không có thể loại ưa thích hoặc danh sách gợi ý < 4 phim,
            // bổ sung thêm các phim mới nhất đang chiếu mà khách chưa xem
            if (recommendedMovies.Count < 4)
            {
                var currentRecommendedIds = recommendedMovies.Select(r => r.MovieId).ToList();
                var fallbackMovies = _context.Movies
                    .Where(m => m.IsActive == true
                             && !viewedMovieIds.Contains(m.MovieId)
                             && !currentRecommendedIds.Contains(m.MovieId))
                    .OrderByDescending(m => m.ReleaseDate)
                    .Take(8 - recommendedMovies.Count)
                    .ToList();

                recommendedMovies.AddRange(fallbackMovies);
            }

            var result = recommendedMovies
                .Select(m => new { m.MovieId, m.Title, m.PosterUrl })
                .ToList();

            return Json(result);
        }
    }
}