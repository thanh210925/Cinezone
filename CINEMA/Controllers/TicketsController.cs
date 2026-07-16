using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CINEMA.Models;
using CINEMA.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CINEMA.Controllers
{
    public class TicketsController : Controller
    {
        private readonly CinemaContext _context;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;

        public TicketsController(CinemaContext context, IEmailService emailService, IConfiguration config)
        {
            _context = context;
            _emailService = emailService;
            _config = config;
        }

        // =================== [AUTO CHECK EXPIRED] ===================
        private void CheckExpiredOrders()
        {
            var now = DateTime.Now;

            var expiredOrders = _context.Orders
                .Include(o => o.Tickets)
                .Where(o =>
                    (o.Status == "Chờ thanh toán" || o.Status == "Đang chờ thanh toán")
                    && o.ExpiredAt <= now)
                .ToList();

            string reqBaseUrl = _config["AppSettings:BaseUrl"] ?? (HttpContext != null ? $"{Request.Scheme}://{Request.Host}" : "http://localhost");

            foreach (var order in expiredOrders)
            {
                order.Status = "Đã hủy";

                foreach (var t in order.Tickets)
                {
                    t.Status = "Đã hủy";
                    t.PaymentStatus = "Đã hủy";
                }

                // Gửi email hủy vé bất đồng bộ trong background
                int cancelOrderId = order.OrderId;
                Task.Run(async () => {
                    try {
                        await _emailService.SendOrderCanceledEmailAsync(cancelOrderId, reqBaseUrl);
                    } catch (Exception ex) {
                        Console.WriteLine($"[Email Error] Failed to send order canceled email for order {cancelOrderId}: {ex.Message}");
                    }
                });
            }

            _context.SaveChanges();
        }
        // =================== [1] Danh sách vé ===================
        public IActionResult MyTickets()
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
                return RedirectToAction("Login", "Customer");

            // 🔥 kiểm tra hết hạn
            CheckExpiredOrders();

            var orders = _context.Orders
                .Where(o => o.CustomerId == customerId)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Seat)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Auditorium)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            return View(orders);
        }

        // =================== [2] Thanh toán ===================
        public async Task<IActionResult> Pay(int orderId, string? method = null)
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
                return RedirectToAction("Login", "Customer");

            var order = await _context.Orders
                .Include(o => o.Tickets)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return NotFound();

            if (order.CustomerId != customerId)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thực hiện thao tác này!";
                return RedirectToAction("MyTickets");
            }

            var now = DateTime.Now;

            // Nếu đơn hàng đã hết hạn
            if (order.ExpiredAt < now)
            {
                // Kiểm tra xem ghế đã bị người khác đặt chưa
                var seatIds = order.Tickets.Select(t => t.SeatId).ToList();
                var showtimeId = order.Tickets.FirstOrDefault()?.ShowtimeId;

                bool isSeatTaken = await _context.Tickets.AnyAsync(t =>
                    t.ShowtimeId == showtimeId &&
                    seatIds.Contains(t.SeatId) &&
                    t.OrderId != order.OrderId &&
                    t.Order != null &&
                    (t.Order.Status == "Đã thanh toán" ||
                     ((t.Order.Status == "Chờ thanh toán" || t.Order.Status == "Đang chờ thanh toán") && t.Order.ExpiredAt > now))
                );

                if (isSeatTaken)
                {
                    order.Status = "Đã hủy";
                    foreach (var t in order.Tickets)
                    {
                        t.Status = "Đã hủy";
                        t.PaymentStatus = "Đã hủy";
                    }
                    await _context.SaveChangesAsync();

                    TempData["ErrorMessage"] = "⏰ Đơn hàng đã hết hạn và ghế của bạn đã có người khác chọn!";
                    return RedirectToAction("MyTickets");
                }
            }

            // ✅ Gia hạn / thiết lập thời gian hết hạn mới là 15 phút từ hiện tại
            order.ExpiredAt = now.AddMinutes(15);
            order.Status = "Đang chờ thanh toán";
            if (!string.IsNullOrEmpty(method))
            {
                order.PaymentMethod = method;
            }
            foreach (var t in order.Tickets)
            {
                t.Status = "Đã đặt";
                t.PaymentStatus = "Chờ thanh toán";
            }
            await _context.SaveChangesAsync();

            // 👉 redirect sang VNPAY hoặc Payment
            return RedirectToAction("CreatePayment", "Payment", new { orderId = orderId });
        }

        // =================== [3] Hủy vé ===================
        [HttpPost]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
                return RedirectToAction("Login", "Customer");

            var order = await _context.Orders
                .Include(o => o.Tickets)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return NotFound();

            if (order.CustomerId != customerId)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thực hiện thao tác này!";
                return RedirectToAction("MyTickets");
            }

            if (order.Status == "Chờ thanh toán" || order.Status == "Đang chờ thanh toán")
            {
                order.Status = "Đã hủy";

                foreach (var t in order.Tickets)
                {
                    t.Status = "Đã hủy";
                    t.PaymentStatus = "Đã hủy";
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã hủy đơn #{order.OrderId}";
            }
            else
            {
                TempData["ErrorMessage"] = "❌ Không thể hủy đơn đã thanh toán";
            }

            return RedirectToAction("MyTickets");
        }

        // =================== [4] Chi tiết ===================
        public async Task<IActionResult> Details(int id)
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
                return RedirectToAction("Login", "Customer");

            CheckExpiredOrders();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Seat)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Auditorium)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == customerId);

            if (order == null)
                return NotFound();

            return View(order);
        }
    }
}