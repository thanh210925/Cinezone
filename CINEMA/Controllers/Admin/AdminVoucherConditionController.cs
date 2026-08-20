using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CINEMA.Controllers
{
    public class AdminVoucherConditionController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public AdminVoucherConditionController(CinemaContext context)
        {
            _context = context;
        }

        // 📋 DANH SÁCH PHẠM VI/ĐIỀU KIỆN
        public IActionResult Index()
        {
            var conditions = _context.VoucherConditions
                .Include(c => c.Rules)
                .OrderByDescending(c => c.VoucherConditionId)
                .ToList();
            return View(conditions);
        }

        // ➕ CREATE
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(VoucherCondition condition)
        {
            if (string.IsNullOrEmpty(condition.Name))
            {
                ModelState.AddModelError("Name", "Tên phạm vi áp dụng không được để trống");
                return View(condition);
            }

            _context.VoucherConditions.Add(condition);
            _context.SaveChanges();

            TempData["Success"] = "Tạo phạm vi áp dụng thành công!";
            return RedirectToAction("Index");
        }

        // ✏️ EDIT
        public IActionResult Edit(int id)
        {
            var condition = _context.VoucherConditions
                .Include(c => c.Rules)
                .FirstOrDefault(c => c.VoucherConditionId == id);
            if (condition == null) return NotFound();

            return View(condition);
        }

        [HttpPost]
        public IActionResult Edit(VoucherCondition condition)
        {
            if (string.IsNullOrEmpty(condition.Name))
            {
                ModelState.AddModelError("Name", "Tên phạm vi áp dụng không được để trống");
                return View(condition);
            }

            var existing = _context.VoucherConditions
                .Include(c => c.Rules)
                .FirstOrDefault(c => c.VoucherConditionId == condition.VoucherConditionId);
            if (existing == null) return NotFound();

            existing.Name = condition.Name;
            existing.Description = condition.Description;

            // Xóa toàn bộ quy tắc cũ
            _context.VoucherRules.RemoveRange(existing.Rules);

            // Thêm các quy tắc mới
            if (condition.Rules != null)
            {
                foreach (var rule in condition.Rules)
                {
                    existing.Rules.Add(new VoucherRule
                    {
                        Field = rule.Field,
                        Operator = rule.Operator,
                        Value = rule.Value
                    });
                }
            }

            _context.SaveChanges();

            TempData["Success"] = "Cập nhật thành công!";
            return RedirectToAction("Index");
        }

        // ❌ DELETE
        public IActionResult Delete(int id)
        {
            var condition = _context.VoucherConditions.Find(id);
            if (condition == null) return NotFound();

            _context.VoucherConditions.Remove(condition);
            _context.SaveChanges();

            TempData["Success"] = "Xóa thành công!";
            return RedirectToAction("Index");
        }
    }
}
