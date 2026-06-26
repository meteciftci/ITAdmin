using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.Infrastructure.Services;

namespace ITAdmin.UnitTests.Services;

public sealed class AdManagementValidationServiceTests
{
    private readonly AdManagementValidationService _service = new();

    [Fact]
    public async Task ValidateConnectionAsync_WhenDefaultNamingContextMissing_ReturnsFailed()
    {
        var connection = CreateConnection(defaultNamingContext: null);

        var result = await _service.ValidateConnectionAsync(
            connection,
            new AdManagementValidationRequest(null, "tester", null, null));

        Assert.False(result.IsValid);
        Assert.Equal(
            AdManagementApiMessageKeys.SettingsValidation.MissingRequiredSettings,
            result.MessageKey);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenScopeOusMissing_DoesNotReturnMissingRequiredSettings()
    {
        var connection = CreateConnection(usersRootOu: null, disabledUsersOu: null);

        var result = await _service.ValidateConnectionAsync(
            connection,
            new AdManagementValidationRequest(null, "tester", null, null));

        Assert.NotEqual(
            AdManagementApiMessageKeys.SettingsValidation.MissingRequiredSettings,
            result.MessageKey);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenNetbiosDomainNameMissing_ReturnsFailed()
    {
        var connection = CreateConnection(netbiosDomainName: null);

        var result = await _service.ValidateConnectionAsync(
            connection,
            new AdManagementValidationRequest(null, "tester", null, null));

        Assert.False(result.IsValid);
        Assert.Equal(
            AdManagementApiMessageKeys.SettingsValidation.MissingRequiredSettings,
            result.MessageKey);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenBindFails_ReturnsServiceAccountBindFailedMessageKey()
    {
        var connection = CreateConnection(
            domainFqdn: "invalid-ad-host.invalid",
            preferredDomainControllers: new[] { "invalid-ad-host.invalid" });

        var result = await _service.ValidateConnectionAsync(
            connection,
            new AdManagementValidationRequest(null, "tester", null, null));

        Assert.False(result.IsValid);
        Assert.Equal(
            AdManagementApiMessageKeys.SettingsValidation.ServiceAccountBindFailed,
            result.MessageKey);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenBindFails_IncludesServiceAccountBindDetailKey()
    {
        var connection = CreateConnection(
            domainFqdn: "invalid-ad-host.invalid",
            preferredDomainControllers: new[] { "invalid-ad-host.invalid" });

        var result = await _service.ValidateConnectionAsync(
            connection,
            new AdManagementValidationRequest(null, "tester", null, null));

        Assert.Contains(
            result.Details,
            d => d.Key == "serviceAccountBind" && d.Status == "Failed");
    }

    private static AdManagementConnectionParameters CreateConnection(
        string? domainFqdn = "corp.example.com",
        string? netbiosDomainName = "CORP",
        string? defaultNamingContext = "DC=corp,DC=example,DC=com",
        string? baseDn = "DC=corp,DC=example,DC=com",
        string? usersRootOu = "OU=Users,DC=corp,DC=example,DC=com",
        string? disabledUsersOu = "OU=Disabled,DC=corp,DC=example,DC=com",
        IReadOnlyList<string>? preferredDomainControllers = null) =>
        new(
            domainFqdn,
            netbiosDomainName,
            defaultNamingContext,
            baseDn,
            usersRootOu,
            disabledUsersOu,
            null,
            null,
            preferredDomainControllers ?? Array.Empty<string>(),
            "svc_ad",
            "secret-password");
}
