using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks; // Thêm thư viện Task
using CINEMA.Services; // Thêm namespace chứa IMovieService
using Microsoft.AspNetCore.Http;

namespace CINEMA.Controllers
{
    public class ReviewsController : AdminBaseController
    {
        private readonly CinemaContext _context;
        private readonly IMovieService _movieService; // Khai báo service tính điểm

        // Inject service vào constructor
        public ReviewsController(CinemaContext context, IMovieService movieService)
        {
            _context = context;
            _movieService = movieService;
        }

        // 📋 DANH SÁCH BÌNH LUẬN / ĐÁNH GIÁ (Admin)
        public IActionResult Index(bool? hasReport, bool? isHidden)
        {
            var query = _context.Reviews
                .Include(r => r.Movie)
                .Include(r => r.Customer)
                .AsQueryable();

            if (hasReport == true)
            {
                query = query.Where(r => r.HasReport == true);
            }
            if (isHidden == true)
            {
                query = query.Where(r => r.IsHidden == true);
            }

            var reviews = query.OrderByDescending(r => r.CreatedAt).ToList();

            ViewBag.HasReportFilter = hasReport;
            ViewBag.IsHiddenFilter = isHidden;

            return View(reviews);
        }

        // 🔒 DUYỆT / HIỆN ĐÁNH GIÁ (Set IsHidden = false)
        [HttpPost]
        public async Task<IActionResult> Approve(int id) // Đổi thành async
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound();

            review.IsHidden = false;
            // Xóa cờ báo cáo khi được duyệt
            review.HasReport = false;
            review.ReportReason = null;

            await _context.SaveChangesAsync();

            // Cập nhật lại điểm: Review hiện lại thì phải tính gộp vào điểm trung bình
            await _movieService.UpdateMovieBayesianRatingAsync(review.MovieId);

            TempData["SuccessMessage"] = "Đã duyệt và hiển thị lại đánh giá.";
            return RedirectToAction(nameof(Index));
        }

        // 👁️ ẨN ĐÁNH GIÁ (Set IsHidden = true)
        [HttpPost]
        public async Task<IActionResult> Hide(int id) // Đổi thành async
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound();

            review.IsHidden = true;
            await _context.SaveChangesAsync();

            // Cập nhật lại điểm: Review bị ẩn đi phải trừ ra khỏi điểm trung bình
            await _movieService.UpdateMovieBayesianRatingAsync(review.MovieId);

            TempData["SuccessMessage"] = "Đã ẩn đánh giá khỏi giao diện người dùng.";
            return RedirectToAction(nameof(Index));
        }

        // 💬 PHẢN HỒI ĐÁNH GIÁ (Reply)
        [HttpPost]
        public async Task<IActionResult> Reply(int id, string replyText) // Đồng bộ code async
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound();

            review.AdminReply = replyText;
            await _context.SaveChangesAsync();

            // Phản hồi không làm thay đổi điểm số, nên không cần gọi service tính điểm

            TempData["SuccessMessage"] = "Đã gửi phản hồi cho khách hàng.";
            return RedirectToAction(nameof(Index));
        }

        // 📝 THÊM ĐÁNH GIÁ MỚI (Luôn luôn tạo mới 1 cmt độc lập)
        [HttpPost]
        public async Task<IActionResult> AddReview(int movieId, int rating, string comment, int? orderId)
        {
            // 1. Xử lý ID người dùng (Đã đăng nhập hoặc Khách ẩn danh)
            var customerId = GetCurrentCustomerId();
            int finalCustomerId;

            if (!customerId.HasValue)
            {
                // Nếu chưa đăng nhập: Tự động gom vào tài khoản "Khách ẩn danh" chung của hệ thống CineZone
                var guestAccount = _context.Customers.FirstOrDefault(c => c.Email == "anonymous@cinezone.com");
                if (guestAccount == null)
                {
                    guestAccount = new Customer
                    {
                        FullName = "Khách ẩn danh",
                        Email = "anonymous@cinezone.com",
                        PasswordHash = "GUEST_NONE",
                        CreatedAt = DateTime.Now,
                        MembershipLevel = "Đồng",
                        TotalSpent = 0
                    };
                    _context.Customers.Add(guestAccount);
                    await _context.SaveChangesAsync();
                }
                finalCustomerId = guestAccount.CustomerId;
            }
            else
            {
                finalCustomerId = customerId.Value;
            }

            // 2. Kiểm tra số sao hợp lệ
            if (rating < 1 || rating > 5)
            {
                return Json(new { success = false, message = "Đánh giá sao phải từ 1 đến 5." });
            }

            // 3. LUÔN LUÔN TẠO MỚI (Bỏ hoàn toàn logic kiểm tra trùng cũ)
            var review = new Review
            {
                MovieId = movieId,
                CustomerId = finalCustomerId,
                OrderId = orderId,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.Now,
                IsHidden = false,
                LikesCount = 0
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync(); // Lưu trực tiếp bản ghi mới vào cơ sở dữ liệu

            // 4. Gọi thuật toán tự động cộng dồn lượt vote mới này và tính lại BayesianRating
            await _movieService.UpdateMovieBayesianRatingAsync(movieId);

            return Json(new { success = true, message = "Đã gửi đánh giá mới thành công!" });
        }

        // 🗑️ XÓA ĐÁNH GIÁ
        [HttpPost]
        public async Task<IActionResult> Delete(int id) // Đổi thành async
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound();

            int movieId = review.MovieId; // Lấy ra MovieId trước khi đối tượng review bị xóa khỏi database

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            // Cập nhật lại điểm sau khi xóa vĩnh viễn
            await _movieService.UpdateMovieBayesianRatingAsync(movieId);

            TempData["SuccessMessage"] = "Đã xóa đánh giá vĩnh viễn.";
            return RedirectToAction(nameof(Index));
        }

        // >>> HÀM TRỢ GIÚP: Đã sửa thành hàm non-static chuẩn kiểu int? để đọc Session mượt mà <<<
        private int? GetCurrentCustomerId()
        {
            return HttpContext.Session.GetInt32("CustomerId");
        }
    }
}