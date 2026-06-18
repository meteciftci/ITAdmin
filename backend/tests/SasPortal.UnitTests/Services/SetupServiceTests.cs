using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;
using SasPortal.UnitTests.Fakes;

namespace SasPortal.UnitTests.Services;

public sealed class SetupServiceTests
{
    [Fact]
    public void BuildLdapUserNameCandidates_WithDomainSlashUser_ReturnsOriginalThenSamAccountName()
    {
        var result = SetupService.BuildLdapUserNameCandidates(@"DOMAIN\user");
        Assert.Equal(new[] { @"DOMAIN\user", "user" }, result);
    }

    [Fact]
    public void BuildLdapUserNameCandidates_WithEmailFormat_ReturnsOriginalThenLocalPart()
    {
        var result = SetupService.BuildLdapUserNameCandidates("user@domain.local");
        Assert.Equal(new[] { "user@domain.local", "user" }, result);
    }

    [Fact]
    public void BuildLdapUserNameCandidates_WithPlainSamAccount_ReturnsSingleEntry()
    {
        var result = SetupService.BuildLdapUserNameCandidates("user");
        Assert.Equal(new[] { "user" }, result);
    }

    [Fact]
    public void BuildLdapUserNameCandidates_TrimsAndIgnoresDuplicatesCaseInsensitive()
    {
        var result = SetupService.BuildLdapUserNameCandidates(@"  DOMAIN\User  ");
        Assert.Equal(new[] { @"DOMAIN\User", "User" }, result);
    }

    [Fact]
    public async Task CompleteSetupAsync_WhenFirstProfileLookupFails_RetriesWithSamAccountCandidate()
    {
        await using var context = CreateDbContext();
        var ldap = new FakeLdapService
        {
            ResolveUserProfile = request => request.UserName switch
            {
                @"DOMAIN\mete" => null,
                "mete" => new LdapUserProfile("obj-1", "mete", "Mete From Ldap", "mete@corp.test", null),
                _ => null,
            },
        };

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest(
            adminUserName: @"DOMAIN\mete",
            adminPassword: "p");

        var result = await service.CompleteSetupAsync(request);

        Assert.True(result.IsCompleted);
        Assert.Equal(2, ldap.GetUserProfileCallCount);

        var user = await context.PortalUsers.SingleAsync();
        Assert.Equal("mete", user.UserName);
        Assert.Equal("Mete From Ldap", user.DisplayName);
        Assert.Equal("mete@corp.test", user.Email);
    }

    [Fact]
    public async Task CompleteSetupAsync_UsesLdapProfileEmail_WhenPresent()
    {
        await using var context = CreateDbContext();
        var ldap = new FakeLdapService
        {
            ResolveUserProfile = _ => new LdapUserProfile(
                "obj-2",
                "plain",
                "Plain User",
                "plain@ad.test",
                null),
        };

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("plain", "p");

        var result = await service.CompleteSetupAsync(request);

        Assert.True(result.IsCompleted);
        var user = await context.PortalUsers.SingleAsync();
        Assert.Equal("plain@ad.test", user.Email);
    }

    [Fact]
    public async Task CompleteSetupAsync_LeavesEmailNull_WhenLdapProfileHasNoEmail()
    {
        await using var context = CreateDbContext();
        var ldap = new FakeLdapService
        {
            ResolveUserProfile = _ => new LdapUserProfile("obj-3", "noemail", "No Email User", null, null),
        };

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("noemail", "p");

        var result = await service.CompleteSetupAsync(request);

        Assert.True(result.IsCompleted);
        var user = await context.PortalUsers.SingleAsync();
        Assert.Null(user.Email);
    }

    [Fact]
    public async Task CompleteSetupAsync_WhenUseSslDisabled_FailsWithoutCallingLdap()
    {
        await using var context = CreateDbContext();
        var ldap = new FakeLdapService
        {
            ResolveUserProfile = _ => new LdapUserProfile("obj-ssl", "user", "User", null, null),
        };

        var service = CreateSetupService(context, ldap, "setup-secret");
        var baseRequest = CreateMinimalCompleteRequest("user", "p");
        var request = baseRequest with { Ldap = baseRequest.Ldap with { UseSsl = false } };

        var result = await service.CompleteSetupAsync(request);

        Assert.False(result.IsCompleted);
        Assert.Equal(SetupApiMessageKeys.Validation.SecureConnectionRequired, result.Message);
        Assert.Equal(0, ldap.ValidateCallCount);
        Assert.Equal(0, ldap.GetUserProfileCallCount);
        Assert.False(await context.PortalUsers.AnyAsync());
    }

    [Fact]
    public async Task CompleteSetupAsync_WhenProfileLookupFails_ReturnsDetailedEnglishMessage()
    {
        await using var context = CreateDbContext();
        var ldap = new FakeLdapService
        {
            ResolveUserProfile = _ => null,
        };

        var service = CreateSetupService(context, ldap, "setup-secret");
        var request = CreateMinimalCompleteRequest("someuser", "p");

        var result = await service.CompleteSetupAsync(request);

        Assert.False(result.IsCompleted);
        Assert.Equal(SetupService.DirectoryUserProfileCouldNotBeLoadedMessage, result.Message);
    }

    private static SetupService CreateSetupService(
        AppDbContext context,
        FakeLdapService ldap,
        string setupKey)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Setup:SetupKey"] = setupKey,
            })
            .Build();

        return new SetupService(
            context,
            ldap,
            new FakeSecretProtector(),
            configuration,
            NullLogger<SetupService>.Instance);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static CompleteSetupRequest CreateMinimalCompleteRequest(
        string adminUserName,
        string adminPassword)
    {
        return new CompleteSetupRequest(
            "setup-secret",
            new CompleteSetupLdapSettings(
                Name: "Default LDAP",
                Host: "dc01.test",
                Port: 636,
                UseSsl: true,
                BaseDn: "DC=test,DC=local",
                UserSearchBase: "",
                UserSearchFilter: "(&(objectClass=user)(sAMAccountName={0}))",
                BindUserName: "bind",
                BindUserDomain: null,
                BindPassword: "bindpw",
                NationalIdAttribute: null),
            new CompleteSetupAdminUser(adminUserName, adminPassword));
    }
}
