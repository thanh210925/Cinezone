using CINEMA.Controllers;
using CINEMA.Helpers; // Dòng này là bắt buộc để gọi được LogHelper
using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
namespace CINEMA.Controllers
{
    public class MovieController : AdminBaseController
    {
        private readonly CinemaContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Services.IEmailService _emailService;
        public MovieController(CinemaContext context, IHttpContextAccessor httpContextAccessor, Services.IEmailService emailService)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _emailService = emailService;
        }

        // ==================== DANH SÁCH PHIM ====================
        public IActionResult Index()
        {
            var movies = _context.Movies
                .OrderByDescending(m => m.ReleaseDate)
                .ToList();

            return View(movies);
        }

        // ==================== CHI TIẾT PHIM ====================
        public IActionResult Details(int id)
        {
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == id);
            if (movie == null) return NotFound();

            return View(movie);
        }

        // ==================== THÊM PHIM (GET) ====================
        [HttpGet]
        public IActionResult Create()
        {

            return View();
        }

        // ==================== THÊM PHIM (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateMultiple(MovieListViewModel model)
        {
            // Lọc những dòng có nhập Tên phim
            var validMovies = model.Movies.Where(m => !string.IsNullOrWhiteSpace(m.Title)).ToList();

            if (!validMovies.Any())
            {
                TempData["ErrorMessage"] = "Vui lòng nhập ít nhất một bộ phim!";
                return RedirectToAction("Index");
            }

            foreach (var movie in validMovies)
            {
                movie.IsActive = true;
                movie.CreatedAt = DateTime.Now;
                _context.Movies.Add(movie);
            }

            _context.SaveChanges();

            // Gửi email thông báo bất đồng bộ chạy ngầm cho tất cả khách hàng
            try
            {
                var customers = _context.Customers.Where(c => !string.IsNullOrEmpty(c.Email)).ToList();
                if (customers.Any())
                {
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    // Nhân bản list phim để tránh EF DbContext reference issues trong đa luồng
                    var moviesToNotify = validMovies.Select(m => new Movie
                    {
                        Title = m.Title,
                        Description = m.Description,
                        Duration = m.Duration,
                        ReleaseDate = m.ReleaseDate,
                        Language = m.Language,
                        Country = m.Country,
                        AgeRating = m.AgeRating,
                        PosterUrl = m.PosterUrl
                    }).ToList();

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendNewMovieNotificationAsync(moviesToNotify, customers, baseUrl);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Notification Task Error]: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Email Notification Setup Error]: {ex.Message}");
            }

            // Ghi log chung
            LogHelper.Write(_context, _httpContextAccessor, "ADDED_MULTIPLE", "Movie", validMovies.Count);

            TempData["SuccessMessage"] = $"🎉 Đã thêm {validMovies.Count} phim thành công!";
            return RedirectToAction("Index");
        }
        [HttpPost]
        public async Task<IActionResult> ImportExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn file Excel!";
                return RedirectToAction(nameof(Create));
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            ExcelPackage.License.SetNonCommercialPersonal("CineZone");
            using var package = new ExcelPackage(stream);
            var sheet = package.Workbook.Worksheets[0];
            int rowCount = sheet.Dimension.Rows;
            int importedCount = 0; // Đếm số phim thực tế đã thêm

            var importedMovies = new List<Movie>();
            for (int row = 2; row <= rowCount; row++)
            {
                var movie = new Movie
                {
                    Title = sheet.Cells[row, 1].Text,
                    // Thêm check để tránh lỗi khi dữ liệu trống
                    Duration = int.TryParse(sheet.Cells[row, 2].Text, out int dur) ? dur : 0,
                    Country = sheet.Cells[row, 3].Text,
                    Language = sheet.Cells[row, 4].Text,
                    AgeRating = sheet.Cells[row, 5].Text,
                    Description = sheet.Cells[row, 6].Text,
                    PosterUrl = sheet.Cells[row, 7].Text,
                    TrailerUrl = sheet.Cells[row, 8].Text,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.Movies.Add(movie);
                importedMovies.Add(movie);
                importedCount++;
            }

            await _context.SaveChangesAsync();

            // Gửi email thông báo bất đồng bộ chạy ngầm cho tất cả khách hàng
            try
            {
                var customers = await _context.Customers.Where(c => !string.IsNullOrEmpty(c.Email)).ToListAsync();
                if (customers.Any() && importedMovies.Any())
                {
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    // Nhân bản list phim để tránh EF DbContext reference issues trong đa luồng
                    var moviesToNotify = importedMovies.Select(m => new Movie
                    {
                        Title = m.Title,
                        Description = m.Description,
                        Duration = m.Duration,
                        ReleaseDate = m.ReleaseDate,
                        Language = m.Language,
                        Country = m.Country,
                        AgeRating = m.AgeRating,
                        PosterUrl = m.PosterUrl
                    }).ToList();

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendNewMovieNotificationAsync(moviesToNotify, customers, baseUrl);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Notification Task Error]: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Email Notification Setup Error]: {ex.Message}");
            }

            // Đã sửa lỗi: dùng importedCount thay cho validMovies (vốn không tồn tại ở hàm này)
            LogHelper.Write(_context, _httpContextAccessor, "ADDED_MULTIPLE", "Movie", importedCount);

            TempData["SuccessMessage"] = $"✅ Đã import thành công {importedCount} bộ phim!";
            return RedirectToAction(nameof(Index));
        }
        // ==================== SỬA PHIM (GET) ====================
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var movie = _context.Movies.Find(id);
            if (movie == null) return NotFound();

            return View(movie);
        }

        // ==================== SỬA PHIM (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Movie updatedMovie, IFormFile? PosterImage)
        {
            // 1. Kiểm tra tính hợp lệ
            if (!ModelState.IsValid)
                return View(updatedMovie);

            // 2. Lấy bản gốc từ DB
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == updatedMovie.MovieId);
            if (movie == null) return NotFound();

            // 3. Cập nhật tất cả các trường cùng lúc (Ngoại trừ ID và các trường hệ thống không đổi)
            // Lưu ý: Nếu PosterUrl không nằm trong form, nó sẽ bị ghi đè thành null.
            // Cách này sẽ cập nhật ĐẦY ĐỦ các trường, giải quyết triệt để lỗi "không lưu được"
            _context.Entry(movie).CurrentValues.SetValues(updatedMovie);

            // 4. Xử lý ảnh (Chỉ cập nhật nếu có ảnh mới)
            if (PosterImage != null && PosterImage.Length > 0)
            {
                var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "movies");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                // Lưu ý: Nên dùng GUID để tránh trùng tên file ảnh
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(PosterImage.FileName);
                var filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    PosterImage.CopyTo(stream);
                }
                movie.PosterUrl = "/images/movies/" + fileName;
            }

            // 5. Lưu DB (Chỉ cần 1 lần)
            _context.SaveChanges();

            LogHelper.Write(_context, _httpContextAccessor, "MODIFIED", "Movie", movie.MovieId);
            TempData["SuccessMessage"] = "✏️ Cập nhật phim thành công!";
            return RedirectToAction(nameof(Index));
        }
        // ==================== XÓA PHIM (GET) ====================
        [HttpGet]
        public IActionResult Delete(int id)
        {
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == id);
            if (movie == null) return NotFound();

            return View(movie);
        }

        // ==================== XÓA PHIM (POST) ====================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var movie = _context.Movies
                .Include(m => m.Showtimes)
                    .ThenInclude(s => s.Tickets)
                .FirstOrDefault(m => m.MovieId == id);

            if (movie == null) return NotFound();

            // Xóa vé + suất chiếu
            if (movie.Showtimes != null)
            {
                foreach (var show in movie.Showtimes)
                {
                    if (show.Tickets?.Any() == true)
                        _context.Tickets.RemoveRange(show.Tickets);
                }

                _context.Showtimes.RemoveRange(movie.Showtimes);
            }

            _context.Movies.Remove(movie);
            _context.SaveChanges();
            LogHelper.Write(_context, _httpContextAccessor, "DELETED", "Movie", id);
            TempData["SuccessMessage"] = $"🗑️ Đã xóa phim \"{movie.Title}\" cùng toàn bộ suất chiếu!";
            return RedirectToAction(nameof(Index));
        }

        // ==================== TEST GỬI EMAIL ====================
        [HttpGet]
        public async Task<IActionResult> TestSendEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return Content("Vui lòng truyền ?email=... trên URL. Ví dụ: /Movie/TestSendEmail?email=test@gmail.com");
            }

            try
            {
                await _emailService.SendEmailAsync(
                    email, 
                    "🎬 CineZone - Test Email Connection", 
                    "<h1>Kết nối SMTP thành công!</h1><p>Tính năng gửi email thông báo của CineZone hoạt động tốt.</p>"
                );
                return Content($"✅ Gửi email test đến {email} thành công! Hãy kiểm tra hộp thư của bạn.");
            }
            catch (Exception ex)
            {
                return Content($"❌ Gửi email thất bại. Lỗi chi tiết: {ex.Message}");
            }
        }
    }
}
