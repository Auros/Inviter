using System.Text.Json.Serialization;

namespace Inviter.Server.Models;

public class SoriginUser
{
    [JsonPropertyName("id")]
    public ulong ID { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = null!;

    [JsonPropertyName("country")]
    public string? Country { get; set; } = null!;

    [JsonPropertyName("profilePicture")]
    public string ProfilePicture { get; set; } = null!;
}