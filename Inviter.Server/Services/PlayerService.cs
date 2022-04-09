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

    public PlayerService(IClock clock, ILogger<PlayerService> logger, IDbContextFactory<InviterContext> inviterContextFactory)
    {
        _clock = clock;
        _logger = logger;
        _inviterContextFactory = inviterContextFactory;
    }

    public void AddPlayer(PlayerInfo playerInfo)
    {
        RemovePlayer(playerInfo);
        _logger.LogInformation("Adding player {Player} ({PlayerID}) to the manager", playerInfo.User.Username, playerInfo.User.ID);
        _activePlayers.TryAdd(playerInfo.User.ID, playerInfo);
        Subscribe(playerInfo);
    }

    public void RemovePlayer(PlayerInfo playerInfo)
    {
        if (!_activePlayers.Remove(playerInfo.User.ID, out var player))
            return;

        _logger.LogInformation("Removing player {Player} ({PlayerID}) from the manager", playerInfo.User.Username, player.User.ID);
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
        player.FriendsListRequested += Player_FriendsListRequested;
        player.InviteStatusReceived += Player_InviteStatusReceived;
        player.FriendRequestReceived += Player_FriendRequestReceived;
        player.InviteJoinRequestReceived += Player_InviteJoinRequestReceived;
        player.FriendsRequestListRequested += Player_FriendsRequestListRequested;
        player.FriendRequestResponseReceived += Player_FriendRequestResponseReceived;
    }

    private async void Player_InviteSent(PlayerInfo player, ulong targetId, ulong[] lobbyMembers)
    {
        using var inviterContext = await _inviterContextFactory.CreateDbContextAsync();
        var sender = (await inviterContext.Users.FindAsync(player.User.ID))!;
        var target = await inviterContext.Users.Include(u => u.Friends).FirstOrDefaultAsync(u => u.ID == targetId);
        if (target is null)
        {
            await player.SendErrorMessage("Could not send invite. Unable to find the user. Make sure they've used the mod.");
            _logger.LogWarning("Could not find target when sending an invite.");
            return;
        }
        if (!target.AllowInvitesFromEveryone && !target.Friends.Contains(sender))
        {
            // This should be stopped client side as well, but we of course handle it on both sides just in case.
            await player.SendErrorMessage($"{target.Username} does not have invites enabled.");
            _logger.LogWarning("{Target} ({TargetID}) is not accepting non-friend invites and {Sender} {SenderID} is not a friend, not sending request", target.Username, target.ID, sender.Username, sender.ID);
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
        {
            _logger.LogInformation("{Sender} {SenderID} just sent an invite to {Target} ({TargetID})", sender.Username, sender.ID, target.Username, target.ID);
            await targetPlayer.SendInvite(invite);
        }    
    }

    private async void Player_FriendsListRequested(PlayerInfo player, string searchText, int page)
    {
        using var inviterContext = await _inviterContextFactory.CreateDbContextAsync();
        var user = await inviterContext.Users.Include(u => u.Friends).FirstAsync(u => u.ID == player.User.ID);

        var friends = user.Friends.Where(u => u.Username.Contains(searchText, StringComparison.InvariantCultureIgnoreCase)).Skip(page * 10).Take(10).ToList();
        _logger.LogDebug("Sending friends list to {Player} ({PlayerID}", user.Username, user.ID);
        await player.SendFriendsList(friends);
    }

    private async void Player_InviteStatusReceived(PlayerInfo player, Guid inviteId, InviteStatus status)
    {
        using var inviterContext = await _inviterContextFactory.CreateDbContextAsync();
        var invite = await inviterContext.Invites.FirstOrDefaultAsync(i => i.ID == inviteId);
        var user = await inviterContext.Users.FirstAsync(u => u.ID == player.User.ID);

        if (invite is null)
        {
            _logger.LogWarning("Couldn't find an invite with the ID {InviteID}", inviteId);
            return;
        }

        if (invite.Status == InviteStatus.Expired)
        {
            _logger.LogInformation("{Player} ({PlayerID}) tried to accept an expired invite ({InviteID})", user.Username, user.ID, invite.ID);
            await player.SendErrorMessage("Invite has expired.");
            return;
        }

        invite.Status = status;
        invite.End = _clock.GetCurrentInstant();
        await inviterContext.SaveChangesAsync();
        _activePlayers.TryGetValue(invite.To.ID, out PlayerInfo? targetPlayer);

        if (status == InviteStatus.Ignored)
        {
            _logger.LogDebug("{Player} ({PlayerID}) has ignored the invite ({InviteID})", user.Username, user.ID, invite.ID);
            return;
        }

        if (status == InviteStatus.Accepted)
        {
            if (targetPlayer is not null)
            {
                _logger.LogInformation("Sending invite acceptance for {Player} ({PlayerID}) to invite {InviteID}", user.Username, user.ID, invite.ID);
                await targetPlayer.SendInviteAcceptance(invite);
            }    

            return;
        }

        if (status == InviteStatus.Rejected)
        {
            if (targetPlayer is not null)
            {
                _logger.LogInformation("Sending invite rejection for {Player} ({PlayerID}) to invite {InviteID}", user.Username, user.ID, invite.ID);
                await targetPlayer.SendInviteDenial(invite);
            }
            return;
        }
    }

    private async void Player_FriendRequestReceived(PlayerInfo player, ulong userId)
    {
        if (player.User.ID == userId)
        {
            _logger.LogWarning("The user {Player} ({PlayerID}) tried to send a friend request to themselves!", player.User.Username, player.User.ID);
            await player.SendErrorMessage("You cannot send a friend request to yourself!");
            return;
        }

        using var inviterContext = await _inviterContextFactory.CreateDbContextAsync();
        var target = await inviterContext.Users.Include(u => u.FriendRequests).Include(u => u.Friends).FirstOrDefaultAsync(i => i.ID == userId);
        
        if (target is null)
        {
            // Handling if we couldn't find the user to friend request.
            await player.SendErrorMessage("Could not find user.");
            _logger.LogWarning("Could not find user {UserID} when {Player} ({PlayerID}) sent a friend request", userId, player.User.Username, player.User.ID);
            return;
        }
        if (target.FriendRequests.Any(u => u.ID == player.User.ID))
        {
            // Handling if a friend request to the user was alreadty sent.
            _logger.LogInformation("The player {Player} ({PlayerID}) tried to send a friend request to {User} ({UserID}), but they already did.", player.User.Username, player.User.ID, target.Username, target.ID);
            await player.SendErrorMessage("Friend request already sent.");
            return;
        }
        if (target.Friends.Any(u => u.ID == player.User.ID))
        {
            // Handling if the subjects are already friends.
            _logger.LogInformation("The player {Player} ({PlayerID}) tried to send a friend request to {User} ({UserID}), but they are already friends.", player.User.Username, player.User.ID, target.Username, target.ID);
            await player.SendErrorMessage($"You are already friends with {target.Username}.");
            return;
        }
        if (!target.AllowFriendRequests)
        {
            // Handling if the requestee has friend requests disabled.
            _logger.LogInformation("The player {Player} ({PlayerID}) tried to send a friend request to {User} ({UserID}), but the receiver has friend reequests disabled", player.User.Username, player.User.ID, target.Username, target.ID);
            await player.SendErrorMessage($"The user {target.Username} has friend requests disabled.");
            return;
        }

        var sender = await inviterContext.Users.FirstOrDefaultAsync(i => i.ID == player.User.ID);
        if (sender is null)
        {
            _logger.LogWarning("Unable to find original friender {UserId}. This shouldn't happen.", player.User.ID);
            return;
        }

        target.FriendRequests.Add(sender);
        await inviterContext.SaveChangesAsync();

        // IF the player is currently online, send the request.
        if (_activePlayers.TryGetValue(target.ID, out PlayerInfo? targetPlayer))
        {
            _logger.LogInformation("{Sender} {SenderID} just sent a friend request to {Target} ({TargetID})", sender.Username, sender.ID, target.Username, target.ID);
            await targetPlayer.SendFriendRequestInfo(sender);
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
            _logger.LogWarning("{Player} {PlayerID} sent invite data to the wrong receiver.", player.User.Username, player.User.ID);
            return;
        }

        if (_activePlayers.TryGetValue(invite.To.ID, out PlayerInfo? targetPlayer))
        {
            _logger.LogInformation("Sending invite join info to {Player} ({PlayerID}) for the invite {InviteID}", player.User.Username, player.User.ID, inviteId);
            await targetPlayer.SendPlayerJoinInfo(code, endPoint, statusUrl, maxPartySize);
        }
    }

    private async void Player_FriendsRequestListRequested(PlayerInfo player, string searchText, int page)
    {
        using var inviterContext = await _inviterContextFactory.CreateDbContextAsync();
        var user = await inviterContext.Users.Include(u => u.FriendRequests).FirstAsync(u => u.ID == player.User.ID);

        user.FriendRequests.Reverse();
        var friends = user.FriendRequests.Where(u => u.Username.Contains(searchText, StringComparison.InvariantCultureIgnoreCase)).Skip(page * 10).Take(10).ToList();
        _logger.LogDebug("Sending friend requests list to {Player} ({PlayerID}", user.Username, user.ID);
        await player.SendFriendRequestsList(friends);
    }

    private async void Player_FriendRequestResponseReceived(PlayerInfo player, ulong senderId, bool wasAccepted)
    {
        using var inviterContext = await _inviterContextFactory.CreateDbContextAsync();
        var sender = await inviterContext.Users.Include(u => u.Friends).FirstOrDefaultAsync(i => i.ID == senderId);
        var receiver = await inviterContext.Users.Include(u => u.FriendRequests).Include(u => u.Friends).FirstOrDefaultAsync(i => i.ID == player.User.ID);
    
        if (sender is null || receiver is null)
        {
            _logger.LogWarning("Could not gather sender or receiver.");
            return;
        }

        if (!receiver.FriendRequests.Contains(sender))
        {
            _logger.LogInformation("The friend request doesn't exist.");
            await player.SendErrorMessage("Could not find friend request.");
            return;
        }

        receiver.FriendRequests.Remove(sender);
        if (wasAccepted)
        {
            receiver.Friends.Add(sender);
            sender.Friends.Add(receiver);
        }
        await inviterContext.SaveChangesAsync();

        sender.Friends.Clear();
        receiver.Friends.Clear();
        receiver.FriendRequests.Clear();

        if (_activePlayers.TryGetValue(sender.ID, out PlayerInfo? targetPlayer))
        {
            _logger.LogInformation("Sending friend acceptance status of '{Status}' to {Player} ({PlayerID}) ", wasAccepted, sender.Username, sender.ID);
            var requestTask = wasAccepted ? targetPlayer.SendFriendRequestAccepted(receiver) : targetPlayer.SendFriendRequestDenied(receiver);
            await requestTask;
        }
    }

    private void Unsubscribe(PlayerInfo player)
    {
        player.FriendRequestResponseReceived -= Player_FriendRequestResponseReceived;
        player.FriendsRequestListRequested -= Player_FriendsRequestListRequested;
        player.InviteJoinRequestReceived -= Player_InviteJoinRequestReceived;
        player.FriendRequestReceived -= Player_FriendRequestReceived;
        player.InviteStatusReceived -= Player_InviteStatusReceived;
        player.FriendsListRequested -= Player_FriendsListRequested;
        player.InviteSent -= Player_InviteSent;
    }
}