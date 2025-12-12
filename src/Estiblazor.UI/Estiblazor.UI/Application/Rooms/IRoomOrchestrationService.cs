using Estiblazor.UI.Services.Rooms;
using Estiblazor.UI.Services.Users;

namespace Estiblazor.UI.Application.Rooms;

public interface IRoomOrchestrationService
{
    RoomViewModel GetOrCreateRoom(string roomId);
    RoomViewModel? GetExistingRoom(string roomId);
    List<string> GetRoomNames();
    string CreateRoom(IEnumerable<NewStageModel> stages);
    void RevealAllStages(string roomId);
    void ResetAllStages(string roomId);
    void SetChoice(string roomId, int stageIndex, UserId userId, string choice);
}
