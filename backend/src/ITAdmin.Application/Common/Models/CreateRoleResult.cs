namespace ITAdmin.Application.Common.Models;

public sealed record CreateRoleResult(
    bool IsSuccess,
    string Message,
    RoleDetail? Role);
