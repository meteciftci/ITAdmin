using Microsoft.Extensions.Configuration;
using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Security;
using ITAdmin.Application.Common.Setup;
using ITAdmin.UnitTests.Fakes;

namespace ITAdmin.UnitTests.Setup;

public sealed class SetupRequestValidatorTests
{
    [Fact]
    public void TryValidateCompleteSetupRequest_RejectsDuplicateAdminUsersByDirectoryObjectId()
    {
        var request = CreateRequest([
            new CompleteSetupAdminUser("user1", null, "11111111-1111-1111-1111-111111111111"),
            new CompleteSetupAdminUser("user2", null, "11111111-1111-1111-1111-111111111111"),
        ]);

        var isValid = SetupRequestValidator.TryValidateCompleteSetupRequest(request, out var message, out var messageKey);

        Assert.False(isValid);
        Assert.Equal("Duplicate admin user selection is not allowed.", message);
        Assert.Equal("apiMessages.setup.duplicateAdminUser", messageKey);
    }

    [Fact]
    public void TryValidateCompleteSetupRequest_RejectsNullAdminUsers()
    {
        var request = new CompleteSetupRequest(
            "setup-secret",
            CreateRequest([]).Ldap,
            null!);

        var isValid = SetupRequestValidator.TryValidateCompleteSetupRequest(request, out var message, out var messageKey);

        Assert.False(isValid);
        Assert.Equal("At least one admin user is required.", message);
        Assert.Equal("apiMessages.setup.adminUsersRequired", messageKey);
    }

    [Fact]
    public void ValidateSetupKey_AcceptsMatchingHash()
    {
        const string setupKey = "setup-secret";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SetupKeyHashValidator.ConfigurationKey] = SetupKeyHashValidator.ComputeConfiguredHash(setupKey),
            })
            .Build();

        var outcome = SetupRequestValidator.ValidateSetupKey(new SetupKeyHashValidator(), configuration, setupKey);

        Assert.Equal(SetupKeyValidationOutcome.Valid, outcome);
    }

    private static CompleteSetupRequest CreateRequest(IReadOnlyList<CompleteSetupAdminUser> adminUsers) =>
        new(
            "setup-secret",
            new CompleteSetupLdapSettings(
                "Default LDAP",
                "dc01.test",
                "DC=test,DC=local",
                "(&(objectClass=user)(sAMAccountName={0}))",
                "bind",
                null,
                "bindpw"),
            adminUsers);
}
