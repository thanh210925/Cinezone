using CINEMA.DTOs;
using CINEMA.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers.Api.AdminApi
{
    [ApiController]
    [Route("api/admin/customers")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin,SuperAdmin,Manager,Staff,CRM,CrmStaff")]
    public class AdminCustomersApiController : ControllerBase
    {
        private readonly CinemaContext _context;

        public AdminCustomersApiController(CinemaContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy danh sách tài khoản khách hàng (Admin)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCustomers([FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var query = _context.Customers.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.FullName.Contains(search) || c.Email.Contains(search) || (c.Phone != null && c.Phone.Contains(search)));
            }

            var totalItems = await query.CountAsync();
            var customers = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new UserProfileDto
                {
                    CustomerId = c.CustomerId,
                    FullName = c.FullName,
                    Email = c.Email,
                    Phone = c.Phone,
                    BirthDate = c.BirthDate,
                    Gender = c.Gender,
                    Address = c.Address,
                    Avatar = c.AvatarUrl ?? c.Avatar,
                    TotalSpent = c.TotalSpent,
                    MembershipLevel = c.MembershipLevel,
                    ReputationScore = c.ReputationScore
                })
                .ToListAsync();

            return Ok(new { totalItems, page, pageSize, data = customers });
        }
    }
}
