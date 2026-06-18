namespace ITAdmin.Application.Common.Models;

public sealed record UpdateCurrentUserPreferencesResult(
    bool IsSuccess,
    string Message,
    CurrentUserResult? User);
