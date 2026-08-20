using CINEMA.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers.Api.AdminApi
{
    [ApiController]
    [Route("api/admin/dashboard")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin,Manager,Staff")]
    public class AdminDashboardApiController : ControllerBase
    {
        private readonly CinemaContext _context;

        public AdminDashboardApiController(CinemaContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy thống kê tổng quan (Dashboard Admin API)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            var isPaidOrder = (string? s) => s == "Paid" || s == "Đã thanh toán" || s == "Completed" || s == "Đã check-in";

            var totalCustomers = await _context.Customers.CountAsync();
            var totalMovies = await _context.Movies.CountAsync(m => m.IsActive == true);
            var totalOrders = await _context.Orders.CountAsync(o => o.Status != null && (o.Status == "Paid" || o.Status == "Đã thanh toán" || o.Status == "Completed" || o.Status == "Đã check-in"));
            var totalRevenue = await _context.Orders
                .Where(o => o.Status != null && (o.Status == "Paid" || o.Status == "Đã thanh toán" || o.Status == "Completed" || o.Status == "Đã check-in"))
                .SumAsync(o => o.TotalAmount ?? 0);

            var recentOrders = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.Status != null && (o.Status == "Paid" || o.Status == "Đã thanh toán" || o.Status == "Completed" || o.Status == "Đã check-in"))
                .OrderByDescending(o => o.PaidAt ?? o.CreatedAt)
                .Take(5)
                .Select(o => new
                {
                    o.OrderId,
                    CustomerName = o.Customer != null ? o.Customer.FullName : "Khách ẩn danh",
                    o.TotalAmount,
                    o.PaymentMethod,
                    Date = o.PaidAt ?? o.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                totalCustomers,
                totalMovies,
                totalOrders,
                totalRevenue,
                recentOrders
            });
        }
    }
}
