using Estiblazor.Application;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Estiblazor.UI.Services;

public sealed class CircuitPresence(IRoomUpdates updates) : CircuitHandler
{
    public Guid ConnectionId { get; } = Guid.NewGuid();

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken token) =>
        updates.SetConnectedAsync(ConnectionId, true);

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken token) =>
        updates.SetConnectedAsync(ConnectionId, false);

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken token) =>
        updates.SetConnectedAsync(ConnectionId, false);
}
