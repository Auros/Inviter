namespace Inviter.Server.Models;

public enum EventType
{
    None = 0,
    Unknown = 1,
    PlayerSentInvite = 2,
    PlayerReceivedInvite = 3,
    PlayerReceivedFriendsList = 4,
    PlayerSentFriendsListInfoRequest = 5,
    PlayerSubmittedInviteStatus = 6,
    Ping = 7,
    PlayerReceivedInviteAcceptance = 8,
    PlayerReceivedInviteDenial = 9,
    PlayerSentJoinRequest = 10,
    PlayerReceivedJoinInfo = 11,
    Error = 12,
    PlayerSentFriendRequest = 13,
    PlayerReceivedFriendRequest = 14,
    PlayerSubmittedFriendRequest = 15,
    PlayerReceivedFriendAcceptance = 16,
    PlayerReceivedFriendDenial = 17,
    PlayerReceivedFriendRequestList = 18,
    PlayerSentFriendRequestsListInfoRequest = 19
}