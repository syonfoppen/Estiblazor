using System;

namespace Estiblazor.UI.Domain.Common;

/// <summary>
/// Marker interface for domain events to support a simple domain event dispatcher.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
