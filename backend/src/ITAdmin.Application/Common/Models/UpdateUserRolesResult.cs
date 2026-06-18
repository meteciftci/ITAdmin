namespace ITAdmin.Application.Common.Models;

public sealed record UpdateUserRolesResult(
    bool IsSuccess,
    string Message,
    UserDetail? User);
