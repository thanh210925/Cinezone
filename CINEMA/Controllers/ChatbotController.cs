using CINEMA.Models;
using CINEMA.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace CINEMA.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly CinemaContext _context;
        private readonly GeminiService _gemini;

        public ChatbotController(CinemaContext context, GeminiService gemini)
        {
            _context = context;
            _gemini = gemini;
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Message))
                return Json("🤖 Bạn hãy nhập câu hỏi nhé!");

            // Khai báo originalMsg để giữ nguyên câu hỏi gốc truyền cho AI
            var originalMsg = req.Message.Trim();
            var msg = RemoveVietnameseTone(originalMsg.ToLower());
            var today = DateOnly.FromDateTime(DateTime.Now);

            // =====================================================
            // 1. CHỈ XỬ LÝ KHI BẤM NÚT GỢI Ý (Exact Match)
            // =====================================================
            if (msg == "phim dang chieu")
            {
                var movies = _context.Movies
                    .Where(m => (m.IsActive ?? false) && m.ReleaseDate != null && m.ReleaseDate <= today)
                    .OrderByDescending(m => m.ReleaseDate)
                    .Select(m => m.Title)
                    .Take(10).ToList();

                if (!movies.Any()) return Json("😢 Hiện tại rạp chưa có phim nào đang chiếu.");
                return Json("🎬 Phim đang chiếu:<br>- " + string.Join("<br>- ", movies));
            }

            if (msg == "lich chieu")
            {
                var shows = _context.Showtimes.Include(s => s.Movie)
                    .Where(s => (s.IsActive ?? false) && s.StartTime >= DateTime.Now)
                    .OrderBy(s => s.StartTime).Take(5).ToList();

                if (!shows.Any()) return Json("😢 Rất tiếc, hiện tại chưa có lịch chiếu nào sắp tới.");

                var result = shows.Select(s =>
                    $"<div style='margin-bottom:10px'>🎬 <b>{s.Movie.Title}</b><br>⏰ {s.StartTime:HH:mm dd/MM}<br><a href='/Home/BookTicket?id={s.MovieId}&showtimeId={s.ShowtimeId}' style='color:#198754;font-weight:bold'>🎟 Đặt vé</a></div>");
                return Json("⏰ Lịch chiếu sắp tới:<br>" + string.Join("<br><br>", result));
            }

            if (msg == "ghe trong")
            {
                int totalSeats = _context.Seats.Count();
                int booked = _context.Tickets.Count();
                return Json($"💺 Hiện hệ thống đang còn khoảng <b>{totalSeats - booked}</b> ghế trống");
            }

            if (msg == "phim sap chieu" || msg == "sap ra")
            {
                var upcoming = _context.Movies
                    .Where(m => (m.IsActive ?? false) && m.ReleaseDate != null && m.ReleaseDate > today)
                    .OrderBy(m => m.ReleaseDate).Select(m => m.Title).Take(5).ToList();

                if (!upcoming.Any()) return Json("😢 Hiện tại rạp CineZone chưa cập nhật danh sách phim sắp chiếu.");
                return Json("🍿 Những siêu phẩm sắp đổ bộ rạp CineZone:<br>- " + string.Join("<br>- ", upcoming));
            }

            // =====================================================
            // 2. TÌM TÊN PHIM TRỰC TIẾP TRONG CÂU TRẢ LỜI
            // =====================================================
            var exactMovie = _context.Movies.AsEnumerable().FirstOrDefault(m =>
            {
                var title = RemoveVietnameseTone(m.Title.ToLower());
                // Câu nói của user phải chứa TOÀN BỘ cụm tên phim thì mới tính là đúng
                return (m.IsActive ?? false) && msg.Contains(title);
            });

            if (exactMovie != null)
            {
                var shows = _context.Showtimes
                    .Where(s => s.MovieId == exactMovie.MovieId && (s.IsActive ?? false) && s.StartTime >= DateTime.Now)
                    .OrderBy(s => s.StartTime).Take(5).ToList();

                if (!shows.Any()) return Json($"😢 Phim '<b>{exactMovie.Title}</b>' hiện chưa có suất chiếu nào.");
                var result = shows.Select(s => $"<div style='margin-bottom:10px'>🎬 <b>{exactMovie.Title}</b><br>⏰ {s.StartTime:HH:mm dd/MM}<br><a href='/Home/BookTicket?id={exactMovie.MovieId}&showtimeId={s.ShowtimeId}' style='color:#198754;font-weight:bold'>🎟 Đặt vé</a></div>");
                return Json(string.Join("", result));
            }

            // =====================================================
            // 3. GEMINI AI (ĐƯỢC BƠM NGỮ CẢNH TỪ DATABASE)
            // =====================================================
            try
            {
                // Lấy danh sách phim thực tế từ CSDL
                var currentMovies = _context.Movies.Where(m => (m.IsActive ?? false) && m.ReleaseDate <= today).Select(m => m.Title).ToList();
                string movieListStr = currentMovies.Any() ? string.Join(", ", currentMovies) : "Hiện không có phim nào";

                // Bơm thông tin cho AI đóng vai nhân viên
                string systemPrompt = $@"
Bạn là nhân viên CSKH của rạp chiếu phim CineZone. Khách hàng vừa hỏi: '{originalMsg}'.
Hãy dựa vào thông tin nội bộ sau để trả lời khách:
- Danh sách phim RẠP ĐANG CHIẾU: {movieListStr}.
Quy tắc:
1. Nếu khách hỏi phim không có trong danh sách, hãy nói rõ là rạp không chiếu phim đó và mời xem phim khác.
2. Trả lời ngắn gọn, lịch sự, thân thiện (có emoji). KHÔNG bịa đặt thêm phim ngoài danh sách.";

                var ai = await _gemini.Ask(systemPrompt);
                dynamic json = JsonConvert.DeserializeObject(ai);

                if (json?.error != null) return Json($"❌ Lỗi từ Google: {json.error.message}");

                string text = "⚠️ AI chưa phản hồi";
                if (json?.candidates != null && json.candidates.Count > 0)
                {
                    text = json.candidates[0].content.parts[0].text;
                    text = text.Replace("**", "<b>").Replace("**", "</b>").Replace("\n", "<br>");
                }

                return Json("🤖 " + text);
            }
            catch (Exception ex)
            {
                return Json($"❌ Lỗi hệ thống: {ex.Message}");
            }
        }

        [HttpGet]
        public IActionResult GetChatHistory()
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
                return Json(new { success = false, message = "Chưa đăng nhập" });

            var messages = _context.ChatMessages
                .Where(m => m.CustomerId == customerId.Value)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    type = m.IsFromCustomer ? "user" : "admin",
                    senderName = m.IsFromCustomer ? "Bạn" : "Nhân viên hỗ trợ",
                    text = m.MessageText,
                    time = m.CreatedAt.ToString("HH:mm dd/MM")
                })
                .ToList();

            return Json(new { success = true, messages = messages });
        }

        // =====================================================
        // 🔥 REMOVE DẤU TIẾNG VIỆT
        // =====================================================
        public static string RemoveVietnameseTone(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            string[] arr1 = {
                "á","à","ả","ã","ạ","ă","ắ","ằ","ẳ","ẵ","ặ","â","ấ","ầ","ẩ","ẫ","ậ",
                "đ",
                "é","è","ẻ","ẽ","ẹ","ê","ế","ề","ể","ễ","ệ",
                "í","ì","ỉ","ĩ","ị",
                "ó","ò","ỏ","õ","ọ","ô","ố","ồ","ổ","ỗ","ộ","ơ","ớ","ờ","ở","ỡ","ợ",
                "ú","ù","ủ","ũ","ụ","ư","ứ","ừ","ử","ữ","ự",
                "ý","ỳ","ỷ","ỹ","ỵ"
            };

            string[] arr2 = {
                "a","a","a","a","a","a","a","a","a","a","a","a","a","a","a","a","a",
                "d",
                "e","e","e","e","e","e","e","e","e","e","e",
                "i","i","i","i","i",
                "o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o",
                "u","u","u","u","u","u","u","u","u","u","u",
                "y","y","y","y","y"
            };

            for (int i = 0; i < arr1.Length; i++)
            {
                text = text.Replace(arr1[i], arr2[i]);
            }

            return text;
        }
    }
}