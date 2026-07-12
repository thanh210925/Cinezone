using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace CINEMA.Controllers
{
    public class ReviewsController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public ReviewsController(CinemaContext context)
        {
            _context = context;
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
        public IActionResult Approve(int id)
        {
            var review = _context.Reviews.Find(id);
            if (review == null) return NotFound();

            review.IsHidden = false;
            // Xóa cờ báo cáo khi được duyệt
            review.HasReport = false;
            review.ReportReason = null;

            _context.SaveChanges();
            TempData["SuccessMessage"] = "Đã duyệt và hiển thị lại đánh giá.";
            return RedirectToAction(nameof(Index));
        }

        // 👁️ ẨN ĐÁNH GIÁ (Set IsHidden = true)
        [HttpPost]
        public IActionResult Hide(int id)
        {
            var review = _context.Reviews.Find(id);
            if (review == null) return NotFound();

            review.IsHidden = true;
            _context.SaveChanges();
            TempData["SuccessMessage"] = "Đã ẩn đánh giá khỏi giao diện người dùng.";
            return RedirectToAction(nameof(Index));
        }

        // 💬 PHẢN HỒI ĐÁNH GIÁ (Reply)
        [HttpPost]
        public IActionResult Reply(int id, string replyText)
        {
            var review = _context.Reviews.Find(id);
            if (review == null) return NotFound();

            review.AdminReply = replyText;
            _context.SaveChanges();
            TempData["SuccessMessage"] = "Đã gửi phản hồi cho khách hàng.";
            return RedirectToAction(nameof(Index));
        }

        // 🗑️ XÓA ĐÁNH GIÁ
        [HttpPost]
        public IActionResult Delete(int id)
        {
            var review = _context.Reviews.Find(id);
            if (review == null) return NotFound();

            _context.Reviews.Remove(review);
            _context.SaveChanges();
            TempData["SuccessMessage"] = "Đã xóa đánh giá vĩnh viễn.";
            return RedirectToAction(nameof(Index));
        }
    }
}
