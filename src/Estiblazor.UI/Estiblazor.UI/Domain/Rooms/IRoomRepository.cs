namespace Estiblazor.UI.Domain.Rooms;

public interface IRoomRepository
{
    Room? Get(string roomId);
    void Save(Room room);
    IEnumerable<string> GetRoomNames();
}
