using CINEMA.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CINEMA.Hubs;

namespace CINEMA.Controllers
{
    public class GroupBookingController : Controller
    {
        private readonly CinemaContext _context;
        private readonly IHubContext<GroupBookingHub> _hubContext;

        public GroupBookingController(CinemaContext context, IHubContext<GroupBookingHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // POST: GroupBooking/Create
        [HttpPost]
        public IActionResult Create(int showtimeId)
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
            {
                // Chuyển hướng đến đăng nhập, khi đăng nhập xong sẽ quay về BookTicket
                return RedirectToAction("Login", "Customer", new { ReturnUrl = Url.Action("BookTicket", "Home", new { id = _context.Showtimes.Find(showtimeId)?.MovieId, showtimeId = showtimeId }) });
            }

            // Tạo mã phòng chờ ngẫu nhiên: GRP-XXXXXXXX
            string roomId = "GRP-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

            var room = new GroupBookingRoom
            {
                RoomId = roomId,
                ShowtimeId = showtimeId,
                CreatedBy = customerId.Value,
                Status = "Waiting",
                ExpiresAt = DateTime.Now.AddMinutes(15),
                CreatedAt = DateTime.Now
            };

            _context.GroupBookingRooms.Add(room);
            _context.SaveChanges();

            // Tự động thêm trưởng phòng làm thành viên thứ 1
            var member = new GroupBookingMember
            {
                RoomId = roomId,
                CustomerId = customerId.Value,
                Status = "Joined"
            };
            _context.GroupBookingMembers.Add(member);
            _context.SaveChanges();

            return RedirectToAction("Room", new { roomId });
        }

        // GET: GroupBooking/Room/{roomId}
        public IActionResult Room(string roomId)
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
            {
                // Chuyển hướng đăng nhập kèm ReturnUrl để quay lại phòng nhóm sau khi đăng nhập
                return RedirectToAction("Login", "Customer", new { ReturnUrl = Url.Action("Room", "GroupBooking", new { roomId = roomId }) });
            }

            var room = _context.GroupBookingRooms
                .Include(r => r.Showtime).ThenInclude(s => s.Movie)
                .Include(r => r.Showtime).ThenInclude(s => s.Auditorium).ThenInclude(a => a.Theater)
                .Include(r => r.Members).ThenInclude(m => m.Customer)
                .Include(r => r.Members).ThenInclude(m => m.Seat)
                .FirstOrDefault(r => r.RoomId == roomId);

            if (room == null) return NotFound("Không tìm thấy phòng chờ đặt vé nhóm.");

            // Kiểm tra hết hạn phòng
            if (room.ExpiresAt < DateTime.Now && room.Status == "Waiting")
            {
                room.Status = "Expired";
                _context.SaveChanges();
            }

            if (room.Status == "Expired")
            {
                TempData["ErrorMessage"] = "Phòng chờ đặt vé nhóm này đã hết hạn (15 phút)!";
                return RedirectToAction("Index", "Home");
            }

            // Nếu người dùng hiện tại chưa thuộc phòng chờ, tự động thêm vào
            var currentMember = room.Members.FirstOrDefault(m => m.CustomerId == customerId.Value);
            if (currentMember == null)
            {
                if (room.Members.Count >= room.MaxMembers)
                {
                    TempData["ErrorMessage"] = "Phòng chờ đã đạt số lượng thành viên tối đa!";
                    return RedirectToAction("Index", "Home");
                }

                currentMember = new GroupBookingMember
                {
                    RoomId = roomId,
                    CustomerId = customerId.Value,
                    Status = "Joined"
                };
                _context.GroupBookingMembers.Add(currentMember);
                _context.SaveChanges();
                
                // Nạp lại danh sách thành viên mới
                _context.Entry(room).Collection(r => r.Members).Load();
            }

            // Lấy danh sách toàn bộ ghế trong phòng chiếu
            var seats = _context.Seats
                .Where(s => s.AuditoriumId == room.Showtime.AuditoriumId && s.IsActive == true)
                .ToList();

            // Ghế đã bán/đang thanh toán ngoài hệ thống (thanh toán bình thường hoặc của nhóm khác đã thanh toán)
            var now = DateTime.Now;
            var bookedSeats = _context.Tickets
                .Include(t => t.Order)
                .Where(t => t.ShowtimeId == room.ShowtimeId
                    && t.Order != null
                    && (t.Order.Status == "Đã thanh toán" 
                        || ((t.Order.Status == "Chờ thanh toán" || t.Order.Status == "Đang chờ thanh toán") 
                            && t.Order.ExpiredAt > now)))
                .Select(t => t.Seat.RowLabel + t.Seat.SeatNumber)
                .ToList();

            ViewBag.Seats = seats;
            ViewBag.BookedSeats = bookedSeats;
            ViewBag.CurrentCustomerId = customerId.Value;

            return View(room);
        }

        // POST: GroupBooking/ConfirmSeat
        [HttpPost]
        public async Task<IActionResult> ConfirmSeat(string roomId, int? seatId)
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null) return Unauthorized();

            var room = _context.GroupBookingRooms
                .Include(r => r.Members)
                .FirstOrDefault(r => r.RoomId == roomId);

            if (room == null || room.Status != "Waiting" || room.ExpiresAt < DateTime.Now)
                return BadRequest("Phòng chờ không khả dụng hoặc đã hết hạn.");

            var member = room.Members.FirstOrDefault(m => m.CustomerId == customerId.Value);
            if (member == null) return BadRequest("Thành viên không thuộc phòng chờ.");

            if (seatId != null)
            {
                var seat = _context.Seats.Find(seatId.Value);
                if (seat == null) return NotFound("Không tìm thấy ghế.");

                string seatCode = seat.RowLabel + seat.SeatNumber;

                // Kiểm tra xem ghế này đã được giữ bởi người khác TRONG CÙNG PHÒNG CHỜ chưa
                var duplicateMember = room.Members.FirstOrDefault(m => m.SeatId == seatId && m.CustomerId != customerId.Value);
                if (duplicateMember != null)
                {
                    return BadRequest("Ghế này đã được một thành viên khác trong phòng giữ.");
                }

                // Kiểm tra xem ghế đã bị chọn bởi các giao dịch khác (đã thanh toán hoặc chờ thanh toán) trong hệ thống chưa
                var now = DateTime.Now;
                var seatIsBooked = _context.Tickets
                    .Include(t => t.Order)
                    .Any(t => t.ShowtimeId == room.ShowtimeId
                        && t.SeatId == seatId.Value
                        && t.Order != null
                        && (t.Order.Status == "Đã thanh toán" 
                            || ((t.Order.Status == "Chờ thanh toán" || t.Order.Status == "Đang chờ thanh toán") 
                                && t.Order.ExpiredAt > now)));

                if (seatIsBooked)
                {
                    return BadRequest("Ghế đã bị người khác đặt hoặc đang thanh toán.");
                }

                // Đặt ghế cho thành viên
                member.SeatId = seatId;
                member.Status = "SeatSelected";
            }
            else
            {
                // Giải phóng ghế
                member.SeatId = null;
                member.Status = "Joined";
            }

            _context.SaveChanges();

            // Gửi tín hiệu SignalR đồng bộ đến tất cả thành viên trong nhóm
            var updatedSeat = seatId != null ? _context.Seats.Find(seatId.Value) : null;
            string updatedSeatCode = updatedSeat != null ? updatedSeat.RowLabel + updatedSeat.SeatNumber : "";

            if (seatId != null)
            {
                await _hubContext.Clients.Group(roomId).SendAsync("SeatSelectedByMember", customerId.Value, seatId.Value, updatedSeatCode);
            }
            else
            {
                await _hubContext.Clients.Group(roomId).SendAsync("SeatReleasedByMember", customerId.Value, 0, "");
            }

            return Json(new { success = true });
        }

        // GET: GroupBooking/RoomStatus
        [HttpGet]
        public IActionResult RoomStatus(string roomId)
        {
            var room = _context.GroupBookingRooms
                .Include(r => r.Members).ThenInclude(m => m.Customer)
                .Include(r => r.Members).ThenInclude(m => m.Seat)
                .FirstOrDefault(r => r.RoomId == roomId);

            if (room == null) return NotFound();

            var membersData = room.Members.Select(m => new
            {
                CustomerId = m.CustomerId,
                FullName = m.Customer.FullName,
                AvatarUrl = m.Customer.AvatarUrl ?? "/images/default-avatar.png",
                SeatCode = m.Seat != null ? m.Seat.RowLabel + m.Seat.SeatNumber : "Chưa chọn",
                SeatId = m.SeatId ?? 0,
                Status = m.Status,
                IsCreator = room.CreatedBy == m.CustomerId
            }).ToList();

            double secondsLeft = (room.ExpiresAt - DateTime.Now).TotalSeconds;
            if (secondsLeft < 0) secondsLeft = 0;

            return Json(new
            {
                status = room.Status,
                secondsLeft = Math.Floor(secondsLeft),
                members = membersData
            });
        }

        // GET: GroupBooking/StartCheckout
        // GET: GroupBooking/StartCheckout
        [HttpGet]
        public IActionResult StartCheckout(string roomId)
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null) return RedirectToAction("Login", "Customer");

            var room = _context.GroupBookingRooms
                .Include(r => r.Showtime) // BỔ SUNG DÒNG NÀY ĐỂ FIX LỖI NULL
                .Include(r => r.Members).ThenInclude(m => m.Seat)
                .FirstOrDefault(r => r.RoomId == roomId);

            if (room == null || room.Status != "Waiting" || room.ExpiresAt < DateTime.Now)
            {
                TempData["ErrorMessage"] = "Phòng chờ không còn khả dụng hoặc đã hết hạn.";
                return RedirectToAction("Index", "Home");
            }

            var member = room.Members.FirstOrDefault(m => m.CustomerId == customerId.Value);
            if (member == null || member.SeatId == null || member.Seat == null)
            {
                TempData["ErrorMessage"] = "Bạn chưa chọn ghế hoặc không thuộc phòng chờ này.";
                return RedirectToAction("Room", new { roomId });
            }

            // Lưu phòng chờ Id vào Session để sau khi Confirm / PaymentReturn biết giao dịch thuộc phòng đặt vé nhóm này
            HttpContext.Session.SetString("GroupBooking_RoomId", roomId);

            // Redirect sang controller Payment/Index với các tham số tương tự như BookTicket submit
            return RedirectToAction("RedirectToPaymentIndex", new
            {
                movieId = room.Showtime.MovieId, // Giờ đây room.Showtime đã có dữ liệu
                showtimeId = room.ShowtimeId,
                selectedSeats = room.Showtime.AuditoriumId == null ? "" : $"{member.Seat.RowLabel}{member.Seat.SeatNumber}"
            });
        }
        // Helper action để tạo HTTP POST giả lập qua trang trung gian hoặc chuyển tiếp tham số an toàn
        [HttpGet]
        public IActionResult RedirectToPaymentIndex(int movieId, int showtimeId, string selectedSeats)
        {
            var showtime = _context.Showtimes
                .Include(s => s.Auditorium)
                .FirstOrDefault(s => s.ShowtimeId == showtimeId);
            
            decimal basePrice = showtime?.BasePrice ?? 0m;
            decimal surcharge = 0m;

            if (showtime != null && !string.IsNullOrEmpty(selectedSeats))
            {
                var seat = _context.Seats.FirstOrDefault(s => s.AuditoriumId == showtime.AuditoriumId && (s.RowLabel + s.SeatNumber.ToString()) == selectedSeats);
                if (seat != null)
                {
                    if (seat.SeatType == "VIP")
                        surcharge = 30000m;
                    else if (seat.SeatType == "Couple")
                        surcharge = 100000m;
                }
            }

            ViewBag.MovieId = movieId;
            ViewBag.ShowtimeId = showtimeId;
            ViewBag.SelectedSeats = selectedSeats;
            ViewBag.TotalPrice = basePrice + surcharge;

            return View();
        }
    }
}
