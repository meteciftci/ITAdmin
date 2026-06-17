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
        Assert.Equal(expectedKey, AdLdapErrorNormalizer.NormalizeMessageKey(ldapCode, null));
    }

    [Fact]
    public void GroupsAndComputers_InvalidTargetOuKeys_AreDefined()
    {
        Assert.Equal("apiMessages.groups.invalidTargetOu", AdManagementApiMessageKeys.Groups.InvalidTargetOu);
        Assert.Equal("apiMessages.computers.invalidTargetOu", AdManagementApiMessageKeys.Computers.InvalidTargetOu);
    }

    [Fact]
    public void AdManagementApiMessages_LegacyFileDoesNotExist()
    {
        var legacyPath = Path.Combine(
            FindRepositoryRoot(),
            "backend/src/SasPortal.Application/Common/AdManagement/AdManagementApiMessages.cs");

        Assert.False(File.Exists(legacyPath));
    }

    [Fact]
    public void AdUserDirectoryServicePartials_DoNotReferenceLegacyMessages()
    {
        var servicesDir = Path.Combine(FindRepositoryRoot(), "backend/src/SasPortal.Infrastructure/Services");
        var partialFiles = Directory.GetFiles(servicesDir, "AdUserDirectoryService*.cs");

        foreach (var file in partialFiles)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("AdManagementApiMessages.Legacy", source, StringComparison.Ordinal);
        }
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

    [Fact]
    public void AdManagementContracts_DoNotExposeUserFacingMessageProperty()
    {
        var contractsDir = Path.Combine(
            FindRepositoryRoot(),
            "backend/src/SasPortal.Api/Contracts/AdManagement");

        foreach (var file in Directory.GetFiles(contractsDir, "*.cs"))
        {
            var source = File.ReadAllText(file);
            Assert.False(
                Regex.IsMatch(source, @"\bstring Message\b", RegexOptions.Multiline),
                $"Contract {Path.GetFileName(file)} exposes user-facing Message property.");
        }
    }

    [Fact]
    public void AdManagementResultModels_DoNotExposeUserFacingMessageProperty()
    {
        var modelsDir = Path.Combine(
            FindRepositoryRoot(),
            "backend/src/SasPortal.Application/Common/Models");

        foreach (var file in Directory.GetFiles(modelsDir, "Ad*.cs"))
        {
            var source = File.ReadAllText(file);
            Assert.False(
                Regex.IsMatch(source, @"\bstring Message\b", RegexOptions.Multiline),
                $"Model {Path.GetFileName(file)} exposes user-facing Message property.");
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
