using System.Threading;
using System.Threading.Tasks;

namespace Estiblazor.UI.Domain.Common;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
