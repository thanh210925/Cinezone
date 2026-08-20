using CINEMA.DTOs;
using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers.Api.CustomerApi
{
    [ApiController]
    [Route("api/showtimes")]
    public class ShowtimesApiController : ControllerBase
    {
        private readonly CinemaContext _context;

        public ShowtimesApiController(CinemaContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy danh sách suất chiếu theo Phim, Chi nhánh hoặc Ngày chiếu
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetShowtimes(
            [FromQuery] int? movieId = null,
            [FromQuery] int? branchId = null,
            [FromQuery] DateTime? date = null)
        {
            var query = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a!.Theater)
                        .ThenInclude(t => t!.Branch)
                .AsNoTracking()
                .Where(s => s.IsActive == true && s.StartTime >= DateTime.Now);

            if (movieId.HasValue)
            {
                query = query.Where(s => s.MovieId == movieId.Value);
            }

            if (branchId.HasValue)
            {
                query = query.Where(s => s.Auditorium != null && s.Auditorium.Theater != null && s.Auditorium.Theater.Branch != null && s.Auditorium.Theater.Branch.BranchId == branchId.Value);
            }

            if (date.HasValue)
            {
                var targetDate = date.Value.Date;
                query = query.Where(s => s.StartTime.HasValue && s.StartTime.Value.Date == targetDate);
            }

            var showtimes = await query
                .OrderBy(s => s.StartTime)
                .Select(s => new ShowtimeDto
                {
                    ShowtimeId = s.ShowtimeId,
                    MovieId = s.MovieId,
                    MovieTitle = s.Movie != null ? s.Movie.Title : null,
                    PosterUrl = s.Movie != null ? s.Movie.PosterUrl : null,
                    AuditoriumId = s.AuditoriumId,
                    AuditoriumName = s.Auditorium != null ? s.Auditorium.Name : null,
                    TheaterName = s.Auditorium != null && s.Auditorium.Theater != null ? s.Auditorium.Theater.Name : null,
                    BranchName = s.Auditorium != null && s.Auditorium.Theater != null && s.Auditorium.Theater.Branch != null ? s.Auditorium.Theater.Branch.BranchName : null,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    BasePrice = s.BasePrice ?? 0,
                    Language = s.Language
                })
                .ToListAsync();

            return Ok(showtimes);
        }

        /// <summary>
        /// Lấy chi tiết suất chiếu & sơ đồ ghế kèm trạng thái (trống / đã đặt / đang giữ chỗ)
        /// </summary>
        [HttpGet("{id}/seats")]
        public async Task<IActionResult> GetShowtimeSeats(int id)
        {
            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);

            if (showtime == null)
                return NotFound(new { message = "Không tìm thấy suất chiếu." });

            if (showtime.AuditoriumId == null)
                return BadRequest(new { message = "Suất chiếu không hợp lệ (không có phòng chiếu)." });

            var seats = await _context.Seats
                .Where(st => st.AuditoriumId == showtime.AuditoriumId && st.IsActive == true)
                .AsNoTracking()
                .ToListAsync();

            var now = DateTime.Now;

            // 1. Ghế đã bán / đã thanh toán / đã check-in
            var bookedSeatIds = await _context.Tickets
                .Where(t => t.ShowtimeId == id 
                    && t.Order != null 
                    && (t.Order.Status == "Đã thanh toán" || t.Order.Status == "Completed" || t.Order.Status == "Paid" || t.Order.Status == "Đã check-in"))
                .Select(t => t.SeatId)
                .Where(seatId => seatId.HasValue)
                .Select(seatId => seatId!.Value)
                .Distinct()
                .ToListAsync();

            // 2. Ghế đang trong quá trình thanh toán (chưa hết hạn 15 phút)
            var pendingSeatIds = await _context.Tickets
                .Where(t => t.ShowtimeId == id 
                    && t.Order != null 
                    && (t.Order.Status == "Chờ thanh toán" || t.Order.Status == "Đang chờ thanh toán" || t.Order.Status == "Pending")
                    && (t.Order.ExpiredAt == null || t.Order.ExpiredAt > now))
                .Select(t => t.SeatId)
                .Where(seatId => seatId.HasValue)
                .Select(seatId => seatId!.Value)
                .Distinct()
                .ToListAsync();

            decimal basePrice = showtime.BasePrice ?? 80000;

            var seatStatuses = seats.Select(seat =>
            {
                decimal seatPrice = basePrice;
                if (seat.SeatType == "VIP") seatPrice += 20000;
                else if (seat.SeatType == "Couple") seatPrice += 40000;

                bool isBooked = bookedSeatIds.Contains(seat.SeatId);
                bool isHeld = pendingSeatIds.Contains(seat.SeatId);

                return new SeatStatusDto
                {
                    SeatId = seat.SeatId,
                    RowLabel = seat.RowLabel,
                    SeatNumber = seat.SeatNumber,
                    SeatType = seat.SeatType ?? "Standard",
                    Price = seatPrice,
                    IsBooked = isBooked,
                    IsHeldByOther = isHeld
                };
            })
            .OrderBy(s => s.RowLabel)
            .ThenBy(s => s.SeatNumber)
            .ToList();

            var result = new AuditoriumLayoutDto
            {
                ShowtimeId = showtime.ShowtimeId,
                AuditoriumId = showtime.AuditoriumId.Value,
                AuditoriumName = showtime.Auditorium?.Name ?? "",
                MovieTitle = showtime.Movie?.Title ?? "",
                StartTime = showtime.StartTime,
                BasePrice = basePrice,
                Seats = seatStatuses
            };

            return Ok(result);
        }

        /// <summary>
        /// Gợi ý chuỗi ghế đẹp nhất gần trung tâm màn hình cho Mobile App
        /// </summary>
        [HttpGet("{id}/best-seats")]
        public async Task<IActionResult> RecommendBestSeats(int id, [FromQuery] int count = 2)
        {
            if (count <= 0) count = 1;
            if (count > 10) count = 10;

            var showtime = await _context.Showtimes
                .Include(s => s.Auditorium)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);

            if (showtime == null || showtime.AuditoriumId == null)
                return NotFound(new { message = "Không tìm thấy suất chiếu." });

            var seats = await _context.Seats
                .Where(st => st.AuditoriumId == showtime.AuditoriumId && st.IsActive == true)
                .AsNoTracking()
                .ToListAsync();

            var occupiedSeatIds = await _context.Tickets
                .Where(t => t.ShowtimeId == id && t.Order != null && t.Order.Status != "Cancelled" && t.Order.Status != "Expired")
                .Select(t => t.SeatId)
                .Where(sId => sId.HasValue)
                .Select(sId => sId!.Value)
                .Distinct()
                .ToListAsync();

            var availableSeats = seats
                .Where(s => !occupiedSeatIds.Contains(s.SeatId) && !string.IsNullOrEmpty(s.RowLabel) && s.SeatNumber.HasValue)
                .ToList();

            if (availableSeats.Count < count)
            {
                return Ok(new { recommendedSeats = new List<string>(), message = "Sơ đồ phòng chiếu không đủ ghế trống." });
            }

            var rows = availableSeats.Select(s => s.RowLabel!).Distinct().OrderBy(r => r).ToList();
            int midRowIndex = rows.Count / 2;
            int maxSeatNum = availableSeats.Max(s => s.SeatNumber!.Value);
            int minSeatNum = availableSeats.Min(s => s.SeatNumber!.Value);
            double centerSeatNum = (maxSeatNum + minSeatNum) / 2.0;

            var seatsByRow = availableSeats
                .GroupBy(s => s.RowLabel!)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SeatNumber!.Value).ToList());

            List<Seat>? bestBlock = null;
            double minScore = double.MaxValue;

            foreach (var kvp in seatsByRow)
            {
                string rowLabel = kvp.Key;
                var rowSeats = kvp.Value;
                int rowIndex = rows.IndexOf(rowLabel);
                double rowDist = Math.Abs(rowIndex - midRowIndex);

                for (int i = 0; i <= rowSeats.Count - count; i++)
                {
                    var block = rowSeats.Skip(i).Take(count).ToList();
                    bool isConsecutive = true;
                    for (int j = 0; j < block.Count - 1; j++)
                    {
                        if (block[j + 1].SeatNumber!.Value - block[j].SeatNumber!.Value != 1)
                        {
                            isConsecutive = false;
                            break;
                        }
                    }
                    if (!isConsecutive) continue;

                    double blockCenter = (block[0].SeatNumber!.Value + block[^1].SeatNumber!.Value) / 2.0;
                    double seatDist = Math.Abs(blockCenter - centerSeatNum);
                    double vipBonus = block.Any(s => s.SeatType == "VIP") ? -1.0 : 0.0;
                    double score = (rowDist * 3.0) + seatDist + vipBonus;

                    if (score < minScore)
                    {
                        minScore = score;
                        bestBlock = block;
                    }
                }
            }

            if (bestBlock == null)
            {
                bestBlock = availableSeats
                    .OrderBy(s => Math.Abs(rows.IndexOf(s.RowLabel!) - midRowIndex) * 3.0 + Math.Abs(s.SeatNumber!.Value - centerSeatNum))
                    .Take(count)
                    .ToList();
            }

            var recommendedCodes = bestBlock.Select(s => $"{s.RowLabel}{s.SeatNumber}").ToList();
            return Ok(new { 
                recommendedSeats = recommendedCodes, 
                count = recommendedCodes.Count, 
                message = $"Gợi ý {recommendedCodes.Count} ghế đẹp nhất gần trung tâm màn hình: {string.Join(", ", recommendedCodes)}." 
            });
        }
    }
}
