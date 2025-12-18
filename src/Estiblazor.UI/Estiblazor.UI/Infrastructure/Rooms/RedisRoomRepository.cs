using System.Text.Json;
using System.Text.Json.Serialization;
using Estiblazor.UI.Domain.Rooms;
using Estiblazor.UI.Services.Rooms;
using Estiblazor.UI.Services.Users;
using StackExchange.Redis;
using DomainEstimationStage = Estiblazor.UI.Domain.Rooms.EstimationStage;

namespace Estiblazor.UI.Infrastructure.Rooms;

public class RedisRoomRepository : IRoomRepository
{
    private const string RoomNamesKey = "rooms:names";
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromDays(7);

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public RedisRoomRepository(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public Room? Get(string roomId)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var value = db.StringGet(GetRoomKey(roomId));
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        var persistedRoom = JsonSerializer.Deserialize<PersistedRoom>(value!, _jsonSerializerOptions);
        return persistedRoom?.ToDomain();
    }

    public IEnumerable<string> GetRoomNames()
    {
        var db = _connectionMultiplexer.GetDatabase();
        foreach (var name in db.SetMembers(RoomNamesKey).Select(x => (string)x!))
        {
            if (db.KeyExists(GetRoomKey(name)))
            {
                yield return name;
            }
            else
            {
                db.SetRemove(RoomNamesKey, name);
            }
        }
    }

    public void Save(Room room)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var model = PersistedRoom.From(room);
        var json = JsonSerializer.Serialize(model, _jsonSerializerOptions);

        db.StringSet(GetRoomKey(room.Id.Name), json, SlidingExpiration);
        db.SetAdd(RoomNamesKey, room.Name);
    }

    private static string GetRoomKey(string roomId) => $"rooms:{roomId}";

    private record PersistedRoom(string Id, string Name, List<PersistedEstimationStage> Stages, List<string> Users)
    {
        public static PersistedRoom From(Room room)
        {
            var stages = room.EstimationStages.Select(PersistedEstimationStage.From).ToList();
            var users = room.Users.Select(u => u.name).ToList();
            return new PersistedRoom(room.Id.Name, room.Name, stages, users);
        }

        public Room ToDomain()
        {
            var stageModels = Stages.Select(s => s.ToDomain()).ToList();
            var room = new Room(new RoomId(Id), stageModels);

            foreach (var user in Users)
            {
                room.Join(new UserId(user));
            }

            return room;
        }
    }

    private record PersistedEstimationStage(string Name, string[] AvailableChoices, bool IsRevealed, Dictionary<string, string> Choices)
    {
        public static PersistedEstimationStage From(DomainEstimationStage stage)
        {
            var choices = stage.Choices.ToDictionary(choice => choice.Key.name, choice => choice.Value);
            return new PersistedEstimationStage(stage.Name, stage.AvailableChoices.ToArray(), stage.IsRevealed, choices);
        }

        public DomainEstimationStage ToDomain()
        {
            var stage = new DomainEstimationStage(Name, AvailableChoices);

            foreach (var choice in Choices)
            {
                stage.SetChoice(new UserId(choice.Key), choice.Value);
            }

            if (IsRevealed)
            {
                stage.Reveal();
            }

            return stage;
        }
    }
}
