using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Estiblazor.Application;
using Estiblazor.Domain;
using StackExchange.Redis;
namespace Estiblazor.Infrastructure;

public sealed class RedisRoomStore(IConnectionMultiplexer redis, string prefix = "estiblazor:room:") : IRoomStore, IRoomDirectory
{
    private readonly IDatabase db = redis.GetDatabase();
    private string RecentKey(Guid user) => prefix + "directory:" + user.ToString("N") + ":recent";
    private string OwnedKey(Guid user) => prefix + "directory:" + user.ToString("N") + ":owned";
    private static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public async Task<RoomState?> ReadAsync(string code) => await Guard(async () =>
    {
        var value = await db.HashGetAsync(prefix + code, "data");
        if (value.IsNull) return null;
        var room = JsonSerializer.Deserialize<RoomState>(value.ToString()) ?? throw new RoomException("Room data is invalid.");
        if (room.SchemaVersion != 1) throw new RoomException("This room needs a newer application version.");
        return room;
    });
    public Task<bool> CreateAsync(RoomState room) => Guard(async () =>
    {
        var tx = db.CreateTransaction();
        tx.AddCondition(Condition.KeyNotExists(prefix + room.Code));
        var writes = new List<Task> { tx.HashSetAsync(prefix + room.Code, Entries(room)) };
        foreach (var owner in room.Owners)
        {
            writes.Add(tx.SortedSetAddAsync(OwnedKey(owner), room.Code, Now));
            writes.Add(tx.SortedSetAddAsync(RecentKey(owner), room.Code, Now));
        }
        var committed = await tx.ExecuteAsync();
        await Observe(writes, committed);
        return committed;
    });
    public Task<bool> CompareExchangeAsync(RoomState before, RoomState? after) => Guard(async () =>
    {
        var tx = db.CreateTransaction();
        var key = prefix + before.Code;
        tx.AddCondition(Condition.HashEqual(key, "version", before.Version.ToString(CultureInfo.InvariantCulture)));
        var writes = new List<Task> { after == null ? tx.KeyDeleteAsync(key) : tx.HashSetAsync(key, Entries(after)) };
        foreach (var owner in before.Owners.Except(after?.Owners ?? ImmutableHashSet<Guid>.Empty))
            writes.Add(tx.SortedSetRemoveAsync(OwnedKey(owner), before.Code));
        if (after != null)
            foreach (var owner in after.Owners.Except(before.Owners))
            {
                writes.Add(tx.SortedSetAddAsync(OwnedKey(owner), before.Code, Now));
                writes.Add(tx.SortedSetAddAsync(RecentKey(owner), before.Code, Now));
            }
        else
            foreach (var user in before.Participants.Keys.Union(before.Owners))
                writes.Add(tx.SortedSetRemoveAsync(RecentKey(user), before.Code));
        var committed = await tx.ExecuteAsync();
        await Observe(writes, committed);
        return committed;
    });
    public Task<bool> RecordVisitAsync(RoomState room, Guid user) => Guard(async () =>
    {
        var tx = db.CreateTransaction();
        tx.AddCondition(Condition.HashEqual(prefix + room.Code, "version", room.Version.ToString(CultureInfo.InvariantCulture)));
        var writes = new List<Task> {
            tx.SortedSetAddAsync(RecentKey(user), room.Code, Now),
            tx.SortedSetRemoveRangeByRankAsync(RecentKey(user), 0, -51)
        };
        if (room.Owners.Contains(user)) writes.Add(tx.SortedSetAddAsync(OwnedKey(user), room.Code, Now));
        var committed = await tx.ExecuteAsync();
        await Observe(writes, committed);
        return committed;
    });
    public Task<ImmutableArray<RoomReference>> ListAsync(Guid user) => Guard(async () =>
    {
        var recent = await db.SortedSetRangeByRankWithScoresAsync(RecentKey(user), 0, 19, Order.Descending);
        var owned = await db.SortedSetRangeByRankAsync(OwnedKey(user), 0, 49, Order.Descending);
        var references = recent.Select(e => new RoomReference(e.Element.ToString(), DateTimeOffset.FromUnixTimeMilliseconds((long)e.Score))).ToList();
        references.AddRange(owned.Where(c => references.All(r => r.Code != c.ToString())).Select(c => new RoomReference(c.ToString(), null)));
        return references.ToImmutableArray();
    });
    public Task ForgetAsync(string code, Guid user) => Guard(async () =>
    {
        await db.SortedSetRemoveAsync(RecentKey(user), code);
        await db.SortedSetRemoveAsync(OwnedKey(user), code);
        return true;
    });
    private static HashEntry[] Entries(RoomState room) => [new("version", room.Version.ToString(CultureInfo.InvariantCulture)), new("data", JsonSerializer.Serialize(room))];
    private static async Task Observe(IEnumerable<Task> writes, bool committed)
    {
        foreach (var write in writes)
            try { await write; } catch (TaskCanceledException) when (!committed) { }
    }
    private static async Task<T> Guard<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (RedisException ex) { throw new StorageUnavailableException(ex); }
        catch (TimeoutException ex) { throw new StorageUnavailableException(ex); }
    }
}
