using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SasPortal.Api.Authorization;

public sealed class PermissionAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var permission = policyName[RequirePermissionAttribute.PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return policy;
        }

        if (policyName.StartsWith(RequireAnyPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var permissions = policyName[RequireAnyPermissionAttribute.PolicyPrefix.Length..]
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new AnyPermissionRequirement(permissions))
                .Build();
            return policy;
        }

        return await base.GetPolicyAsync(policyName).ConfigureAwait(false);
    }
}
