namespace SasPortal.Api.Contracts.AdManagement;

public sealed record UpdateAdUserAccountExpirationRequest
{
    public bool NeverExpires { get; init; }
    public string? ExpiresAt { get; init; }
}
