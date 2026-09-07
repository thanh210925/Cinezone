using System.ComponentModel.DataAnnotations;
using CINEMA.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers.Api.AdminApi
{
    public class ScanQrRequestDto
    {
        [Required(ErrorMessage = "Dữ liệu QR Code không được để trống")]
        public string QrData { get; set; } = null!;
    }

    public class ScanQrResultDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = null!;
        public int? OrderId { get; set; }
        public string? CustomerName { get; set; }
        public string? MovieTitle { get; set; }
        public string? TheaterName { get; set; }
        public string? AuditoriumName { get; set; }
        public DateTime? ShowtimeStart { get; set; }
        public List<string> SeatNames { get; set; } = new();
        public List<string> ComboNames { get; set; } = new();
        public decimal? TotalAmount { get; set; }
        public string? OrderStatus { get; set; }
        public DateTime? CheckedInAt { get; set; }
    }

    [Route("api/admin/tickets")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin,Manager,Staff")]
    public class AdminTicketScanApiController : ControllerBase
    {
        private readonly CinemaContext _context;

        public AdminTicketScanApiController(CinemaContext context)
        {
            _context = context;
        }

        /// <summary>
        /// API Quét QR Code mã đơn hàng / mã vé để check-in trực tiếp tại cửa rạp
        /// </summary>
        /// <param name="model">Dữ liệu mã QR Code quét từ vé khách hàng</param>
        /// <response code="200">Xác thực vé thành công hoặc vé đã qua check-in trước đó</response>
        /// <response code="400">Dữ liệu mã QR không hợp lệ hoặc không tìm thấy đơn hàng</response>
        /// <response code="401">Chưa đăng nhập hoặc không có quyền Nhân viên/Admin</response>
        [HttpPost("scan-qr")]
        [ProducesResponseType(typeof(ScanQrResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ScanQrResultDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ScanQrCode([FromBody] ScanQrRequestDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ScanQrResultDto
                {
                    IsSuccess = false,
                    Message = "Dữ liệu QR không hợp lệ."
                });
            }

            string rawCode = model.QrData.Trim();
            int orderId = 0;

            // Thử trích xuất OrderId từ chuỗi QR (ví dụ: "ORD-12345", "12345", "CZ-12345")
            if (rawCode.StartsWith("ORD-", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(rawCode.Substring(4), out orderId);
            }
            else if (rawCode.StartsWith("CZ-", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(rawCode.Substring(3), out orderId);
            }
            else
            {
                int.TryParse(rawCode, out orderId);
            }

            if (orderId <= 0)
            {
                return BadRequest(new ScanQrResultDto
                {
                    IsSuccess = false,
                    Message = "Không nhận diện được mã vé / đơn hàng."
                });
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Tickets).ThenInclude(t => t.Seat)
                .Include(o => o.Tickets).ThenInclude(t => t.Showtime).ThenInclude(s => s!.Movie)
                .Include(o => o.Tickets).ThenInclude(t => t.Showtime).ThenInclude(s => s!.Auditorium).ThenInclude(a => a!.Theater)
                .Include(o => o.OrderCombos).ThenInclude(oc => oc.Combo)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                return NotFound(new ScanQrResultDto
                {
                    IsSuccess = false,
                    Message = $"Không tìm thấy đơn hàng mã #{orderId} trong hệ thống."
                });
            }

            var firstTicket = order.Tickets.FirstOrDefault();
            var showtime = firstTicket?.Showtime;
            var seatNames = order.Tickets.Select(t => $"{t.Seat?.RowLabel}{t.Seat?.SeatNumber}").ToList();
            var comboNames = order.OrderCombos.Select(oc => $"{oc.Combo?.Name} (x{oc.Quantity})").ToList();

            if (order.Status == "Đã check-in" || order.Status == "CheckedIn")
            {
                return Ok(new ScanQrResultDto
                {
                    IsSuccess = false,
                    Message = "Vé này ĐÃ ĐƯỢC CHECK-IN trước đó!",
                    OrderId = order.OrderId,
                    CustomerName = order.Customer?.FullName ?? "Khách vãng lai",
                    MovieTitle = showtime?.Movie?.Title,
                    TheaterName = showtime?.Auditorium?.Theater?.Name,
                    AuditoriumName = showtime?.Auditorium?.Name,
                    ShowtimeStart = showtime?.StartTime,
                    SeatNames = seatNames,
                    ComboNames = comboNames,
                    TotalAmount = order.TotalAmount,
                    OrderStatus = order.Status,
                    CheckedInAt = order.PaidAt
                });
            }

            if (order.Status == "Đã hủy" || order.Status == "Cancelled" || order.Status == "Expired")
            {
                return Ok(new ScanQrResultDto
                {
                    IsSuccess = false,
                    Message = "Đơn hàng này đã bị HỦY hoặc HẾT HẠN thanh toán!",
                    OrderId = order.OrderId,
                    CustomerName = order.Customer?.FullName ?? "Khách vãng lai",
                    MovieTitle = showtime?.Movie?.Title,
                    OrderStatus = order.Status
                });
            }

            // Kiểm tra trạng thái đơn hàng
            if (order.Status != "Đã thanh toán" && order.Status != "Completed" && order.Status != "Paid")
            {
                return Ok(new ScanQrResultDto
                {
                    IsSuccess = false,
                    Message = $"Đơn hàng chưa thanh toán thành công (Trạng thái: {order.Status}).",
                    OrderId = order.OrderId,
                    OrderStatus = order.Status
                });
            }

            // Tiến hành Check-in vé
            order.Status = "Đã check-in";
            foreach (var ticket in order.Tickets)
            {
                ticket.Status = "Đã check-in";
            }

            await _context.SaveChangesAsync();

            return Ok(new ScanQrResultDto
            {
                IsSuccess = true,
                Message = "XÁC NHẬN VÉ THÀNH CÔNG! Chúc quý khách xem phim vui vẻ.",
                OrderId = order.OrderId,
                CustomerName = order.Customer?.FullName ?? "Khách vãng lai",
                MovieTitle = showtime?.Movie?.Title,
                TheaterName = showtime?.Auditorium?.Theater?.Name,
                AuditoriumName = showtime?.Auditorium?.Name,
                ShowtimeStart = showtime?.StartTime,
                SeatNames = seatNames,
                ComboNames = comboNames,
                TotalAmount = order.TotalAmount,
                OrderStatus = order.Status,
                CheckedInAt = DateTime.Now
            });
        }
    }
}
