using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace CINEMA.Hubs
{
    public class SeatHub : Hub
    {
        // Quản lý ghế đang được giữ tạm thời: showtimeId -> (seatCode -> connectionId)
        private static readonly ConcurrentDictionary<int, ConcurrentDictionary<string, string>> _heldSeats = new();

        /// <summary>
        /// Tham gia phòng suất chiếu real-time
        /// </summary>
        public async Task JoinShowtimeRoom(int showtimeId)
        {
            string groupName = $"showtime_{showtimeId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            if (_heldSeats.TryGetValue(showtimeId, out var seatsMap))
            {
                var heldByOthers = seatsMap.Where(kv => kv.Value != Context.ConnectionId).Select(kv => kv.Key).ToList();
                var myHeldSeats = seatsMap.Where(kv => kv.Value == Context.ConnectionId).Select(kv => kv.Key).ToList();
                await Clients.Caller.SendAsync("CurrentHeldSeats", heldByOthers, myHeldSeats);
            }
            else
            {
                await Clients.Caller.SendAsync("CurrentHeldSeats", new List<string>(), new List<string>());
            }
        }

        /// <summary>
        /// Đang giữ chỗ tạm thời (vừa click chọn ghế)
        /// </summary>
        public async Task HoldSeat(int showtimeId, string seatCode)
        {
            if (showtimeId <= 0 || string.IsNullOrEmpty(seatCode)) return;

            var showtimeSeats = _heldSeats.GetOrAdd(showtimeId, _ => new ConcurrentDictionary<string, string>());

            if (showtimeSeats.TryGetValue(seatCode, out var existingConnId) && existingConnId != Context.ConnectionId)
            {
                await Clients.Caller.SendAsync("SeatHoldFailed", seatCode, "Ghế này đang được người khác chọn.");
                return;
            }

            showtimeSeats[seatCode] = Context.ConnectionId;
            string groupName = $"showtime_{showtimeId}";
            await Clients.OthersInGroup(groupName).SendAsync("SeatHeldByOther", seatCode);
            await Clients.Caller.SendAsync("SeatHoldSuccess", seatCode);
        }

        /// <summary>
        /// Bỏ chọn / nhả ghế
        /// </summary>
        public async Task ReleaseSeat(int showtimeId, string seatCode)
        {
            if (showtimeId <= 0 || string.IsNullOrEmpty(seatCode)) return;

            if (_heldSeats.TryGetValue(showtimeId, out var showtimeSeats))
            {
                if (showtimeSeats.TryGetValue(seatCode, out var connId) && connId == Context.ConnectionId)
                {
                    showtimeSeats.TryRemove(seatCode, out _);
                    string groupName = $"showtime_{showtimeId}";
                    await Clients.OthersInGroup(groupName).SendAsync("SeatReleasedByOther", seatCode);
                }
            }
        }

        /// <summary>
        /// Tự động nhả tất cả ghế của khách khi đóng tab / rời trang
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            foreach (var kvp in _heldSeats)
            {
                int showtimeId = kvp.Key;
                var showtimeSeats = kvp.Value;
                var seatsToRemove = showtimeSeats.Where(s => s.Value == Context.ConnectionId).Select(s => s.Key).ToList();

                foreach (var seatCode in seatsToRemove)
                {
                    if (showtimeSeats.TryRemove(seatCode, out _))
                    {
                        string groupName = $"showtime_{showtimeId}";
                        await Clients.OthersInGroup(groupName).SendAsync("SeatReleasedByOther", seatCode);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
