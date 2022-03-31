using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Inviter.Server;

public static class InviterProtocol
{
    public const string InviteEventType = "invite";
    public const string FriendsListEventType = "friends-list";
    
    public static readonly JsonSerializerOptions JSON;

    static InviterProtocol()
    {
        JSON = new JsonSerializerOptions();
        JSON.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        JSON.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        JSON.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault;
    }
}