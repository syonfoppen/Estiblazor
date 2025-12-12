using Estiblazor.UI.Domain.Common;
using Estiblazor.UI.Services.Users;

namespace Estiblazor.UI.Domain.Rooms.Events;

public record ChoiceChangedDomainEvent(string RoomName, string StageName, UserId UserId, string? Choice) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
