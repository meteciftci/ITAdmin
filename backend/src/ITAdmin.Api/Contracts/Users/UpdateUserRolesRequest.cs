namespace ITAdmin.Api.Contracts.Users;

public sealed record UpdateUserRolesRequest(
    IReadOnlyCollection<Guid> RoleIds);
