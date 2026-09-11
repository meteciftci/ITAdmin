using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services;
using ITAdmin.UnitTests.Fakes;
using Microsoft.EntityFrameworkCore;
using ITAdmin.Api.Contracts.DnsManagement;

namespace ITAdmin.UnitTests.DnsManagement;

public sealed class DnsManagementAdministrationServiceTests
{
    [Fact]
    public void Credential_response_contract_never_contains_a_secret_value()
    {
        var properties = typeof(DnsCredentialProfileResponse).GetProperties().Select(x => x.Name).ToArray();
        Assert.Contains("HasPassword", properties);
        Assert.DoesNotContain("Password", properties);
        Assert.DoesNotContain("EncryptedPassword", properties);
    }

    [Fact]
    public async Task Credential_secret_is_encrypted_hidden_and_retained_when_update_password_is_blank()
    {
        await using var context = CreateContext();
        var service = new DnsManagementAdministrationService(context, new FakeSecretProtector());
        var actor = new DnsActorContext(null, "admin", null, null);

        var created = await service.SaveCredentialProfileAsync(new(null, "Internal DNS", DnsAuthenticationMode.Negotiate, "DOMAIN\\dns-svc", "top-secret", true, actor));
        Assert.True(created.IsSuccess);
        Assert.True(created.Value!.HasPassword);
        Assert.Equal("protected:top-secret", (await context.DnsCredentialProfiles.SingleAsync()).EncryptedPassword);

        var updated = await service.SaveCredentialProfileAsync(new(created.Value.Id, "Internal DNS", DnsAuthenticationMode.Negotiate, "DOMAIN\\dns-svc", "", true, actor));
        Assert.True(updated.IsSuccess);
        Assert.Equal("protected:top-secret", (await context.DnsCredentialProfiles.SingleAsync()).EncryptedPassword);
        Assert.DoesNotContain("top-secret", string.Join(' ', context.AuditLogs.Select(x => x.Description)));
    }

    [Fact]
    public async Task Credential_assigned_to_server_cannot_be_deleted()
    {
        await using var context = CreateContext();
        var credential = new DnsCredentialProfile { Name = "Public", UserName = "dns-user", EncryptedPassword = "protected:x", IsEnabled = true };
        context.DnsCredentialProfiles.Add(credential);
        context.DnsServers.Add(new DnsServer { DisplayName = "Public DNS", HostName = "192.0.2.10", Port = 5986, CredentialProfile = credential });
        await context.SaveChangesAsync();

        var result = await new DnsManagementAdministrationService(context, new FakeSecretProtector())
            .DeleteCredentialProfileAsync(credential.Id, new(null, "admin", null, null));

        Assert.False(result.IsSuccess);
        Assert.Single(context.DnsCredentialProfiles);
    }

    [Fact]
    public async Task Server_endpoint_is_normalized_and_requires_an_active_credential()
    {
        await using var context = CreateContext();
        var credential = new DnsCredentialProfile { Name = "Internal", UserName = "dns-user", EncryptedPassword = "protected:x", IsEnabled = true };
        context.DnsCredentialProfiles.Add(credential);
        await context.SaveChangesAsync();
        var service = new DnsManagementAdministrationService(context, new FakeSecretProtector());

        var result = await service.SaveServerAsync(new(null, "İç DNS", "DNS01.EXAMPLE.LOCAL.", 5986,
            DnsServerEnvironment.Internal, credential.Id, true, 30, "AA BB CC DD EE FF 00 11 22 33 44 55 66 77 88 99 AA BB CC DD", null,
            new(null, "admin", null, null)));

        Assert.True(result.IsSuccess);
        Assert.Equal("dns01.example.local", result.Value!.HostName);
        Assert.Equal("AABBCCDDEEFF00112233445566778899AABBCCDD", result.Value.TlsCertificateThumbprint);
    }

    [Fact]
    public async Task Invalid_settings_are_rejected_before_persistence()
    {
        await using var context = CreateContext();
        var result = await new DnsManagementAdministrationService(context, new FakeSecretProtector()).UpdateSettingsAsync(
            new(true, true, 0, 15, 60, 3, 30, true, true, 15, new(null, "admin", null, null)));
        Assert.False(result.IsSuccess);
        Assert.Empty(context.DnsManagementSettings);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
}
