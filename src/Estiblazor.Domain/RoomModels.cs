using System.Collections.Immutable;

namespace Estiblazor.Domain;

public sealed record Participant(Guid Id, string Name, bool IsObserver = false);

public sealed record Ballot(Guid UserId, int Phase, string Option);

public sealed record Round(
    Guid Id,
    string? Title,
    Configuration Configuration,
    bool Revealed,
    ImmutableArray<Ballot> Votes,
    int ActivePhase = 0
);

public sealed record ArchivedRound(
    Guid Id,
    string? Title,
    Configuration Configuration,
    ImmutableArray<Participant> Participants,
    ImmutableArray<Ballot> Votes,
    DateTimeOffset RevealedAt
);
