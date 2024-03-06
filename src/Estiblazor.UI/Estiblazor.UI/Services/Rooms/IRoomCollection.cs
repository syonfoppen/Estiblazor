
using Estiblazor.UI.Services.Rooms;

namespace Estiblazor.UI.Services
{
    public interface IRoomCollection
    {
        RoomViewModel GetOrCreateRoom(string roomid);
        RoomViewModel? GetExistingRoom(string roomId);
        List<string> GetRoomNames();
    }
}