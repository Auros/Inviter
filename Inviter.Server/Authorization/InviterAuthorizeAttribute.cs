using Inviter.Server.Models;
using Microsoft.AspNetCore.Authorization;

namespace Inviter.Server.Authorization;

public class InviterAuthorizeAttribute : AuthorizeAttribute
{
    public InviterAuthorizeAttribute(State state = State.None)
        => State = state;

    public State State
    {
        get => Policy.ToState();
        set => Policy = value.ToPolicy();
    }
}