using System.Collections.Immutable;

namespace Estiblazor.Domain;

public sealed record RoomState(
    int SchemaVersion,
    string Code,
    long Version,
    ImmutableHashSet<Guid> Owners,
    string ManagementHash,
    Configuration Configuration,
    Configuration? PendingConfiguration,
    ImmutableDictionary<Guid, Participant> Participants,
    Round Round,
    DateTimeOffset? RevealedAt,
    ImmutableArray<ArchivedRound> History,
    ImmutableHashSet<Guid> Commands,
    ImmutableArray<Participant> RevealedParticipants,
    bool NeedsManagementLink = false
)
{
    public static RoomState Create(string code, Guid owner, string hash, Configuration config)
    {
        var configuration = config.Validate();
        return new(
            1,
            code,
            0,
            [owner],
            hash,
            configuration,
            null,
            ImmutableDictionary<Guid, Participant>.Empty,
            new(Guid.NewGuid(), null, configuration, false, []),
            null,
            [],
            [],
            []
        );
    }

    public RoomState Apply(Guid actor, RoomCommand command, DateTimeOffset now)
    {
        if (command.Id == Guid.Empty)
            throw new RoomException("A command ID is required.");
        // Check duplicates before round freshness: a successful retry may refer to the
        // previous round after NextRound has already been committed.
        if (Commands.Contains(command.Id))
            return this;
        if (command.RoundId != Round.Id)
            throw new RoomException("This round has changed. Refresh and try again.");
        var next = this;
        switch (command)
        {
            case Join join:
                if (string.IsNullOrWhiteSpace(join.Name) || join.Name.Trim().Length > 50)
                    throw new RoomException("Display name must contain 1–50 characters.");
                var joiningVotes = Round.Votes;
                if (join.IsObserver && !Round.Revealed && joiningVotes.Any(v => v.UserId == actor))
                {
                    if (!join.DiscardVotesConfirmed)
                        throw new RoomException(
                            "Confirm withdrawing your open votes before observing."
                        );
                    joiningVotes = joiningVotes.Where(v => v.UserId != actor).ToImmutableArray();
                }
                next = this with
                {
                    Participants = Participants.SetItem(
                        actor,
                        new(actor, join.Name.Trim(), join.IsObserver)
                    ),
                    Round = Round with { Votes = joiningVotes },
                };
                break;
            case Vote vote:
                if (!Participants.ContainsKey(actor))
                    throw new RoomException("Join the room before voting.");
                if (Participants[actor].IsObserver)
                    throw new RoomException("Observers cannot vote. Switch to voter mode first.");
                if (Round.Revealed)
                    throw new RoomException("Voting is closed.");
                if (
                    vote.Phase < 0
                    || vote.Phase >= Round.Configuration.Phases.Length
                    || (
                        vote.Option != null
                        && !Round.Configuration.Phases[vote.Phase].Options.Contains(vote.Option)
                    )
                )
                    throw new RoomException("Choose an available option.");
                var votes = Round
                    .Votes.Where(v => v.UserId != actor || v.Phase != vote.Phase)
                    .ToImmutableArray();
                if (vote.Option != null)
                    votes = votes.Add(new(actor, vote.Phase, vote.Option));
                next = this with { Round = Round with { Votes = votes } };
                break;
            case Reveal:
                RequireOwner(actor);
                if (Round.Revealed)
                    return this;
                // Freeze participant names and roles with the votes for results and history.
                next = this with
                {
                    Round = Round with { Revealed = true },
                    RevealedAt = now,
                    RevealedParticipants = Participants
                        .Values.OrderBy(p => p.Name)
                        .ThenBy(p => p.Id)
                        .ToImmutableArray(),
                };
                break;
            case NextRound nextRound:
                RequireOwner(actor);
                if (!Round.Revealed && Round.Votes.Length > 0 && !nextRound.DiscardConfirmed)
                    throw new RoomException("Confirm discarding the unrevealed votes.");
                if (nextRound.Title?.Length > 200)
                    throw new RoomException("Round title may contain at most 200 characters.");
                var history = History;
                if (Round.Revealed)
                    history = history.Add(
                        new(
                            Round.Id,
                            Round.Title,
                            Round.Configuration,
                            RevealedParticipants,
                            Round.Votes,
                            RevealedAt!.Value
                        )
                    );
                var config = PendingConfiguration ?? Configuration;
                next = this with
                {
                    Configuration = config,
                    PendingConfiguration = null,
                    Round = new(
                        Guid.NewGuid(),
                        string.IsNullOrWhiteSpace(nextRound.Title) ? null : nextRound.Title.Trim(),
                        config,
                        false,
                        []
                    ),
                    History = history,
                    RevealedAt = null,
                    RevealedParticipants = [],
                };
                break;
            case Configure configure:
                RequireOwner(actor);
                if (configure.ExpectedVersion != Version)
                    throw new RoomException(
                        "The room changed while editing. Reload the editor before saving."
                    );
                if (!Round.Revealed && Round.Votes.Length > 0)
                    throw new RoomException("Reveal this round before changing configuration.");
                next = this with { PendingConfiguration = configure.Configuration.Validate() };
                break;
            case SetActivePhase setActivePhase:
                RequireOwner(actor);
                if (
                    setActivePhase.Phase < 0
                    || setActivePhase.Phase >= Round.Configuration.Phases.Length
                )
                    throw new RoomException("Choose an existing phase.");
                next = this with { Round = Round with { ActivePhase = setActivePhase.Phase } };
                break;
            case GrantOwner grantOwner:
                RequireOwner(actor);
                if (!Participants.ContainsKey(grantOwner.UserId))
                    throw new RoomException("Choose a participant in this room.");
                next = this with { Owners = Owners.Add(grantOwner.UserId) };
                break;
            case TransferOwnership transfer:
                RequireOwner(actor);
                if (transfer.UserId == actor || !Participants.ContainsKey(transfer.UserId))
                    throw new RoomException("Choose another participant in this room.");
                ValidateManagementHash(transfer.NewManagementHash);
                if (transfer.NewManagementHash == ManagementHash)
                    throw new RoomException(
                        "Ownership transfer must invalidate the old management link."
                    );
                next = this with
                {
                    Owners = [transfer.UserId],
                    ManagementHash = transfer.NewManagementHash,
                    NeedsManagementLink = true,
                };
                break;
            case RotateManagementLink rotate:
                RequireOwner(actor);
                ValidateManagementHash(rotate.NewManagementHash);
                next = this with
                {
                    ManagementHash = rotate.NewManagementHash,
                    NeedsManagementLink = false,
                };
                break;
            case Delete:
                RequireOwner(actor);
                break;
            default:
                throw new RoomException("Unknown command.");
        }
        return next with { Version = Version + 1, Commands = next.Commands.Add(command.Id) };
    }

    private static void ValidateManagementHash(string hash)
    {
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
            throw new RoomException("A valid management hash is required.");
    }

    private void RequireOwner(Guid actor)
    {
        if (!Owners.Contains(actor))
            throw new RoomException("Only the room owner can do this.");
    }
}
