using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Estiblazor.Application;
using Estiblazor.Domain;
using StackExchange.Redis;

namespace Estiblazor.Infrastructure;

public sealed class RedisRoomStore(IConnectionMultiplexer redis, string prefix = "estiblazor:room:")
    : IRoomStore,
        IRoomDirectory
{
    private readonly IDatabase database = redis.GetDatabase();

    private string RecentKey(Guid user) => prefix + "directory:" + user.ToString("N") + ":recent";

    private string OwnedKey(Guid user) => prefix + "directory:" + user.ToString("N") + ":owned";

    private static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task<RoomState?> ReadAsync(string code) =>
        await Guard(async () =>
        {
            var value = await database.HashGetAsync(prefix + code, "data");
            if (value.IsNull)
                return null;
            var room = JsonSerializer.Deserialize<RoomState>(value.ToString())
                ?? throw new RoomException("Room data is invalid.");
            if (room.SchemaVersion != 1)
                throw new RoomException("This room needs a newer application version.");
            return room;
        });

    public Task<bool> CreateAsync(RoomState room) =>
        Guard(async () =>
        {
            var transaction = database.CreateTransaction();
            transaction.AddCondition(Condition.KeyNotExists(prefix + room.Code));
            var writes = new List<Task>
            {
                transaction.HashSetAsync(prefix + room.Code, Entries(room)),
            };
            foreach (var owner in room.Owners)
            {
                writes.Add(transaction.SortedSetAddAsync(OwnedKey(owner), room.Code, Now));
                writes.Add(transaction.SortedSetAddAsync(RecentKey(owner), room.Code, Now));
            }
            var committed = await transaction.ExecuteAsync();
            await ObserveWritesAsync(writes, committed);
            return committed;
        });

    public Task<bool> CompareExchangeAsync(RoomState before, RoomState? after) =>
        Guard(async () =>
        {
            var transaction = database.CreateTransaction();
            var key = prefix + before.Code;
            // Room data and directory changes must commit together at the expected version.
            transaction.AddCondition(
                Condition.HashEqual(
                    key,
                    "version",
                    before.Version.ToString(CultureInfo.InvariantCulture)
                )
            );
            var writes = new List<Task>
            {
                after == null
                    ? transaction.KeyDeleteAsync(key)
                    : transaction.HashSetAsync(key, Entries(after)),
            };
            foreach (
                var owner in before.Owners.Except(after?.Owners ?? ImmutableHashSet<Guid>.Empty)
            )
                writes.Add(transaction.SortedSetRemoveAsync(OwnedKey(owner), before.Code));
            if (after != null)
                foreach (var owner in after.Owners.Except(before.Owners))
                {
                    writes.Add(transaction.SortedSetAddAsync(OwnedKey(owner), before.Code, Now));
                    writes.Add(transaction.SortedSetAddAsync(RecentKey(owner), before.Code, Now));
                }
            else
                foreach (var user in before.Participants.Keys.Union(before.Owners))
                    writes.Add(transaction.SortedSetRemoveAsync(RecentKey(user), before.Code));
            var committed = await transaction.ExecuteAsync();
            await ObserveWritesAsync(writes, committed);
            return committed;
        });

    public Task<bool> RecordVisitAsync(RoomState room, Guid user) =>
        Guard(async () =>
        {
            var transaction = database.CreateTransaction();
            transaction.AddCondition(
                Condition.HashEqual(
                    prefix + room.Code,
                    "version",
                    room.Version.ToString(CultureInfo.InvariantCulture)
                )
            );
            var writes = new List<Task>
            {
                transaction.SortedSetAddAsync(RecentKey(user), room.Code, Now),
                transaction.SortedSetRemoveRangeByRankAsync(RecentKey(user), 0, -51),
            };
            if (room.Owners.Contains(user))
                writes.Add(transaction.SortedSetAddAsync(OwnedKey(user), room.Code, Now));
            var committed = await transaction.ExecuteAsync();
            await ObserveWritesAsync(writes, committed);
            return committed;
        });

    public Task<ImmutableArray<RoomReference>> ListAsync(Guid user) =>
        Guard(async () =>
        {
            var recent = await database.SortedSetRangeByRankWithScoresAsync(
                RecentKey(user),
                0,
                19,
                Order.Descending
            );
            var owned = await database.SortedSetRangeByRankAsync(
                OwnedKey(user),
                0,
                49,
                Order.Descending
            );
            var references = recent
                .Select(e => new RoomReference(
                    e.Element.ToString(),
                    DateTimeOffset.FromUnixTimeMilliseconds((long)e.Score)
                ))
                .ToList();
            references.AddRange(
                owned
                    .Where(c => references.All(r => r.Code != c.ToString()))
                    .Select(c => new RoomReference(c.ToString(), null))
            );
            return references.ToImmutableArray();
        });

    public Task ForgetAsync(string code, Guid user) =>
        Guard(async () =>
        {
            await database.SortedSetRemoveAsync(RecentKey(user), code);
            await database.SortedSetRemoveAsync(OwnedKey(user), code);
            return true;
        });

    private static HashEntry[] Entries(RoomState room) =>
        [
            new("version", room.Version.ToString(CultureInfo.InvariantCulture)),
            new("data", JsonSerializer.Serialize(room)),
        ];

    private static async Task ObserveWritesAsync(IEnumerable<Task> writes, bool committed)
    {
        // Redis cancels queued writes when a transaction condition fails. Observe those
        // tasks, but let storage errors from a committed transaction reach the caller.
        foreach (var write in writes)
            try
            {
                await write;
            }
            catch (TaskCanceledException) when (!committed) { }
    }

    private static async Task<T> Guard<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (RedisException ex)
        {
            throw new StorageUnavailableException(ex);
        }
        catch (TimeoutException ex)
        {
            throw new StorageUnavailableException(ex);
        }
    }
}
