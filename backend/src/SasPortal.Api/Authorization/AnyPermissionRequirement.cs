using Microsoft.AspNetCore.Authorization;

namespace SasPortal.Api.Authorization;

public sealed class AnyPermissionRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; }

    public AnyPermissionRequirement(IEnumerable<string> permissions)
    {
        Permissions = permissions.ToList();
    }
}
