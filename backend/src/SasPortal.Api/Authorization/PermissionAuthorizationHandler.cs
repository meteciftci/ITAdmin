using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SasPortal.Application.Common.Security;

namespace SasPortal.Api.Authorization;

public sealed class PermissionAuthorizationHandler(
    ForbiddenAccessSecurityLogger forbiddenAccessSecurityLogger) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (IsSuperAdmin(context.User))
        {
            context.Succeed(requirement);
            return;
        }

        var hasPermission = context.User.FindAll(CustomClaimTypes.Permission)
            .Any(c => string.Equals(c.Value, requirement.Permission, StringComparison.Ordinal));

        if (hasPermission)
        {
            context.Succeed(requirement);
            return;
        }

        await forbiddenAccessSecurityLogger.TryLogPermissionDeniedAsync(
            context,
            requirement.Permission,
            CancellationToken.None);
    }

    private static bool IsSuperAdmin(ClaimsPrincipal user)
    {
        return user.FindAll(ClaimTypes.Role)
            .Any(c => string.Equals(c.Value, SystemRoles.SuperAdmin, StringComparison.Ordinal));
    }
}
