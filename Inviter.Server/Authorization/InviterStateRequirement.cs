using Inviter.Server.Models;
using Microsoft.AspNetCore.Authorization;

namespace Inviter.Server.Authorization;

public class InviterStateRequirement : IAuthorizationRequirement
{
    public State State { get; }

    public InviterStateRequirement(State state)
    {
        State = state;
    }
}