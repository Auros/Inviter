using Inviter.Server.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Inviter.Server.Authorization;

public class InviterAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _defaultAuthorizationPolicyProvider;

    public InviterAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _defaultAuthorizationPolicyProvider = new(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _defaultAuthorizationPolicyProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _defaultAuthorizationPolicyProvider.GetFallbackPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(InviterExtensions.StatePrefix, StringComparison.OrdinalIgnoreCase) && Enum.TryParse(policyName[InviterExtensions.StatePrefix.Length..], out State state))
        {
            AuthorizationPolicyBuilder policy = new(CookieAuthenticationDefaults.AuthenticationScheme);
            policy.AddRequirements(new InviterStateRequirement(state));
            return Task.FromResult<AuthorizationPolicy?>(policy.Build());
        }
        return _defaultAuthorizationPolicyProvider.GetPolicyAsync(policyName);
    }
}