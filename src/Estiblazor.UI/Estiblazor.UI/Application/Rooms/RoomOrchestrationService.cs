using Estiblazor.UI.Domain.Common;
using Estiblazor.UI.Domain.Rooms;
using Estiblazor.UI.Domain.Rooms.Events;
using Estiblazor.UI.Services.Rooms;
using Estiblazor.UI.Services.Users;
using System.Linq;
using DomainEstimationStage = Estiblazor.UI.Domain.Rooms.EstimationStage;
using ViewEstimationStage = Estiblazor.UI.Services.Rooms.EstimationStage;

namespace Estiblazor.UI.Application.Rooms;

public class RoomOrchestrationService : IRoomOrchestrationService
{
    private readonly IRoomRepository _roomRepository;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly Dictionary<string, RoomViewModel> _readModels = new();

    public RoomOrchestrationService(IRoomRepository roomRepository, IDomainEventDispatcher dispatcher)
    {
        _roomRepository = roomRepository;
        _dispatcher = dispatcher;
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

        if (!_readModels.TryGetValue(roomId, out var readModel))
        {
            readModel = BuildReadModel(room);
            _readModels[roomId] = readModel;
        }

        return readModel;
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
    }
}
