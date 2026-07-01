using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CINEMA.Models;

namespace CINEMA.Controllers
{
    public class TicketsController : Controller
    {
        private readonly CinemaContext _context;

        public TicketsController(CinemaContext context)
        {
            _context = context;
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

            foreach (var order in expiredOrders)
            {
                order.Status = "Đã hủy";

                foreach (var t in order.Tickets)
                {
                    t.Status = "Đã hủy";
                    t.PaymentStatus = "Đã hủy";
                }
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
        public async Task<IActionResult> Pay(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);

            if (order == null)
                return NotFound();

            // ❌ hết hạn → hủy luôn
            if (order.ExpiredAt < DateTime.Now)
            {
                order.Status = "Đã hủy";
                await _context.SaveChangesAsync();

                TempData["ErrorMessage"] = "⏰ Đơn đã hết hạn 10 phút!";
                return RedirectToAction("MyTickets");
            }

            // ✅ còn hạn → cho thanh toán
            order.Status = "Đang chờ thanh toán";
            await _context.SaveChangesAsync();

            // 👉 redirect sang VNPAY hoặc Payment
            return RedirectToAction("CreatePayment", "Payment", new { orderId = orderId });
        }

        // =================== [3] Hủy vé ===================
        [HttpPost]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Tickets)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return NotFound();

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