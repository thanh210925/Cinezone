using CINEMA.Helpers;
using CINEMA.Models;
using CINEMA.Services;
using CINEMA.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace CINEMA.Controllers
{
    public class PaymentController : Controller
    {
        private readonly CinemaContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentController> _logger;
        private readonly IVnpayService _vnpayService;
        private readonly RecommendationEngine _recommendationEngine;
        private readonly IEmailService _emailService;

        public PaymentController(
            CinemaContext context, 
            IConfiguration config, 
            ILogger<PaymentController> logger, 
            IVnpayService vnpayService,
            RecommendationEngine recommendationEngine,
            IEmailService emailService)
        {
            _context = context;
            _config = config;
            _logger = logger;
            _vnpayService = vnpayService;
            _recommendationEngine = recommendationEngine;
            _emailService = emailService;
        }

        // =================== [1] Trang xác nhận thanh toán ===================
        [HttpPost]
        public IActionResult Index(
            int MovieId,
            int ShowtimeId,
            string selectedSeats,
            int AdultTickets,
            int ChildTickets,
            int StudentTickets,
            decimal TotalPrice,
            string VoucherCode,
            decimal? DiscountAmount)
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
            {
                HttpContext.Session.SetInt32("Booking_MovieId", MovieId);
                HttpContext.Session.SetInt32("Booking_ShowtimeId", ShowtimeId);
                
                var seatArray = selectedSeats?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    ?? Array.Empty<string>();
                HttpContext.Session.SetString("Booking_Seats", JsonSerializer.Serialize(seatArray));
                
                HttpContext.Session.SetInt32("Booking_AdultTickets", AdultTickets);
                HttpContext.Session.SetInt32("Booking_ChildTickets", ChildTickets);
                HttpContext.Session.SetInt32("Booking_StudentTickets", StudentTickets);
                HttpContext.Session.SetString("Booking_TotalPrice", TotalPrice.ToString(CultureInfo.InvariantCulture));
                HttpContext.Session.SetString("Booking_VoucherCode", VoucherCode ?? "");
                HttpContext.Session.SetString("Booking_DiscountAmount", (DiscountAmount ?? 0).ToString(CultureInfo.InvariantCulture));
                
                var comboDict = Request.Form.Keys
                    .Where(k => k.StartsWith("Combo_"))
                    .ToDictionary(k => k, k => Request.Form[k].ToString());
                HttpContext.Session.SetString("Booking_Combos", JsonSerializer.Serialize(comboDict));

                var returnUrl = Url.Action(nameof(ResumePayment), "Payment");
                return RedirectToAction("Login", "Customer", new { ReturnUrl = returnUrl });
            }

            var customer = _context.Customers.Find(customerId);
            if (customer == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Customer", new { message = "Tài khoản không tồn tại." });
            }

            // Luôn lưu vào Session để phục vụ tính năng Nâng cấp Combo (Upselling)
            HttpContext.Session.SetInt32("Booking_MovieId", MovieId);
            HttpContext.Session.SetInt32("Booking_ShowtimeId", ShowtimeId);
            var seatArrayLogged = selectedSeats?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            HttpContext.Session.SetString("Booking_Seats", JsonSerializer.Serialize(seatArrayLogged));
            HttpContext.Session.SetInt32("Booking_AdultTickets", AdultTickets);
            HttpContext.Session.SetInt32("Booking_ChildTickets", ChildTickets);
            HttpContext.Session.SetInt32("Booking_StudentTickets", StudentTickets);
            HttpContext.Session.SetString("Booking_TotalPrice", TotalPrice.ToString(CultureInfo.InvariantCulture));
            HttpContext.Session.SetString("Booking_VoucherCode", VoucherCode ?? "");
            HttpContext.Session.SetString("Booking_DiscountAmount", (DiscountAmount ?? 0).ToString(CultureInfo.InvariantCulture));
            
            var comboDictLogged = Request.Form.Keys
                .Where(k => k.StartsWith("Combo_"))
                .ToDictionary(k => k, k => Request.Form[k].ToString());
            HttpContext.Session.SetString("Booking_Combos", JsonSerializer.Serialize(comboDictLogged));

            int totalTickets = AdultTickets + ChildTickets + StudentTickets;
            if (totalTickets > 10)
            {
                TempData["Error"] = "Bạn chỉ được đặt tối đa 10 vé cho mỗi đơn hàng!";
                return RedirectToAction("BookTicket", "Home", new { id = MovieId, showtimeId = ShowtimeId });
            }

            var seatList = selectedSeats?
    .Split(',', StringSplitOptions.RemoveEmptyEntries)
    .ToList() ?? new List<string>();
            // 🔥 Load đầy đủ Movie + Auditorium + Theater
            var showtime = _context.Showtimes
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .Include(s => s.Movie)
                .FirstOrDefault(s => s.ShowtimeId == ShowtimeId);

            if (showtime == null) return NotFound();

            // 📌 Lấy combo đã chọn
            var combosVm = new List<ComboViewModel>();
            foreach (var key in Request.Form.Keys.Where(k => k.StartsWith("Combo_")))
            {
                if (int.TryParse(key.Replace("Combo_", ""), out int comboId) &&
                    int.TryParse(Request.Form[key], out int qty) && qty > 0)
                {
                    var combo = _context.Combos.FirstOrDefault(c => c.ComboId == comboId);
                    if (combo != null)
                    {
                        combosVm.Add(new ComboViewModel
                        {
                            ComboId = combo.ComboId,
                            ComboName = combo.Name,
                            Quantity = qty,
                            Price = combo.Price ?? 0
                        });
                    }
                }
            }
            // 🎟️ TÍNH TOÁN CHIẾT KHẤU HẠNG THÀNH VIÊN
            decimal originalPrice = TotalPrice;
            string membershipLevel = customer.MembershipLevel ?? "Đồng";
            decimal mDiscountPercent = 0m;

            if (membershipLevel == "Kim cương")
                mDiscountPercent = 0.10m; // 10%
            else if (membershipLevel == "Bạc")
                mDiscountPercent = 0.05m; // 5%

            decimal membershipDiscountAmount = originalPrice * mDiscountPercent;
            decimal priceAfterMembership = originalPrice - membershipDiscountAmount;
            decimal finalPrice = priceAfterMembership;
            decimal voucherDiscountAmount = 0m;

            // 🎟️ APPLY VOUCHER (HIỂN THỊ)
            if (!string.IsNullOrEmpty(VoucherCode))
            {
                var voucher = _context.Vouchers
                    .FirstOrDefault(v => v.Code == VoucherCode && v.IsActive);

                if (voucher != null)
                {
                    if (voucher.DiscountPercent != null)
                        voucherDiscountAmount = priceAfterMembership * (decimal)voucher.DiscountPercent;
                    else if (voucher.DiscountAmount != null)
                        voucherDiscountAmount = voucher.DiscountAmount.Value;

                    finalPrice -= voucherDiscountAmount;
                }
            }

            if (finalPrice < 0) finalPrice = 0;

            // 📌 Gửi ViewModel
            var vm = new PaymentViewModel
            {
                CustomerName = customer.FullName,
                CustomerEmail = customer.Email,
                CustomerPhone = customer.Phone,

                MovieId = MovieId,
                ShowtimeId = ShowtimeId,

                MovieTitle = showtime.Movie?.Title,
                Showtime = showtime.StartTime?.ToString("dd/MM/yyyy HH:mm"),
                Auditorium = showtime.Auditorium?.Name,

                TheaterName = showtime.Auditorium?.Theater?.Name,
                TheaterAddress = showtime.Auditorium?.Theater?.Address,
                TheaterPhone = showtime.Auditorium?.Theater?.Phone,

                SelectedSeats = seatList,
                AdultTickets = AdultTickets,
                ChildTickets = ChildTickets,
                StudentTickets = StudentTickets,
                TotalPrice = finalPrice,
                VoucherCode = VoucherCode,
                DiscountAmount = voucherDiscountAmount,
                Combos = combosVm,

                // Thông tin chi tiết ưu đãi thành viên
                MembershipLevel = membershipLevel,
                MembershipDiscountPercent = mDiscountPercent * 100,
                MembershipDiscountAmount = membershipDiscountAmount,
                OriginalPrice = originalPrice
            };

            // Tính toán gợi ý nâng cấp combo
            CalculateUpsellOffers(combosVm);

            return View("Index", vm);
        }

        // =================== [2] Xử lý thanh toán ===================
        [HttpPost]
        public IActionResult Confirm(PaymentViewModel model, string method)
        {
            _logger.LogInformation("[Confirm] Phương thức: {Method}", method);
            var customerId = HttpContext.Session.GetInt32("CustomerId");

            if (customerId == null)
                return RedirectToAction("Login", "Customer");

            using var transaction = _context.Database.BeginTransaction();

            try
            {
                // Giới hạn chỉ được đặt tối đa 10 vé
                int totalTickets = model.AdultTickets + model.ChildTickets + model.StudentTickets;
                if (totalTickets > 10)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Bạn chỉ được đặt tối đa 10 vé cho mỗi đơn hàng!";
                    return RedirectToAction("BookTicket", "Home", new { id = model.MovieId, showtimeId = model.ShowtimeId });
                }

                // 1. Kiểm tra xem có ghế nào đã được đặt hoặc giữ chỗ (chưa hết hạn) hay chưa
                var now = DateTime.Now;
                var bookedSeatsForShowtime = _context.Tickets
                    .Include(t => t.Seat)
                    .Include(t => t.Order)
                    .Where(t => t.ShowtimeId == model.ShowtimeId
                        && t.Order != null
                        && (t.Order.Status == "Đã thanh toán" 
                            || ((t.Order.Status == "Chờ thanh toán" || t.Order.Status == "Đang chờ thanh toán") 
                                && t.Order.ExpiredAt > now)))
                    .Select(t => t.Seat.RowLabel + t.Seat.SeatNumber)
                    .ToList();

                foreach (var seatStr in model.SelectedSeats)
                {
                    if (bookedSeatsForShowtime.Contains(seatStr))
                    {
                        transaction.Rollback();
                        TempData["Error"] = $"Ghế {seatStr} đã bị người khác chọn hoặc đang trong quá trình thanh toán!";
                        return RedirectToAction("BookTicket", "Home", new { id = model.MovieId, showtimeId = model.ShowtimeId });
                    }
                }

                // 2. Lấy thông tin suất chiếu để lấy phòng chiếu (AuditoriumId)
                var showtime = _context.Showtimes.FirstOrDefault(s => s.ShowtimeId == model.ShowtimeId);
                if (showtime == null)
                {
                    transaction.Rollback();
                    return NotFound("Không tìm thấy suất chiếu.");
                }

                decimal basePrice = showtime.BasePrice ?? 0m;
                decimal ticketOnlyOriginal = model.AdultTickets * basePrice
                                           + model.ChildTickets * (basePrice * 0.7m)
                                           + model.StudentTickets * (basePrice * 0.8m);

                decimal comboTotal = model.Combos?.Sum(c => c.Price * c.Quantity) ?? 0;
                decimal originalPrice = ticketOnlyOriginal + comboTotal;

                // TÍNH TOÁN CHIẾT KHẤU HẠNG THÀNH VIÊN TRÊN SERVER (BẢO MẬT)
                var customer = _context.Customers.Find(customerId.Value);
                string membershipLevel = customer?.MembershipLevel ?? "Đồng";
                decimal mDiscountPercent = 0m;

                if (membershipLevel == "Kim cương")
                    mDiscountPercent = 0.10m;
                else if (membershipLevel == "Bạc")
                    mDiscountPercent = 0.05m;

                decimal membershipDiscount = originalPrice * mDiscountPercent;
                decimal priceAfterMembership = originalPrice - membershipDiscount;

                decimal total = priceAfterMembership;
                decimal voucherDiscount = 0m;

                // 🎟️ CHECK VOUCHER DB
                if (!string.IsNullOrEmpty(model.VoucherCode))
                {
                    var voucher = _context.Vouchers
                        .FirstOrDefault(v => v.Code == model.VoucherCode && v.IsActive);

                    if (voucher != null)
                    {
                        if (voucher.ExpiryDate < DateTime.Now)
                            return Content("Voucher hết hạn");

                        if (voucher.UsedCount >= voucher.Quantity)
                            return Content("Voucher đã hết lượt");

                        if (total < voucher.MinOrderValue)
                            return Content("Chưa đủ điều kiện");

                        if (voucher.DiscountPercent != null)
                            voucherDiscount = total * (decimal)voucher.DiscountPercent;
                        else if (voucher.DiscountAmount != null)
                            voucherDiscount = voucher.DiscountAmount.Value;

                        total -= voucherDiscount;
                    }
                }

                if (total < 0) total = 0;

                decimal ticketOnlyTotal = total - comboTotal;
                if (ticketOnlyTotal < 0) ticketOnlyTotal = 0;

                decimal pricePerTicket = model.SelectedSeats.Count > 0
                    ? ticketOnlyTotal / model.SelectedSeats.Count
                    : 0;

                // 🔹 Tạo đơn hàng
                var order = new Order
                {
                    CustomerId = customerId.Value,
                    CreatedAt = DateTime.Now,
                    PaidAt = DateTime.Now,
                    ExpiredAt = DateTime.Now.AddMinutes(15),

                    TotalAmount = total,

                    VoucherCode = model.VoucherCode,

                    DiscountAmount = membershipDiscount + voucherDiscount,

                    Status = (method == "Chuyển khoản")
                        ? "Đang chờ thanh toán"
                        : "Chờ thanh toán",

                    PaymentMethod = method
                };

                _context.Orders.Add(order);
                _context.SaveChanges();

                // 🔹 Tạo vé
                 foreach (var seatStr in model.SelectedSeats)
                {
                    var seat = _context.Seats
                        .FirstOrDefault(s => s.AuditoriumId == showtime.AuditoriumId && (s.RowLabel + s.SeatNumber.ToString()) == seatStr);

                    _context.Tickets.Add(new Ticket
                    {
                        ShowtimeId = model.ShowtimeId,
                        SeatId = seat.SeatId,
                        CustomerId = customerId.Value,
                        OrderId = order.OrderId,
                        Price = pricePerTicket,
                        Status = "Đã đặt",
                        PaymentStatus = (method == "Chuyển khoản")
                            ? "Chờ thanh toán"
                            : "Đang chờ thanh toán",
                        BookedAt = DateTime.Now
                    });
                }
                _context.SaveChanges();

                // 🔹 Lưu combo nếu có
                if (model.Combos != null)
                {
                    var orderCombos = model.Combos
                        .Select(c => new OrderCombo
                        {
                            OrderId = order.OrderId,
                            ComboId = c.ComboId,
                            Quantity = c.Quantity,
                            UnitPrice = c.Price
                        }).ToList();

                    _context.OrderCombos.AddRange(orderCombos);
                    _context.SaveChanges();

                    int firstTicketId = _context.Tickets
                        .Where(t => t.OrderId == order.OrderId)
                        .OrderBy(t => t.TicketId)
                        .Select(t => t.TicketId)
                        .FirstOrDefault();

                    if (firstTicketId > 0)
                    {
                        var ticketCombos = orderCombos
                            .Select(c => new TicketCombo
                            {
                                TicketId = firstTicketId,
                                OrderComboId = c.OrderComboId,
                                Quantity = c.Quantity
                            }).ToList();

                        _context.TicketCombos.AddRange(ticketCombos);
                        _context.SaveChanges();
                    }
                }

                transaction.Commit();

                if (method == "Tại quầy")
                {
                    // Gửi email nhắc nhở thanh toán tại quầy
                    string reqBaseUrl = _config["AppSettings:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
                    int currentOrderId = order.OrderId;
                    Task.Run(async () => {
                        try {
                            await _emailService.SendPaymentReminderEmailAsync(currentOrderId, reqBaseUrl);
                        } catch (Exception ex) {
                            _logger.LogError(ex, "Failed to send payment reminder email for order {OrderId}", currentOrderId);
                        }
                    });

                    ViewBag.PaymentMethod = "Tại quầy";
                    ViewBag.PaymentStatus = "Chờ thanh toán";
                    ViewBag.BookingCode = $"CZ{order.OrderId:D6}";
                    ViewBag.Total = model.TotalPrice;
                    return View("Success", model);
                }

                // 🔹 Thanh toán VNPay
                if (method == "Chuyển khoản")
                {
                    // Gửi email nhắc nhở thanh toán VNPAY (chứa link thanh toán lại nếu xảy ra sự cố)
                    string reqBaseUrl = _config["AppSettings:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
                    int currentOrderId = order.OrderId;
                    Task.Run(async () => {
                        try {
                            await _emailService.SendPaymentReminderEmailAsync(currentOrderId, reqBaseUrl);
                        } catch (Exception ex) {
                            _logger.LogError(ex, "Failed to send payment reminder email for order {OrderId}", currentOrderId);
                        }
                    });

                    var pay = new VnpayLibrary();
                    string baseUrl = _config["Vnpay:BaseUrl"];
                    string returnUrl = _config["Vnpay:ReturnUrl"];
                    if (!returnUrl.StartsWith("http"))
                    {
                        returnUrl = $"{Request.Scheme}://{Request.Host}{returnUrl}";
                    }
                    string tmnCode = _config["Vnpay:TmnCode"];
                    string hashSecret = _config["Vnpay:HashSecret"];

                    pay.AddRequestData("vnp_Version", "2.1.0");
                    pay.AddRequestData("vnp_Command", "pay");
                    pay.AddRequestData("vnp_TmnCode", tmnCode);
                    pay.AddRequestData("vnp_Amount", ((long)total * 100).ToString());
                    pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                    pay.AddRequestData("vnp_CurrCode", "VND");
                    string ipAddr = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                    if (ipAddr == "::1") ipAddr = "127.0.0.1";
                    pay.AddRequestData("vnp_IpAddr", ipAddr);
                    pay.AddRequestData("vnp_Locale", "vn");
                    pay.AddRequestData("vnp_OrderInfo", $"Thanh toán đơn #{order.OrderId}");
                    pay.AddRequestData("vnp_OrderType", "billpayment");
                    pay.AddRequestData("vnp_ReturnUrl", returnUrl);
                    pay.AddRequestData("vnp_TxnRef", order.OrderId.ToString());
                    string paymentUrl = pay.CreateRequestUrl(baseUrl, hashSecret);
                    return Redirect(paymentUrl);
                }

                return View("PaymentError");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return Content("LỖI: " + ex.InnerException?.Message ?? ex.Message);
            }

        }

        // =================== [3] Thanh toán VNPay Callback ===================
        [HttpGet]
        public IActionResult PaymentReturn()
        {
            string hashSecret = _config["Vnpay:HashSecret"];

            var pay = new VnpayLibrary();
            foreach (var key in Request.Query.Keys)
                if (key.StartsWith("vnp_"))
                    pay.AddResponseData(key, Request.Query[key]);

            string sOrderId = pay.GetResponseData("vnp_TxnRef");
            string responseCode = pay.GetResponseData("vnp_ResponseCode");
            string secureHash = pay.GetResponseData("vnp_SecureHash");

            if (!long.TryParse(sOrderId, out long orderId))
                return View("PaymentError");

            bool validSignature = pay.ValidateSignature(secureHash, hashSecret);
            if (!validSignature)
                return View("PaymentError");

             var order = _context.Orders
                .Include(o => o.Tickets)
                .FirstOrDefault(o => o.OrderId == orderId);

            if (order == null)
                return View("PaymentError");

            // Kiểm tra số tiền thực nhận từ VNPAY
            string sAmount = pay.GetResponseData("vnp_Amount");
            if (!long.TryParse(sAmount, out long amountCents) || Math.Abs(((decimal)amountCents / 100) - (order.TotalAmount ?? 0)) > 1.0m)
            {
                _logger.LogWarning("VNPay amount mismatch. Order: {OrderId}, Expected: {Expected}, Received: {Received}", orderId, order.TotalAmount, (decimal)amountCents / 100);
                return View("PaymentError");
            }

            if (responseCode == "00")
            {
                order.Status = "Đã thanh toán";
                foreach (var t in order.Tickets)
                {
                    t.PaymentStatus = "Đã thanh toán";
                    t.Status = "Đã thanh toán";
                }
                // 💎 UPDATE MEMBERSHIP
                var customer = _context.Customers.Find(order.CustomerId);

                if (customer != null)
                {
                    customer.TotalSpent += order.TotalAmount ?? 0;
                    customer.MembershipLevel = customer.CalculateMembershipLevel();

                }
                var voucherCode = order.VoucherCode;

                if (!string.IsNullOrEmpty(voucherCode))
                {
                    var voucher = _context.Vouchers
                        .FirstOrDefault(v => v.Code == voucherCode);

                    if (voucher != null)
                    {
                        voucher.UsedCount++;
                    }
                }

                // 📌 GHI LOG ĐẶT VÉ THÀNH CÔNG VÀO DATABASE
                var firstTicket = order.Tickets.FirstOrDefault();
                int? logMovieId = null;
                if (firstTicket != null)
                {
                    var showtimeObj = _context.Showtimes.Find(firstTicket.ShowtimeId);
                    logMovieId = showtimeObj?.MovieId;
                }
                
                var successLog = new UserActivityLog
                {
                    CustomerId = order.CustomerId,
                    SessionId = HttpContext.Session.Id,
                    ActivityType = "BOOK_TICKET",
                    MovieId = logMovieId,
                    Metadata = $"Thanh toán thành công qua VNPAY cho đơn hàng #{order.OrderId}.",
                    CreatedAt = DateTime.Now
                };
                _context.UserActivityLogs.Add(successLog);

                _context.SaveChanges();

                // Gửi email đặt vé thành công
                string reqBaseUrl = _config["AppSettings:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
                int successOrderId = order.OrderId;
                Task.Run(async () => {
                    try {
                        await _emailService.SendOrderSuccessEmailAsync(successOrderId, reqBaseUrl);
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Failed to send order success email for order {OrderId}", successOrderId);
                    }
                });

                ViewBag.Total = order.TotalAmount;
                ViewBag.PaymentStatus = "Đã thanh toán";
                ViewBag.BookingCode = $"CZ{order.OrderId:D6}";
                return View("Success");
            }

            order.Status = "Thanh toán thất bại";
            foreach (var t in order.Tickets)
                t.Status = "Thanh toán thất bại";

            _context.SaveChanges();
            return View("PaymentError");
        }

        // =================== [4] Khôi phục sau khi login ===================
        [HttpGet]
        public IActionResult ResumePayment()
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null) return RedirectToAction("Login", "Customer");

            var customer = _context.Customers.Find(customerId);
            if (customer == null)
                return RedirectToAction("Login", "Customer");

            if (HttpContext.Session.GetInt32("Booking_MovieId") == null)
                return RedirectToAction("Index", "Home");

            int movieId = HttpContext.Session.GetInt32("Booking_MovieId").Value;
            int showtimeId = HttpContext.Session.GetInt32("Booking_ShowtimeId").Value;
            var seats = JsonSerializer.Deserialize<string[]>(HttpContext.Session.GetString("Booking_Seats"));
            int adult = HttpContext.Session.GetInt32("Booking_AdultTickets").Value;
            int child = HttpContext.Session.GetInt32("Booking_ChildTickets").Value;
            int student = HttpContext.Session.GetInt32("Booking_StudentTickets").Value;
            decimal total = decimal.Parse(HttpContext.Session.GetString("Booking_TotalPrice"), CultureInfo.InvariantCulture);
            string voucherCode = HttpContext.Session.GetString("Booking_VoucherCode");
            decimal? discountAmount = decimal.Parse(HttpContext.Session.GetString("Booking_DiscountAmount") ?? "0", CultureInfo.InvariantCulture);

            // 📌 Load Theater
            var showtime = _context.Showtimes
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .Include(s => s.Movie)
                .FirstOrDefault(s => s.ShowtimeId == showtimeId);

            if (showtime == null) return NotFound();

            // 📌 Lấy combo từ Session
            var combos = new List<ComboViewModel>();
            var combosJson = HttpContext.Session.GetString("Booking_Combos");
            if (!string.IsNullOrEmpty(combosJson))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(combosJson);
                foreach (var kv in dict)
                {
                    if (int.TryParse(kv.Key.Replace("Combo_", ""), out int comboId) &&
                        int.TryParse(kv.Value, out int qty) &&
                        qty > 0)
                    {
                        var combo = _context.Combos.FirstOrDefault(c => c.ComboId == comboId);
                        if (combo != null)
                        {
                            combos.Add(new ComboViewModel
                            {
                                ComboId = combo.ComboId,
                                ComboName = combo.Name,
                                Quantity = qty,
                                Price = combo.Price ?? 0
                            });
                        }
                    }
                }
            }

            // 🎟️ TÍNH TOÁN CHIẾT KHẤU HẠNG THÀNH VIÊN KHI KHÔI PHỤC (RẤT QUAN TRỌNG)
            decimal originalPrice = total; // Giá trị gốc trước khi chiết khấu
            string membershipLevel = customer.MembershipLevel ?? "Đồng";
            decimal mDiscountPercent = 0m;

            if (membershipLevel == "Kim cương")
                mDiscountPercent = 0.10m; // 10%
            else if (membershipLevel == "Bạc")
                mDiscountPercent = 0.05m; // 5%

            decimal membershipDiscountAmount = originalPrice * mDiscountPercent;
            decimal priceAfterMembership = originalPrice - membershipDiscountAmount;
            decimal finalPrice = priceAfterMembership;
            decimal voucherDiscountAmount = 0m;

            // 🎟️ APPLY VOUCHER KHI KHÔI PHỤC
            if (!string.IsNullOrEmpty(voucherCode))
            {
                var voucher = _context.Vouchers
                    .FirstOrDefault(v => v.Code == voucherCode && v.IsActive);

                if (voucher != null)
                {
                    if (voucher.DiscountPercent != null)
                        voucherDiscountAmount = priceAfterMembership * (decimal)voucher.DiscountPercent;
                    else if (voucher.DiscountAmount != null)
                        voucherDiscountAmount = voucher.DiscountAmount.Value;

                    finalPrice -= voucherDiscountAmount;
                }
            }

            if (finalPrice < 0) finalPrice = 0;

            // 📌 Build ViewModel
            var vm = new PaymentViewModel
            {
                CustomerName = customer.FullName,
                CustomerEmail = customer.Email,
                CustomerPhone = customer.Phone,

                MovieId = movieId,
                ShowtimeId = showtimeId,

                MovieTitle = showtime.Movie?.Title,
                Showtime = showtime.StartTime?.ToString("dd/MM/yyyy HH:mm"),
                Auditorium = showtime.Auditorium?.Name,

                TheaterName = showtime.Auditorium?.Theater?.Name,
                TheaterAddress = showtime.Auditorium?.Theater?.Address,
                TheaterPhone = showtime.Auditorium?.Theater?.Phone,

                SelectedSeats = seats?.ToList() ?? new List<string>(),
                AdultTickets = adult,
                ChildTickets = child,
                StudentTickets = student,
                TotalPrice = finalPrice,
                VoucherCode = voucherCode,
                DiscountAmount = voucherDiscountAmount,
                Combos = combos,

                // Gửi thông tin ưu đãi thành viên
                MembershipLevel = membershipLevel,
                MembershipDiscountPercent = mDiscountPercent * 100,
                MembershipDiscountAmount = membershipDiscountAmount,
                OriginalPrice = originalPrice
            };

            // Tính toán gợi ý nâng cấp combo khi khôi phục
            CalculateUpsellOffers(combos);

            return View("Index", vm);
        }

        // =================== [5] API Thanh toán lại ===================
        [HttpGet]
        public IActionResult CreatePayment(int orderId)
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
                return RedirectToAction("Login", "Customer");

            var order = _context.Orders
                .Include(o => o.Tickets)
                .FirstOrDefault(o => o.OrderId == orderId);

            if (order == null)
                return NotFound("Không tìm thấy đơn hàng.");

            if (order.CustomerId != customerId)
                return Forbid();

            if (order.Status != "Chờ thanh toán" && order.Status != "Đang chờ thanh toán")
            {
                TempData["ErrorMessage"] = "Đơn hàng này không ở trạng thái chờ thanh toán!";
                return RedirectToAction("MyTickets", "Tickets");
            }

            if (order.ExpiredAt < DateTime.Now)
            {
                order.Status = "Đã hủy";
                foreach (var t in order.Tickets)
                {
                    t.Status = "Đã hủy";
                    t.PaymentStatus = "Đã hủy";
                }
                _context.SaveChanges();

                // Gửi email hủy vé
                string reqBaseUrl = _config["AppSettings:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
                int cancelOrderId = order.OrderId;
                Task.Run(async () => {
                    try {
                        await _emailService.SendOrderCanceledEmailAsync(cancelOrderId, reqBaseUrl);
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Failed to send order canceled email for order {OrderId}", cancelOrderId);
                    }
                });

                TempData["ErrorMessage"] = "Đơn hàng đã hết hạn thanh toán!";
                return RedirectToAction("MyTickets", "Tickets");
            }

            if (order.PaymentMethod == "Chuyển khoản")
            {
                var pay = new VnpayLibrary();
                string baseUrl = _config["Vnpay:BaseUrl"];
                string returnUrl = _config["Vnpay:ReturnUrl"];
                if (!returnUrl.StartsWith("http"))
                {
                    returnUrl = $"{Request.Scheme}://{Request.Host}{returnUrl}";
                }
                string tmnCode = _config["Vnpay:TmnCode"];
                string hashSecret = _config["Vnpay:HashSecret"];

                pay.AddRequestData("vnp_Version", "2.1.0");
                pay.AddRequestData("vnp_Command", "pay");
                pay.AddRequestData("vnp_TmnCode", tmnCode);
                long finalAmount = Convert.ToInt64(Math.Round((order.TotalAmount ?? 0) * 100));
                pay.AddRequestData("vnp_Amount", finalAmount.ToString());
                pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                pay.AddRequestData("vnp_CurrCode", "VND");
                string ipAddr = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                if (ipAddr == "::1") ipAddr = "127.0.0.1";
                pay.AddRequestData("vnp_IpAddr", ipAddr);
                pay.AddRequestData("vnp_Locale", "vn");
                pay.AddRequestData("vnp_OrderInfo", $"Thanh toán đơn #{order.OrderId}");
                pay.AddRequestData("vnp_OrderType", "billpayment");
                pay.AddRequestData("vnp_ReturnUrl", returnUrl);
                pay.AddRequestData("vnp_TxnRef", order.OrderId.ToString());
                string paymentUrl = pay.CreateRequestUrl(baseUrl, hashSecret);

                order.Status = "Đang chờ thanh toán";
                _context.SaveChanges();

                return Redirect(paymentUrl);
            }
            else
            {
                TempData["SuccessMessage"] = "Đơn hàng của bạn sẽ được thanh toán tại quầy.";
                return RedirectToAction("MyTickets", "Tickets");
            }
        }

        // =================== [6] API Nâng cấp Combo (Upselling) ===================
        [HttpGet]
        public IActionResult UpgradeCombo(int currentId, int upgradedId)
        {
            var combosJson = HttpContext.Session.GetString("Booking_Combos");
            if (!string.IsNullOrEmpty(combosJson))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(combosJson);
                string currentKey = $"Combo_{currentId}";
                string upgradedKey = $"Combo_{upgradedId}";

                if (dict.ContainsKey(currentKey))
                {
                    string qty = dict[currentKey];
                    dict.Remove(currentKey);
                    dict[upgradedKey] = qty;

                    HttpContext.Session.SetString("Booking_Combos", JsonSerializer.Serialize(dict));
                }
            }
            return RedirectToAction(nameof(ResumePayment));
        }

        private void CalculateUpsellOffers(List<ComboViewModel> currentCombos)
        {
            var upsellOffers = new List<UpsellOffer>();

            // Tự động seed các Combo lớn nếu chưa có để đảm bảo có hàng hóa nâng cấp (Upselling)
            if (!_context.Combos.Any(c => c.Name.Contains("Lớn") || c.Name.Contains("Double") || c.Name.Contains("Solo") || c.Name.Contains("Party")))
            {
                try
                {
                    var seedCombos = new List<Combo>
                    {
                        new Combo { Name = "Combo Solo (Lớn)", Price = 55000, Description = "1 Bắp Caramel 64OZ + 1 Nước ngọt Lớn 32OZ", IsActive = true, ImageUrl = "/images/combo_solo.jpg" },
                        new Combo { Name = "Combo Double (Lớn)", Price = 85000, Description = "1 Bắp Caramel 64OZ + 2 Nước ngọt Lớn 32OZ", IsActive = true, ImageUrl = "/images/combo_double.jpg" },
                        new Combo { Name = "Combo Party (Lớn)", Price = 115000, Description = "2 Bắp Caramel 64OZ + 2 Nước ngọt Lớn 32OZ + 1 Snack", IsActive = true, ImageUrl = "/images/combo_party.jpg" }
                    };
                    _context.Combos.AddRange(seedCombos);
                    _context.SaveChanges();
                }
                catch {}
            }

            var allActiveCombos = _context.Combos.Where(c => c.IsActive == true).ToList();
            var rules = _recommendationEngine.GetRules();

            // Tập hợp tất cả ComboId đang có trong giỏ hàng (để loại trừ hoàn toàn)
            var cartComboIds = currentCombos.Select(c => c.ComboId).ToHashSet();

            foreach (var cartCombo in currentCombos)
            {
                Combo bestUpgrade = null;
                double maxConf = -1;

                // Danh sách combo hợp lệ để nâng cấp: không phải combo đang có trong giỏ, và giá cao hơn
                var upgradeCandidates = allActiveCombos
                    .Where(c => !cartComboIds.Contains(c.ComboId) && c.Price > cartCombo.Price)
                    .ToList();

                // ╔══════════════════════════════════════════════════════════════╗
                // ║  [APRIORI] Upselling Combo tại bước Thanh toán             ║
                // ║  Luật khai phá: Combo_A → Combo_B (giá cao hơn)            ║
                // ║  Nguồn: Lịch sử đơn hàng chứa nhiều Combo cùng lúc         ║
                // ╚══════════════════════════════════════════════════════════════╝
                foreach (var rule in rules)
                {
                    // [APRIORI] Kiểm tra: LHS của luật có chứa combo đang chọn không?
                    // Ví dụ luật: {Combo_1 (Bắp nhỏ)} → {Combo_3 (Solo Lớn)}  Confidence=58%
                    if (rule.LHS.Contains($"Combo_{cartCombo.ComboId}"))
                    {
                        foreach (var rhsItem in rule.RHS)
                        {
                            if (rhsItem.StartsWith("Combo_") && int.TryParse(rhsItem.Substring(6), out int upId))
                            {
                                // RHS là Combo đắt hơn & chưa có trong giỏ → đây là gợi ý nâng cấp
                                var cand = upgradeCandidates.FirstOrDefault(c => c.ComboId == upId);
                                if (cand != null && rule.Confidence > maxConf)
                                {
                                    bestUpgrade = cand;
                                    maxConf = rule.Confidence; // Chọn luật có Confidence cao nhất
                                }
                            }
                        }
                    }
                }

                // 2. Fallback: Tìm combo lớn hơn cùng danh mục (bắp/nước) không có trong giỏ
                if (bestUpgrade == null)
                {
                    // Xác định từ khóa danh mục dựa theo tên combo hiện tại
                    bool isBap = cartCombo.ComboName.Contains("Bắp") || cartCombo.ComboName.Contains("Bap") 
                                 || cartCombo.ComboName.Contains("Caramel") || cartCombo.ComboName.Contains("Popcorn");
                    bool isNuoc = !isBap; // Nước/Coke/Fanta/Sprite...

                    if (isBap)
                    {
                        // Tìm combo bắp lớn hơn hoặc combo kết hợp bắp nước
                        bestUpgrade = upgradeCandidates
                            .Where(c => c.Price <= cartCombo.Price + 80000
                                     && (c.Name.Contains("Bắp") || c.Name.Contains("Solo") 
                                         || c.Name.Contains("Double") || c.Name.Contains("Party") 
                                         || c.Name.Contains("Combo") || c.Name.Contains("Lớn")))
                            .OrderBy(c => c.Price)
                            .FirstOrDefault();
                    }
                    else
                    {
                        // Tìm combo nước lớn hơn hoặc combo kết hợp
                        bestUpgrade = upgradeCandidates
                            .Where(c => c.Price <= cartCombo.Price + 60000
                                     && (c.Name.Contains("Combo") || c.Name.Contains("Double") 
                                         || c.Name.Contains("Lớn") || c.Name.Contains("Party")
                                         || c.Name.Contains("Solo")))
                            .OrderBy(c => c.Price)
                            .FirstOrDefault();
                    }
                }

                // 3. Fallback cuối: Chỉ đề xuất nếu tìm được upgrade hợp lý (không đề xuất bất kỳ giá cao hơn nào)
                if (bestUpgrade != null)
                {
                    upsellOffers.Add(new UpsellOffer
                    {
                        CurrentComboId = cartCombo.ComboId,
                        CurrentComboName = cartCombo.ComboName,
                        UpgradedComboId = bestUpgrade.ComboId,
                        UpgradedComboName = bestUpgrade.Name,
                        PriceDiff = (bestUpgrade.Price ?? 0) - cartCombo.Price,
                        NewPrice = bestUpgrade.Price ?? 0
                    });
                }
            }
            ViewBag.UpsellOffers = upsellOffers;
        }
    }
}
