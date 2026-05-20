using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SasPortal.Application.Common.Security;

namespace SasPortal.Api.Authorization;

public sealed class AnyPermissionAuthorizationHandler : AuthorizationHandler<AnyPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnyPermissionRequirement requirement)
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

        var userPermissions = context.User.FindAll(CustomClaimTypes.Permission)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        if (requirement.Permissions.Any(permission => userPermissions.Contains(permission)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool IsSuperAdmin(ClaimsPrincipal user) =>
        user.FindAll(ClaimTypes.Role)
            .Any(c => string.Equals(c.Value, SystemRoles.SuperAdmin, StringComparison.Ordinal));
}
