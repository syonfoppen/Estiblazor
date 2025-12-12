using Estiblazor.UI.Domain.Common;

namespace Estiblazor.UI.Domain.Rooms.Events;

public record UserMembershipChangedDomainEvent(string RoomName, string UserId, bool Joined) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
