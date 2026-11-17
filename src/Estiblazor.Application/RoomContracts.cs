using System.Collections.Immutable;
using Estiblazor.Domain;

namespace Estiblazor.Application;

public interface IRoomStore
{
    Task<RoomState?> ReadAsync(string code);
    Task<bool> CreateAsync(RoomState room);

    // Commit only if the stored version still matches before. A null after deletes
    // the room; false signals a concurrent change that the caller can retry.
    Task<bool> CompareExchangeAsync(RoomState before, RoomState? after);
}

public sealed record RoomReference(string Code, DateTimeOffset? LastVisited);

public interface IRoomDirectory
{
    Task<bool> RecordVisitAsync(RoomState room, Guid user);
    Task<ImmutableArray<RoomReference>> ListAsync(Guid user);
    Task ForgetAsync(string code, Guid user);
}

public interface IBrowserSession
{
    Task<Guid> GetUserIdAsync();
}

public interface IRoomUpdates
{
    IDisposable Subscribe(string code, Guid user, Guid connection, Func<Task> changed);
    Task PublishAsync(string code);
    bool IsOnline(string code, Guid user);
    Task SetConnectedAsync(Guid connection, bool connected);
}
