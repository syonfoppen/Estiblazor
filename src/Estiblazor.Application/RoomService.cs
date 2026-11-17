using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Estiblazor.Domain;

namespace Estiblazor.Application;

public sealed class RoomService(IRoomStore store, IRoomUpdates updates, IRoomDirectory directory)
{
    private const int CreationAttempts = 5;
    private const int ConcurrencyAttempts = 100;
    public const int HistoryPageSize = 5;

    public async Task<CreatedRoom> CreateAsync(Guid actor, Configuration configuration)
    {
        if (actor == Guid.Empty)
            throw new RoomException("A browser identity is required.");
        configuration = configuration.Validate();
        for (var attempt = 0; attempt < CreationAttempts; attempt++)
        {
            var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
            var secret = NewSecret();
            if (await store.CreateAsync(RoomState.Create(code, actor, Hash(secret), configuration)))
                return new(code, secret);
        }
        throw new RoomException("Could not allocate a room code. Please retry.");
    }

    public async Task<RoomView> ReadAsync(string code, Guid actor)
    {
        var room = await GetAsync(code);
        var isOwner = room.Owners.Contains(actor);
        var participants = room
            .Participants.Values.OrderBy(participant => participant.Name)
            .ThenBy(participant => participant.Id)
            .Select(participant => BuildParticipantView(room, participant))
            .ToImmutableArray();

        // Vote progress is public, but card values remain private until the reveal.
        var visibleVotes = room
            .Round.Votes.Where(vote => room.Round.Revealed || vote.UserId == actor)
            .ToImmutableArray();

        return new RoomView(
            Code: room.Code,
            Version: room.Version,
            Name: room.Configuration.Name,
            IsOwner: isOwner,
            RoundId: room.Round.Id,
            Title: room.Round.Title,
            Revealed: room.Round.Revealed,
            Configuration: room.Round.Configuration,
            PendingConfiguration: room.PendingConfiguration,
            Participants: participants,
            VisibleVotes: visibleVotes,
            HistoryCount: room.History.Length,
            ActivePhase: room.Round.ActivePhase,
            Results: BuildResults(room),
            NeedsManagementLink: isOwner && room.NeedsManagementLink
        );
    }

    private ParticipantView BuildParticipantView(RoomState room, Participant participant)
    {
        var votedPhases = room
            .Round.Votes.Where(vote => vote.UserId == participant.Id)
            .Select(vote => vote.Phase)
            .ToImmutableArray();

        return new ParticipantView(
            participant.Id,
            participant.Name,
            updates.IsOnline(room.Code, participant.Id),
            votedPhases,
            participant.IsObserver,
            room.Owners.Contains(participant.Id)
        );
    }

    public async Task VisitAsync(string code, Guid actor)
    {
        for (var attempt = 0; attempt < ConcurrencyAttempts; attempt++)
        {
            var room = await GetAsync(code);
            if (await directory.RecordVisitAsync(room, actor))
                return;
        }
        throw new RoomException("The room is busy. Refresh to add it to your recent rooms.");
    }

    public async Task<ImmutableArray<RecentRoom>> RecentAsync(Guid actor)
    {
        var rooms = ImmutableArray.CreateBuilder<RecentRoom>();
        foreach (var reference in await directory.ListAsync(actor))
        {
            var room = await store.ReadAsync(reference.Code);
            if (room == null)
            {
                await directory.ForgetAsync(reference.Code, actor);
                continue;
            }
            rooms.Add(
                new(
                    room.Code,
                    room.Configuration.Name,
                    room.Owners.Contains(actor),
                    reference.LastVisited
                )
            );
        }
        return rooms.ToImmutable();
    }

    public async Task<Configuration> CopyConfigurationAsync(string code)
    {
        var room = await GetAsync(code);
        var name = room.Configuration.Name;
        const string suffix = " (copy)";
        const int maxNameLength = 100;
        return room.Configuration with
        {
            Name = name[..Math.Min(name.Length, maxNameLength - suffix.Length)] + suffix,
        };
    }

    public Task TransferAsync(string code, Guid actor, Guid roundId, Guid userId) =>
        ExecuteAsync(
            code,
            actor,
            new TransferOwnership(Guid.NewGuid(), roundId, userId, Hash(NewSecret()))
        );

    public async Task<string> RotateManagementAsync(string code, Guid actor, Guid roundId)
    {
        var secret = NewSecret();
        await ExecuteAsync(
            code,
            actor,
            new RotateManagementLink(Guid.NewGuid(), roundId, Hash(secret))
        );
        return secret;
    }

    private static ImmutableArray<PhaseResult> BuildResults(RoomState room)
    {
        if (!room.Round.Revealed)
            return [];
        // Use the reveal snapshot so later name or observer changes cannot rewrite results.
        var participants = room.RevealedParticipants.ToDictionary(p => p.Id);
        var eligible = participants.Values.Count(p => !p.IsObserver);
        return room
            .Round.Configuration.Phases.Select(
                (phase, index) =>
                {
                    var votes = room.Round.Votes.Where(v => v.Phase == index).ToArray();
                    var distribution = phase
                        .Options.Select(option =>
                        {
                            var voters = votes
                                .Where(v => v.Option == option)
                                .Select(v => participants[v.UserId])
                                .OrderBy(p => p.Name)
                                .ThenBy(p => p.Id)
                                .ToImmutableArray();
                            return new OptionResult(option, voters.Length, voters);
                        })
                        .Where(o => o.Count > 0)
                        .ToImmutableArray();
                    return new PhaseResult(index, eligible, votes.Length, distribution);
                }
            )
            .ToImmutableArray();
    }

    public async Task<ImmutableArray<ArchivedRound>> HistoryAsync(string code, int page)
    {
        var room = await GetAsync(code);
        return room
            .History.Reverse()
            .Skip(Math.Max(0, page) * HistoryPageSize)
            .Take(HistoryPageSize)
            .ToImmutableArray();
    }

    public async Task ExecuteAsync(string code, Guid actor, RoomCommand command)
    {
        for (var attempt = 0; attempt < ConcurrencyAttempts; attempt++)
        {
            var before = await GetAsync(code);
            var after = before.Apply(actor, command, DateTimeOffset.UtcNow);
            if (ReferenceEquals(before, after))
                return;
            // Retry against fresh state if another command committed first. Keep the command
            // ID unchanged so a repeated request cannot apply the same action twice.
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
        if (secret.Length != 64)
            throw new RoomException("Invalid management link.");
        for (var attempt = 0; attempt < ConcurrencyAttempts; attempt++)
        {
            var before = await GetAsync(code);
            if (
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(Hash(secret)),
                    Encoding.ASCII.GetBytes(before.ManagementHash)
                )
            )
                throw new RoomException("Invalid management link.");
            var after = before with
            {
                Version = before.Version + 1,
                Owners = before.Owners.Add(actor),
            };
            if (await store.CompareExchangeAsync(before, after))
            {
                await updates.PublishAsync(before.Code);
                return;
            }
        }
        throw new RoomException("The room is busy. Please retry.");
    }

    private async Task<RoomState> GetAsync(string code)
    {
        code = code.Trim().ToUpperInvariant();
        if (code.Length != 12 || !code.All(Uri.IsHexDigit))
            throw new RoomException("Room not found. Check the invitation or code.");
        return await store.ReadAsync(code)
            ?? throw new RoomException("Room not found. It may have been deleted.");
    }

    private static string NewSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
