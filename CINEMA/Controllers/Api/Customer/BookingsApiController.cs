using System.Security.Claims;
using CINEMA.DTOs;
using CINEMA.Models;
using CINEMA.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers.Api.CustomerApi
{
    [ApiController]
    [Route("api/bookings")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class BookingsApiController : ControllerBase
    {
        private readonly CinemaContext _context;
        private readonly IVnpayService _vnpayService;

        public BookingsApiController(CinemaContext context, IVnpayService vnpayService)
        {
            _context = context;
            _vnpayService = vnpayService;
        }

        /// <summary>
        /// Tạo đơn hàng đặt vé xem phim
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int customerId))
                return Unauthorized(new { message = "Token không hợp lệ." });

            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                .FirstOrDefaultAsync(s => s.ShowtimeId == model.ShowtimeId);

            if (showtime == null)
                return NotFound(new { message = "Suất chiếu không tồn tại." });

            if (model.SeatIds == null || !model.SeatIds.Any())
                return BadRequest(new { message = "Vui lòng chọn ít nhất một ghế." });

            var alreadyBookedSeats = await _context.Tickets
                .Where(t => t.ShowtimeId == model.ShowtimeId
                       && t.SeatId.HasValue
                       && model.SeatIds.Contains(t.SeatId.Value)
                       && t.Order != null
                       && t.Order.Status != "Cancelled"
                       && t.Order.Status != "Expired")
                .Select(t => t.SeatId)
                .ToListAsync();

            if (alreadyBookedSeats.Any())
            {
                return BadRequest(new { message = "Một số ghế bạn chọn đã được người khác đặt. Vui lòng chọn ghế khác." });
            }

            var seats = await _context.Seats
                .Where(s => model.SeatIds.Contains(s.SeatId))
                .ToListAsync();

            decimal basePrice = showtime.BasePrice ?? 80000;
            decimal ticketTotal = 0;
            var tickets = new List<Ticket>();

            foreach (var seat in seats)
            {
                decimal price = basePrice;
                if (seat.SeatType == "VIP") price += 20000;
                else if (seat.SeatType == "Couple") price += 40000;

                ticketTotal += price;

                tickets.Add(new Ticket
                {
                    ShowtimeId = showtime.ShowtimeId,
                    SeatId = seat.SeatId,
                    CustomerId = customerId,
                    Price = price,
                    Status = "Pending",
                    BookedAt = DateTime.Now,
                    PaymentStatus = "Unpaid"
                });
            }

            decimal comboTotal = 0;
            var orderCombos = new List<OrderCombo>();

            if (model.Combos != null && model.Combos.Any())
            {
                var comboIds = model.Combos.Select(c => c.ComboId).ToList();
                var comboEntities = await _context.Combos.Where(c => comboIds.Contains(c.ComboId)).ToListAsync();

                foreach (var item in model.Combos)
                {
                    var entity = comboEntities.FirstOrDefault(c => c.ComboId == item.ComboId);
                    if (entity != null && item.Quantity > 0)
                    {
                        decimal itemPrice = entity.Price ?? 0;
                        comboTotal += itemPrice * item.Quantity;

                        orderCombos.Add(new OrderCombo
                        {
                            ComboId = entity.ComboId,
                            Quantity = item.Quantity,
                            UnitPrice = itemPrice
                        });
                    }
                }
            }

            decimal discountAmount = 0;
            if (!string.IsNullOrWhiteSpace(model.VoucherCode))
            {
                var now = DateTime.Now;
                var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => 
                    v.Code.ToUpper() == model.VoucherCode.ToUpper() 
                    && v.IsActive
                    && (v.StartDate == null || v.StartDate <= now)
                    && (v.EndDate == null || v.EndDate >= now));
                if (voucher != null)
                {
                    decimal subTotal = ticketTotal + comboTotal;
                    if (subTotal >= voucher.MinOrderValue)
                    {
                        if (voucher.DiscountAmount.HasValue && voucher.DiscountAmount.Value > 0)
                            discountAmount = voucher.DiscountAmount.Value;
                        else if (voucher.DiscountPercent.HasValue && voucher.DiscountPercent.Value > 0)
                            discountAmount = subTotal * (decimal)(voucher.DiscountPercent.Value / 100.0);

                        if (discountAmount > subTotal) discountAmount = subTotal;

                        voucher.UsedCount += 1;
                    }
                }
            }

            decimal totalAmount = Math.Max(0, ticketTotal + comboTotal - discountAmount);

            var order = new Order
            {
                CustomerId = customerId,
                Status = model.PaymentMethod == "Cash" ? "Paid" : "Pending",
                PaymentMethod = model.PaymentMethod,
                TicketTotal = ticketTotal,
                ComboTotal = comboTotal,
                VoucherCode = model.VoucherCode,
                DiscountAmount = discountAmount,
                TotalAmount = totalAmount,
                CreatedAt = DateTime.Now,
                ExpiredAt = DateTime.Now.AddMinutes(15),
                PaidAt = model.PaymentMethod == "Cash" ? DateTime.Now : null,
                Tickets = tickets,
                OrderCombos = orderCombos
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            string? paymentUrl = null;
            if (model.PaymentMethod == "VNPay")
            {
                paymentUrl = _vnpayService.CreatePaymentUrl(order, HttpContext);
            }
            else if (model.PaymentMethod == "VietQR" || model.PaymentMethod == "MoMo" || model.PaymentMethod == "ZaloPay")
            {
                paymentUrl = $"/Payment/VietQRPayment?orderId={order.OrderId}";
            }

            var seatNames = seats.Select(s => $"{s.RowLabel}{s.SeatNumber}").ToList();

            return Ok(new OrderResponseDto
            {
                OrderId = order.OrderId,
                Status = order.Status,
                PaymentMethod = order.PaymentMethod,
                TicketTotal = ticketTotal,
                ComboTotal = comboTotal,
                DiscountAmount = discountAmount,
                TotalAmount = totalAmount,
                CreatedAt = order.CreatedAt ?? DateTime.Now,
                PaymentUrl = paymentUrl,
                MovieTitle = showtime.Movie?.Title,
                AuditoriumName = showtime.Auditorium?.Name,
                ShowtimeStart = showtime.StartTime,
                SeatNames = seatNames,
                QrCodeData = $"CINEZONE-ORDER-{order.OrderId}"
            });
        }

        /// <summary>
        /// Lấy lịch sử đặt vé của khách hàng
        /// </summary>
        [HttpGet("my-orders")]
        public async Task<IActionResult> GetMyOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int customerId))
                return Unauthorized(new { message = "Token không hợp lệ." });

            var query = _context.Orders
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s!.Movie)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Seat)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .Where(o => o.CustomerId == customerId)
                .AsNoTracking();

            var totalItems = await query.CountAsync();
            var rawOrders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var orders = rawOrders.Select(o => new OrderResponseDto
            {
                OrderId = o.OrderId,
                Status = o.Status ?? "Pending",
                PaymentMethod = o.PaymentMethod ?? "Unknown",
                TicketTotal = o.TicketTotal ?? 0,
                ComboTotal = o.ComboTotal ?? 0,
                DiscountAmount = o.DiscountAmount ?? 0,
                TotalAmount = o.TotalAmount ?? 0,
                CreatedAt = o.CreatedAt ?? DateTime.Now,
                MovieTitle = o.Tickets.FirstOrDefault()?.Showtime?.Movie?.Title ?? "Phim",
                AuditoriumName = o.Tickets.FirstOrDefault()?.Showtime?.Auditorium?.Name ?? "",
                ShowtimeStart = o.Tickets.FirstOrDefault()?.Showtime?.StartTime,
                SeatNames = o.Tickets.Select(t => t.Seat != null ? $"{t.Seat.RowLabel}{t.Seat.SeatNumber}" : "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
                ComboNames = o.OrderCombos.Select(oc => oc.Combo != null ? $"{oc.Combo.Name} (x{oc.Quantity})" : "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
                QrCodeData = $"CINEZONE-ORDER-{o.OrderId}"
            }).ToList();

            return Ok(new { totalItems, page, pageSize, data = orders });
        }

        /// <summary>
        /// Lấy chi tiết vé / đơn hàng theo OrderId
        /// </summary>
        [HttpGet("{orderId}")]
        public async Task<IActionResult> GetOrderDetail(int orderId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int customerId))
                return Unauthorized(new { message = "Token không hợp lệ." });

            var o = await _context.Orders
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s!.Movie)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Seat)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId);

            if (o == null)
                return NotFound(new { message = "Không tìm thấy đơn hàng." });

            var result = new OrderResponseDto
            {
                OrderId = o.OrderId,
                Status = o.Status ?? "Pending",
                PaymentMethod = o.PaymentMethod ?? "Unknown",
                TicketTotal = o.TicketTotal ?? 0,
                ComboTotal = o.ComboTotal ?? 0,
                DiscountAmount = o.DiscountAmount ?? 0,
                TotalAmount = o.TotalAmount ?? 0,
                CreatedAt = o.CreatedAt ?? DateTime.Now,
                MovieTitle = o.Tickets.FirstOrDefault()?.Showtime?.Movie?.Title,
                AuditoriumName = o.Tickets.FirstOrDefault()?.Showtime?.Auditorium?.Name,
                ShowtimeStart = o.Tickets.FirstOrDefault()?.Showtime?.StartTime,
                SeatNames = o.Tickets.Select(t => t.Seat != null ? $"{t.Seat.RowLabel}{t.Seat.SeatNumber}" : "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
                ComboNames = o.OrderCombos.Select(oc => oc.Combo != null ? $"{oc.Combo.Name} (x{oc.Quantity})" : "").Where(s => !string.IsNullOrEmpty(s)).ToList(),
                QrCodeData = $"CINEZONE-ORDER-{o.OrderId}"
            };

            return Ok(result);
        }
    }
}
