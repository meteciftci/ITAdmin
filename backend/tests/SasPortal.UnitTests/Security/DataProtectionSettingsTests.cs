using Microsoft.Extensions.Configuration;
using SasPortal.Infrastructure.Security;

namespace SasPortal.UnitTests.Security;

public sealed class DataProtectionSettingsTests
{
    [Fact]
    public void Load_with_empty_configuration_uses_defaults_without_persistence()
    {
        var settings = DataProtectionSettings.Load(new ConfigurationBuilder().Build());

        Assert.Equal(DataProtectionSettings.DefaultApplicationName, settings.ApplicationName);
        Assert.False(settings.PersistKeysToFileSystem);
        Assert.False(settings.ProtectKeysWithCertificate);
    }

    [Fact]
    public void Load_reads_configured_values_and_trims_them()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:ApplicationName"] = " SAS Portal ",
                ["DataProtection:KeysPath"] = " C:\\SASPortal\\dataprotection-keys ",
                ["DataProtection:CertificateThumbprint"] = " abc123 ",
            })
            .Build();

        var settings = DataProtectionSettings.Load(configuration);

        Assert.Equal("SAS Portal", settings.ApplicationName);
        Assert.Equal("C:\\SASPortal\\dataprotection-keys", settings.KeysPath);
        Assert.Equal("abc123", settings.CertificateThumbprint);
        Assert.True(settings.PersistKeysToFileSystem);
        Assert.True(settings.ProtectKeysWithCertificate);
    }

    [Fact]
    public void Load_treats_whitespace_values_as_missing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:ApplicationName"] = "  ",
                ["DataProtection:KeysPath"] = "",
                ["DataProtection:CertificateThumbprint"] = "   ",
            })
            .Build();

        var settings = DataProtectionSettings.Load(configuration);

        Assert.Equal(DataProtectionSettings.DefaultApplicationName, settings.ApplicationName);
        Assert.False(settings.PersistKeysToFileSystem);
        Assert.False(settings.ProtectKeysWithCertificate);
    }

    [Fact]
    public void Missing_certificate_produces_clear_error_message()
    {
        // No certificate with this thumbprint exists on the build machine, so the loader
        // must fail fast with the explicit configuration-oriented message.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DataProtectionCertificateLoader.LoadByThumbprint("0000000000000000000000000000000000000000"));

        Assert.Contains("DataProtection:CertificateThumbprint", exception.Message);
        Assert.DoesNotContain("0000000000000000000000000000000000000000", exception.Message);
    }
}
