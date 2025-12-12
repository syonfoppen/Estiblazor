using Estiblazor.UI.Domain.Rooms;
using Estiblazor.UI.Services.Rooms;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;

namespace Estiblazor.UI.Infrastructure.Rooms;

public class InMemoryRoomRepository : IRoomRepository
{
    private readonly IMemoryCache _memoryCache;
    private readonly HashSet<string> _roomNames = [];

    public InMemoryRoomRepository(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public Room? Get(string roomId)
    {
        return _memoryCache.Get<Room>(new RoomId(roomId));
    }

    public IEnumerable<string> GetRoomNames() => _roomNames.ToArray();

    public void Save(Room room)
    {
        using var entry = _memoryCache.CreateEntry(room.Id);
        entry.Value = room;
        entry.SlidingExpiration = TimeSpan.FromDays(7);
        entry.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            if (key is RoomId id)
            {
                _roomNames.Remove(id.Name);
            }
        });

        _roomNames.Add(room.Name);
    }
}
