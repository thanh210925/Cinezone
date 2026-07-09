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

        public PaymentController(CinemaContext context, IConfiguration config, ILogger<PaymentController> logger, IVnpayService vnpayService)
        {
            _context = context;
            _config = config;
            _logger = logger;
            _vnpayService = vnpayService;
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
                TempData["MovieId"] = MovieId;
                TempData["ShowtimeId"] = ShowtimeId;
                var seatArray = selectedSeats?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    ?? Array.Empty<string>();

                TempData["Seats"] = JsonSerializer.Serialize(seatArray); TempData["AdultTickets"] = AdultTickets;
                TempData["ChildTickets"] = ChildTickets;
                TempData["StudentTickets"] = StudentTickets;
                TempData["TotalPrice"] = TotalPrice.ToString(CultureInfo.InvariantCulture);
                TempData["VoucherCode"] = VoucherCode;
                TempData["DiscountAmount"] = DiscountAmount;
                var comboDict = Request.Form.Keys
                    .Where(k => k.StartsWith("Combo_"))
                    .ToDictionary(k => k, k => Request.Form[k].ToString());

                TempData["Combos"] = JsonSerializer.Serialize(comboDict);

                var returnUrl = Url.Action(nameof(ResumePayment), "Payment");
                return RedirectToAction("Login", "Customer", new { ReturnUrl = returnUrl });
            }

            var customer = _context.Customers.Find(customerId);
            if (customer == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Customer", new { message = "Tài khoản không tồn tại." });
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
            // 🎟️ APPLY VOUCHER (HIỂN THỊ)
            if (!string.IsNullOrEmpty(VoucherCode))
            {
                var voucher = _context.Vouchers
                    .FirstOrDefault(v => v.Code == VoucherCode && v.IsActive);

                if (voucher != null)
                {
                    if (voucher.DiscountPercent != null)
                        TotalPrice -= TotalPrice * (decimal)voucher.DiscountPercent;

                    if (voucher.DiscountAmount != null)
                        TotalPrice -= voucher.DiscountAmount.Value;
                }
            }

            if (TotalPrice < 0) TotalPrice = 0;
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
                TotalPrice = TotalPrice,
                VoucherCode = VoucherCode,
                Combos = combosVm
            };

            return View(vm);
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

                decimal comboTotal = model.Combos?.Sum(c => c.Price * c.Quantity) ?? 0;
                decimal ticketOnlyTotal = model.TotalPrice - comboTotal;


                decimal pricePerTicket = model.SelectedSeats.Count > 0
                    ? ticketOnlyTotal / model.SelectedSeats.Count
                    : 0;
                decimal total = model.TotalPrice;

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
                            total -= total * (decimal)voucher.DiscountPercent;

                        if (voucher.DiscountAmount != null)
                            total -= voucher.DiscountAmount.Value;

                        //voucher.UsedCount++;
                        //_context.SaveChanges();
                    }
                }

                if (total < 0) total = 0;

                // 🔹 Tạo đơn hàng
                var order = new Order
                {
                    CustomerId = customerId.Value,
                    CreatedAt = DateTime.Now,
                    PaidAt = DateTime.Now,
                    ExpiredAt = DateTime.Now.AddMinutes(15),

                    TotalAmount = total,

                    VoucherCode = model.VoucherCode,

                    DiscountAmount = model.TotalPrice - total,

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
                    ViewBag.PaymentMethod = "Tại quầy";
                    ViewBag.PaymentStatus = "Chờ thanh toán";
                    ViewBag.BookingCode = $"CZ{order.OrderId:D6}";
                    ViewBag.Total = model.TotalPrice;
                    return View("Success", model);
                }

                // 🔹 Thanh toán VNPay
                if (method == "Chuyển khoản")
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

            if (TempData["MovieId"] == null)
                return RedirectToAction("Index", "Home");

            int movieId = (int)TempData["MovieId"];
            int showtimeId = (int)TempData["ShowtimeId"];
            var seats = JsonSerializer.Deserialize<string[]>((string)TempData["Seats"]);
            int adult = (int)TempData["AdultTickets"];
            int child = (int)TempData["ChildTickets"];
            int student = (int)TempData["StudentTickets"];
            decimal total = decimal.Parse((string)TempData["TotalPrice"], CultureInfo.InvariantCulture);
            string voucherCode = (string)TempData["VoucherCode"];
            decimal? discountAmount = TempData["DiscountAmount"] as decimal?;
            TempData.Keep();

            // 📌 Load Theater
            var showtime = _context.Showtimes
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .Include(s => s.Movie)
                .FirstOrDefault(s => s.ShowtimeId == showtimeId);

            if (showtime == null) return NotFound();

            // 📌 Lấy combo từ TempData
            var combos = new List<ComboViewModel>();
            if (TempData["Combos"] != null)
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>((string)TempData["Combos"]);
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
                TotalPrice = total,
                VoucherCode = voucherCode,
                DiscountAmount = discountAmount,
                Combos = combos
            };

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
    }
}
