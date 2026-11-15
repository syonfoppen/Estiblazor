using System.Collections.Immutable;
namespace Estiblazor.Domain;

public sealed class RoomException(string message) : Exception(message);
public sealed record Phase(string Name, ImmutableArray<string> Options);
public sealed record Configuration(string Name, ImmutableArray<Phase> Phases)
{
    public Configuration Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 100) throw new RoomException("Room name must contain 1–100 characters.");
        if (Phases.IsDefault || Phases.Length is < 1 or > 10) throw new RoomException("Use 1–10 phases.");
        foreach (var phase in Phases)
        {
            if (string.IsNullOrWhiteSpace(phase.Name) || phase.Name.Trim().Length > 50) throw new RoomException("Phase names must contain 1–50 characters.");
            if (phase.Options.IsDefault || phase.Options.Length is < 2 or > 30 || phase.Options.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > 30))
                throw new RoomException("Each phase needs 2–30 options of 1–30 characters.");
            if (phase.Options.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != phase.Options.Length)
                throw new RoomException("Options within a phase must be unique.");
        }
        return this with { Name = Name.Trim(), Phases = Phases.Select(p => p with { Name = p.Name.Trim(), Options = p.Options.Select(x => x.Trim()).ToImmutableArray() }).ToImmutableArray() };
    }
    public static Configuration Template(string name) => RoomTemplates.CreateConfiguration(name);
}
public sealed record Participant(Guid Id, string Name, bool IsObserver = false);
public sealed record Ballot(Guid UserId, int Phase, string Option);
public sealed record Round(Guid Id, string? Title, Configuration Configuration, bool Revealed, ImmutableArray<Ballot> Votes, int ActivePhase = 0);
public sealed record ArchivedRound(Guid Id, string? Title, Configuration Configuration, ImmutableArray<Participant> Participants, ImmutableArray<Ballot> Votes, DateTimeOffset RevealedAt);
public sealed record RoomState(int SchemaVersion, string Code, long Version, ImmutableHashSet<Guid> Owners, string ManagementHash,
    Configuration Configuration, Configuration? PendingConfiguration, ImmutableDictionary<Guid, Participant> Participants,
    Round Round, DateTimeOffset? RevealedAt, ImmutableArray<ArchivedRound> History, ImmutableHashSet<Guid> Commands, ImmutableArray<Participant> RevealedParticipants, bool NeedsManagementLink = false)
{
    public static RoomState Create(string code, Guid owner, string hash, Configuration config) => new(1, code, 0, [owner], hash,
        config.Validate(), null, ImmutableDictionary<Guid, Participant>.Empty,
        new(Guid.NewGuid(), null, config.Validate(), false, []), null, [], [], []);

    public RoomState Apply(Guid actor, RoomCommand command, DateTimeOffset now)
    {
        if (command.Id == Guid.Empty) throw new RoomException("A command ID is required.");
        if (Commands.Contains(command.Id)) return this;
        if (command.RoundId != Round.Id) throw new RoomException("This round has changed. Refresh and try again.");
        var next = this;
        switch (command)
        {
            case Join c:
                if (string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > 50) throw new RoomException("Display name must contain 1–50 characters.");
                var joiningVotes = Round.Votes;
                if (c.IsObserver && !Round.Revealed && joiningVotes.Any(v => v.UserId == actor))
                {
                    if (!c.DiscardVotesConfirmed) throw new RoomException("Confirm withdrawing your open votes before observing.");
                    joiningVotes = joiningVotes.Where(v => v.UserId != actor).ToImmutableArray();
                }
                next = this with { Participants = Participants.SetItem(actor, new(actor, c.Name.Trim(), c.IsObserver)), Round = Round with { Votes = joiningVotes } };
                break;
            case Vote c:
                if (!Participants.ContainsKey(actor)) throw new RoomException("Join the room before voting.");
                if (Participants[actor].IsObserver) throw new RoomException("Observers cannot vote. Switch to voter mode first.");
                if (Round.Revealed) throw new RoomException("Voting is closed.");
                if (c.Phase < 0 || c.Phase >= Round.Configuration.Phases.Length || (c.Option != null && !Round.Configuration.Phases[c.Phase].Options.Contains(c.Option)))
                    throw new RoomException("Choose an available option.");
                var votes = Round.Votes.Where(v => v.UserId != actor || v.Phase != c.Phase).ToImmutableArray();
                if (c.Option != null) votes = votes.Add(new(actor, c.Phase, c.Option));
                next = this with { Round = Round with { Votes = votes } };
                break;
            case Reveal:
                RequireOwner(actor);
                if (Round.Revealed) return this;
                next = this with { Round = Round with { Revealed = true }, RevealedAt = now, RevealedParticipants = Participants.Values.OrderBy(p => p.Name).ThenBy(p => p.Id).ToImmutableArray() };
                break;
            case NextRound c:
                RequireOwner(actor);
                if (!Round.Revealed && Round.Votes.Length > 0 && !c.DiscardConfirmed) throw new RoomException("Confirm discarding the unrevealed votes.");
                if (c.Title?.Length > 200) throw new RoomException("Round title may contain at most 200 characters.");
                var history = History;
                if (Round.Revealed) history = history.Add(new(Round.Id, Round.Title, Round.Configuration, RevealedParticipants, Round.Votes, RevealedAt!.Value));
                var config = PendingConfiguration ?? Configuration;
                next = this with { Configuration = config, PendingConfiguration = null, Round = new(Guid.NewGuid(), string.IsNullOrWhiteSpace(c.Title) ? null : c.Title.Trim(), config, false, []), History = history, RevealedAt = null, RevealedParticipants = [] };
                break;
            case Configure c:
                RequireOwner(actor);
                if (c.ExpectedVersion != Version) throw new RoomException("The room changed while editing. Reload the editor before saving.");
                if (!Round.Revealed && Round.Votes.Length > 0) throw new RoomException("Reveal this round before changing configuration.");
                next = this with { PendingConfiguration = c.Configuration.Validate() };
                break;
            case SetActivePhase c:
                RequireOwner(actor);
                if (c.Phase < 0 || c.Phase >= Round.Configuration.Phases.Length) throw new RoomException("Choose an existing phase.");
                next = this with { Round = Round with { ActivePhase = c.Phase } };
                break;
            case GrantOwner c:
                RequireOwner(actor);
                if (!Participants.ContainsKey(c.UserId)) throw new RoomException("Choose a participant in this room.");
                next = this with { Owners = Owners.Add(c.UserId) };
                break;
            case TransferOwnership c:
                RequireOwner(actor);
                if (c.UserId == actor || !Participants.ContainsKey(c.UserId)) throw new RoomException("Choose another participant in this room.");
                ValidateManagementHash(c.NewManagementHash);
                if (c.NewManagementHash == ManagementHash) throw new RoomException("Ownership transfer must invalidate the old management link.");
                next = this with { Owners = [c.UserId], ManagementHash = c.NewManagementHash, NeedsManagementLink = true };
                break;
            case RotateManagementLink c:
                RequireOwner(actor);
                ValidateManagementHash(c.NewManagementHash);
                next = this with { ManagementHash = c.NewManagementHash, NeedsManagementLink = false };
                break;
            case Delete:
                RequireOwner(actor);
                break;
            default: throw new RoomException("Unknown command.");
        }
        return next with { Version = Version + 1, Commands = next.Commands.Add(command.Id) };
    }
    private static void ValidateManagementHash(string hash)
    {
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)) throw new RoomException("A valid management hash is required.");
    }
    public void RequireOwner(Guid actor)
    {
        if (!Owners.Contains(actor)) throw new RoomException("Only the room owner can do this.");
    }
}
public abstract record RoomCommand(Guid Id, Guid RoundId);
public sealed record Join(Guid Id, Guid RoundId, string Name, bool IsObserver = false, bool DiscardVotesConfirmed = false) : RoomCommand(Id, RoundId);
public sealed record Vote(Guid Id, Guid RoundId, int Phase, string? Option) : RoomCommand(Id, RoundId);
public sealed record Reveal(Guid Id, Guid RoundId) : RoomCommand(Id, RoundId);
public sealed record NextRound(Guid Id, Guid RoundId, string? Title, bool DiscardConfirmed) : RoomCommand(Id, RoundId);
public sealed record Configure(Guid Id, Guid RoundId, long ExpectedVersion, Configuration Configuration) : RoomCommand(Id, RoundId);
public sealed record Delete(Guid Id, Guid RoundId) : RoomCommand(Id, RoundId);

public sealed record SetActivePhase(Guid Id, Guid RoundId, int Phase) : RoomCommand(Id, RoundId);
public sealed record GrantOwner(Guid Id, Guid RoundId, Guid UserId) : RoomCommand(Id, RoundId);
public sealed record TransferOwnership(Guid Id, Guid RoundId, Guid UserId, string NewManagementHash) : RoomCommand(Id, RoundId);
public sealed record RotateManagementLink(Guid Id, Guid RoundId, string NewManagementHash) : RoomCommand(Id, RoundId);
