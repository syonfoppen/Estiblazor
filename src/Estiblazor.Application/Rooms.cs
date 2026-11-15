using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Estiblazor.Domain;
namespace Estiblazor.Application;

public interface IRoomStore
{
    Task<RoomState?> ReadAsync(string code);
    Task<bool> CreateAsync(RoomState room);
    Task<bool> CompareExchangeAsync(RoomState before, RoomState? after);
}
public sealed record RoomReference(string Code, DateTimeOffset? LastVisited);
public interface IRoomDirectory
{
    Task<bool> RecordVisitAsync(RoomState room, Guid user);
    Task<ImmutableArray<RoomReference>> ListAsync(Guid user);
    Task ForgetAsync(string code, Guid user);
}
public interface IBrowserSession { Task<Guid> GetUserIdAsync(); }
public interface IRoomUpdates
{
    IDisposable Subscribe(string code, Guid user, Guid connection, Func<Task> changed);
    Task PublishAsync(string code);
    bool IsOnline(string code, Guid user);
    Task SetConnectedAsync(Guid connection, bool connected);
}
public sealed class StorageUnavailableException(Exception? inner = null) : Exception("Storage is temporarily unavailable. Nothing is confirmed saved. Reconnect and retry.", inner);
public sealed record ParticipantView(Guid Id, string Name, bool Online, ImmutableArray<int> VotedPhases, bool IsObserver = false, bool IsOwner = false);
public sealed record RoomView(string Code, long Version, string Name, bool IsOwner, Guid RoundId, string? Title, bool Revealed,
    Configuration Configuration, Configuration? PendingConfiguration, ImmutableArray<ParticipantView> Participants,
    ImmutableArray<Ballot> VisibleVotes, int HistoryCount, int ActivePhase, ImmutableArray<PhaseResult> Results, bool NeedsManagementLink);
public sealed record OptionResult(string Option, int Count, ImmutableArray<Participant> Voters);
public sealed record PhaseResult(int Phase, int EligibleVoters, int VotesCast, ImmutableArray<OptionResult> Distribution)
{
    public bool HasDifferences => Distribution.Length > 1;
}
public sealed record RecentRoom(string Code, string Name, bool IsOwner, DateTimeOffset? LastVisited);
public sealed record CreatedRoom(string Code, string ManagementSecret);

public sealed class RoomService(IRoomStore store, IRoomUpdates updates, IRoomDirectory directory)
{
    public async Task<CreatedRoom> CreateAsync(Guid actor, Configuration configuration)
    {
        if (actor == Guid.Empty) throw new RoomException("A browser identity is required.");
        configuration = configuration.Validate();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
            var secret = NewSecret();
            if (await store.CreateAsync(RoomState.Create(code, actor, Hash(secret), configuration))) return new(code, secret);
        }
        throw new RoomException("Could not allocate a room code. Please retry.");
    }
    public async Task<RoomView> ReadAsync(string code, Guid actor)
    {
        var r = await GetAsync(code);
        return new(r.Code, r.Version, r.Configuration.Name, r.Owners.Contains(actor), r.Round.Id, r.Round.Title, r.Round.Revealed,
            r.Round.Configuration, r.PendingConfiguration,
            r.Participants.Values.OrderBy(p => p.Name).ThenBy(p => p.Id).Select(p => new ParticipantView(p.Id, p.Name, updates.IsOnline(r.Code, p.Id), r.Round.Votes.Where(v => v.UserId == p.Id).Select(v => v.Phase).ToImmutableArray(), p.IsObserver, r.Owners.Contains(p.Id))).ToImmutableArray(),
            r.Round.Votes.Where(v => r.Round.Revealed || v.UserId == actor).ToImmutableArray(), r.History.Length, r.Round.ActivePhase, BuildResults(r), r.Owners.Contains(actor) && r.NeedsManagementLink);
    }
    public async Task VisitAsync(string code, Guid actor)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var room = await GetAsync(code);
            if (await directory.RecordVisitAsync(room, actor)) return;
        }
        throw new RoomException("The room is busy. Refresh to add it to your recent rooms.");
    }
    public async Task<ImmutableArray<RecentRoom>> RecentAsync(Guid actor)
    {
        var rooms = ImmutableArray.CreateBuilder<RecentRoom>();
        foreach (var reference in await directory.ListAsync(actor))
        {
            var room = await store.ReadAsync(reference.Code);
            if (room == null) { await directory.ForgetAsync(reference.Code, actor); continue; }
            rooms.Add(new(room.Code, room.Configuration.Name, room.Owners.Contains(actor), reference.LastVisited));
        }
        return rooms.ToImmutable();
    }
    public async Task<Configuration> CopyConfigurationAsync(string code)
    {
        var room = await GetAsync(code);
        var name = room.Configuration.Name;
        return room.Configuration with { Name = name[..Math.Min(name.Length, 93)] + " (copy)" };
    }
    public Task TransferAsync(string code, Guid actor, Guid roundId, Guid userId) =>
        ExecuteAsync(code, actor, new TransferOwnership(Guid.NewGuid(), roundId, userId, Hash(NewSecret())));
    public async Task<string> RotateManagementAsync(string code, Guid actor, Guid roundId)
    {
        var secret = NewSecret();
        await ExecuteAsync(code, actor, new RotateManagementLink(Guid.NewGuid(), roundId, Hash(secret)));
        return secret;
    }
    private static ImmutableArray<PhaseResult> BuildResults(RoomState room)
    {
        if (!room.Round.Revealed) return [];
        var participants = room.RevealedParticipants.ToDictionary(p => p.Id);
        var eligible = participants.Values.Count(p => !p.IsObserver);
        return room.Round.Configuration.Phases.Select((phase, index) =>
        {
            var votes = room.Round.Votes.Where(v => v.Phase == index).ToArray();
            var distribution = phase.Options.Select(option =>
            {
                var voters = votes.Where(v => v.Option == option).Select(v => participants[v.UserId]).OrderBy(p => p.Name).ThenBy(p => p.Id).ToImmutableArray();
                return new OptionResult(option, voters.Length, voters);
            }).Where(o => o.Count > 0).ToImmutableArray();
            return new PhaseResult(index, eligible, votes.Length, distribution);
        }).ToImmutableArray();
    }
    public async Task<ImmutableArray<ArchivedRound>> HistoryAsync(string code, int page)
    {
        var r = await GetAsync(code);
        return r.History.Reverse().Skip(Math.Max(0, page) * 5).Take(5).ToImmutableArray();
    }
    public async Task ExecuteAsync(string code, Guid actor, RoomCommand command)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var before = await GetAsync(code);
            var after = before.Apply(actor, command, DateTimeOffset.UtcNow);
            if (ReferenceEquals(before, after)) return;
            if (await store.CompareExchangeAsync(before, command is Delete ? null : after))
            {
                await updates.PublishAsync(before.Code);
                return;
            }
            await Task.Delay(Random.Shared.Next(1, 12));
        }
        throw new RoomException("The room is busy. Please retry.");
    }
    public async Task RecoverAsync(string code, Guid actor, string secret)
    {
        if (secret.Length != 64) throw new RoomException("Invalid management link.");
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var before = await GetAsync(code);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(secret)), Encoding.ASCII.GetBytes(before.ManagementHash))) throw new RoomException("Invalid management link.");
            var after = before with { Version = before.Version + 1, Owners = before.Owners.Add(actor) };
            if (await store.CompareExchangeAsync(before, after)) { await updates.PublishAsync(before.Code); return; }
        }
        throw new RoomException("The room is busy. Please retry.");
    }
    private async Task<RoomState> GetAsync(string code)
    {
        code = code.Trim().ToUpperInvariant();
        if (code.Length != 12 || !code.All(Uri.IsHexDigit)) throw new RoomException("Room not found. Check the invitation or code.");
        return await store.ReadAsync(code) ?? throw new RoomException("Room not found. It may have been deleted.");
    }
    private static string NewSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
