using System.Collections.Concurrent;
using Estiblazor.Application;

namespace Estiblazor.UI.Services;

public sealed class RoomUpdates(ILogger<RoomUpdates> logger) : IRoomUpdates
{
    private sealed record Subscription(string Code, Guid User, Guid Connection, Func<Task> Changed);

    private readonly ConcurrentDictionary<Guid, Subscription> subscriptions = new();
    private readonly ConcurrentDictionary<Guid, bool> connections = new();

    public IDisposable Subscribe(string code, Guid user, Guid connection, Func<Task> changed)
    {
        var id = Guid.NewGuid();
        subscriptions[id] = new(code, user, connection, changed);
        return new Registration(() =>
        {
            subscriptions.TryRemove(id, out _);
        });
    }

    public bool IsOnline(string code, Guid user) =>
        subscriptions.Values.Any(s =>
            s.Code == code && s.User == user && connections.GetValueOrDefault(s.Connection)
        );

    public async Task PublishAsync(string code)
    {
        foreach (var s in subscriptions.Values.Where(s => s.Code == code).ToArray())
            try
            {
                await s.Changed();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Room subscriber is unavailable");
            }
    }

    public async Task SetConnectedAsync(Guid connection, bool connected)
    {
        if (connected)
            connections[connection] = true;
        else
            connections.TryRemove(connection, out _);
        foreach (
            var code in subscriptions
                .Values.Where(s => s.Connection == connection)
                .Select(s => s.Code)
                .Distinct()
                .ToArray()
        )
            await PublishAsync(code);
    }

    private sealed class Registration(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
