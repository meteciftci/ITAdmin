namespace SasPortal.Application.Common.Models;

public sealed record LogoutResult(
    bool IsSuccess,
    string Message);
