using NodaTime;

namespace Inviter.Server.Models;

public class Invite
{
    public Guid ID { get; set; }
    public User To { get; set; } = null!;
    public User From { get; set; } = null!;
    public List<ulong> Lobby { get; set; } = new();

    public InviteStatus Status { get; set; }
    public Instant Start { get; set; }
    public Instant? End { get; set; }
}