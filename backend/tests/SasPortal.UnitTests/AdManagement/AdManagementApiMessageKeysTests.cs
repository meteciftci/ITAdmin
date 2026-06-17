using System.Text.RegularExpressions;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdManagementApiMessageKeysTests
{
    private static readonly string[] TurkishUserFacingPatterns =
    [
        "bulunamadı",
        "oluşturulamadı",
        "güncellenemedi",
        "başarısız oldu",
        "etkin değil",
        "zorunludur",
    ];

    [Theory]
    [InlineData(68, AdManagementApiMessageKeys.Ldap.EntryAlreadyExists)]
    [InlineData(50, AdManagementApiMessageKeys.Ldap.InsufficientAccessRights)]
    [InlineData(32, AdManagementApiMessageKeys.Ldap.NoSuchObject)]
    public void LdapNormalizer_ReturnsExpectedMessageKey(int ldapCode, string expectedKey)
    {
        var key = AdLdapErrorNormalizer.NormalizeMessageKey(ldapCode, null);
        Assert.Equal(expectedKey, key);
        Assert.Equal(AdManagementApiMessages.Legacy(expectedKey), AdLdapErrorNormalizer.Normalize(ldapCode, null));
    }

    [Fact]
    public void ValidationMessages_ExposeMessageKeys()
    {
        var key = AdManagementApiMessageKeys.SettingsValidation.MissingRequiredSettings;
        Assert.Equal(key, key);
        Assert.Contains("zorunlu", AdManagementApiMessages.Legacy(key), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateUserFailureKey_MapsToLegacyTurkishMessage()
    {
        var key = AdManagementApiMessageKeys.Users.CreateFailed;
        Assert.StartsWith("apiMessages.", key, StringComparison.Ordinal);
        Assert.Contains("oluşturulamadı", AdManagementApiMessages.Legacy(key), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OuMoveKeys_MapToLegacyTurkishMessages()
    {
        Assert.Contains(
            "taşındı",
            AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Users.OuMoveSuccess),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "başarısız",
            AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Users.OuMoveFailed),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeletedObjectRestoreKeys_MapToLegacyTurkishMessages()
    {
        Assert.Contains(
            "geri yüklendi",
            AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.DeletedObjects.RestoreSuccess),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "geri yüklenemedi",
            AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.DeletedObjects.RestoreFailed),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreflightDuplicateKeys_MapToLegacyTurkishMessages()
    {
        Assert.Contains(
            "kullanıcı adı",
            AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.OperationFailures.PreflightSamAccountNameDuplicate),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "UPN",
            AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.OperationFailures.PreflightUpnDuplicate),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AdUserDirectoryServicePartials_DoNotContainUserFacingTurkishConstMessages()
    {
        var servicesDir = Path.Combine(FindRepositoryRoot(), "backend/src/SasPortal.Infrastructure/Services");
        var partialFiles = Directory.GetFiles(servicesDir, "AdUserDirectoryService*.cs");

        foreach (var file in partialFiles)
        {
            var source = File.ReadAllText(file);
            var constMatches = Regex.Matches(
                source,
                @"private const string \w+Message\s*=\s*""([^""]+)"";",
                RegexOptions.Multiline);

            foreach (Match match in constMatches)
            {
                var constName = match.Value;
                if (constName.Contains("LoggingFailedMessage", StringComparison.Ordinal))
                {
                    continue;
                }

                var value = match.Groups[1].Value;
                foreach (var pattern in TurkishUserFacingPatterns)
                {
                    Assert.False(
                        value.Contains(pattern, StringComparison.OrdinalIgnoreCase),
                        $"File {Path.GetFileName(file)} contains user-facing Turkish const {constName}");
                }
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "backend", "src")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
