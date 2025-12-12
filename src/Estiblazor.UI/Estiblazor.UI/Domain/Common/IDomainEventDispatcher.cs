using System.Threading;
using System.Threading.Tasks;

namespace Estiblazor.UI.Domain.Common;

public interface IDomainEventDispatcher
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
