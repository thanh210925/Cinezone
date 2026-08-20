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

        // 🎨 Thiết kế sơ đồ ghế (GET)
        public IActionResult DesignLayout(int id)
        {
            var auditorium = _context.Auditoriums
                .Include(a => a.Theater)
                .FirstOrDefault(a => a.AuditoriumId == id);
            if (auditorium == null) return NotFound();

            var seats = _context.Seats
                .Where(s => s.AuditoriumId == id)
                .OrderBy(s => s.RowLabel)
                .ThenBy(s => s.SeatNumber)
                .ToList();

            ViewBag.Seats = seats;
            return View(auditorium);
        }

        // 🎨 Lưu sơ đồ ghế từ JSON (POST)
        [HttpPost]
        public IActionResult SaveLayout(int id, [FromBody] List<SeatLayoutDto> seats)
        {
            var auditorium = _context.Auditoriums.Find(id);
            if (auditorium == null) 
                return Json(new { success = false, message = "Không tìm thấy phòng chiếu." });

            if (seats == null)
                return Json(new { success = false, message = "Dữ liệu sơ đồ ghế trống." });

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    // 1. Lấy danh sách ghế hiện tại trong DB
                    var existingDbSeats = _context.Seats.Where(s => s.AuditoriumId == id).ToList();

                    // 2. Tạo tập hợp các ghế mới để đối chiếu nhanh
                    var newSeatsMap = seats.ToDictionary(s => $"{s.RowLabel}_{s.SeatNumber}");

                    // 3. Cập nhật hoặc vô hiệu hóa các ghế cũ
                    foreach (var dbSeat in existingDbSeats)
                    {
                        var key = $"{dbSeat.RowLabel}_{dbSeat.SeatNumber}";
                        if (newSeatsMap.TryGetValue(key, out var newSeatDto))
                        {
                            // Ghế vẫn tồn tại trong sơ đồ mới -> Cập nhật thông tin
                            dbSeat.SeatType = newSeatDto.SeatType;
                            dbSeat.IsActive = newSeatDto.IsActive;
                            _context.Seats.Update(dbSeat);
                        }
                        else
                        {
                            // Ghế không còn trong sơ đồ mới (bị chuyển thành ô trống)
                            // Kiểm tra xem ghế đã có vé nào chưa
                            bool hasTickets = _context.Tickets.Any(t => t.SeatId == dbSeat.SeatId);
                            if (hasTickets)
                            {
                                // Nếu đã có vé, không thể xóa -> Chỉ ẩn đi (IsActive = false)
                                dbSeat.IsActive = false;
                                _context.Seats.Update(dbSeat);
                            }
                            else
                            {
                                // Nếu chưa có vé, xóa hẳn khỏi DB
                                _context.Seats.Remove(dbSeat);
                            }
                        }
                    }

                    // 4. Thêm các ghế mới hoàn toàn
                    var existingDbSeatsKeys = existingDbSeats.Select(s => $"{s.RowLabel}_{s.SeatNumber}").ToHashSet();
                    int maxRowIdx = 0;
                    int maxColIdx = 0;

                    foreach (var s in seats)
                    {
                        var key = $"{s.RowLabel}_{s.SeatNumber}";
                        if (!existingDbSeatsKeys.Contains(key))
                        {
                            var seat = new Seat
                            {
                                AuditoriumId = id,
                                RowLabel = s.RowLabel,
                                SeatNumber = s.SeatNumber,
                                SeatType = s.SeatType,
                                IsActive = s.IsActive
                            };
                            _context.Seats.Add(seat);
                        }

                        // Tính kích thước grid lớn nhất
                        if (!string.IsNullOrEmpty(s.RowLabel))
                        {
                            int curRowIdx = s.RowLabel[0] - 'A' + 1;
                            if (curRowIdx > maxRowIdx) maxRowIdx = curRowIdx;
                        }
                        if (s.SeatNumber > maxColIdx) maxColIdx = s.SeatNumber;
                    }
                    _context.SaveChanges();

                    // 5. Cập nhật số hàng & số cột phòng chiếu
                    auditorium.SeatRows = maxRowIdx;
                    auditorium.SeatCols = maxColIdx;
                    _context.Auditoriums.Update(auditorium);
                    _context.SaveChanges();

                    transaction.Commit();
                    return Json(new { success = true, message = "Lưu sơ đồ ghế thành công!" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Lỗi khi lưu sơ đồ: " + ex.Message });
                }
            }
        }
    }

    public class SeatLayoutDto
    {
        public string RowLabel { get; set; }
        public int SeatNumber { get; set; }
        public string SeatType { get; set; }
        public bool IsActive { get; set; }
    }
}