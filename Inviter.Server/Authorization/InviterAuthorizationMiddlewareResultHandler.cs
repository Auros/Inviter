using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Inviter.Server.Authorization;

public class InviterAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler DefaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var requirement = policy.Requirements.FirstOrDefault(r => r is InviterStateRequirement);
        if (!authorizeResult.Succeeded && requirement != null)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync<Error>(new("missing-state", $"State [{((InviterStateRequirement)requirement).State}] is missing."));
            return;
        }

        await DefaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}