using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CINEMA.Controllers
{
    public class AdminVoucherController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public AdminVoucherController(CinemaContext context)
        {
            _context = context;
        }

        // 📋 DANH SÁCH
        public IActionResult Index()
        {
            var vouchers = _context.Vouchers
                .Include(v => v.VoucherCondition)
                .OrderByDescending(v => v.VoucherId)
                .ToList();
            return View(vouchers);
        }

        // ➕ CREATE
        public IActionResult Create()
        {
            ViewBag.VoucherConditions = _context.VoucherConditions.ToList();
            return View();
        }

        [HttpPost]
        public IActionResult Create(Voucher v)
        {
            if (string.IsNullOrEmpty(v.Code))
            {
                ModelState.AddModelError("", "Mã voucher không được để trống");
                ViewBag.VoucherConditions = _context.VoucherConditions.ToList();
                return View(v);
            }

            if (_context.Vouchers.Any(x => x.Code == v.Code))
            {
                ModelState.AddModelError("", "Mã voucher đã tồn tại");
                ViewBag.VoucherConditions = _context.VoucherConditions.ToList();
                return View(v);
            }

            if (v.StartDate.HasValue && v.EndDate.HasValue && v.EndDate.Value < v.StartDate.Value)
            {
                ModelState.AddModelError("", "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu");
                ViewBag.VoucherConditions = _context.VoucherConditions.ToList();
                return View(v);
            }

            v.UsedCount = 0;
            v.IsActive = true;

            _context.Vouchers.Add(v);
            _context.SaveChanges();

            TempData["Success"] = "Tạo voucher thành công!";
            return RedirectToAction("Index");
        }

        // ✏️ EDIT
        public IActionResult Edit(int id)
        {
            var v = _context.Vouchers.Find(id);
            if (v == null) return NotFound();

            ViewBag.VoucherConditions = _context.VoucherConditions.ToList();
            return View(v);
        }

        [HttpPost]
        public IActionResult Edit(Voucher v)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.VoucherConditions = _context.VoucherConditions.ToList();
                return View(v);
            }

            if (v.StartDate.HasValue && v.EndDate.HasValue && v.EndDate.Value < v.StartDate.Value)
            {
                ModelState.AddModelError("", "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu");
                ViewBag.VoucherConditions = _context.VoucherConditions.ToList();
                return View(v);
            }

            var existing = _context.Vouchers.Find(v.VoucherId);

            if (existing == null) return NotFound();

            existing.Code = v.Code;
            existing.DiscountPercent = v.DiscountPercent;
            existing.DiscountAmount = v.DiscountAmount;
            existing.MinOrderValue = v.MinOrderValue;
            existing.StartDate = v.StartDate;
            existing.EndDate = v.EndDate;
            existing.ExpiryDate = v.ExpiryDate;
            existing.Quantity = v.Quantity;
            existing.IsActive = v.IsActive;

            // Cập nhật thêm phạm vi áp dụng (điều kiện)
            existing.VoucherConditionId = v.VoucherConditionId;
            existing.TermsAndConditions = v.TermsAndConditions;

            _context.SaveChanges();

            TempData["Success"] = "Cập nhật thành công!";
            return RedirectToAction("Index");
        }

        // ❌ DELETE
        public IActionResult Delete(int id)
        {
            var v = _context.Vouchers.Find(id);
            if (v == null) return NotFound();

            _context.Vouchers.Remove(v);
            _context.SaveChanges();

            TempData["Success"] = "Xóa thành công!";
            return RedirectToAction("Index");
        }
    }
}