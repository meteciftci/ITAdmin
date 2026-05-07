namespace SasPortal.Api.Contracts.Settings;

public sealed record ValidateLdapSettingsResponse(bool IsValid, string Message);
