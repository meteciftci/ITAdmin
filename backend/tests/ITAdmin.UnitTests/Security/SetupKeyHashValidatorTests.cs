using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Common.Security;

namespace ITAdmin.UnitTests.Security;

public sealed class SetupKeyHashValidatorTests
{
    private readonly SetupKeyHashValidator _validator = new();

    [Fact]
    public void ComputeConfiguredHash_ProducesSha256Base64UrlFormat()
    {
        var hash = SetupKeyHashValidator.ComputeConfiguredHash("setup-secret");

        Assert.StartsWith("sha256:", hash, StringComparison.OrdinalIgnoreCase);
        Assert.True(_validator.IsValidHashFormat(hash));
    }

    [Fact]
    public void Validate_ReturnsValid_ForMatchingSetupKey()
    {
        const string setupKey = "setup-secret";
        var configuredHash = SetupKeyHashValidator.ComputeConfiguredHash(setupKey);

        var outcome = _validator.Validate(configuredHash, setupKey);

        Assert.Equal(SetupKeyValidationOutcome.Valid, outcome);
    }

    [Fact]
    public void Validate_ReturnsInvalidKey_ForWrongSetupKey()
    {
        var configuredHash = SetupKeyHashValidator.ComputeConfiguredHash("expected-key");

        var outcome = _validator.Validate(configuredHash, "wrong-key");

        Assert.Equal(SetupKeyValidationOutcome.InvalidKey, outcome);
    }

    [Fact]
    public void Validate_ReturnsMissingHashConfiguration_WhenHashNotConfigured()
    {
        var outcome = _validator.Validate(null, "setup-secret");

        Assert.Equal(SetupKeyValidationOutcome.MissingHashConfiguration, outcome);
    }

    [Theory]
    [InlineData("sha256:")]
    [InlineData("md5:abc")]
    [InlineData("sha256:not-valid-base64url")]
    public void Validate_ReturnsInvalidHashFormat_ForInvalidConfiguredHash(string configuredHash)
    {
        var outcome = _validator.Validate(configuredHash, "setup-secret");

        Assert.Equal(SetupKeyValidationOutcome.InvalidHashFormat, outcome);
    }

    [Fact]
    public void IsValidHashFormat_ReturnsFalse_ForPlaintextValue()
    {
        Assert.False(_validator.IsValidHashFormat("plaintext-setup-key"));
    }
}
