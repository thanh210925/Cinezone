using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace CINEMA.Hubs
{
    public class GroupBookingHub : Hub
    {
        public async Task JoinRoom(string roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            await Clients.Group(roomId).SendAsync("UserJoinedRoom");
        }

        public async Task SelectSeat(string roomId, int customerId, int seatId, string seatCode)
        {
            await Clients.Group(roomId).SendAsync("SeatSelectedByMember", customerId, seatId, seatCode);
        }

        public async Task ReleaseSeat(string roomId, int customerId, int seatId, string seatCode)
        {
            await Clients.Group(roomId).SendAsync("SeatReleasedByMember", customerId, seatId, seatCode);
        }
    }
}
