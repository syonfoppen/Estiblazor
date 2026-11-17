using System.Collections.Immutable;
using Estiblazor.Domain;

namespace Estiblazor.Application;

public sealed record ParticipantView(
    Guid Id,
    string Name,
    bool Online,
    ImmutableArray<int> VotedPhases,
    bool IsObserver = false,
    bool IsOwner = false
);

public sealed record RoomView(
    string Code,
    long Version,
    string Name,
    bool IsOwner,
    Guid RoundId,
    string? Title,
    bool Revealed,
    Configuration Configuration,
    Configuration? PendingConfiguration,
    ImmutableArray<ParticipantView> Participants,
    ImmutableArray<Ballot> VisibleVotes,
    int HistoryCount,
    int ActivePhase,
    ImmutableArray<PhaseResult> Results,
    bool NeedsManagementLink
);

public sealed record OptionResult(string Option, int Count, ImmutableArray<Participant> Voters);

public sealed record PhaseResult(
    int Phase,
    int EligibleVoters,
    int VotesCast,
    ImmutableArray<OptionResult> Distribution
)
{
    public bool HasDifferences => Distribution.Length > 1;
}

public sealed record RecentRoom(
    string Code,
    string Name,
    bool IsOwner,
    DateTimeOffset? LastVisited
);

public sealed record CreatedRoom(string Code, string ManagementSecret);
