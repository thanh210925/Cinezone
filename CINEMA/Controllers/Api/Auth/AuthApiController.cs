using System.Security.Claims;
using CINEMA.DTOs;
using CINEMA.Models;
using CINEMA.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers.Api.AuthApi
{
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly CinemaContext _context;
        private readonly IJwtService _jwtService;

        public AuthApiController(CinemaContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        /// <summary>
        /// Đăng nhập Khách hàng
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email.ToLower() == model.Email.ToLower());

            if (customer == null || !BCrypt.Net.BCrypt.Verify(model.Password, customer.PasswordHash))
            {
                return Unauthorized(new { message = "Email hoặc mật khẩu không chính xác." });
            }

            customer.LastLogin = DateTime.Now;
            await _context.SaveChangesAsync();

            var token = _jwtService.GenerateCustomerToken(customer);
            var refreshToken = await _jwtService.GenerateAndSaveRefreshTokenAsync(customer.CustomerId, "Customer");

            return Ok(new AuthResponseDto
            {
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiresAt = refreshToken.ExpiresAt,
                UserId = customer.CustomerId,
                FullName = customer.FullName,
                Email = customer.Email,
                UserType = "Customer",
                Avatar = customer.AvatarUrl ?? customer.Avatar
            });
        }

        /// <summary>
        /// Đăng nhập Admin / Quản trị viên
        /// </summary>
        [HttpPost("admin-login")]
        public async Task<IActionResult> AdminLogin([FromBody] LoginDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var admin = await _context.Admins
                .FirstOrDefaultAsync(a => a.Email.ToLower() == model.Email.ToLower());

            if (admin == null || string.IsNullOrEmpty(admin.PasswordHash) || !BCrypt.Net.BCrypt.Verify(model.Password, admin.PasswordHash))
            {
                return Unauthorized(new { message = "Tài khoản Admin hoặc mật khẩu không chính xác." });
            }

            if (!admin.IsActive)
                return Unauthorized(new { message = "Tài khoản Admin đã bị khóa." });

            admin.LastLogin = DateTime.Now;
            await _context.SaveChangesAsync();

            var token = _jwtService.GenerateAdminToken(admin);
            var refreshToken = await _jwtService.GenerateAndSaveRefreshTokenAsync(admin.AdminId ?? 0, "Admin");

            return Ok(new AuthResponseDto
            {
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiresAt = refreshToken.ExpiresAt,
                UserId = admin.AdminId ?? 0,
                FullName = admin.FullName ?? "",
                Email = admin.Email ?? "",
                UserType = "Admin",
                Role = admin.Role,
                Avatar = admin.Avatar
            });
        }

        /// <summary>
        /// Đăng ký tài khoản Khách hàng
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existing = await _context.Customers
                .AnyAsync(c => c.Email.ToLower() == model.Email.ToLower());

            if (existing)
                return BadRequest(new { message = "Email này đã được sử dụng." });

            var customer = new Customer
            {
                FullName = model.FullName,
                Email = model.Email.ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Phone = model.Phone,
                BirthDate = model.BirthDate,
                Gender = model.Gender,
                Address = model.Address,
                CreatedAt = DateTime.Now,
                TotalSpent = 0,
                MembershipLevel = "Đồng",
                ReputationScore = 100
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            var token = _jwtService.GenerateCustomerToken(customer);
            var refreshToken = await _jwtService.GenerateAndSaveRefreshTokenAsync(customer.CustomerId, "Customer");

            return Ok(new AuthResponseDto
            {
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiresAt = refreshToken.ExpiresAt,
                UserId = customer.CustomerId,
                FullName = customer.FullName,
                Email = customer.Email,
                UserType = "Customer"
            });
        }

        /// <summary>
        /// Cấp lại Access Token từ Refresh Token
        /// </summary>
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var storedRefreshToken = await _jwtService.ValidateRefreshTokenAsync(model.RefreshToken);
            if (storedRefreshToken == null)
            {
                return Unauthorized(new { message = "Refresh Token không hợp lệ hoặc đã hết hạn." });
            }

            await _jwtService.RevokeRefreshTokenAsync(model.RefreshToken);

            if (storedRefreshToken.UserType == "Customer")
            {
                var customer = await _context.Customers.FindAsync(storedRefreshToken.UserId);
                if (customer == null)
                    return Unauthorized(new { message = "Người dùng không tồn tại." });

                var newToken = _jwtService.GenerateCustomerToken(customer);
                var newRefreshToken = await _jwtService.GenerateAndSaveRefreshTokenAsync(customer.CustomerId, "Customer");

                return Ok(new AuthResponseDto
                {
                    Token = newToken,
                    ExpiresAt = DateTime.UtcNow.AddDays(7),
                    RefreshToken = newRefreshToken.Token,
                    RefreshTokenExpiresAt = newRefreshToken.ExpiresAt,
                    UserId = customer.CustomerId,
                    FullName = customer.FullName,
                    Email = customer.Email,
                    UserType = "Customer",
                    Avatar = customer.AvatarUrl ?? customer.Avatar
                });
            }
            else
            {
                var admin = await _context.Admins.FindAsync(storedRefreshToken.UserId);
                if (admin == null || !admin.IsActive)
                    return Unauthorized(new { message = "Tài khoản Admin không tồn tại hoặc đã bị khóa." });

                var newToken = _jwtService.GenerateAdminToken(admin);
                var newRefreshToken = await _jwtService.GenerateAndSaveRefreshTokenAsync(admin.AdminId ?? 0, "Admin");

                return Ok(new AuthResponseDto
                {
                    Token = newToken,
                    ExpiresAt = DateTime.UtcNow.AddDays(7),
                    RefreshToken = newRefreshToken.Token,
                    RefreshTokenExpiresAt = newRefreshToken.ExpiresAt,
                    UserId = admin.AdminId ?? 0,
                    FullName = admin.FullName ?? "",
                    Email = admin.Email ?? "",
                    UserType = "Admin",
                    Role = admin.Role,
                    Avatar = admin.Avatar
                });
            }
        }

        /// <summary>
        /// Lấy thông tin cá nhân của người dùng hiện tại
        /// </summary>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int customerId))
                return Unauthorized(new { message = "Token không hợp lệ." });

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            return Ok(new UserProfileDto
            {
                CustomerId = customer.CustomerId,
                FullName = customer.FullName,
                Email = customer.Email,
                Phone = customer.Phone,
                BirthDate = customer.BirthDate,
                Gender = customer.Gender,
                Address = customer.Address,
                Avatar = customer.AvatarUrl ?? customer.Avatar,
                TotalSpent = customer.TotalSpent,
                MembershipLevel = customer.MembershipLevel ?? customer.CalculateMembershipLevel(),
                ReputationScore = customer.ReputationScore
            });
        }

        /// <summary>
        /// Cập nhật thông tin cá nhân
        /// </summary>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] RegisterDto model)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int customerId))
                return Unauthorized(new { message = "Token không hợp lệ." });

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            customer.FullName = model.FullName;
            customer.Phone = model.Phone;
            customer.BirthDate = model.BirthDate;
            customer.Gender = model.Gender;
            customer.Address = model.Address;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật thông tin thành công." });
        }

        /// <summary>
        /// Đổi mật khẩu
        /// </summary>
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int customerId))
                return Unauthorized(new { message = "Token không hợp lệ." });

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, customer.PasswordHash))
                return BadRequest(new { message = "Mật khẩu hiện tại không đúng." });

            customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đổi mật khẩu thành công." });
        }
    }
}
