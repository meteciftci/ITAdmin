namespace SasPortal.Application.Common.Models;

public sealed record RefreshTokenRequest(
    string RefreshToken,
    string? IpAddress,
    string? UserAgent);
