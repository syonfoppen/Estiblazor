using System.Text.Json;
using System.Text.Json.Nodes;
using Estiblazor.Application;
using Estiblazor.Domain;
using Estiblazor.Infrastructure;
using StackExchange.Redis;
using Xunit;

namespace Estiblazor.Tests;

public sealed class SessionFeatureTests
{
    private readonly Guid owner = Guid.NewGuid();
    private RoomState Create() => RoomState.Create("AABBCCDDEEFF", owner, new string('A', 64), Configuration.Template("Effort and risk"));
    private static RoomState Apply(RoomState r, Guid user, RoomCommand command) => r.Apply(user, command, DateTimeOffset.UtcNow);

    [Fact]
    public void ObserverCannotVoteAndSwitchingWithdrawsOnlyConfirmedOpenVotes()
    {
        var r = Create(); var observer = Guid.NewGuid();
        r = Apply(r, owner, new Join(Guid.NewGuid(), r.Round.Id, "Alex"));
        r = Apply(r, observer, new Join(Guid.NewGuid(), r.Round.Id, "Alex", true));
        Assert.Throws<RoomException>(() => Apply(r, observer, new Vote(Guid.NewGuid(), r.Round.Id, 0, "3")));
        r = Apply(r, owner, new Vote(Guid.NewGuid(), r.Round.Id, 0, "3"));
        Assert.Throws<RoomException>(() => Apply(r, owner, new Join(Guid.NewGuid(), r.Round.Id, "Alex", true)));
        r = Apply(r, owner, new Join(Guid.NewGuid(), r.Round.Id, "Alex", true, true));
        Assert.Empty(r.Round.Votes); Assert.Contains(owner, r.Owners);
        r = Apply(r, owner, new Join(Guid.NewGuid(), r.Round.Id, "Alex", false));
        r = Apply(r, owner, new Vote(Guid.NewGuid(), r.Round.Id, 0, "8"));
        r = Apply(r, owner, new Reveal(Guid.NewGuid(), r.Round.Id));
        r = Apply(r, owner, new Join(Guid.NewGuid(), r.Round.Id, "Changed later", true));
        Assert.Single(r.Round.Votes);
        Assert.False(r.RevealedParticipants.Single(p => p.Id == owner).IsObserver);
        Assert.Equal("Alex", r.RevealedParticipants.Single(p => p.Id == owner).Name);
    }

    [Fact]
    public void ActivePhaseIsOwnerControlledValidatedRoundBoundAndResetOnNextRound()
    {
        var r = Create(); var oldRound = r.Round.Id;
        Assert.Throws<RoomException>(() => Apply(r, Guid.NewGuid(), new SetActivePhase(Guid.NewGuid(), oldRound, 1)));
        Assert.Throws<RoomException>(() => Apply(r, owner, new SetActivePhase(Guid.NewGuid(), oldRound, 2)));
        r = Apply(r, owner, new SetActivePhase(Guid.NewGuid(), oldRound, 1));
        Assert.Equal(1, r.Round.ActivePhase);
        r = Apply(r, owner, new NextRound(Guid.NewGuid(), oldRound, null, false));
        Assert.Equal(0, r.Round.ActivePhase);
        Assert.Throws<RoomException>(() => Apply(r, owner, new SetActivePhase(Guid.NewGuid(), oldRound, 1)));
    }

    [Fact]
    public void TransferRevokesEveryOtherOwnerAndInvalidatesTheManagementHash()
    {
        var r = Create(); var a = Guid.NewGuid(); var b = Guid.NewGuid();
        r = Apply(r, a, new Join(Guid.NewGuid(), r.Round.Id, "Alex"));
        r = Apply(r, b, new Join(Guid.NewGuid(), r.Round.Id, "Alex", true));
        Assert.Throws<RoomException>(() => Apply(r, a, new GrantOwner(Guid.NewGuid(), r.Round.Id, b)));
        Assert.Throws<RoomException>(() => Apply(r, owner, new GrantOwner(Guid.NewGuid(), r.Round.Id, Guid.NewGuid())));
        r = Apply(r, owner, new GrantOwner(Guid.NewGuid(), r.Round.Id, a));
        Assert.Equal(2, r.Owners.Count);
        Assert.Throws<RoomException>(() => Apply(r, a, new TransferOwnership(Guid.NewGuid(), r.Round.Id, b, r.ManagementHash)));
        var transfer = new TransferOwnership(Guid.NewGuid(), r.Round.Id, b, new string('B', 64));
        r = Apply(r, a, transfer);
        Assert.Single(r.Owners); Assert.Contains(b, r.Owners); Assert.True(r.NeedsManagementLink); Assert.True(r.Participants[b].IsObserver);
        Assert.Same(r, Apply(r, a, transfer)); // Retrying a committed transfer cannot transfer a second time.
        Assert.Throws<RoomException>(() => Apply(r, owner, new Reveal(Guid.NewGuid(), r.Round.Id)));
        Assert.Throws<RoomException>(() => Apply(r, a, new RotateManagementLink(Guid.NewGuid(), r.Round.Id, new string('C', 64))));
    }

    [Fact]
    public void ExistingSchemaOneDocumentsDefaultToVoterAndFirstPhase()
    {
        var r = Create(); r = Apply(r, owner, new Join(Guid.NewGuid(), r.Round.Id, "Alex"));
        var json = JsonNode.Parse(JsonSerializer.Serialize(r))!;
        json["Round"]!.AsObject().Remove("ActivePhase");
        json.AsObject().Remove("NeedsManagementLink");
        foreach (var participant in json["Participants"]!.AsObject()) participant.Value!.AsObject().Remove("IsObserver");
        var restored = JsonSerializer.Deserialize<RoomState>(json.ToJsonString())!;
        Assert.False(restored.NeedsManagementLink); Assert.Equal(0, restored.Round.ActivePhase); Assert.False(restored.Participants[owner].IsObserver);
        Assert.Single(Apply(restored, owner, new Vote(Guid.NewGuid(), restored.Round.Id, 0, "3")).Round.Votes);
    }

    [Fact]
    public async Task RealRedisDirectoryDuplicationSummariesTransferAndRecoveryStayConsistent()
    {
        var connection = Environment.GetEnvironmentVariable("TEST_REDIS");
        if (connection == null) { Assert.False(Environment.GetEnvironmentVariable("CI") == "true", "TEST_REDIS is required in CI"); return; }
        using var redis = await ConnectionMultiplexer.ConnectAsync(connection);
        var prefix = "test:session:" + Guid.NewGuid() + ":";
        var store = new RedisRoomStore(redis, prefix); var updates = new SilentUpdates(); var service = new RoomService(store, updates, store);
        var created = await service.CreateAsync(owner, Configuration.Template("Effort and risk"));
        var code = created.Code; var r = await service.ReadAsync(code, owner); var round = r.RoundId;
        var guest = Guid.NewGuid(); var observer = Guid.NewGuid();
        Assert.True((await service.RecentAsync(owner)).Single().IsOwner);
        await service.VisitAsync(code, guest);
        Assert.False((await service.RecentAsync(guest)).Single().IsOwner);
        Assert.Empty(await service.RecentAsync(Guid.NewGuid()));
        await service.ExecuteAsync(code, owner, new Join(Guid.NewGuid(), round, "Alex"));
        await service.ExecuteAsync(code, guest, new Join(Guid.NewGuid(), round, "Alex"));
        await service.ExecuteAsync(code, observer, new Join(Guid.NewGuid(), round, "Watcher", true));
        await service.ExecuteAsync(code, owner, new Vote(Guid.NewGuid(), round, 0, "3"));
        await service.ExecuteAsync(code, guest, new Vote(Guid.NewGuid(), round, 0, "8"));
        Assert.Empty((await service.ReadAsync(code, observer)).Results);
        Assert.Empty((await service.ReadAsync(code, observer)).VisibleVotes);
        await service.ExecuteAsync(code, owner, new SetActivePhase(Guid.NewGuid(), round, 1));
        Assert.Equal(1, (await service.ReadAsync(code, guest)).ActivePhase);
        await service.ExecuteAsync(code, owner, new Reveal(Guid.NewGuid(), round));
        var result = (await service.ReadAsync(code, observer)).Results[0];
        Assert.Equal(2, result.EligibleVoters); Assert.Equal(2, result.VotesCast); Assert.True(result.HasDifferences);
        Assert.Equal(new[] { "3", "8" }, result.Distribution.Select(d => d.Option));
        await service.ExecuteAsync(code, guest, new Join(Guid.NewGuid(), round, "Renamed", true));
        await service.ExecuteAsync(code, observer, new Join(Guid.NewGuid(), round, "Watcher", false));
        Assert.Equal(2, (await service.ReadAsync(code, owner)).Results[0].EligibleVoters);
        Assert.All((await service.ReadAsync(code, owner)).Results[0].Distribution.SelectMany(d => d.Voters), p => Assert.Equal("Alex", p.Name));
        r = await service.ReadAsync(code, owner);
        await service.ExecuteAsync(code, owner, new Configure(Guid.NewGuid(), round, r.Version, Configuration.Template("Score")));
        var draft = await service.CopyConfigurationAsync(code);
        Assert.Equal(2, draft.Phases.Length); Assert.EndsWith(" (copy)", draft.Name);
        var copy = await service.CreateAsync(owner, draft);
        var duplicate = (await store.ReadAsync(copy.Code))!;
        Assert.Empty(duplicate.Participants); Assert.Empty(duplicate.Round.Votes); Assert.Empty(duplicate.History); Assert.Null(duplicate.PendingConfiguration); Assert.Equal(0, duplicate.Round.ActivePhase);
        Assert.NotEqual((await store.ReadAsync(code))!.ManagementHash, duplicate.ManagementHash);
        await Task.WhenAll(
            service.ExecuteAsync(code, owner, new GrantOwner(Guid.NewGuid(), round, guest)),
            service.ExecuteAsync(code, owner, new GrantOwner(Guid.NewGuid(), round, observer)));
        Assert.Equal(3, (await store.ReadAsync(code))!.Owners.Count);
        Assert.True((await service.RecentAsync(guest)).Single().IsOwner);
        var before = (await store.ReadAsync(code))!;
        await service.TransferAsync(code, owner, round, guest);
        Assert.False((await service.ReadAsync(code, owner)).IsOwner);
        Assert.False((await service.RecentAsync(owner)).Single(c => c.Code == code).IsOwner);
        Assert.False(await store.CompareExchangeAsync(before, before with { Version = before.Version + 1 }));
        await Assert.ThrowsAsync<RoomException>(() => service.RecoverAsync(code, owner, created.ManagementSecret));
        await Assert.ThrowsAsync<RoomException>(() => service.ExecuteAsync(code, owner, new Reveal(Guid.NewGuid(), round)));
        Assert.True((await service.ReadAsync(code, guest)).NeedsManagementLink);
        var secret = await service.RotateManagementAsync(code, guest, round);
        Assert.False((await service.ReadAsync(code, guest)).NeedsManagementLink);
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(await store.ReadAsync(code)));
        var recoveredUser = Guid.NewGuid(); await service.RecoverAsync(code, recoveredUser, secret);
        Assert.True((await service.RecentAsync(recoveredUser)).Single().IsOwner);
        await service.ExecuteAsync(code, guest, new NextRound(Guid.NewGuid(), round, "After transfer", false));
        Assert.Single(await service.HistoryAsync(code, 0));
        Assert.Equal(0, (await service.ReadAsync(code, guest)).ActivePhase);
        using (var freshClient = await ConnectionMultiplexer.ConnectAsync(connection)) {
            var freshStore = new RedisRoomStore(freshClient, prefix); var fresh = new RoomService(freshStore, updates, freshStore);
            Assert.True((await fresh.RecentAsync(guest)).Single().IsOwner);
            Assert.True((await fresh.ReadAsync(code, recoveredUser)).IsOwner);
        }
        var current = (await store.ReadAsync(code))!;
        await service.ExecuteAsync(code, guest, new Delete(Guid.NewGuid(), current.Round.Id));
        Assert.False(await store.RecordVisitAsync(current, owner));
        Assert.Empty(await service.RecentAsync(guest));
        Assert.DoesNotContain(await service.RecentAsync(owner), c => c.Code == code);
        await service.ExecuteAsync(copy.Code, owner, new Delete(Guid.NewGuid(), duplicate.Round.Id));
    }
}
