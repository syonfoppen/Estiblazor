using System.Collections.Immutable;
using Estiblazor.Application;
using Estiblazor.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Estiblazor.UI.Components.Pages;

public partial class Rooms
{
    [Parameter]
    public string Code { get; set; } = "";
    private RoomView? room;
    private Guid actor;
    private string name = "";
    private bool observer;
    private bool? savedObserver;
    private string? error;
    private string announcement = "";
    private bool busy;

    private ElementReference votingHeading;
    private bool focusVoting;
    private bool copied;
    private bool showInvite;
    private string ownerTarget = "";
    private string? managementLink;

    private bool showNext;
    private string nextTitle = "";
    private int page;
    private ImmutableArray<ArchivedRound> history = [];

    private bool editing;
    private long editorVersion;
    private Guid editorRound;
    private Shared.RoomEditor? editor;

    private bool disposed;
    private IDisposable? subscription;
    private string? subscribedCode;

    private string PublicBase =>
        (Settings["Site:PublicUrl"] ?? Navigation.BaseUri).TrimEnd('/') + "/";
    private string Invite => PublicBase + "rooms/" + room!.Code;

    protected override async Task OnParametersSetAsync()
    {
        // Prerendering may read data, but presence belongs to the interactive circuit.
        actor = await Session.GetUserIdAsync();
        if (!RendererInfo.IsInteractive)
        {
            await Reload();
            await RememberVisit();
            return;
        }
        var code = Code.Trim().ToUpperInvariant();
        if (subscribedCode != code)
        {
            subscription?.Dispose();
            if (subscribedCode != null)
                await Updates.PublishAsync(subscribedCode);
            room = null;
            page = 0;
            editing = false;
            savedObserver = null;
            observer = false;
            managementLink = null;
            ownerTarget = "";
            subscribedCode = code;
            subscription = Updates.Subscribe(code, actor, Presence.ConnectionId, OnRoomChanged);
        }
        await Reload();
        await RememberVisit();
        await Updates.PublishAsync(code);
    }

    private Task OnRoomChanged() =>
        InvokeAsync(async () =>
        {
            if (disposed)
                return;
            await Reload();
            StateHasChanged();
        });

    private async Task RememberVisit()
    {
        if (room == null)
            return;
        try
        {
            await RoomsService.VisitAsync(room.Code, actor);
        }
        catch (Exception ex) when (ex is RoomException or StorageUnavailableException)
        {
            error = ex.Message;
        }
    }

    private async Task Reload()
    {
        try
        {
            var previous = room;
            room = await RoomsService.ReadAsync(Code, actor);
            if (
                previous != null
                && (previous.RoundId != room.RoundId || previous.ActivePhase != room.ActivePhase)
            )
                announcement = "Active phase: " + room.Configuration.Phases[room.ActivePhase].Name;
            if (!room.IsOwner)
                managementLink = null;
            var currentParticipant = room.Participants.FirstOrDefault(p => p.Id == actor);
            if (currentParticipant != null && savedObserver != currentParticipant.IsObserver)
            {
                observer = currentParticipant.IsObserver;
                savedObserver = currentParticipant.IsObserver;
            }
            if (page > 0 && page * RoomService.HistoryPageSize >= room.HistoryCount)
                page = Math.Max(0, (room.HistoryCount - 1) / RoomService.HistoryPageSize);
            history = await RoomsService.HistoryAsync(Code, page);
            if (string.IsNullOrEmpty(name))
                name = currentParticipant?.Name ?? "";
            error = null;
        }
        catch (Exception ex) when (ex is RoomException or StorageUnavailableException)
        {
            error = ex.Message;
            if (ex is RoomException)
                room = null;
        }
    }

    private async Task Run(RoomCommand command)
    {
        if (busy)
            return;
        busy = true;
        error = null;
        try
        {
            await RoomsService.ExecuteAsync(Code, actor, command);
            await Reload();
            if (error == null && command is Join or NextRound)
                focusVoting = true;
            announcement = command switch
            {
                Vote vote => vote.Option == null
                    ? "Your vote was withdrawn."
                    : "Your vote was saved.",
                Join join => join.IsObserver
                    ? "Your participation was saved. You are observing."
                    : "Your display name was saved. You can vote now.",
                Configure => "Configuration saved for the next round.",
                NextRound => "New round started. Votes are empty.",
                GrantOwner => "Co-owner access granted.",
                SetActivePhase => "Active phase updated.",
                _ => "Room updated.",
            };
        }
        catch (Exception ex) when (ex is RoomException or StorageUnavailableException)
        {
            error = ex.Message;
        }
        finally
        {
            busy = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (focusVoting)
        {
            focusVoting = false;
            await votingHeading.FocusAsync();
        }
    }

    private static string OptionName(string value) =>
        value switch
        {
            "∞" => "Unlimited",
            "?" => "Unsure",
            "👍" => "Like",
            "👎" => "Dislike",
            _ => value,
        };

    private async Task JoinRoom()
    {
        if (room == null)
            return;
        var roundId = room.RoundId;
        var discard = observer && !room.Revealed && room.VisibleVotes.Any(v => v.UserId == actor);
        if (
            discard
            && !await JS.InvokeAsync<bool>(
                "confirm",
                "Switch to observer and withdraw all your open votes? Revealed history is kept."
            )
        )
        {
            observer = savedObserver ?? false;
            announcement = "Mode change canceled. Your votes are unchanged.";
            return;
        }
        await Run(new Join(Guid.NewGuid(), roundId, name, observer, discard));
    }

    private bool TryTarget(out Guid target)
    {
        if (Guid.TryParse(ownerTarget, out target))
            return true;
        error = "Choose a participant first.";
        return false;
    }

    private async Task AddOwner(Guid roundId)
    {
        if (!TryTarget(out var target))
            return;
        if (
            !await JS.InvokeAsync<bool>(
                "confirm",
                "Give this participant full owner access, including deleting the room?"
            )
        )
            return;
        await Run(new GrantOwner(Guid.NewGuid(), roundId, target));
    }

    private async Task Transfer(Guid roundId)
    {
        if (!TryTarget(out var target))
            return;
        if (
            !await JS.InvokeAsync<bool>(
                "confirm",
                "Make this participant the only owner? You and all other owners lose management access, and every old management link stops working."
            )
        )
            return;
        await RunAction(
            () => RoomsService.TransferAsync(Code, actor, roundId, target),
            "Ownership transferred. Your management access has ended."
        );
    }

    private async Task RenewManagementLink(Guid roundId)
    {
        if (
            !await JS.InvokeAsync<bool>(
                "confirm",
                "Invalidate every previous management link and create a new private recovery link? Current owner sessions keep access."
            )
        )
            return;
        await RunAction(
            async () =>
            {
                var secret = await RoomsService.RotateManagementAsync(Code, actor, roundId);
                managementLink = PublicBase + "manage/" + room!.Code + "?secret=" + secret;
            },
            "New management link created. Save it privately."
        );
    }

    private async Task RunAction(Func<Task> action, string success)
    {
        if (busy)
            return;
        busy = true;
        error = null;
        try
        {
            await action();
            await Reload();
            announcement = success;
        }
        catch (Exception ex) when (ex is RoomException or StorageUnavailableException)
        {
            error = ex.Message;
        }
        finally
        {
            busy = false;
        }
    }

    private Task Cast(Guid roundId, int phase, string? option) =>
        Run(new Vote(Guid.NewGuid(), roundId, phase, option));

    private Task RevealVotes(Guid roundId) => Run(new Reveal(Guid.NewGuid(), roundId));

    // Keep the version and round from when editing started; a refresh must not
    // silently make a stale configuration draft valid.
    private void OpenEditor()
    {
        editing = true;
        editorVersion = room!.Version;
        editorRound = room.RoundId;
    }

    private async Task SaveConfiguration()
    {
        try
        {
            await Run(
                new Configure(
                    Guid.NewGuid(),
                    editorRound,
                    editorVersion,
                    editor!.GetConfiguration()
                )
            );
            if (error == null)
                editing = false;
        }
        catch (RoomException ex)
        {
            error = ex.Message;
        }
    }

    private async Task StartRound(Guid currentRound)
    {
        var discard = !room!.Revealed && room.Participants.Any(p => p.VotedPhases.Length > 0);
        if (
            discard
            && !await JS.InvokeAsync<bool>(
                "confirm",
                "Discard all unrevealed votes? This cannot be undone and no history will be saved."
            )
        )
            return;
        await Run(new NextRound(Guid.NewGuid(), currentRound, nextTitle, discard));
        if (error == null)
        {
            showNext = false;
            nextTitle = "";
            editing = false;
        }
    }

    private async Task DeleteRoom(Guid currentRound)
    {
        if (
            !await JS.InvokeAsync<bool>(
                "confirm",
                "Permanently delete this room, its votes and all history?"
            )
        )
            return;
        await Run(new Delete(Guid.NewGuid(), currentRound));
        if (room == null)
            Navigation.NavigateTo("/", forceLoad: true);
    }

    private async Task CopyInvite()
    {
        copied = await JS.InvokeAsync<bool>("estiblazor.copy", Invite);
        if (!copied)
        {
            showInvite = true;
            announcement = "Copy the invitation from the link below.";
        }
    }

    private async Task Previous()
    {
        page--;
        await Reload();
        announcement = $"History page {page + 1} loaded.";
    }

    private async Task Next()
    {
        page++;
        await Reload();
        announcement = $"History page {page + 1} loaded.";
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        subscription?.Dispose();
        if (subscribedCode != null)
            await Updates.PublishAsync(subscribedCode);
    }
}
