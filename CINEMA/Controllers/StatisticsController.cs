using CINEMA.Models;
using CINEMA.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CINEMA.ViewModels.RevenueDashboardViewModel;
using CINEMA.Services;

namespace CINEMA.Controllers
{
    public class StatisticsController : AdminBaseController
    {
        private readonly CinemaContext _context;
        private readonly RecommendationEngine _recommendationEngine;
        private readonly CustomerClusteringService _clusteringService;

        public StatisticsController(CinemaContext context, RecommendationEngine recommendationEngine, CustomerClusteringService clusteringService)
        {
            _context = context;
            _recommendationEngine = recommendationEngine;
            _clusteringService = clusteringService;
        }

        // ============================
        //  HIỂN THỊ TRANG THỐNG KÊ
        // ============================
        public async Task<IActionResult> Index(DateTime? from, DateTime? to, int? theaterId, int? movieId)
        {
            ViewBag.Theaters = await _context.Theaters.Where(t => t.IsActive == true).ToListAsync();
            ViewBag.Movies = await _context.Movies.Where(m => m.IsActive == true).ToListAsync();
            ViewBag.SelectedTheaterId = theaterId;
            ViewBag.SelectedMovieId = movieId;

            var model = await BuildDashboard(from, to, theaterId, movieId);
            return View(model);
        }

        // ============================
        //  HÀM XỬ LÝ THỐNG KÊ GỘP CHUẨN (GIT + LOCAL)
        // ============================
     
        public async Task<RevenueDashboardViewModel> BuildDashboard(DateTime? from, DateTime? to, int? theaterId = null, int? movieId = null)
        {
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var lastMonthStart = startOfMonth.AddMonths(-1);

            // MẶC ĐỊNH: Reset doanh thu theo tháng hiện tại nếu không bấm lọc
            if (!from.HasValue && !to.HasValue)
            {
                from = startOfMonth;
                to = today;
            }

            var model = new RevenueDashboardViewModel
            {
                FromDate = from,
                ToDate = to,
                SelectedTheaterId = theaterId
            };

            // =====================================================
            // 1. LẤY ĐƠN HÀNG ĐÃ THANH TOÁN (Lúc này query luôn có khoảng thời gian giới hạn)
            // =====================================================
            var paidOrdersQuery = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Auditorium)
                            .ThenInclude(a => a.Theater)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .Where(o => o.Status != null &&
                            o.Status.ToLower().Contains("thanh toán") &&
                            o.CreatedAt != null);

            if (from.HasValue)
                paidOrdersQuery = paidOrdersQuery.Where(o => o.CreatedAt >= from.Value.Date);

            if (to.HasValue)
            {
                var toEnd = to.Value.Date.AddDays(1).AddTicks(-1);
                paidOrdersQuery = paidOrdersQuery.Where(o => o.CreatedAt <= toEnd);
            }

            if (theaterId.HasValue && theaterId.Value > 0)
            {
                paidOrdersQuery = paidOrdersQuery.Where(o => o.Tickets.Any(t => t.Showtime.Auditorium.TheaterId == theaterId.Value));
            }

            if (movieId.HasValue && movieId.Value > 0)
            {
                paidOrdersQuery = paidOrdersQuery.Where(o => o.Tickets.Any(t => t.Showtime.MovieId == movieId.Value));
            }

            var paidOrders = await paidOrdersQuery.ToListAsync();


            // =====================================================
            // 2. TỔNG DOANH THU & KPI SỐ LƯỢNG VÉ
            // =====================================================
            model.TotalRevenue = paidOrders.Sum(o => (decimal?)o.TotalAmount) ?? 0;
            model.TotalTickets = paidOrders.SelectMany(o => o.Tickets).Count();

            var totalOrdersCount = paidOrders.Count;
            model.AvgOrderValue = totalOrdersCount > 0 ? (model.TotalRevenue / totalOrdersCount) : 0;

            model.RevenueCurrentMonth = paidOrders.Where(o => o.CreatedAt >= startOfMonth).Sum(o => (decimal?)o.TotalAmount) ?? 0;
            model.RevenueLastMonth = paidOrders.Where(o => o.CreatedAt >= lastMonthStart && o.CreatedAt < startOfMonth).Sum(o => (decimal?)o.TotalAmount) ?? 0;

            // =====================================================
            // 3. THỐNG KÊ HÔM NAY, THÁNG NÀY & TRUNG BÌNH ĐƠN CHI TIẾT
            // =====================================================
            var todayOrders = paidOrders.Where(o => o.CreatedAt!.Value.Date == today).ToList();
            model.TodayRevenue = todayOrders.Sum(o => (decimal?)o.TotalAmount) ?? 0;
            model.TodayTickets = todayOrders.SelectMany(o => o.Tickets).Count();

            var monthOrders = paidOrders.Where(o => o.CreatedAt!.Value >= startOfMonth).ToList();
            model.MonthRevenue = monthOrders.Sum(o => (decimal?)o.TotalAmount) ?? 0;
            model.MonthTickets = monthOrders.SelectMany(o => o.Tickets).Count();

            // KPI Trung bình đơn (Local)
            model.AvgOrderValueToday = todayOrders.Any() ? todayOrders.Average(o => (decimal?)o.TotalAmount ?? 0) : 0;
            model.AvgOrderValueMonth = monthOrders.Any() ? monthOrders.Average(o => (decimal?)o.TotalAmount ?? 0) : 0;

            int currentQuarter = (today.Month - 1) / 3 + 1;
            var quarterOrders = paidOrders.Where(o => o.CreatedAt?.Year == today.Year && ((o.CreatedAt!.Value.Month - 1) / 3 + 1) == currentQuarter).ToList();
            model.AvgOrderValueQuarter = quarterOrders.Any() ? quarterOrders.Average(o => (decimal?)o.TotalAmount ?? 0) : 0;

            var yearOrders = paidOrders.Where(o => o.CreatedAt?.Year == today.Year).ToList();
            model.AvgOrderValueYear = yearOrders.Any() ? yearOrders.Average(o => (decimal?)o.TotalAmount ?? 0) : 0;

            // =====================================================
            // 4. SỐ LƯỢNG ĐƠN HÀNG TOÀN BỘ TRẠNG THÁI
            // =====================================================
            var allOrdersQuery = _context.Orders.AsQueryable();
            if (from.HasValue) allOrdersQuery = allOrdersQuery.Where(o => o.CreatedAt >= from.Value.Date);
            if (to.HasValue)
            {
                var toEnd = to.Value.Date.AddDays(1).AddTicks(-1);
                allOrdersQuery = allOrdersQuery.Where(o => o.CreatedAt <= toEnd);
            }
            if (theaterId.HasValue && theaterId.Value > 0)
            {
                allOrdersQuery = allOrdersQuery.Where(o => o.Tickets.Any(t => t.Showtime.Auditorium.TheaterId == theaterId.Value));
            }
            if (movieId.HasValue && movieId.Value > 0)
            {
                allOrdersQuery = allOrdersQuery.Where(o => o.Tickets.Any(t => t.Showtime.MovieId == movieId.Value));
            }

            model.PaidOrders = await allOrdersQuery.CountAsync(o => o.Status != null && o.Status.ToLower().Contains("thanh toán"));
            model.PendingOrders = await allOrdersQuery.CountAsync(o => o.Status != null && o.Status.ToLower().Contains("chờ"));
            model.CancelledOrders = await allOrdersQuery.CountAsync(o => o.Status != null && (o.Status.ToLower().Contains("hủy") || o.Status.ToLower().Contains("thất bại")));

            // =====================================================
            // 5. THỐNG KÊ PHIM & DOANH THU PHIM CHUYÊN SÂU
            // =====================================================
            var movieTicketStats = paidOrders
                .SelectMany(o => o.Tickets)
                .Where(t => t.Showtime != null && t.Showtime.Movie != null)
                .GroupBy(t => t.Showtime.Movie.Title)
                .Select(g => new {
                    MovieTitle = g.Key,
                    TicketCount = g.Count(),
                    Revenue = g.Sum(x => x.Price) ?? 0
                })
                .ToList();

            if (movieTicketStats.Any())
            {
                // Tìm kiếm các danh hiệu Phim từ dữ liệu Local
                var topSell = movieTicketStats.OrderByDescending(x => x.TicketCount).First();
                var leastSell = movieTicketStats.OrderBy(x => x.TicketCount).First();
                var topRev = movieTicketStats.OrderByDescending(x => x.Revenue).First();

                model.TopSellingMovie = topSell.MovieTitle ?? "";
                model.TopSellingTickets = topSell.TicketCount;
                model.LeastSellingMovie = leastSell.MovieTitle ?? "";
                model.LeastSellingTickets = leastSell.TicketCount;
                model.TopRevenueMovie = topRev.MovieTitle ?? "";
                model.TopRevenueAmount = topRev.Revenue;

                // Nạp biểu đồ Top 5 Phim theo doanh thu từ Git
                var top5Movies = movieTicketStats.OrderByDescending(x => x.Revenue).Take(5).ToList();
                foreach (var mv in top5Movies)
                {
                    model.MovieLabels.Add(mv.MovieTitle ?? "");
                    model.RevenueByMovie.Add(mv.Revenue);
                    model.TicketsByMovie.Add(mv.TicketCount);
                }
            }

            // =====================================================
            // 6. THỐNG KÊ COMBO & KPI ĐÍNH KÈM NÂNG CAO
            // =====================================================
            var comboStats = paidOrders
                .SelectMany(o => o.OrderCombos)
                .Where(oc => oc.Combo != null)
                .GroupBy(oc => new { oc.Combo.ComboId, oc.Combo.Name })
                .Select(g => new {
                    ComboName = g.Key.Name,
                    QuantitySold = g.Sum(x => x.Quantity ?? 0),
                    Revenue = g.Sum(x => (x.UnitPrice ?? 0) * (x.Quantity ?? 0))
                })
                .OrderByDescending(x => x.QuantitySold)
                .ToList();

            var totalComboSold = comboStats.Sum(x => x.QuantitySold);
            model.ComboRevenue = comboStats.Sum(x => x.Revenue);
            model.ComboSold = totalComboSold;

            // Thiết lập Combo bán chạy nhất và bán chạy nhất số lượng
            var bestCombo = comboStats.FirstOrDefault();
            if (bestCombo != null)
            {
                model.BestSellingCombo = bestCombo.ComboName;
                model.BestSellingQuantity = bestCombo.QuantitySold;
            }

            // Thiết lập Combo bán ế nhất từ Git gốc
            var worstCombo = comboStats.OrderBy(x => x.QuantitySold).FirstOrDefault();
            if (worstCombo != null)
            {
                model.WorstSellingCombo = worstCombo.ComboName;
                model.WorstSellingQuantity = worstCombo.QuantitySold;
            }

            // Đổ dữ liệu ra cấu trúc bảng và cấu trúc hình tròn (Pie Chart)
            foreach (var item in comboStats)
            {
                model.ComboStatistics.Add(new ComboStatisticViewModel
                {
                    ComboName = item.ComboName ?? "",
                    QuantitySold = item.QuantitySold,
                    Revenue = item.Revenue,
                    Percentage = totalComboSold == 0 ? 0 : Math.Round(item.QuantitySold * 100.0 / totalComboSold, 2)
                });

                model.ComboPieLabels.Add(item.ComboName ?? "");
                model.ComboPieValues.Add(item.QuantitySold);
            }

            // Thống kê bộ Chỉ số KPI Combo Nâng Cao
            var ordersWithComboCount = paidOrders.Count(o => o.OrderCombos.Any());

            model.ComboAttachRate = totalOrdersCount == 0
                ? 0
                : Math.Round(ordersWithComboCount * 100.0 / totalOrdersCount, 2);

            model.ComboRevenueRate = model.TotalRevenue == 0
                ? 0
                : Math.Round((double)(model.ComboRevenue / model.TotalRevenue * 100), 2);

            model.AverageComboPerOrder = ordersWithComboCount == 0
                ? 0
                : Math.Round(model.ComboRevenue / ordersWithComboCount, 0);

            // Bổ sung dữ liệu Top 5 Combo bán chạy & Doanh thu cao cho Chart
            foreach (var item in comboStats.Take(5))
            {
                model.TopComboLabels.Add(item.ComboName ?? "");
                model.TopComboValues.Add(item.QuantitySold);
            }

            var topRevenueCombos = comboStats.OrderByDescending(x => x.Revenue).Take(5).ToList();
            foreach (var item in topRevenueCombos)
            {
                model.TopRevenueComboLabels.Add(item.ComboName ?? "");
                model.TopRevenueComboValues.Add(item.Revenue);
            }

            // =====================================================
            // 7. BIỂU ĐỒ THEO TIẾN TRÌNH THỜI GIAN (Ngày, Tháng, Năm)
            // =====================================================
            // -- Thống kê tiến độ theo từng ngày --
            var dailyStats = paidOrders
                .GroupBy(x => x.CreatedAt!.Value.Date)
                .Select(g => new {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.TotalAmount ?? 0),
                    Tickets = g.Sum(x => x.Tickets.Count),
                    OrderCount = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            foreach (var d in dailyStats)
            {
                model.LabelsByDate.Add(d.Date.ToString("dd/MM"));
                model.RevenueByDate.Add(d.Revenue);
                model.TicketCountByDate.Add(d.Tickets);
                model.OrderCountByDate.Add(d.OrderCount);
            }

            // -- Thống kê tiến độ theo từng tháng --
            var monthlyStats = paidOrders
                .GroupBy(x => new { x.CreatedAt!.Value.Year, x.CreatedAt.Value.Month })
                .Select(g => new {
                    g.Key.Year,
                    g.Key.Month,
                    Revenue = g.Sum(x => x.TotalAmount ?? 0),
                    Tickets = g.Sum(x => x.Tickets.Count)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

            foreach (var m in monthlyStats)
            {
                model.LabelsByMonth.Add($"{m.Month}/{m.Year}");
                model.RevenueByMonth.Add(m.Revenue);
                model.TicketCountByMonth.Add(m.Tickets);
            }

            // -- Thống kê tiến độ theo từng năm (Lấy Top 5 năm) --
            var yearlyStats = paidOrders
                .GroupBy(x => x.CreatedAt!.Value.Year)
                .Select(g => new {
                    Year = g.Key,
                    Revenue = g.Sum(x => x.TotalAmount ?? 0),
                    Tickets = g.Sum(x => x.Tickets.Count)
                })
                .OrderByDescending(x => x.Year)
                .Take(5)
                .ToList();

            yearlyStats.Reverse();
            foreach (var y in yearlyStats)
            {
                model.LabelsByYear.Add(y.Year.ToString());
                model.RevenueByYear.Add(y.Revenue);
                model.TicketCountByYear.Add(y.Tickets);
            }

            // -- Doanh số Combo phát sinh theo từng tháng --
            var comboByMonth = paidOrders
                .SelectMany(o => o.OrderCombos)
                .Where(oc => oc.Order != null && oc.Order.CreatedAt != null)
                .GroupBy(oc => new { oc.Order.CreatedAt!.Value.Year, oc.Order.CreatedAt.Value.Month })
                .Select(g => new {
                    g.Key.Year,
                    g.Key.Month,
                    Revenue = g.Sum(x => (x.UnitPrice ?? 0) * (x.Quantity ?? 0)),
                    Quantity = g.Sum(x => x.Quantity ?? 0)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

            foreach (var item in comboByMonth)
            {
                model.ComboLabelsByMonth.Add($"{item.Month}/{item.Year}");
                model.ComboRevenueByMonth.Add(item.Revenue);
                model.ComboQuantityByMonth.Add(item.Quantity);
            }

            // =====================================================
            // 8. DOANH THU THEO TỪNG SUẤT CHIẾU CỤ THỂ
            // =====================================================
            var showtimeStats = paidOrders
                .SelectMany(o => o.Tickets)
                .Where(t => t.Showtime != null && t.Showtime.Movie != null && t.Showtime.Auditorium != null)
                .GroupBy(t => new {
                    t.ShowtimeId,
                    MovieName = t.Showtime.Movie.Title,
                    AuditoriumName = t.Showtime.Auditorium.Name,
                    StartTime = t.Showtime.StartTime
                })
                .Select(g => new ShowtimeStatisticViewModel
                {
                    ShowtimeId = g.Key.ShowtimeId ?? 0,
                    MovieName = g.Key.MovieName ?? "",
                    AuditoriumName = g.Key.AuditoriumName ?? "",
                    StartTime = g.Key.StartTime ?? DateTime.MinValue,
                    TicketCount = g.Count(),
                    Revenue = g.Sum(x => x.Price ?? 0)
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            model.ShowtimeStatistics = showtimeStats;

            foreach (var item in showtimeStats)
            {
                model.ShowtimeLabels.Add($"{item.MovieName} ({item.StartTime:dd/MM HH:mm})");
                model.RevenueByShowtime.Add(item.Revenue);
            }

            // =====================================================
            // 9. THỐNG KÊ THỂ LOẠI & TÌNH TRẠNG PHIM HIỆN TẠI
            // =====================================================
            var todayDateOnly = DateOnly.FromDateTime(today);
            model.NowShowingCount = await _context.Movies.CountAsync(m => m.IsActive == true && m.ReleaseDate <= todayDateOnly);
            model.ComingSoonCount = await _context.Movies.CountAsync(m => m.IsActive == true && m.ReleaseDate > todayDateOnly);

            var totalActiveMovies = await _context.Movies.CountAsync(m => m.IsActive == true);
            if (totalActiveMovies > 0)
            {
                var genreStats = await _context.Genres
                    .Select(g => new {
                        g.Name,
                        MovieCount = _context.Movies.Count(m => m.IsActive == true && m.Genres.Any(genre => genre.GenreId == g.GenreId))
                    })
                    .Where(x => x.MovieCount > 0)
                    .OrderByDescending(x => x.MovieCount)
                    .ToListAsync();

                foreach (var item in genreStats)
                {
                    model.GenreStatistics.Add(new GenreStatisticViewModel
                    {
                        GenreName = item.Name ?? "",
                        MovieCount = item.MovieCount,
                        Percentage = Math.Round(item.MovieCount * 100.0 / totalActiveMovies, 2)
                    });
                }
            }
            // =====================================================
            // 🎟 THỐNG KÊ VOUCHER
            // =====================================================

            model.TotalVouchers =
                await _context.Vouchers.CountAsync();

            model.ActiveVouchers =
                await _context.Vouchers.CountAsync(v =>
                    v.IsActive &&
                    (v.EndDate == null || v.EndDate >= DateTime.Now));

            model.ExpiredVouchers =
                await _context.Vouchers.CountAsync(v =>
                    v.EndDate != null &&
                    v.EndDate < DateTime.Now);

            model.UsedVouchers =
                await _context.Vouchers.SumAsync(v =>
                    v.UsedCount);

            model.TotalDiscountAmount =
                await _context.Orders
                    .Where(o => o.DiscountAmount != null)
                    .SumAsync(o => o.DiscountAmount ?? 0);

            var totalOrders =
                await _context.Orders.CountAsync();

            var voucherOrders =
                await _context.Orders.CountAsync(o =>
                    !string.IsNullOrEmpty(o.VoucherCode));

            model.VoucherUsageRate =
                totalOrders == 0
                ? 0
                : Math.Round(
                    voucherOrders * 100.0 / totalOrders,
                    2);

            // Top voucher được dùng nhiều nhất
            var topVoucherStats =
                await _context.Orders
                    .Where(o => !string.IsNullOrEmpty(o.VoucherCode))
                    .GroupBy(o => o.VoucherCode)
                    .Select(g => new
                    {
                        VoucherCode = g.Key,
                        UsedCount = g.Count(),
                        TotalDiscount =
                            g.Sum(x => x.DiscountAmount ?? 0)
                    })
                    .OrderByDescending(x => x.UsedCount)
                    .Take(5)
                    .ToListAsync();

            foreach (var item in topVoucherStats)
            {
                model.VoucherLabels.Add(
                    item.VoucherCode ?? "");

                model.VoucherValues.Add(
                    item.UsedCount);

                model.VoucherStatistics.Add(
                    new VoucherStatisticViewModel
                    {
                        VoucherCode =
                            item.VoucherCode ?? "",

                        UsedCount =
                            item.UsedCount,

                        TotalDiscount =
                            item.TotalDiscount,

                        UsageRate =
                            voucherOrders == 0
                            ? 0
                            : Math.Round(
                                item.UsedCount * 100.0 /
                                voucherOrders,
                                2)
                    });
            }

            // =====================================================
            // 🪙 THÀNH PHẦN THỐNG KÊ CHI TIẾT NÂNG CAO
            // =====================================================
            model.TicketRevenue = paidOrders.SelectMany(o => o.Tickets).Sum(t => t.Price ?? 0);
            model.GrossComboRevenue = paidOrders.SelectMany(o => o.OrderCombos).Sum(oc => (oc.UnitPrice ?? 0) * (oc.Quantity ?? 0));
            model.TotalDiscount = paidOrders.Sum(o => o.DiscountAmount ?? 0);
            model.NetRevenue = model.TotalRevenue;

            // -- Doanh thu theo Rạp & Tỷ lệ lấp đầy --
            var allTheaters = await _context.Theaters.ToListAsync();
            foreach (var th in allTheaters)
            {
                var thTickets = paidOrders
                    .SelectMany(o => o.Tickets)
                    .Where(t => t.Showtime?.Auditorium?.TheaterId == th.TheaterId)
                    .ToList();

                var thOrders = paidOrders
                    .Where(o => o.Tickets.Any(t => t.Showtime?.Auditorium?.TheaterId == th.TheaterId))
                    .ToList();

                var ticketRev = thTickets.Sum(t => t.Price ?? 0);
                var comboRev = thOrders.SelectMany(o => o.OrderCombos).Sum(oc => (oc.UnitPrice ?? 0) * (oc.Quantity ?? 0));
                var totalRev = thOrders.Sum(o => o.TotalAmount ?? 0);

                // Tính tỷ lệ lấp đầy ghế theo rạp
                var showtimeIds = thTickets.Select(t => t.ShowtimeId).Distinct().ToList();
                int totalSeatsAvailable = 0;
                foreach (var sId in showtimeIds)
                {
                    var showtimeObj = _context.Showtimes.FirstOrDefault(s => s.ShowtimeId == sId);
                    if (showtimeObj?.AuditoriumId != null)
                    {
                        var seatCount = await _context.Seats.CountAsync(s => s.AuditoriumId == showtimeObj.AuditoriumId);
                        totalSeatsAvailable += seatCount;
                    }
                }
                double occRate = totalSeatsAvailable > 0 ? Math.Round((double)thTickets.Count / totalSeatsAvailable * 100, 2) : 0;

                model.TheaterRevenues.Add(new TheaterRevenueViewModel
                {
                    TheaterName = th.Name,
                    TicketsSold = thTickets.Count,
                    TicketRevenue = ticketRev,
                    ComboRevenue = comboRev,
                    TotalRevenue = totalRev,
                    OccupancyRate = occRate
                });
            }

            // -- Khung giờ & Ngày vàng --
            var dayOfWeekNames = new Dictionary<DayOfWeek, string>
            {
                { DayOfWeek.Monday, "Thứ Hai" },
                { DayOfWeek.Tuesday, "Thứ Ba" },
                { DayOfWeek.Wednesday, "Thứ Tư" },
                { DayOfWeek.Thursday, "Thứ Năm" },
                { DayOfWeek.Friday, "Thứ Sáu" },
                { DayOfWeek.Saturday, "Thứ Bảy" },
                { DayOfWeek.Sunday, "Chủ Nhật" }
            };

            var peakDaysMap = dayOfWeekNames.ToDictionary(kvp => kvp.Value, kvp => new PeakDayViewModel { DayOfWeek = kvp.Value });
            var dbPeakDays = paidOrders
                .SelectMany(o => o.Tickets)
                .Where(t => t.Order?.CreatedAt != null)
                .GroupBy(t => t.Order.CreatedAt.Value.DayOfWeek)
                .Select(g => new {
                    Day = g.Key,
                    Count = g.Count(),
                    Rev = g.Sum(t => t.Price ?? 0)
                }).ToList();

            foreach (var item in dbPeakDays)
            {
                var dayName = dayOfWeekNames[item.Day];
                if (peakDaysMap.ContainsKey(dayName))
                {
                    peakDaysMap[dayName].TicketCount = item.Count;
                    peakDaysMap[dayName].Revenue = item.Rev;
                }
            }
            model.PeakDays = peakDaysMap.Values.ToList();

            var hourSlots = new List<string> { "Sáng (08:00 - 12:00)", "Chiều (12:00 - 17:00)", "Tối (17:00 - 22:00)", "Đêm (22:00 - 07:59)" };
            var peakHoursMap = hourSlots.ToDictionary(slot => slot, slot => new PeakHourViewModel { TimeSlot = slot });

            var dbPeakHours = paidOrders
                .SelectMany(o => o.Tickets)
                .Where(t => t.Showtime != null && t.Showtime.StartTime != null)
                .GroupBy(t => {
                    int hour = t.Showtime.StartTime.Value.Hour;
                    if (hour >= 8 && hour < 12) return "Sáng (08:00 - 12:00)";
                    if (hour >= 12 && hour < 17) return "Chiều (12:00 - 17:00)";
                    if (hour >= 17 && hour < 22) return "Tối (17:00 - 22:00)";
                    return "Đêm (22:00 - 07:59)";
                })
                .Select(g => new {
                    Slot = g.Key,
                    Count = g.Count(),
                    Rev = g.Sum(t => t.Price ?? 0)
                }).ToList();

            foreach (var item in dbPeakHours)
            {
                if (peakHoursMap.ContainsKey(item.Slot))
                {
                    peakHoursMap[item.Slot].TicketCount = item.Count;
                    peakHoursMap[item.Slot].Revenue = item.Rev;
                }
            }
            model.PeakHours = peakHoursMap.Values.ToList();

            // -- Phân tích phương thức thanh toán --
            model.PaymentMethodStats = paidOrders
                .GroupBy(o => string.IsNullOrEmpty(o.PaymentMethod) ? "Khác" : o.PaymentMethod)
                .Select(g => new PaymentMethodStatViewModel
                {
                    PaymentMethod = g.Key,
                    OrderCount = g.Count(),
                    TotalRevenue = g.Sum(o => o.TotalAmount ?? 0)
                })
                .ToList();

            // -- Doanh thu phòng chiếu --
            model.RoomRevenues = paidOrders
                .SelectMany(o => o.Tickets)
                .Where(t => t.Showtime?.Auditorium != null)
                .GroupBy(t => new { t.Showtime.Auditorium.AuditoriumId, RoomName = t.Showtime.Auditorium.Name, TheaterName = t.Showtime.Auditorium.Theater.Name })
                .Select(g => new RoomRevenueViewModel
                {
                    RoomName = g.Key.RoomName,
                    TheaterName = g.Key.TheaterName,
                    TicketsSold = g.Count(),
                    Revenue = g.Sum(t => t.Price ?? 0)
                })
                .OrderByDescending(r => r.Revenue)
                .Take(10)
                .ToList();

            // -- Top khách hàng VIP --
            model.TopCustomers = paidOrders
                .Where(o => o.Customer != null)
                .GroupBy(o => o.Customer)
                .Select(g => new CustomerSpendViewModel
                {
                    FullName = g.Key.FullName,
                    Email = g.Key.Email,
                    Phone = g.Key.Phone ?? "Không có",
                    MembershipLevel = g.Key.MembershipLevel,
                    TicketsBooked = g.SelectMany(o => o.Tickets).Count(),
                    TotalSpent = g.Sum(o => o.TotalAmount ?? 0)
                })
                .OrderByDescending(c => c.TotalSpent)
                .Take(5)
                .ToList();

            return model;
        }

        // =====================================================
        // CÁC CHỨC NĂNG LỌC NÂNG CAO BIỂU ĐỒ & API JAVASCRIPT
        // =====================================================
        public async Task<IActionResult> AdvancedStats(int? tId, int? mId, int? tId2, DateTime? from, DateTime? to)
        {
            var model = await BuildDashboard(from, to);

            // --- LỌC BIỂU ĐỒ 1 (Rạp) ---
            var query1 = _context.Tickets.Where(t => t.Order.Status.Contains("thanh toán"));
            if (tId.HasValue)
            {
                query1 = query1.Where(t => t.Showtime.Auditorium.TheaterId == tId.Value);
            }

            var theaterStats = await query1.GroupBy(t => t.Showtime.Auditorium.Theater.Name)
                                           .Select(g => new { Name = g.Key, Count = g.Count() }).ToListAsync();
            model.CinemaLabels = theaterStats.Select(x => x.Name ?? "N/A").ToList();
            model.CinemaRevenue = theaterStats.Select(x => (decimal)x.Count).ToList();

            // --- LỌC BIỂU ĐỒ 2 (Suất chiếu nâng cao) ---
            var query2 = _context.Tickets.Where(t => t.Order.Status.Contains("thanh toán"));
            if (mId.HasValue) query2 = query2.Where(t => t.Showtime.MovieId == mId.Value);
            if (tId2.HasValue) query2 = query2.Where(t => t.Showtime.Auditorium.TheaterId == tId2.Value);

            var advShowtimeStats = await query2.OrderByDescending(t => t.Showtime.StartTime).Take(10)
                                             .GroupBy(t => new { t.Showtime.Movie.Title, t.Showtime.StartTime })
                                             .Select(g => new { Info = $"{g.Key.Title} ({g.Key.StartTime:HH:mm})", Count = g.Count() }).ToListAsync();

            model.ShowtimeLabels = advShowtimeStats.Select(x => x.Info).ToList();
            model.TicketsByShowtime = advShowtimeStats.Select(x => x.Count).ToList();

            ViewBag.Theaters = await _context.Theaters.ToListAsync();
            ViewBag.Movies = await _context.Movies.ToListAsync();

            var totalSeats = await _context.Seats.CountAsync();
            var soldSeats = await _context.Tickets.CountAsync(t => t.Order.Status.Contains("thanh toán"));
            model.GlobalOccupancyRate = totalSeats > 0 ? Math.Round((double)soldSeats / totalSeats * 100, 2) : 0;

            return View(model);
        }

        [HttpGet]
        public async Task<JsonResult> GetTheaterData(int? tId)
        {
            var query = _context.Tickets.Where(t => t.Order.Status.Contains("thanh toán"));
            if (tId.HasValue && tId > 0) query = query.Where(t => t.Showtime.Auditorium.TheaterId == tId.Value);

            var data = await query.GroupBy(t => t.Showtime.Auditorium.Theater.Name)
                                  .Select(g => new { Name = g.Key, Count = g.Count() }).ToListAsync();
            return Json(new { labels = data.Select(x => x.Name), values = data.Select(x => x.Count) });
        }

        [HttpGet]
        public async Task<JsonResult> GetMovieTicketData(int? mId)
        {
            var query = _context.Tickets.Where(t => t.Order.Status.Contains("thanh toán") && t.Showtime != null);

            if (mId.HasValue && mId > 0)
                query = query.Where(t => t.Showtime.MovieId == mId.Value);

            var data = await query.GroupBy(t => t.Showtime.Movie.Title)
                                  .Select(g => new { Name = g.Key, Count = g.Count() })
                                  .OrderByDescending(x => x.Count)
                                  .Take(10)
                                  .ToListAsync();

            return Json(new { labels = data.Select(x => x.Name), values = data.Select(x => x.Count) });
        }

        [HttpGet]
        public async Task<JsonResult> GetShowtimeTicketData(int? mId, int? tId2)
        {
            var showtimesQuery = _context.Showtimes
                .Include(s => s.Movie)
                .AsQueryable();

            if (mId.HasValue && mId > 0) showtimesQuery = showtimesQuery.Where(s => s.MovieId == mId.Value);
            if (tId2.HasValue && tId2 > 0) showtimesQuery = showtimesQuery.Where(s => s.Auditorium.TheaterId == tId2.Value);

            var showtimes = await showtimesQuery.OrderBy(s => s.StartTime).Take(10).ToListAsync();

            var tickets = await _context.Tickets
                .Where(t => t.Order.Status.Contains("thanh toán"))
                .GroupBy(t => t.ShowtimeId)
                .Select(g => new { ShowtimeId = g.Key, Count = g.Count() })
                .ToListAsync();

            var data = showtimes.Select(s => new {
                Title = s.Movie.Title,
                Time = s.StartTime?.ToString("HH:mm") ?? "--:--",
                RawTime = s.StartTime,
                Count = tickets.FirstOrDefault(t => t.ShowtimeId == s.ShowtimeId)?.Count ?? 0
            }).ToList();

            return Json(new
            {
                labels = data.Select(x => $"{x.Title} ({x.Time})").ToList(),
                values = data.Select(x => x.Count).ToList()
            });
        }

        [HttpGet]
        public IActionResult RecommendationRules()
        {
            var rules = _recommendationEngine.GetRules(forceRefresh: true);

            var genres = _context.Genres.ToDictionary(g => $"Genre_{g.GenreId}", g => g.Name);
            var movies = _context.Movies.ToDictionary(m => $"Movie_{m.MovieId}", m => m.Title);
            var combos = _context.Combos.ToDictionary(c => $"Combo_{c.ComboId}", c => c);

            ViewBag.Genres = genres;
            ViewBag.Movies = movies;
            ViewBag.Combos = combos;

            return View(rules);
        }

        [HttpGet]
        public IActionResult KnnRecommendation(int? customerId)
        {
            // Lấy danh sách khách hàng có tương tác (đã đánh giá hoặc đã xem phim)
            var activeCustomerIds = _context.Reviews.Select(r => r.CustomerId)
                .Union(_context.UserMovieViews.Select(v => v.CustomerId))
                .Distinct()
                .ToList();

            var customers = _context.Customers
                .Where(c => activeCustomerIds.Contains(c.CustomerId))
                .Select(c => new { c.CustomerId, c.FullName, c.Email })
                .ToList();

            ViewBag.Customers = customers;

            // Mặc định chọn khách hàng đầu tiên nếu chưa chọn
            if (!customerId.HasValue && customers.Any())
            {
                customerId = customers.First().CustomerId;
            }

            KnnRecommendationResult? knnResult = null;
            if (customerId.HasValue)
            {
                knnResult = _recommendationEngine.GetKnnPredictionsDetail(customerId.Value, k: 5);
            }

            return View(knnResult);
        }

        [HttpGet]
        public async Task<IActionResult> CustomerSegmentation(int k = 3)
        {
            if (k < 2) k = 2;
            if (k > 5) k = 5;

            var result = await _clusteringService.ClusterCustomersAsync(k);
            return View(result);
        }
    }
}
