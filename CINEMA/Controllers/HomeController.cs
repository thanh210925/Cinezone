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

        public HomeController(ILogger<HomeController> logger, CinemaContext context, RecommendationEngine recommendationEngine)
        {
            _logger = logger;
            _context = context;
            _recommendationEngine = recommendationEngine;
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

            // 📌 LƯU TƯƠNG TÁC TÌM KIẾM VÀO SESSION ĐỂ GỢI Ý PHIM CÙNG THỂ LOẠI
            HttpContext.Session.SetString("LatestInteraction", "Search");
            HttpContext.Session.SetString("LatestSearchKeyword", keyword);

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
                .Where(v => v.Movie.IsActive == true)
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
                popularMovies = _context.Movies.Where(m => m.IsActive == true).Take(4).ToList();
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
            var selectedDate = date?.Date ?? DateTime.Today;
            var now = DateTime.Now; // Khai báo biến 'now' ở đây

            // 1. Lấy danh sách phim có lịch chiếu trong ngày được chọn
            var movies = _context.Movies
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

            string? interactionType = HttpContext.Session.GetString("LatestInteraction");
            string? searchKeyword = HttpContext.Session.GetString("LatestSearchKeyword");
            int? clickedMovieId = HttpContext.Session.GetInt32("LatestClickedMovieId");

            // Fallback to database logs if Session doesn't have them
            if (string.IsNullOrEmpty(interactionType))
            {
                var latestClick = _context.UserActivityLogs
                    .Where(l => (customerId != null && l.CustomerId == customerId) || l.SessionId == HttpContext.Session.Id)
                    .Where(l => l.MovieId != null && (l.ActivityType == "VIEW_MOVIE" || l.ActivityType == "BOOK_TICKET"))
                    .OrderByDescending(l => l.CreatedAt)
                    .FirstOrDefault();

                var latestSearch = customerId != null ? _context.UserSearchLogs
                    .Where(s => s.CustomerId == customerId)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefault() : null;

                if (latestClick != null && latestSearch != null)
                {
                    if (latestSearch.CreatedAt > latestClick.CreatedAt)
                    {
                        interactionType = "Search";
                        searchKeyword = latestSearch.Keyword;
                    }
                    else
                    {
                        interactionType = "Click";
                        clickedMovieId = latestClick.MovieId;
                    }
                }
                else if (latestClick != null)
                {
                    interactionType = "Click";
                    clickedMovieId = latestClick.MovieId;
                }
                else if (latestSearch != null)
                {
                    interactionType = "Search";
                    searchKeyword = latestSearch.Keyword;
                }
            }

            List<Movie> recommendedMovies = new List<Movie>();
            List<int> targetGenreIds = new List<int>();
            int? excludeMovieId = null;

            if (interactionType == "Click" && clickedMovieId.HasValue)
            {
                excludeMovieId = clickedMovieId.Value;
                var clickedMovie = _context.Movies
                    .Include(m => m.Genres)
                    .FirstOrDefault(m => m.MovieId == excludeMovieId && m.IsActive == true);

                if (clickedMovie != null && clickedMovie.Genres != null)
                {
                    targetGenreIds = clickedMovie.Genres.Select(g => g.GenreId).ToList();
                }
            }
            else if (interactionType == "Search" && !string.IsNullOrEmpty(searchKeyword))
            {
                var keywordNoSign = RemoveDiacritics(searchKeyword);
                var matchedMovies = _context.Movies
                    .Include(m => m.Genres)
                    .Where(m => m.IsActive == true)
                    .AsEnumerable()
                    .Where(m => RemoveDiacritics(m.Title ?? "").Contains(keywordNoSign))
                    .ToList();

                if (matchedMovies.Any())
                {
                    targetGenreIds = matchedMovies
                        .SelectMany(m => m.Genres)
                        .Select(g => g.GenreId)
                        .Distinct()
                        .ToList();
                }
            }

            // Lấy danh sách phim Khách hàng đã xem để loại trừ (chỉ áp dụng nếu đã đăng nhập)
            var viewedMovieIds = customerId.HasValue 
                ? _context.UserMovieViews
                    .Where(v => v.CustomerId == customerId.Value)
                    .Select(v => v.MovieId)
                    .ToList()
                : new List<int>();

            if (targetGenreIds.Any())
            {
                recommendedMovies = _context.Movies
                    .Include(m => m.Genres)
                    .Where(m => m.IsActive == true 
                             && m.MovieId != excludeMovieId
                             && m.Genres.Any(g => targetGenreIds.Contains(g.GenreId))
                             && !viewedMovieIds.Contains(m.MovieId))
                    .OrderByDescending(m => m.ReleaseDate)
                    .Take(8)
                    .ToList();
            }

            // Fallback: nếu danh sách gợi ý < 4 phim, bổ sung thêm các phim mới nhất đang chiếu
            if (recommendedMovies.Count < 4)
            {
                var currentRecommendedIds = recommendedMovies.Select(r => r.MovieId).ToList();
                var fallbackMovies = _context.Movies
                    .Where(m => m.IsActive == true
                             && m.MovieId != excludeMovieId
                             && !viewedMovieIds.Contains(m.MovieId)
                             && !currentRecommendedIds.Contains(m.MovieId))
                    .OrderByDescending(m => m.ReleaseDate)
                    .Take(8 - recommendedMovies.Count)
                    .ToList();

                recommendedMovies.AddRange(fallbackMovies);
            }

            // =====================
            // Phim được xem nhiều
            // =====================

            var topMovieIds = _context.UserMovieViews
                .Where(v => v.Movie.IsActive == true)
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