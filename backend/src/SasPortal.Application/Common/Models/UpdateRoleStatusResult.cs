namespace SasPortal.Application.Common.Models;

public sealed record UpdateRoleStatusResult(
    bool IsSuccess,
    string Message,
    RoleDetail? Role);
