namespace SasPortal.Application.Common.Models;

public sealed record LogoutRequest(
    string RefreshToken,
    string? IpAddress,
    string? UserAgent);
