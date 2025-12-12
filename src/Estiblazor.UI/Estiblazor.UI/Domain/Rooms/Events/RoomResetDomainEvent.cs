using Estiblazor.UI.Domain.Common;

namespace Estiblazor.UI.Domain.Rooms.Events;

public record RoomResetDomainEvent(string RoomName) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
