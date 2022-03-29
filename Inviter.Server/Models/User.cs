using NodaTime;
using System.Text.Json.Serialization;

namespace Inviter.Server.Models;

public class User
{
    public ulong ID { get; set; }

    public string Username { get; set; } = null!;
    
    [JsonIgnore]
    public State State { get; set; }

    [JsonIgnore]
    public string? Country { get; set; } = null!;

    public string ProfilePicture { get; set; } = null!;

    public bool AllowInvitesFromEveryone { get; set; }

    public List<User> Friends { get; set; } = new();

    public Instant LastSeen { get; set; }
}