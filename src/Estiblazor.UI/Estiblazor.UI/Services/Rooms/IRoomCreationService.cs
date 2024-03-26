using Estiblazor.UI.Enums;

namespace Estiblazor.UI.Services.Rooms
{
    public interface IRoomCreationService
    {
        void SetRoomTemplate(RoomTemplates roomTemplate);
        NewStageModel? GetSelectedStage();

        IEnumerable<KeyValuePair<Guid, string>> GetStages();
        void AddNewStage(string stageName);
        void AddNewChoice(string choice);
        void RemoveChoice(Guid choiceId);
        bool IsSelectedStage(Guid stageId);
        void SetSelectedStage(Guid stageId);
        string CreateRoom();
    }
}
