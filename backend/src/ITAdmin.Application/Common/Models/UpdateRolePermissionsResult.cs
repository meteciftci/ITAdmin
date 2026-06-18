namespace ITAdmin.Application.Common.Models;

public sealed record UpdateRolePermissionsResult(
    bool IsSuccess,
    string Message,
    RoleDetail? Role);
