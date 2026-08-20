using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CINEMA.Models;
using System.Linq;
using System.Threading.Tasks;

namespace CINEMA.Controllers
{
    public class AdminOrdersController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public AdminOrdersController(CinemaContext context)
        {
            _context = context;
        }

        // =================== [1] DANH SÁCH ĐƠN HÀNG ===================
        public async Task<IActionResult> Index(string search, string status)
        {
            var now = DateTime.Now;

            // 🔥 1. AUTO HỦY ĐƠN HẾT HẠN
            var expiredOrders = await _context.Orders
       .Include(o => o.Tickets)
       .Where(o => o.Status == "Đang chờ thanh toán"
           && (
               (o.ExpiredAt != null && o.ExpiredAt < now)
               ||
               (o.ExpiredAt == null && o.CreatedAt != null && o.CreatedAt < now.AddMinutes(-15))
           )
       )
       .ToListAsync();

            foreach (var o in expiredOrders)
            {
                o.Status = "Đã hủy";

                // 🔥 update luôn ticket
                foreach (var t in o.Tickets)
                {
                    t.Status = "Đã hủy";
                }
            }

            if (expiredOrders.Any())
            {
                await _context.SaveChangesAsync();
            }

            // 🔥 2. QUERY DATA
            var orders = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Tickets)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .AsQueryable();

            // 🔍 search
            if (!string.IsNullOrWhiteSpace(search))
            {
                orders = orders.Where(o =>
                    (o.Customer != null && o.Customer.FullName.Contains(search)) ||
                    o.OrderId.ToString().Contains(search));
            }

            // 🔍 filter status
            if (!string.IsNullOrWhiteSpace(status))
            {
                orders = orders.Where(o => o.Status == status);
            }

            // 🔥 FIX NULL + SORT
            var list = await orders
                .OrderByDescending(o => o.CreatedAt ?? DateTime.MinValue)
                .ToListAsync();

            return View(list);
        }
        // =================== [2] XEM CHI TIẾT ĐƠN HÀNG ===================
        public async Task<IActionResult> Details(int id)
        {
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
                            .ThenInclude(a => a.Theater)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            return View(order);
        }

        // =================== [3] CẬP NHẬT TRẠNG THÁI (AJAX HOẶC POST FORM) ===================
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return BadRequest("⚠️ Trạng thái không hợp lệ.");

            var order = await _context.Orders
                .Include(o => o.Tickets)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            // ✅ Cập nhật trạng thái đơn hàng
            order.Status = status;

            // ✅ Cập nhật trạng thái vé (nếu có)
            foreach (var ticket in order.Tickets)
            {
                ticket.Status = status;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Đã cập nhật trạng thái đơn hàng #{id} thành '{status}'"
            });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Tickets)
                .Include(o => o.OrderCombos)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            // Xóa vé và combo liên quan
            _context.Tickets.RemoveRange(order.Tickets);
            _context.OrderCombos.RemoveRange(order.OrderCombos);
            _context.Orders.Remove(order);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Đã xóa đơn hàng #{id}.";
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Tickets)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            order.Status = "Đã hủy";

            foreach (var ticket in order.Tickets)
            {
                ticket.Status = "Đã hủy";
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"✅ Đã hủy đơn hàng #{id}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Tickets)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            order.Status = "Đã thanh toán";
            order.PaidAt = DateTime.Now;

            foreach (var ticket in order.Tickets)
            {
                ticket.Status = "Đã thanh toán";
                ticket.PaymentStatus = "Đã thanh toán";
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"✅ Đã xác nhận thanh toán thành công cho đơn hàng #CZ{id:D6}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // =================== [4] GIAO DIỆN QUÉT QR SOÁT VÉ ===================
        public IActionResult ScanQR()
        {
            return View();
        }
    }
}
