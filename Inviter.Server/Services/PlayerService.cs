using Inviter.Server.Models;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using System.Collections.Concurrent;

namespace Inviter.Server.Services;

public class PlayerService
{
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IDbContextFactory<InviterContext> _inviterContextFactory;
    private readonly ConcurrentDictionary<ulong, PlayerInfo> _activePlayers = new();

    public PlayerService(IClock clock, ILogger logger, IDbContextFactory<InviterContext> inviterContextFactory)
    {
        _clock = clock;
        _logger = logger;
        _inviterContextFactory = inviterContextFactory;
    }

    public void AddPlayer(PlayerInfo playerInfo)
    {
        if (_activePlayers.Remove(playerInfo.User.ID, out var player))
        {
            Unsubscribe(player);
            player.Disconnect();
        }
        _activePlayers.TryAdd(playerInfo.User.ID, playerInfo);
        Subscribe(playerInfo);
    }

    public void RemovePlayer(PlayerInfo playerInfo)
    {
        if (!_activePlayers.Remove(playerInfo.User.ID, out var player))
            return;
        
        Unsubscribe(player);
        player.Disconnect();
    }

    public void Poll()
    {
        var currentTime = _clock.GetCurrentInstant();
        foreach (var player in _activePlayers.Values)
            player.Poll(currentTime);
    }

    private void Subscribe(PlayerInfo player)
    {
        player.InviteSent += Player_InviteSent;
        player.WantsToDisconnect += Player_WantsToDisconnect;
        player.FriendsListRequested += Player_FriendsListRequested;
        player.InviteStatusReceived += Player_InviteStatusReceived;
        player.InviteJoinRequestReceived += Player_InviteJoinRequestReceived;
    }

    private async void Player_InviteSent(PlayerInfo player, ulong targetId, ulong[] lobbyMembers)
    {
        using var inviterContext = await _inviterContextFactory.CreateDbContextAsync();
        var sender = (await inviterContext.Users.FindAsync(player.User.ID))!;
        var target = await inviterContext.Users.Include(u => u.Friends).FirstOrDefaultAsync(u => u.ID == targetId);
        if (target is null)
        {
            // TODO: Send something back to the client saying the user does not exist.
            _logger.LogWarning("Could not find target when sending an invite.");
            return;
        }
        if (!target.AllowInvitesFromEveryone && !target.Friends.Contains(sender))
        {
            // TODO: Send something back to the client saying they aren't open to requests.
            // This realistically should be stopped client side as well, but we of course handle it on both sides just in case.
            _logger.LogWarning("{Target} ({TargetId}) is not accepting non-friend invites and {Sender} {SenderId} is not a friend, not sending request", target.Username, target.ID, sender.Username, sender.ID);
            return;
        }

        Invite invite = new()
        {
            ID = Guid.NewGuid(),
            From = sender,
            To = target,
            Start = _clock.GetCurrentInstant(),
            Status = InviteStatus.Pending,
        };
        invite.Lobby.AddRange(lobbyMembers);
        inviterContext.Invites.Add(invite);

        await inviterContext.SaveChangesAsync();
        await inviterContext.DisposeAsync();

        // IF the player is currently online, send the invite.
        if (_activePlayers.TryGetValue(target.ID, out PlayerInfo? targetPlayer))
            await targetPlayer.SendInvite(invite);
    }

    private void Player_WantsToDisconnect(PlayerInfo player)
    {
        RemovePlayer(player);
    }

    private async void Player_FriendsListRequested(PlayerInfo player, string searchText, int page)
    {
        using var inviterContext = await _inviterContextFactory.CreateDbContextAsync();
        var user = await inviterContext.Users.Include(u => u.Friends).FirstAsync(u => u.ID == player.User.ID);

        var friends = user.Friends.Where(u => u.Username.Contains(searchText, StringComparison.InvariantCultureIgnoreCase)).Skip(page * 10).Take(10).ToList();
        await player.SendFriendsList(friends);
    }

    private async void Player_InviteStatusReceived(PlayerInfo player, Guid inviteId, InviteStatus status)
    {
        if (status == InviteStatus.Ignored)
        {
            // TODO. Maybe log this.
            return;
        }

        using var inviterContext = await _inviterContextFactory.CreateDbContextAsync();
        var invite = await inviterContext.Invites.FirstOrDefaultAsync(i => i.ID == inviteId);
        var user = await inviterContext.Users.FirstAsync(u => u.ID == player.User.ID);

        if (invite is null)
        {
            // TODO. Invite not found.
            return;
        }

        if (invite.Status == InviteStatus.Expired)
        {
            // Don't let users accept expired invites.
            return;
        }

        invite.Status = status;
        invite.End = _clock.GetCurrentInstant();
        await inviterContext.SaveChangesAsync();
        _activePlayers.TryGetValue(invite.To.ID, out PlayerInfo? targetPlayer);

        if (status == InviteStatus.Accepted)
        {
            if (targetPlayer is not null)
                await targetPlayer.SendInviteAcceptance(invite);

            return;
        }

        if (status == InviteStatus.Rejected)
        {
            if (targetPlayer is not null)
                await targetPlayer.SendInviteDenial(invite);
            return;
        }
    }

    private async void Player_InviteJoinRequestReceived(PlayerInfo player, string code, Guid inviteId, Uri endPoint, Uri statusUrl, int? maxPartySize)
    {
        using var inviterContext = await _inviterContextFactory.CreateDbContextAsync();
        var invite = await inviterContext.Invites.FirstOrDefaultAsync(i => i.ID == inviteId);

        if (invite is null)
        {
            // Invite not found
            return;
        }

        // Ensure that the user have the ability to send the join info.
        if (invite.From.ID != player.User.ID)
        {
            return;
        }

        if (_activePlayers.TryGetValue(invite.To.ID, out PlayerInfo? targetPlayer))
            await targetPlayer.SendPlayerJoinInfo(code, endPoint, statusUrl, maxPartySize);
    }

    private void Unsubscribe(PlayerInfo player)
    {
        player.InviteJoinRequestReceived -= Player_InviteJoinRequestReceived;
        player.InviteStatusReceived -= Player_InviteStatusReceived;
        player.FriendsListRequested -= Player_FriendsListRequested;
        player.WantsToDisconnect -= Player_WantsToDisconnect;
        player.InviteSent -= Player_InviteSent;
    }
}