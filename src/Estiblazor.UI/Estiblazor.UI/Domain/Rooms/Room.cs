using Estiblazor.UI.Domain.Rooms.Events;
using Estiblazor.UI.Services.Rooms;
using Estiblazor.UI.Services.Users;
using System.Collections.Generic;

namespace Estiblazor.UI.Domain.Rooms;

public class Room
{
    private readonly HashSet<UserId> _users = new();
    private readonly List<EstimationStage> _stages = new();

    public Room(RoomId id, IEnumerable<EstimationStage> stages)
    {
        Id = id;
        Name = id.Name;
        _stages.AddRange(stages);
    }

    public RoomId Id { get; }

    public string Name { get; }

    public IReadOnlyCollection<UserId> Users => _users;

    public IReadOnlyList<EstimationStage> EstimationStages => _stages;

    public RoomCreatedDomainEvent Created() => new(Name);

    public UserMembershipChangedDomainEvent Join(UserId userId)
    {
        _users.Add(userId);
        return new UserMembershipChangedDomainEvent(Name, userId.ToString(), true);
    }

    public UserMembershipChangedDomainEvent Leave(UserId userId)
    {
        _users.Remove(userId);
        return new UserMembershipChangedDomainEvent(Name, userId.ToString(), false);
    }

    public ChoiceChangedDomainEvent SetChoice(int stageIndex, UserId userId, string choice)
    {
        var stage = _stages[stageIndex];
        return stage.SetChoice(userId, choice) with { RoomName = Name };
    }

    public RoomResetDomainEvent ResetAll()
    {
        foreach (var stage in _stages)
        {
            stage.Reset();
        }

        return new RoomResetDomainEvent(Name);
    }

    public RoomRevealedDomainEvent RevealAll()
    {
        foreach (var stage in _stages)
        {
            stage.Reveal();
        }

        return new RoomRevealedDomainEvent(Name);
    }
}
