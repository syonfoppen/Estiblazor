
using Estim8.UI.Services.Rooms;

namespace Estim8.UI.Services
{
    public interface IRoomCollection
    {
        RoomViewModel GetOrCreateRoom(string roomid);
        RoomViewModel? GetExistingRoom(string roomId);
        List<string> GetRoomNames();
    }
}