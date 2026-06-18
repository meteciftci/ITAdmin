namespace ITAdmin.Application.Abstractions.Security;

public enum SetupKeyValidationOutcome
{
    Valid,
    MissingHashConfiguration,
    InvalidHashFormat,
    InvalidKey
}

public interface ISetupKeyValidator
{
    bool IsHashConfigured(string? configuredHash);

    bool IsValidHashFormat(string? configuredHash);

    SetupKeyValidationOutcome Validate(string? configuredHash, string plaintextSetupKey);
}
