using Microsoft.AspNetCore.Authorization;

namespace ITAdmin.Api.Authorization;

public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public RequirePermissionAttribute(string permission)
    {
        Policy = PolicyPrefix + permission;
    }
}
