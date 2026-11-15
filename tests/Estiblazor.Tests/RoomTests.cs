using System.Collections.Immutable;
using Estiblazor.Application;
using Estiblazor.Domain;
using Estiblazor.Infrastructure;
using StackExchange.Redis;
using Xunit;

namespace Estiblazor.Tests;

public sealed class RoomTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static RoomState Room() => RoomState.Create("AABBCCDDEEFF", Owner, "hash", Configuration.Template("Planning poker"));
    private static RoomState JoinUser(RoomState r, Guid user, string name = "Alex") => r.Apply(user, new Join(Guid.NewGuid(), r.Round.Id, name), DateTimeOffset.UtcNow);
    private static RoomState Cast(RoomState r, Guid user, string option = "3") => r.Apply(user, new Vote(Guid.NewGuid(), r.Round.Id, 0, option), DateTimeOffset.UtcNow);

    [Fact]
    public void ConfigurationEnforcesAllBoundsAndUniqueTrimmedOptions()
    {
        var c = Configuration.Template("Planning poker");
        Assert.Throws<RoomException>(() => (c with { Name = new string('a', 101) }).Validate());
        Assert.Throws<RoomException>(() => (c with { Phases = [] }).Validate());
        Assert.Throws<RoomException>(() => (c with { Phases = Enumerable.Repeat(c.Phases[0], 11).ToImmutableArray() }).Validate());
        foreach (var p in new Phase[] { new(new string('a', 51), ["a", "b"]), new("x", ["a"]), new("x", [" a", "A "]), new("x", [new string('a', 31), "b"]), new("x", Enumerable.Range(0, 31).Select(i => i.ToString()).ToImmutableArray()) })
            Assert.Throws<RoomException>(() => (c with { Phases = [p] }).Validate());
        Assert.Equal("room", (c with { Name = " room " }).Validate().Name);
        Assert.Equal(2, new Configuration(new string('a', 100), [new(new string('b', 50), [new string('c', 30), "d"])]).Validate().Phases[0].Options.Length);
    }
    [Fact]
    public void GuestsCannotManageAndOwnersCanVote()
    {
        var guest = Guid.NewGuid(); var r = JoinUser(Room(), Owner);
        foreach (var c in new RoomCommand[] { new Reveal(Guid.NewGuid(), r.Round.Id), new NextRound(Guid.NewGuid(), r.Round.Id, null, false), new Delete(Guid.NewGuid(), r.Round.Id), new Configure(Guid.NewGuid(), r.Round.Id, r.Version, r.Configuration) })
            Assert.Throws<RoomException>(() => r.Apply(guest, c, DateTimeOffset.UtcNow));
        Assert.Single(Cast(r, Owner).Round.Votes);
    }
    [Fact]
    public void SameNamesHaveIndependentVotesAndVotesCanChangeOrWithdraw()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var r = JoinUser(JoinUser(Room(), a), b);
        r = Cast(Cast(r, a), b, "5");
        Assert.Equal(2, r.Participants.Count); Assert.Equal(2, r.Round.Votes.Length);
        r = Cast(r, a, "8"); Assert.Equal("8", r.Round.Votes.Single(v => v.UserId == a).Option);
        r = r.Apply(a, new Vote(Guid.NewGuid(), r.Round.Id, 0, null), DateTimeOffset.UtcNow);
        Assert.Single(r.Round.Votes);
    }
    [Fact]
    public void RevealClosesVotingAndArchiveIsAnImmutableSnapshot()
    {
        var a = Guid.NewGuid(); var r = Cast(JoinUser(Room(), a), a);
        var original = r; var now = DateTimeOffset.UtcNow;
        r = r.Apply(Owner, new Reveal(Guid.NewGuid(), r.Round.Id), now);
        Assert.Throws<RoomException>(() => Cast(r, a));
        r = JoinUser(r, a, "Renamed later");
        r = r.Apply(Owner, new Configure(Guid.NewGuid(), r.Round.Id, r.Version, Configuration.Template("Score")), now);
        var cmd = new NextRound(Guid.NewGuid(), r.Round.Id, "Next story", false);
        r = r.Apply(Owner, cmd, now);
        Assert.Single(r.History); Assert.Empty(r.Round.Votes); Assert.False(r.Round.Revealed);
        Assert.Equal("Alex", r.History[0].Participants.Single().Name);
        Assert.Equal(original.Round.Configuration, r.History[0].Configuration);
        Assert.Equal(now, r.History[0].RevealedAt); Assert.Equal("Next story", r.Round.Title);
        Assert.Single(r.Configuration.Phases); Assert.Equal(2, r.History[0].Configuration.Phases.Length);
        Assert.Same(r, r.Apply(Owner, cmd, now));
        Assert.Throws<RoomException>(() => r.Apply(Owner, new NextRound(Guid.NewGuid(), original.Round.Id, null, false), now));
        Assert.Empty(original.History); Assert.False(original.Round.Revealed);
    }
    [Fact]
    public void DiscardNeedsConfirmationAndCreatesNoHistory()
    {
        var r = Cast(JoinUser(Room(), Owner), Owner);
        Assert.Throws<RoomException>(() => r.Apply(Owner, new NextRound(Guid.NewGuid(), r.Round.Id, null, false), DateTimeOffset.UtcNow));
        var next = r.Apply(Owner, new NextRound(Guid.NewGuid(), r.Round.Id, null, true), DateTimeOffset.UtcNow);
        Assert.Empty(next.Round.Votes); Assert.Empty(next.History);
    }
    [Fact]
    public void ConfigurationAndCommandsRejectStaleEdits()
    {
        var r = Room();
        Assert.Throws<RoomException>(() => r.Apply(Owner, new Configure(Guid.NewGuid(), r.Round.Id, -1, r.Configuration), DateTimeOffset.UtcNow));
        Assert.Throws<RoomException>(() => r.Apply(Owner, new Reveal(Guid.Empty, r.Round.Id), DateTimeOffset.UtcNow));
    }
    [Fact]
    public async Task ReadModelNeverLeaksOthersUnrevealedVotes()
    {
        var store = new MemoryStore(); var service = new RoomService(store, new SilentUpdates(), store);
        var created = await service.CreateAsync(Owner, Configuration.Template("Planning poker"));
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var r = await service.ReadAsync(created.Code, a);
        foreach (var u in new[] { a, b })
        {
            await service.ExecuteAsync(created.Code, u, new Join(Guid.NewGuid(), r.RoundId, "Alex"));
            await service.ExecuteAsync(created.Code, u, new Vote(Guid.NewGuid(), r.RoundId, 0, "3"));
        }
        Assert.Single((await service.ReadAsync(created.Code, a)).VisibleVotes);
        Assert.Empty((await service.ReadAsync(created.Code, Owner)).VisibleVotes);
        await service.ExecuteAsync(created.Code, Owner, new Reveal(Guid.NewGuid(), r.RoundId));
        Assert.Equal(2, (await service.ReadAsync(created.Code, a)).VisibleVotes.Length);
        Assert.DoesNotContain(created.ManagementSecret, System.Text.Json.JsonSerializer.Serialize(store.Value));
        await Assert.ThrowsAsync<RoomException>(() => service.RecoverAsync(created.Code, b, new string('0', 64)));
        await service.RecoverAsync(created.Code, b, created.ManagementSecret);
        Assert.True((await service.ReadAsync(created.Code, b)).IsOwner);
    }
    [Fact]
    public async Task FailedStoragePublishesNoUpdate()
    {
        var updates = new SilentUpdates(); var store = new MemoryStore(); var service = new RoomService(store, updates, store);
        var c = await service.CreateAsync(Owner, Configuration.Template("Score")); var r = await service.ReadAsync(c.Code, Owner);
        store.Fail = true;
        await Assert.ThrowsAsync<StorageUnavailableException>(() => service.ExecuteAsync(c.Code, Owner, new Join(Guid.NewGuid(), r.RoundId, "Alex")));
        Assert.Equal(0, updates.Published);
    }
    private sealed class MemoryStore : IRoomStore, IRoomDirectory
    {
        public RoomState? Value; public bool Fail;
        public Task<bool> RecordVisitAsync(RoomState room, Guid user) => Task.FromResult(true);
        public Task<ImmutableArray<RoomReference>> ListAsync(Guid user) => Task.FromResult(ImmutableArray<RoomReference>.Empty);
        public Task ForgetAsync(string code, Guid user) => Task.CompletedTask;
        public Task<RoomState?> ReadAsync(string code) { if (Fail) throw new StorageUnavailableException(); return Task.FromResult(Value); }
        public Task<bool> CreateAsync(RoomState r) { Value = r; return Task.FromResult(true); }
        public Task<bool> CompareExchangeAsync(RoomState a, RoomState? b) { Value = b; return Task.FromResult(true); }
    }
}
internal sealed class SilentUpdates : IRoomUpdates
{
    public int Published;
    public bool IsOnline(string code, Guid user) => false;
    public Task PublishAsync(string code) { Published++; return Task.CompletedTask; }
    public IDisposable Subscribe(string code, Guid user, Guid connection, Func<Task> changed) => throw new NotSupportedException();
    public Task SetConnectedAsync(Guid connection, bool connected) => Task.CompletedTask;
}

public sealed class RedisTests
{
    [Fact]
    public async Task ConcurrentCommandsDuplicatesRecoveryDeletionAndReconnectUseRealRedis()
    {
        // Explicitly require Redis in CI; skip locally only when no connection was configured.
        var connection = Environment.GetEnvironmentVariable("TEST_REDIS");
        if (connection == null) { Assert.False(Environment.GetEnvironmentVariable("CI") == "true", "TEST_REDIS is required in CI"); return; }
        var options = ConfigurationOptions.Parse(connection); options.AbortOnConnectFail = false; options.AsyncTimeout = 1000; options.ConnectTimeout = 1000;
        using var redis = await ConnectionMultiplexer.ConnectAsync(options);
        var prefix = "test:" + Guid.NewGuid() + ":";
        var store = new RedisRoomStore(redis, prefix); var updates = new SilentUpdates(); var service = new RoomService(store, updates, store);
        var owner = Guid.NewGuid(); var created = await service.CreateAsync(owner, Configuration.Template("Planning poker"));
        var r = await service.ReadAsync(created.Code, owner);
        var users = Enumerable.Range(0, 24).Select(_ => Guid.NewGuid()).ToArray();
        await Task.WhenAll(users.Select(u => service.ExecuteAsync(created.Code, u, new Join(Guid.NewGuid(), r.RoundId, "Alex"))));
        await Task.WhenAll(users.Select(u => service.ExecuteAsync(created.Code, u, new Vote(Guid.NewGuid(), r.RoundId, 0, "5"))));
        Assert.Equal(24, (await store.ReadAsync(created.Code))!.Round.Votes.Length);
        var reveal = new Reveal(Guid.NewGuid(), r.RoundId);
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => service.ExecuteAsync(created.Code, owner, reveal)));
        var next = new NextRound(Guid.NewGuid(), r.RoundId, "Story two", false);
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => service.ExecuteAsync(created.Code, owner, next)));
        Assert.Single(await service.HistoryAsync(created.Code, 0));
        await Assert.ThrowsAsync<RoomException>(() => service.ExecuteAsync(created.Code, users[0], new Vote(Guid.NewGuid(), r.RoundId, 0, "8")));
        var recovered = Guid.NewGuid(); await service.RecoverAsync(created.Code, recovered, created.ManagementSecret);
        // A new client/application service loads persisted configuration, owners, votes and history.
        using (var other = await ConnectionMultiplexer.ConnectAsync(options))
        {
            var freshStore = new RedisRoomStore(other, prefix);
            var fresh = new RoomService(freshStore, updates, freshStore);
            Assert.True((await fresh.ReadAsync(created.Code, recovered)).IsOwner);
            Assert.Equal(24, (await fresh.HistoryAsync(created.Code, 0))[0].Votes.Length);
        }
        // A real client with unavailable transport reports a recoverable error, without publishing.
        var dead = new ConfigurationOptions { AbortOnConnectFail = false, ConnectTimeout = 100, AsyncTimeout = 100, ConnectRetry = 0, BacklogPolicy = BacklogPolicy.FailFast };
        dead.EndPoints.Add("127.0.0.1", 1);
        using (var offline = await ConnectionMultiplexer.ConnectAsync(dead))
        {
            await Assert.ThrowsAsync<StorageUnavailableException>(() => new RedisRoomStore(offline, prefix).ReadAsync(created.Code));
        }
        Assert.NotNull(await store.ReadAsync(created.Code));
        var before = (await store.ReadAsync(created.Code))!;
        await service.ExecuteAsync(created.Code, recovered, new Delete(Guid.NewGuid(), before.Round.Id));
        Assert.Null(await store.ReadAsync(created.Code));
        Assert.False(await store.CompareExchangeAsync(before, before with { Version = before.Version + 1 }));
        await Assert.ThrowsAsync<RoomException>(() => service.ReadAsync(created.Code, owner));
    }
}
