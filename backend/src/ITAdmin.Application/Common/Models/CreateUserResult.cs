namespace ITAdmin.Application.Common.Models;

public sealed record CreateUserResult(
    bool IsSuccess,
    string Message,
    UserDetail? User);
