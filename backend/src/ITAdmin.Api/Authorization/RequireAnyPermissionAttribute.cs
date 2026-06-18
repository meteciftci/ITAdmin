using Microsoft.AspNetCore.Authorization;

namespace ITAdmin.Api.Authorization;

public sealed class RequireAnyPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "PermissionAny:";

    public RequireAnyPermissionAttribute(params string[] permissions)
    {
        if (permissions.Length == 0)
        {
            throw new ArgumentException("At least one permission is required.", nameof(permissions));
        }

        Policy = PolicyPrefix + string.Join('|', permissions);
    }
}
