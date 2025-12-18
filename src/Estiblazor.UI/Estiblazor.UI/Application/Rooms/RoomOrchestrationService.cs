using Estiblazor.UI.Domain.Common;
using Estiblazor.UI.Domain.Rooms;
using Estiblazor.UI.Domain.Rooms.Events;
using Estiblazor.UI.Services.Rooms;
using Estiblazor.UI.Services.Users;
using System.Linq;
using DomainEstimationStage = Estiblazor.UI.Domain.Rooms.EstimationStage;
using ViewEstimationStage = Estiblazor.UI.Services.Rooms.EstimationStage;
using System.Collections.Concurrent;

namespace Estiblazor.UI.Application.Rooms;

public class RoomOrchestrationService : IRoomOrchestrationService
{
    private readonly IRoomRepository _roomRepository;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly IRoomEventBackplane _eventBackplane;
    private readonly ConcurrentDictionary<string, RoomViewModel> _readModels = new();
    private readonly Guid _instanceId = Guid.NewGuid();

    public RoomOrchestrationService(IRoomRepository roomRepository, IDomainEventDispatcher dispatcher, IRoomEventBackplane eventBackplane)
    {
        _roomRepository = roomRepository;
        _dispatcher = dispatcher;
        _eventBackplane = eventBackplane;

        _eventBackplane.Subscribe(OnBackplaneEventAsync);
    }

    public RoomViewModel GetOrCreateRoom(string roomId)
    {
        var room = _roomRepository.Get(roomId);
        if (room is null)
        {
            room = new Room(new RoomId(roomId), DefaultStages());
            _roomRepository.Save(room);
            PublishAsync(room.Created());
        }

        return _readModels.GetOrAdd(roomId, _ => BuildReadModel(room));
    }

    public RoomViewModel? GetExistingRoom(string roomId)
    {
        if (_readModels.TryGetValue(roomId, out var vm))
        {
            return vm;
        }

        var room = _roomRepository.Get(roomId);
        if (room is null)
        {
            return null;
        }

        var readModel = BuildReadModel(room);
        _readModels[roomId] = readModel;
        return readModel;
    }

    public List<string> GetRoomNames() => _roomRepository.GetRoomNames().ToList();

    public string CreateRoom(IEnumerable<NewStageModel> stages)
    {
        var roomId = GetRandomRoomName();
        while (_roomRepository.Get(roomId) is not null)
        {
            roomId = GetRandomRoomName();
        }

        var domainStages = stages.Select(ToDomainStage).ToList();
        var room = new Room(new RoomId(roomId), domainStages);

        _roomRepository.Save(room);
        PublishAsync(room.Created());

        var readModel = BuildReadModel(room);
        _readModels[roomId] = readModel;

        return roomId;
    }

    public void RevealAllStages(string roomId)
    {
        if (_roomRepository.Get(roomId) is not { } room) return;
        var evt = room.RevealAll();
        PublishAsync(evt);

        if (_readModels.TryGetValue(roomId, out var vm))
        {
            foreach (var stage in vm.EstimationStages)
            {
                stage.Reveal();
            }
        }

        _roomRepository.Save(room);
    }

    public void ResetAllStages(string roomId)
    {
        if (_roomRepository.Get(roomId) is not { } room) return;
        var evt = room.ResetAll();
        PublishAsync(evt);

        if (_readModels.TryGetValue(roomId, out var vm))
        {
            foreach (var stage in vm.EstimationStages)
            {
                stage.Reset();
            }
        }

        _roomRepository.Save(room);
    }

    public void SetChoice(string roomId, int stageIndex, UserId userId, string choice)
    {
        if (_roomRepository.Get(roomId) is not { } room) return;

        var evt = room.SetChoice(stageIndex, userId, choice);
        PublishAsync(evt);

        if (_readModels.TryGetValue(roomId, out var vm))
        {
            vm.EstimationStages[stageIndex].SetChoice(userId, choice);
        }

        _roomRepository.Save(room);
    }

    private static IEnumerable<DomainEstimationStage> DefaultStages() =>
    [
        new DomainEstimationStage(
            "effort",
            ["0.5", "1", "2", "3", "5", "8", "13", "20", "<i class=\"fa-solid fa-infinity\"></i>"]
        ),
        new DomainEstimationStage(
            "complexity",
            ["S", "M", "L", "<i class=\"fa-solid fa-infinity\"></i>"]
        ),
        new DomainEstimationStage(
            "Like",
            ["<i class=\"fa-solid fa-thumbs-up\"></i>", "<i class=\"fas fa-meh\"></i>", "<i class=\"fa-solid fa-thumbs-down\"></i>"]
        )
    ];

    private static DomainEstimationStage ToDomainStage(NewStageModel stage)
    {
        return new DomainEstimationStage(stage.StageName, stage.AvailableChoices.Select(x => x.ChoiceName));
    }

    private RoomViewModel BuildReadModel(Room room)
    {
        var vmStages = room.EstimationStages.Select(stage => new ViewEstimationStage
        {
            Name = stage.Name,
            AvailableChoices = stage.AvailableChoices.ToArray(),
            IsRevealed = stage.IsRevealed
        }).ToList();

        var viewModel = new RoomViewModel(vmStages)
        {
            Id = room.Id,
            Name = room.Name,
        };

        return viewModel;
    }

    private static string GetRandomRoomName() => Random.Shared.Next(10000, 99999 + 1).ToString();

    private void PublishAsync(IDomainEvent domainEvent)
    {
        _ = _dispatcher.PublishAsync(domainEvent);
        _ = _eventBackplane.PublishAsync(domainEvent, _instanceId);
    }

    private Task OnBackplaneEventAsync(IDomainEvent domainEvent, Guid originId)
    {
        if (originId == _instanceId)
        {
            return Task.CompletedTask;
        }

        switch (domainEvent)
        {
            case RoomCreatedDomainEvent created:
                HandleRoomCreated(created.RoomName);
                break;
            case RoomResetDomainEvent reset:
                HandleReset(reset.RoomName);
                break;
            case RoomRevealedDomainEvent revealed:
                HandleReveal(revealed.RoomName);
                break;
            case ChoiceChangedDomainEvent choiceChanged:
                HandleChoiceChanged(choiceChanged);
                break;
        }

        return Task.CompletedTask;
    }

    private void HandleChoiceChanged(ChoiceChangedDomainEvent domainEvent)
    {
        if (!TryEnsureReadModel(domainEvent.RoomName, out var roomViewModel))
        {
            return;
        }

        var stage = roomViewModel.EstimationStages.FirstOrDefault(stage => stage.Name == domainEvent.StageName);
        if (stage is null)
        {
            return;
        }

        if (domainEvent.Choice is null)
        {
            stage.RemoveChoice(domainEvent.UserId);
        }
        else
        {
            stage.SetChoice(domainEvent.UserId, domainEvent.Choice);
        }
    }

    private void HandleRoomCreated(string roomName)
    {
        if (_readModels.ContainsKey(roomName))
        {
            return;
        }

        var room = _roomRepository.Get(roomName);
        if (room is null)
        {
            return;
        }

        _readModels[roomName] = BuildReadModel(room);
    }

    private void HandleReset(string roomName)
    {
        if (!TryEnsureReadModel(roomName, out var roomViewModel))
        {
            return;
        }

        foreach (var stage in roomViewModel.EstimationStages)
        {
            stage.Reset();
        }
    }

    private void HandleReveal(string roomName)
    {
        if (!TryEnsureReadModel(roomName, out var roomViewModel))
        {
            return;
        }

        foreach (var stage in roomViewModel.EstimationStages)
        {
            stage.Reveal();
        }
    }

    private bool TryEnsureReadModel(string roomId, out RoomViewModel roomViewModel)
    {
        if (_readModels.TryGetValue(roomId, out roomViewModel!))
        {
            return true;
        }

        var room = _roomRepository.Get(roomId);
        if (room is null)
        {
            roomViewModel = null!;
            return false;
        }

        roomViewModel = BuildReadModel(room);
        _readModels[roomId] = roomViewModel;
        return true;
    }
}
