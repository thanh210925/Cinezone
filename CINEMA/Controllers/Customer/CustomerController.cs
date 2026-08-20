using CINEMA.Models;
using CINEMA.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
namespace CINEMA.Controllers
{
    public class CustomerController : Controller
    {
        private readonly CinemaContext _context;

        public CustomerController(CinemaContext context)
        {
            _context = context;
        }

        // ------------------ 🟢 ĐĂNG KÝ ------------------
        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Kiểm tra email trùng
            var exist = _context.Customers.FirstOrDefault(c => c.Email == model.Email);
            if (exist != null)
            {
                ViewBag.Error = "Email đã tồn tại!";
                return View(model);
            }

            // Tạo mới khách hàng
            var customer = new Customer
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                BirthDate = model.BirthDate,
                Gender = model.Gender,
                CreatedAt = DateTime.Now,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password)
            };

            _context.Customers.Add(customer);
            _context.SaveChanges();

            // Ghi log hoạt động đăng ký
            _context.UserActivityLogs.Add(new UserActivityLog
            {
                CustomerId = customer.CustomerId,
                SessionId = HttpContext.Session.Id,
                ActivityType = "REGISTER",
                Metadata = $"Khách hàng {customer.FullName} đăng ký tài khoản thành công.",
                CreatedAt = DateTime.Now
            });
            _context.SaveChanges();

            // Sau khi đăng ký → về trang Login
            TempData["Success"] = "Đăng ký thành công! Hãy đăng nhập để tiếp tục.";
            return RedirectToAction("Login", "Customer");
        }

        // ------------------ 🟢 ĐĂNG NHẬP ------------------
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Giữ returnUrl để sau đăng nhập xong quay lại trang trước
            var model = new LoginViewModel { ReturnUrl = returnUrl ?? Url.Action("Index", "Home") };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin.";
                return View(model);
            }

            // Tìm khách hàng
            var customer = _context.Customers.FirstOrDefault(c => c.Email == model.Email);

            if (customer == null)
            {
                ViewBag.Error = "Sai tài khoản hoặc mật khẩu!";
                return View(model);
            }

            bool checkPassword = false;

            try
            {
                checkPassword = BCrypt.Net.BCrypt.Verify(
                    model.Password,
                    customer.PasswordHash
                );
            }
            catch
            {
                ViewBag.Error = "Tài khoản này đăng nhập bằng Google!";
                return View(model);
            }

            if (!checkPassword)
            {
                ViewBag.Error = "Sai tài khoản hoặc mật khẩu!";
                return View(model);
            }

            // 🟩 Lưu thông tin session
            HttpContext.Session.SetInt32("CustomerId", customer.CustomerId);
            HttpContext.Session.SetString("CustomerName", customer.FullName);
            HttpContext.Session.SetString("CustomerEmail", customer.Email);

            // Cập nhật đăng nhập cuối của người dùng
            customer.LastLogin = DateTime.Now;
            
            // Ghi log hoạt động đăng nhập
            _context.UserActivityLogs.Add(new UserActivityLog
            {
                CustomerId = customer.CustomerId,
                SessionId = HttpContext.Session.Id,
                ActivityType = "LOGIN",
                Metadata = "Người dùng đăng nhập thành công.",
                CreatedAt = DateTime.Now
            });
            _context.SaveChanges();

            // 🟩 Điều hướng
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            else
                return RedirectToAction("Index", "Home");
        }

        // ------------------ 🟢 QUÊN MẬT KHẨU ------------------
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewBag.Error = "Vui lòng nhập email.";
                return View();
            }

            var customer = _context.Customers.FirstOrDefault(c => c.Email == email);
            if (customer == null)
            {
                ViewBag.Message = $"Nếu email {email} tồn tại, chúng tôi đã gửi hướng dẫn đặt lại mật khẩu.";
                return View();
            }

            // Ghi log hoạt động quên mật khẩu
            _context.UserActivityLogs.Add(new UserActivityLog
            {
                CustomerId = customer.CustomerId,
                SessionId = HttpContext.Session.Id,
                ActivityType = "FORGOT_PASSWORD",
                Metadata = "Khách hàng yêu cầu khôi phục mật khẩu.",
                CreatedAt = DateTime.Now
            });
            _context.SaveChanges();
 
            ViewBag.Message = $"Hướng dẫn đặt lại mật khẩu đã được gửi đến {email}.";
            return View();
        }

        [HttpGet]
        public IActionResult Logout()
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId.HasValue)
            {
                _context.UserActivityLogs.Add(new UserActivityLog
                {
                    CustomerId = customerId,
                    SessionId = HttpContext.Session.Id,
                    ActivityType = "LOGOUT",
                    Metadata = "Khách hàng đăng xuất.",
                    CreatedAt = DateTime.Now
                });
                _context.SaveChanges();
            }
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Customer");
        }

        private void CheckExpiredOrders()
        {
            var now = DateTime.Now;
            var expiredOrders = _context.Orders
                .Include(o => o.Tickets)
                .Where(o => (o.Status == "Chờ thanh toán" || o.Status == "Đang chờ thanh toán") && o.ExpiredAt <= now)
                .ToList();

            if (expiredOrders.Any())
            {
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
        }

        // ------------------ 🟢 HỒ SƠ CÁ NHÂN ------------------
        [HttpGet]
        public IActionResult Profile()
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
                return RedirectToAction("Login", "Customer");

            CheckExpiredOrders();

            var customer = _context.Customers
                .Include(c => c.Orders)
                    .ThenInclude(o => o.Tickets)
                        .ThenInclude(t => t.Seat)
                .Include(c => c.Orders)
                    .ThenInclude(o => o.Tickets)
                        .ThenInclude(t => t.Showtime)
                            .ThenInclude(s => s.Movie)
                .Include(c => c.Orders)
                    .ThenInclude(o => o.Tickets)
                        .ThenInclude(t => t.Showtime)
                            .ThenInclude(s => s.Auditorium)
                .Include(c => c.Orders)
                    .ThenInclude(o => o.OrderCombos)
                        .ThenInclude(oc => oc.Combo)
                .FirstOrDefault(c => c.CustomerId == customerId);

            if (customer == null)
                return RedirectToAction("Login", "Customer");

            return View(customer);
        }
        [HttpGet]
        public IActionResult EditProfile()
        {
            var userId = HttpContext.Session.GetInt32("CustomerId");
            var customer = _context.Customers.Find(userId);
            return View(customer);
        }
        [HttpPost]
        public IActionResult EditProfile(Customer model, IFormFile avatarFile)
        {
            var userId = HttpContext.Session.GetInt32("CustomerId");
            var customer = _context.Customers.Find(userId);

            if (customer == null) return NotFound();

            customer.FullName = model.FullName;
            customer.Phone = model.Phone;

            if (avatarFile != null && avatarFile.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName);
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    avatarFile.CopyTo(stream);
                }

                customer.Avatar = "/images/" + fileName;
            }

            _context.SaveChanges();

            // Ghi log hoạt động cập nhật hồ sơ
            _context.UserActivityLogs.Add(new UserActivityLog
            {
                CustomerId = customer.CustomerId,
                SessionId = HttpContext.Session.Id,
                ActivityType = "EDIT_PROFILE",
                Metadata = "Khách hàng cập nhật thông tin cá nhân.",
                CreatedAt = DateTime.Now
            });
            _context.SaveChanges();
 
            return RedirectToAction("Profile");
        }
        // ================= LOGIN GOOGLE =================

        // ================= LOGIN GOOGLE =================

        public IActionResult LoginGoogle(string? returnUrl = null)
        {
            var redirectUrl = Url.Action(
                "GoogleResponse",
                "Customer",
                new { returnUrl }
            );

            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl
            };

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        public async Task<IActionResult> GoogleResponse(string? returnUrl = null)
        {
            var result = await HttpContext.AuthenticateAsync();

            if (!result.Succeeded || result.Principal == null)
            {
                return RedirectToAction("Login");
            }

            var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var name = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login");
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(x => x.Email == email);

            if (customer == null)
            {
                customer = new Customer
                {
                    Email = email,
                    FullName = name ?? email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    CreatedAt = DateTime.Now
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            HttpContext.Session.SetInt32("CustomerId", customer.CustomerId);
            HttpContext.Session.SetString("CustomerName", customer.FullName);
            HttpContext.Session.SetString("CustomerEmail", customer.Email);

            // Cập nhật đăng nhập cuối của người dùng
            customer.LastLogin = DateTime.Now;

            // Ghi log hoạt động đăng nhập
            _context.UserActivityLogs.Add(new UserActivityLog
            {
                CustomerId = customer.CustomerId,
                SessionId = HttpContext.Session.Id,
                ActivityType = "LOGIN",
                Metadata = "Người dùng đăng nhập thành công qua Google.",
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl)
                && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
