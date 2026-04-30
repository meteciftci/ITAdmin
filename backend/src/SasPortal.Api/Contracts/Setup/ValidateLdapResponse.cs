namespace SasPortal.Api.Contracts.Setup;

public sealed record ValidateLdapResponse(bool IsValid, string Message);
