namespace Estiblazor.Domain;

public abstract record RoomCommand(Guid Id, Guid RoundId);

public sealed record Join(
    Guid Id,
    Guid RoundId,
    string Name,
    bool IsObserver = false,
    bool DiscardVotesConfirmed = false
) : RoomCommand(Id, RoundId);

public sealed record Vote(Guid Id, Guid RoundId, int Phase, string? Option)
    : RoomCommand(Id, RoundId);

public sealed record Reveal(Guid Id, Guid RoundId) : RoomCommand(Id, RoundId);

public sealed record NextRound(Guid Id, Guid RoundId, string? Title, bool DiscardConfirmed)
    : RoomCommand(Id, RoundId);

public sealed record Configure(
    Guid Id,
    Guid RoundId,
    long ExpectedVersion,
    Configuration Configuration
) : RoomCommand(Id, RoundId);

public sealed record Delete(Guid Id, Guid RoundId) : RoomCommand(Id, RoundId);

public sealed record SetActivePhase(Guid Id, Guid RoundId, int Phase) : RoomCommand(Id, RoundId);

public sealed record GrantOwner(Guid Id, Guid RoundId, Guid UserId) : RoomCommand(Id, RoundId);

public sealed record TransferOwnership(Guid Id, Guid RoundId, Guid UserId, string NewManagementHash)
    : RoomCommand(Id, RoundId);

public sealed record RotateManagementLink(Guid Id, Guid RoundId, string NewManagementHash)
    : RoomCommand(Id, RoundId);
