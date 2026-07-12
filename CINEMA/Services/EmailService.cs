using CINEMA.Models;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly CinemaContext _context;

        public EmailService(IOptions<EmailSettings> emailSettings, CinemaContext context)
        {
            _emailSettings = emailSettings.Value;
            _context = context;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            using (var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password);
                client.EnableSsl = _emailSettings.EnableSsl;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true,
                    BodyEncoding = Encoding.UTF8
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
            }
        }

        public async Task SendNewMovieNotificationAsync(List<Movie> movies, List<Customer> customers, string baseUrl)
        {
            if (movies == null || movies.Count == 0 || customers == null || customers.Count == 0)
                return;

            string subject = movies.Count == 1
                ? $"🎬 PHIM MỚI: \"{movies[0].Title}\" Đã Có Mặt Tại CineZone!"
                : $"🎬 BÙNG NỔ: {movies.Count} Siêu Phẩm Phim Mới Đã Có Mặt Tại CineZone!";

            // Tạo nội dung HTML đẹp mắt
            StringBuilder sb = new StringBuilder();
            sb.Append(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body { font-family: 'Segoe UI', Arial, sans-serif; background-color: #0b0f19; margin: 0; padding: 0; color: #f8fafc; }
        .wrapper { width: 100%; max-width: 600px; margin: 20px auto; background-color: #0f172a; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 25px rgba(0, 0, 0, 0.5); border: 1px solid #1e293b; }
        .header { background: linear-gradient(135deg, #1e1b4b 0%, #311042 100%); padding: 30px 20px; text-align: center; border-bottom: 2px solid #ec4899; }
        .header h1 { margin: 0; font-size: 28px; font-weight: 800; letter-spacing: 2px; color: #ffffff; text-shadow: 0 2px 4px rgba(0,0,0,0.5); }
        .header p { margin: 5px 0 0 0; color: #e2e8f0; font-size: 14px; text-transform: uppercase; letter-spacing: 1px; }
        .content { padding: 30px 20px; }
        .welcome-text { text-align: center; margin-bottom: 30px; }
        .welcome-text h2 { color: #f43f5e; margin: 0 0 10px 0; font-size: 22px; }
        .welcome-text p { color: #94a3b8; font-size: 15px; line-height: 1.5; margin: 0; }
        
        .movie-card { background: #1e293b; border-radius: 12px; margin-bottom: 25px; border: 1px solid #334155; overflow: hidden; }
        .movie-info-container { display: table; width: 100%; }
        .movie-poster-cell { display: table-cell; width: 140px; vertical-align: top; padding: 15px; }
        .movie-poster { width: 100%; border-radius: 8px; box-shadow: 0 4px 10px rgba(0,0,0,0.3); display: block; object-fit: cover; }
        .movie-details-cell { display: table-cell; vertical-align: top; padding: 15px 15px 15px 5px; }
        .movie-title { font-size: 18px; font-weight: 700; color: #ffffff; margin: 0 0 8px 0; line-height: 1.3; }
        .movie-meta { font-size: 12px; color: #94a3b8; margin-bottom: 8px; }
        .meta-tag { background-color: #334155; color: #f1f5f9; padding: 2px 8px; border-radius: 4px; font-weight: 600; margin-right: 5px; display: inline-block; }
        .age-rating { background-color: #e11d48; color: #ffffff; }
        .movie-desc { font-size: 13px; color: #cbd5e1; line-height: 1.5; margin: 8px 0 0 0; display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden; }
        
        .cta-container { text-align: center; margin: 30px 0 10px 0; }
        .cta-btn { display: inline-block; background: linear-gradient(90deg, #ec4899 0%, #f43f5e 100%); color: #ffffff !important; font-weight: 700; font-size: 16px; padding: 12px 35px; text-decoration: none; border-radius: 30px; box-shadow: 0 4px 15px rgba(244, 63, 94, 0.4); }
        
        .footer { background-color: #0b0f19; padding: 20px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #1e293b; }
        .footer a { color: #f43f5e; text-decoration: none; }
        .footer p { margin: 5px 0; }
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='header'>
            <h1>CINEZONE</h1>
            <p>Thế giới điện ảnh trong tầm tay</p>
        </div>
        <div class='content'>
            <div class='welcome-text'>
                <h2>🔥 HÀNG LOẠT SIÊU PHẨM MỚI ĐÃ LÊN KỆ!</h2>
                <p>Xin chào quý khách hàng thân mến của CineZone, chúng tôi xin trân trọng giới thiệu danh sách các bộ phim mới nhất vừa cập bến rạp tuần này. Hãy nhanh tay chọn cho mình một bộ phim ưng ý nhé!</p>
            </div>
");

            foreach (var movie in movies)
            {
                string movieDetailUrl = $"{baseUrl.TrimEnd('/')}/Home/BookTicket/{movie.MovieId}";
                // Xử lý PosterUrl
                string posterUrl = movie.PosterUrl ?? "";
                if (!string.IsNullOrEmpty(posterUrl) && (posterUrl.StartsWith("/") || posterUrl.StartsWith("\\")))
                {
                    posterUrl = baseUrl.TrimEnd('/') + "/" + posterUrl.TrimStart('/', '\\');
                }
                else if (string.IsNullOrEmpty(posterUrl))
                {
                    posterUrl = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=500&auto=format&fit=crop&q=60"; // Ảnh mặc định sang xịn mịn
                }

                string durationText = movie.Duration.HasValue ? $"{movie.Duration.Value} phút" : "Chưa cập nhật";
                string releaseDateText = movie.ReleaseDate.HasValue ? movie.ReleaseDate.Value.ToString("dd/MM/yyyy") : "Chưa rõ";
                string ageRating = string.IsNullOrWhiteSpace(movie.AgeRating) ? "P" : movie.AgeRating.Trim();
                string ageClass = ageRating.Contains("18") || ageRating.Contains("16") || ageRating.Contains("T18") || ageRating.Contains("T16") ? "meta-tag age-rating" : "meta-tag";

                sb.Append($@"
            <div class='movie-card'>
                <a href='{movieDetailUrl}' style='text-decoration: none; display: block; color: inherit;'>
                    <div class='movie-info-container'>
                        <div class='movie-poster-cell'>
                            <img class='movie-poster' src='{posterUrl}' alt='{movie.Title}' />
                        </div>
                        <div class='movie-details-cell'>
                            <h3 class='movie-title'>{movie.Title}</h3>
                            <div class='movie-meta'>
                                <span class='{ageClass}'>{ageRating}</span>
                                <span class='meta-tag'>{movie.Language ?? "Phụ đề"}</span>
                            </div>
                            <div style='font-size: 13px; color: #94a3b8; margin: 4px 0;'>
                                <strong>Thời lượng:</strong> {durationText}
                            </div>
                            <div style='font-size: 13px; color: #94a3b8; margin: 4px 0;'>
                                <strong>Quốc gia:</strong> {movie.Country ?? "Chưa rõ"}
                            </div>
                            <div style='font-size: 13px; color: #94a3b8; margin: 4px 0;'>
                                <strong>Khởi chiếu:</strong> {releaseDateText}
                            </div>
                            <p class='movie-desc'>{movie.Description ?? "Không có mô tả chi tiết cho bộ phim này."}</p>
                            <div style='margin-top: 12px;'>
                                <span style='display: inline-block; background: linear-gradient(90deg, #ec4899 0%, #f43f5e 100%); color: #ffffff; font-size: 12px; font-weight: bold; padding: 6px 15px; border-radius: 20px; text-decoration: none; box-shadow: 0 2px 5px rgba(244, 63, 94, 0.3);'>🎟 ĐẶT VÉ NGAY</span>
                            </div>
                        </div>
                    </div>
                </a>
            </div>
");
            }

            string globalCtaUrl = movies.Count == 1
                ? $"{baseUrl.TrimEnd('/')}/Home/BookTicket/{movies[0].MovieId}"
                : baseUrl;

            sb.Append($@"
            <div class='cta-container'>
                <a href='{globalCtaUrl}' class='cta-btn'>ĐẶT VÉ NGAY</a>
            </div>
        </div>
        <div class='footer'>
            <p>Hệ thống rạp chiếu phim hiện đại CineZone</p>
            <p>Địa chỉ: Trường Đại học Ngoại ngữ - Tin học TP.HCM (HUFLIT)</p>
            <p>Hotline: 1900 1234 | Email: <a href='mailto:support@cinezone.com'>support@cinezone.com</a></p>
            <p style='font-size: 10px; margin-top: 15px; color: #475569;'>Bạn nhận được email này vì bạn là thành viên đã đăng ký tài khoản tại CineZone.</p>
        </div>
    </div>
</body>
</html>
");

            string htmlBody = sb.ToString();

            // Gửi email bất đồng bộ cho từng khách hàng
            List<Task> sendTasks = new List<Task>();
            foreach (var customer in customers)
            {
                if (string.IsNullOrWhiteSpace(customer.Email)) continue;

                sendTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await SendEmailAsync(customer.Email, subject, htmlBody);
                    }
                    catch (Exception ex)
                    {
                        // Tránh để lỗi của một email làm gián đoạn toàn bộ tiến trình
                        Console.WriteLine($"[Email Error] Failed to send to {customer.Email}: {ex.Message}");
                    }
                }));
            }

            await Task.WhenAll(sendTasks);
        }

        public async Task SendOrderSuccessEmailAsync(int orderId, string baseUrl)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Seat)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Auditorium)
                            .ThenInclude(a => a.Theater)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null || order.Customer == null || string.IsNullOrEmpty(order.Customer.Email))
                return;

            bool isConcessionOnly = order.Tickets == null || !order.Tickets.Any();
            string subject = isConcessionOnly
                ? $"🍿 CINEZONE: Đặt Bắp Nước Thành Công - Mã Đơn #{order.OrderId:D6}"
                : $"🎟️ CINEZONE: Đặt Vé Thành Công - Mã Đơn #{order.OrderId:D6}";

            string movieTitle = "";
            string showtimeText = "";
            string roomName = "";
            string seatsText = "";
            string theaterName = "CineZone Cinema";

            if (!isConcessionOnly)
            {
                var t = order.Tickets.FirstOrDefault();
                movieTitle = t?.Showtime?.Movie?.Title ?? "Phim chiếu rạp";
                showtimeText = t?.Showtime?.StartTime?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa rõ";
                roomName = t?.Showtime?.Auditorium?.Name ?? "Chưa rõ";
                theaterName = t?.Showtime?.Auditorium?.Theater?.Name ?? "CineZone Cinema";
                seatsText = string.Join(", ", order.Tickets.Select(tk => tk.Seat?.RowLabel + tk.Seat?.SeatNumber));
            }
            else
            {
                string sourceText = order.PaymentMethod ?? order.Status ?? "";
                if (sourceText.Contains("Nhận tại:"))
                {
                    int idx = sourceText.IndexOf("Nhận tại:");
                    theaterName = sourceText.Substring(idx + 9).Replace(")", "").Trim();
                }
            }

            var sb = new StringBuilder();
            sb.Append($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #0b0f19; color: #f8fafc; margin: 0; padding: 0; }}
        .wrapper {{ max-width: 600px; margin: 20px auto; background-color: #0f172a; border-radius: 16px; overflow: hidden; border: 1px solid #1e293b; box-shadow: 0 10px 25px rgba(0,0,0,0.5); }}
        .header {{ background: linear-gradient(135deg, #1e1b4b 0%, #311042 100%); padding: 25px; text-align: center; border-bottom: 2px solid #f2b705; }}
        .header h1 {{ margin: 0; color: #ffffff; font-size: 26px; font-weight: 800; letter-spacing: 2px; }}
        .content {{ padding: 30px 25px; }}
        .greeting {{ font-size: 18px; color: #f2b705; font-weight: bold; margin-bottom: 15px; }}
        .intro {{ color: #94a3b8; font-size: 14px; line-height: 1.6; margin-bottom: 25px; }}
        .ticket-info {{ background: #1e293b; border-radius: 12px; padding: 20px; border: 1px solid #334155; margin-bottom: 25px; }}
        .info-row {{ display: flex; justify-content: space-between; border-bottom: 1px dashed #334155; padding: 10px 0; font-size: 14px; }}
        .info-row:last-child {{ border-bottom: none; }}
        .label {{ color: #94a3b8; font-weight: 500; }}
        .value {{ color: #ffffff; font-weight: bold; text-align: right; }}
        .total-amount {{ color: #f2b705 !important; font-size: 18px; }}
        .footer {{ background-color: #0b0f19; padding: 20px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #1e293b; }}
        .footer a {{ color: #f2b705; text-decoration: none; }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='header'>
            <h1>CINEZONE</h1>
        </div>
        <div class='content'>
            <div class='greeting'>Xin chào {order.Customer.FullName},</div>
");

            if (isConcessionOnly)
            {
                sb.Append($@"
            <div class='intro'>Chúc mừng bạn đã đặt bắp nước trực tuyến thành công tại CineZone! Vui lòng xuất trình mã đơn hàng này tại quầy bắp nước của rạp để nhận phần của mình.</div>
            
            <div class='ticket-info'>
                <div class='info-row'>
                    <span class='label'>Mã đơn hàng</span>
                    <span class='value' style='color:#f2b705;'>CZ{order.OrderId:D6}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Rạp nhận bắp nước</span>
                    <span class='value'>{theaterName}</span>
                </div>
");
            }
            else
            {
                sb.Append($@"
            <div class='intro'>Chúc mừng bạn đã đặt vé xem phim thành công tại CineZone! Dưới đây là thông tin chi tiết đơn hàng của bạn. Vui lòng xuất trình mã đơn hàng này tại quầy để nhận vé.</div>
            
            <div class='ticket-info'>
                <div class='info-row'>
                    <span class='label'>Mã đơn hàng</span>
                    <span class='value' style='color:#f2b705;'>CZ{order.OrderId:D6}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Rạp chiếu</span>
                    <span class='value'>{theaterName}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Phim</span>
                    <span class='value'>{movieTitle}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Suất chiếu</span>
                    <span class='value'>{showtimeText}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Phòng chiếu</span>
                    <span class='value'>{roomName}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Số ghế đã chọn</span>
                    <span class='value'>{seatsText}</span>
                </div>
");
            }

            if (order.OrderCombos != null && order.OrderCombos.Any())
            {
                string comboText = string.Join("<br/>", order.OrderCombos.Select(oc => $"{oc.Combo?.Name} (x{oc.Quantity})"));
                sb.Append($@"
                <div class='info-row'>
                    <span class='label'>Bắp nước đã chọn</span>
                    <span class='value'>{comboText}</span>
                </div>
");
            }

            sb.Append($@"
                <div class='info-row' style='border-top: 1px solid #475569; padding-top: 15px;'>
                    <span class='label' style='font-size:16px;'>Tổng thanh toán</span>
                    <span class='value total-amount'>{order.TotalAmount?.ToString("C0", new CultureInfo("vi-VN"))}</span>
                </div>
            </div>
            
            <div style='text-align: center; margin-top: 30px;'>
                <p style='font-size: 13px; color: #94a3b8; margin-bottom: 5px;'>Cảm ơn bạn đã lựa chọn dịch vụ của CineZone!</p>
            </div>
        </div>
        <div class='footer'>
            <p>Hệ thống rạp chiếu phim hiện đại CineZone</p>
            <p>Hotline: 1900 1234 | Email: support@cinezone.com</p>
        </div>
    </div>
</body>
</html>
");
            await SendEmailAsync(order.Customer.Email, subject, sb.ToString());
        }

        public async Task SendPaymentReminderEmailAsync(int orderId, string baseUrl)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Seat)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Auditorium)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null || order.Customer == null || string.IsNullOrEmpty(order.Customer.Email))
                return;

            string subject = $"⏳ CINEZONE: Đơn Hàng Đang Chờ Thanh Toán - Mã Đơn #{order.OrderId:D6}";
            var firstTicket = order.Tickets.FirstOrDefault();
            string movieTitle = firstTicket?.Showtime?.Movie?.Title ?? "Phim chiếu rạp";
            string showtimeText = firstTicket?.Showtime?.StartTime?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa rõ";
            string roomName = firstTicket?.Showtime?.Auditorium?.Name ?? "Chưa rõ";
            string seatsText = string.Join(", ", order.Tickets.Select(t => t.Seat?.RowLabel + t.Seat?.SeatNumber));
            string expiredText = order.ExpiredAt?.ToString("HH:mm dd/MM/yyyy") ?? "Chưa rõ";
            string paymentUrl = $"{baseUrl.TrimEnd('/')}/Payment/CreatePayment?orderId={order.OrderId}";

            var sb = new StringBuilder();
            sb.Append($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #0b0f19; color: #f8fafc; margin: 0; padding: 0; }}
        .wrapper {{ max-width: 600px; margin: 20px auto; background-color: #0f172a; border-radius: 16px; overflow: hidden; border: 1px solid #1e293b; box-shadow: 0 10px 25px rgba(0,0,0,0.5); }}
        .header {{ background: linear-gradient(135deg, #1e1b4b 0%, #311042 100%); padding: 25px; text-align: center; border-bottom: 2px solid #ffc107; }}
        .header h1 {{ margin: 0; color: #ffffff; font-size: 26px; font-weight: 800; }}
        .content {{ padding: 30px 25px; }}
        .greeting {{ font-size: 18px; color: #ffc107; font-weight: bold; margin-bottom: 15px; }}
        .intro {{ color: #94a3b8; font-size: 14px; line-height: 1.6; margin-bottom: 25px; }}
        .ticket-info {{ background: #1e293b; border-radius: 12px; padding: 20px; border: 1px solid #334155; margin-bottom: 25px; }}
        .info-row {{ display: flex; justify-content: space-between; border-bottom: 1px dashed #334155; padding: 10px 0; font-size: 14px; }}
        .info-row:last-child {{ border-bottom: none; }}
        .label {{ color: #94a3b8; font-weight: 500; }}
        .value {{ color: #ffffff; font-weight: bold; text-align: right; }}
        .cta-box {{ text-align: center; margin: 30px 0; }}
        .cta-btn {{ display: inline-block; background: linear-gradient(90deg, #ff9800 0%, #ff5722 100%); color: #ffffff !important; font-weight: bold; font-size: 16px; padding: 12px 35px; text-decoration: none; border-radius: 30px; box-shadow: 0 4px 15px rgba(255, 87, 34, 0.4); }}
        .footer {{ background-color: #0b0f19; padding: 20px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #1e293b; }}
        .footer a {{ color: #ffc107; text-decoration: none; }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='header'>
            <h1>CINEZONE</h1>
        </div>
        <div class='content'>
            <div class='greeting'>Xin chào {order.Customer.FullName},</div>
            <div class='intro'>Bạn đang có đơn đặt vé phim ở trạng thái **Chờ thanh toán**. Vui lòng hoàn tất thanh toán trước thời gian giữ ghế để tránh vé bị hủy tự động.</div>
            
            <div class='ticket-info'>
                <div class='info-row'>
                    <span class='label'>Mã đơn hàng</span>
                    <span class='value' style='color:#ffc107;'>CZ{order.OrderId:D6}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Phim</span>
                    <span class='value'>{movieTitle}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Suất chiếu</span>
                    <span class='value'>{showtimeText}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Số ghế</span>
                    <span class='value'>{seatsText}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Thời hạn giữ ghế</span>
                    <span class='value' style='color:#ef4444;'>{expiredText}</span>
                </div>
                <div class='info-row' style='border-top: 1px solid #475569; padding-top: 15px;'>
                    <span class='label' style='font-size:16px;'>Tổng thanh toán</span>
                    <span class='value' style='color:#ffc107; font-size:18px;'>{order.TotalAmount?.ToString("C0", new CultureInfo("vi-VN"))}</span>
                </div>
            </div>
            
            <div class='cta-box'>
                <a href='{paymentUrl}' class='cta-btn'>THANH TOÁN NGAY</a>
            </div>
        </div>
        <div class='footer'>
            <p>Hệ thống rạp chiếu phim hiện đại CineZone</p>
            <p>Hotline: 1900 1234 | Email: support@cinezone.com</p>
        </div>
    </div>
</body>
</html>
");
            await SendEmailAsync(order.Customer.Email, subject, sb.ToString());
        }

        public async Task SendOrderCanceledEmailAsync(int orderId, string baseUrl)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Seat)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null || order.Customer == null || string.IsNullOrEmpty(order.Customer.Email))
                return;

            string subject = $"❌ CINEZONE: Đơn Hàng Đã Bị Hủy - Mã Đơn #{order.OrderId:D6}";
            var firstTicket = order.Tickets.FirstOrDefault();
            string movieTitle = firstTicket?.Showtime?.Movie?.Title ?? "Phim chiếu rạp";
            string showtimeText = firstTicket?.Showtime?.StartTime?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa rõ";
            string seatsText = string.Join(", ", order.Tickets.Select(t => t.Seat?.RowLabel + t.Seat?.SeatNumber));

            var sb = new StringBuilder();
            sb.Append($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #0b0f19; color: #f8fafc; margin: 0; padding: 0; }}
        .wrapper {{ max-width: 600px; margin: 20px auto; background-color: #0f172a; border-radius: 16px; overflow: hidden; border: 1px solid #1e293b; box-shadow: 0 10px 25px rgba(0,0,0,0.5); }}
        .header {{ background: linear-gradient(135deg, #1e1b4b 0%, #200f19 100%); padding: 25px; text-align: center; border-bottom: 2px solid #ef4444; }}
        .header h1 {{ margin: 0; color: #ffffff; font-size: 26px; font-weight: 800; }}
        .content {{ padding: 30px 25px; }}
        .greeting {{ font-size: 18px; color: #ef4444; font-weight: bold; margin-bottom: 15px; }}
        .intro {{ color: #94a3b8; font-size: 14px; line-height: 1.6; margin-bottom: 25px; }}
        .ticket-info {{ background: #1e293b; border-radius: 12px; padding: 20px; border: 1px solid #334155; margin-bottom: 25px; }}
        .info-row {{ display: flex; justify-content: space-between; border-bottom: 1px dashed #334155; padding: 10px 0; font-size: 14px; }}
        .info-row:last-child {{ border-bottom: none; }}
        .label {{ color: #94a3b8; font-weight: 500; }}
        .value {{ color: #ffffff; font-weight: bold; text-align: right; }}
        .footer {{ background-color: #0b0f19; padding: 20px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #1e293b; }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='header'>
            <h1>CINEZONE</h1>
        </div>
        <div class='content'>
            <div class='greeting'>Xin chào {order.Customer.FullName},</div>
            <div class='intro'>Đơn đặt vé phim mã số **CZ{order.OrderId:D6}** của bạn đã bị hủy do quá thời gian thanh toán quy định (15 phút) hoặc bạn đã yêu cầu hủy. Các ghế đặt chỗ hiện đã được mở lại cho khách hàng khác.</div>
            
            <div class='ticket-info'>
                <div class='info-row'>
                    <span class='label'>Mã đơn hàng</span>
                    <span class='value' style='color:#ef4444;'>CZ{order.OrderId:D6}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Phim</span>
                    <span class='value'>{movieTitle}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Suất chiếu</span>
                    <span class='value'>{showtimeText}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Số ghế</span>
                    <span class='value'>{seatsText}</span>
                </div>
                <div class='info-row'>
                    <span class='label'>Trạng thái</span>
                    <span class='value' style='color:#ef4444;'>ĐÃ HỦY VÉ</span>
                </div>
            </div>
            <p style='font-size: 13px; color: #94a3b8; text-align: center;'>Nếu đây là sự nhầm lẫn, vui lòng quay lại trang chủ CineZone để đặt vé mới. Rất mong được phục vụ bạn lần sau!</p>
        </div>
        <div class='footer'>
            <p>Hệ thống rạp chiếu phim hiện đại CineZone</p>
        </div>
    </div>
</body>
</html>
");
            await SendEmailAsync(order.Customer.Email, subject, sb.ToString());
        }
    }
}
