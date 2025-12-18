using System.Text.Json;
using System.Text.Json.Serialization;
using Estiblazor.UI.Application.Rooms;
using Estiblazor.UI.Domain.Common;
using StackExchange.Redis;

namespace Estiblazor.UI.Infrastructure.Messaging;

public class RedisRoomEventBackplane : IRoomEventBackplane
{
    private const string ChannelName = "backplane:rooms";
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public RedisRoomEventBackplane(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task PublishAsync(IDomainEvent domainEvent, Guid originId, CancellationToken cancellationToken = default)
    {
        var message = new BackplaneMessage(originId, domainEvent.GetType().AssemblyQualifiedName!,
            JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), _jsonSerializerOptions));
        var payload = JsonSerializer.Serialize(message, _jsonSerializerOptions);

        var subscriber = _connectionMultiplexer.GetSubscriber();
        await subscriber.PublishAsync(ChannelName, payload).ConfigureAwait(false);
    }

    public void Subscribe(Func<IDomainEvent, Guid, Task> handler)
    {
        var subscriber = _connectionMultiplexer.GetSubscriber();
        subscriber.Subscribe(ChannelName, async (_, value) =>
        {
            BackplaneMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<BackplaneMessage>(value!, _jsonSerializerOptions);
            }
            catch
            {
                return;
            }

            if (message is null)
            {
                return;
            }

            var type = Type.GetType(message.Type);
            if (type is null)
            {
                return;
            }

            var domainEvent = (IDomainEvent?)JsonSerializer.Deserialize(message.Payload, type, _jsonSerializerOptions);
            if (domainEvent is null)
            {
                return;
            }

            await handler(domainEvent, message.OriginId).ConfigureAwait(false);
        });
    }

    private record BackplaneMessage(Guid OriginId, string Type, string Payload);
}
