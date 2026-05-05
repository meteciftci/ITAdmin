namespace SasPortal.Application.Common.Models;

public sealed record UpdateRoleResult(
    bool IsSuccess,
    string Message,
    RoleDetail? Role);
