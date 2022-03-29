using Inviter.Server.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Inviter.Server.Authorization;

public class InviterStateAuthorizationHandler : AuthorizationHandler<InviterStateRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InviterStateAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, InviterStateRequirement requirement)
    {
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        Claim? claim = context.User.Claims.FirstOrDefault(c => c.Type == InviterExtensions.StatePrefix);

        if (claim is not null)
        {
            string stateRaw = InviterExtensions.StatePrefix + claim.Value;
            State state = stateRaw.ToState();

            if (state.HasFlag(requirement.State))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        context.Fail();
        return Task.CompletedTask;
    }
}