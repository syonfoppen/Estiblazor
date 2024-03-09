using Estiblazor.UI.Enums;

namespace Estiblazor.UI.Services.Rooms
{
    public interface IRoomTemplateService
    {
        ICollection<EstimationStage> GetRoomTemplate(RoomTemplates roomTemplate);
    }
}
