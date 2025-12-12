using Estiblazor.UI.Domain.Rooms.Events;
using Estiblazor.UI.Services.Users;
using System.Collections.Generic;
using System.Linq;

namespace Estiblazor.UI.Domain.Rooms;

public class EstimationStage
{
    private readonly Dictionary<UserId, string> _choices = new();

    public EstimationStage(string name, IEnumerable<string> availableChoices)
    {
        Name = name;
        AvailableChoices = availableChoices.ToArray();
    }

    public string Name { get; }

    public string[] AvailableChoices { get; }

    public bool IsRevealed { get; private set; }

    public IReadOnlyDictionary<UserId, string> Choices => _choices;

    public ChoiceChangedDomainEvent SetChoice(UserId userId, string choice)
    {
        _choices[userId] = choice;
        return new ChoiceChangedDomainEvent(string.Empty, Name, userId, choice);
    }

    public ChoiceChangedDomainEvent RemoveChoice(UserId userId)
    {
        _choices.Remove(userId);
        return new ChoiceChangedDomainEvent(string.Empty, Name, userId, null);
    }

    public RoomRevealedDomainEvent Reveal()
    {
        IsRevealed = true;
        return new RoomRevealedDomainEvent(string.Empty);
    }

    public RoomResetDomainEvent Reset()
    {
        IsRevealed = false;
        _choices.Clear();
        return new RoomResetDomainEvent(string.Empty);
    }
}
