using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class AuditoriumController : AdminBaseController
    {
        private readonly CinemaContext _context;

        public AuditoriumController(CinemaContext context)
        {
            _context = context;
        }

        // 📋 Danh sách phòng chiếu
        public IActionResult Index()
        {
            // Load danh sách phòng kèm rạp
            var auditoriums = _context.Auditoriums.Include(a => a.Theater).ToList();

            // Load danh sách rạp để hiển thị tab
            ViewBag.Theaters = _context.Theaters.ToList();

            return View(auditoriums);
        }


        // ➕ Trang thêm mới (GET)
        public IActionResult Create()
        {
            ViewBag.Theaters = _context.Theaters.ToList();
            return View();
        }

        // 🚀 CẬP NHẬT LOGIC LƯU HÀNG LOẠT (POST)
        [HttpPost]
        public IActionResult Create(Auditorium model, List<int> TheaterIds)
        {
            // 1. Kiểm tra mảng ID rạp chiếu
            if (TheaterIds == null || !TheaterIds.Any())
            {
                ModelState.AddModelError(string.Empty, "Vui lòng chọn ít nhất một rạp chiếu.");
            }

            // 2. Loại bỏ kiểm tra hợp lệ cho thuộc tính TheaterId mặc định
            // (Vì đối tượng 'model' không chứa TheaterId do form gửi lên dạng mảng TheaterIds)
            ModelState.Remove("TheaterId");

            if (ModelState.IsValid)
            {
                // 3. Lặp qua từng Rạp được chọn
                foreach (var theaterId in TheaterIds)
                {
                    // Tạo mới instance phòng chiếu cho rạp hiện tại
                    var newAuditorium = new Auditorium
                    {
                        Name = model.Name,
                        SeatRows = model.SeatRows,
                        SeatCols = model.SeatCols,
                        ScreenType = model.ScreenType,
                        IsActive = model.IsActive,
                        TheaterId = theaterId // 👈 Gán ID rạp từ mảng
                    };

                    _context.Auditoriums.Add(newAuditorium);
                    _context.SaveChanges(); // Lưu để EF Core tự sinh ra khóa chính (AuditoriumId)

                    // ✅ Sinh ghế tự động cho phòng chiếu vừa được lưu
                    int rows = newAuditorium.SeatRows ?? 0;
                    int cols = newAuditorium.SeatCols ?? 0;

                    for (int r = 0; r < rows; r++)
                    {
                        char rowLabel = (char)('A' + r);
                        for (int c = 1; c <= cols; c++)
                        {
                            _context.Seats.Add(new Seat
                            {
                                AuditoriumId = newAuditorium.AuditoriumId, // Dùng ID mới sinh
                                RowLabel = rowLabel.ToString(),
                                SeatNumber = c,
                                SeatType = "Thường",
                                IsActive = true
                            });
                        }
                    }
                    // Lưu toàn bộ ghế của phòng này vào Database
                    _context.SaveChanges();
                }

                return RedirectToAction(nameof(Index));
            }

            // Nếu có lỗi, load lại dữ liệu cho form
            ViewBag.Theaters = _context.Theaters.ToList();
            return View(model);
        }


        // ✏️ Sửa
        public IActionResult Edit(int id)
        {
            var auditorium = _context.Auditoriums.Find(id);
            if (auditorium == null) return NotFound();

            ViewBag.Theaters = _context.Theaters.ToList();
            return View(auditorium);
        }

        [HttpPost]
        public IActionResult Edit(Auditorium auditorium)
        {
            if (ModelState.IsValid)
            {
                _context.Auditoriums.Update(auditorium);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Theaters = _context.Theaters.ToList();
            return View(auditorium);
        }

        // 🗑️ Xóa (Ngừng hoạt động / Tạm dừng)
        public IActionResult Delete(int id)
        {
            var auditorium = _context.Auditoriums
                .Include(a => a.Theater)
                .FirstOrDefault(a => a.AuditoriumId == id);

            if (auditorium == null) return NotFound();
            return View(auditorium);
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            var auditorium = _context.Auditoriums.Find(id);
            if (auditorium == null) return NotFound();

            try
            {
                // Thực hiện SOFT DELETE (Tạm dừng hoạt động) để bảo toàn ghế và lịch sử suất chiếu/vé
                auditorium.IsActive = false;
                _context.SaveChanges();
                TempData["SuccessMessage"] = $"Đã ngừng hoạt động phòng chiếu '{auditorium.Name}' thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Đã xảy ra lỗi hệ thống: " + ex.Message;
                return RedirectToAction(nameof(Delete), new { id = id });
            }

            return RedirectToAction(nameof(Index));
        }
    }
}