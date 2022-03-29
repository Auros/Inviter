using Inviter.Server.Models;

namespace Inviter.Server;

public static class InviterExtensions
{
    public const string UserID = "Inviter_UserID";
    public const string StatePrefix = "Inviter_State";

    public static ulong GetInviterID(this HttpContext httpContext)
    {
        return ulong.Parse(httpContext.User.Claims.First(c => c.Type == UserID).Value);
    }

    public static string ToPolicy(this State state)
    {
        return $"{StatePrefix}{(int)state}";
    }

    public static State ToState(this string? policy)
    {
        if (policy is null)
            return State.None;

        if (Enum.TryParse(policy![StatePrefix.Length..], out State state))
            return state;
        return State.None;
    }
}