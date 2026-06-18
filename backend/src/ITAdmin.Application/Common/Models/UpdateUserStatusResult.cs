namespace ITAdmin.Application.Common.Models;

public sealed record UpdateUserStatusResult(
    bool IsSuccess,
    string Message,
    UserDetail? User);
