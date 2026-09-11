using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ITAdmin.Application.Common.Security;

namespace ITAdmin.Api.Authorization;

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

        var grantedPermissions = context.User.FindAll(CustomClaimTypes.Permission)
            .Select(c => c.Value);
        var hasPermission = LicenseManagementPermissionRules.IsSatisfiedBy(
            requirement.Permission,
            grantedPermissions);

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
