using Estiblazor.UI.Domain.Common;

namespace Estiblazor.UI.Application.Rooms;

public interface IRoomEventBackplane
{
    Task PublishAsync(IDomainEvent domainEvent, Guid originId, CancellationToken cancellationToken = default);

    void Subscribe(Func<IDomainEvent, Guid, Task> handler);
}
