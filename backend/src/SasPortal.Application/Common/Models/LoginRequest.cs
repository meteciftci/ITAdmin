namespace SasPortal.Application.Common.Models;

public sealed record LoginRequest(
    string UserName,
    string Password,
    string? IpAddress,
    string? UserAgent,
    bool RememberMe = false);
