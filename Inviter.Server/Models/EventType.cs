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
    Ping = 7
}