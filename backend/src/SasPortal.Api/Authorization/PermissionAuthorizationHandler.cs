using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SasPortal.Application.Common.Security;

namespace SasPortal.Api.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (IsSuperAdmin(context.User))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var hasPermission = context.User.FindAll(CustomClaimTypes.Permission)
            .Any(c => string.Equals(c.Value, requirement.Permission, StringComparison.Ordinal));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool IsSuperAdmin(ClaimsPrincipal user)
    {
        return user.FindAll(ClaimTypes.Role)
            .Any(c => string.Equals(c.Value, SystemRoles.SuperAdmin, StringComparison.Ordinal));
    }
}
