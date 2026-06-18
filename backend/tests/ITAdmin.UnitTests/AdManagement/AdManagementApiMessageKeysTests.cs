using System.Text.RegularExpressions;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;

namespace ITAdmin.UnitTests.AdManagement;

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
            "backend/src/ITAdmin.Application/Common/AdManagement/AdManagementApiMessages.cs");

        Assert.False(File.Exists(legacyPath));
    }

    [Fact]
    public void AdUserDirectoryServicePartials_DoNotReferenceLegacyMessages()
    {
        var servicesDir = Path.Combine(FindRepositoryRoot(), "backend/src/ITAdmin.Infrastructure/Services");
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
        var servicesDir = Path.Combine(FindRepositoryRoot(), "backend/src/ITAdmin.Infrastructure/Services");
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
            "backend/src/ITAdmin.Api/Contracts/AdManagement");

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
            "backend/src/ITAdmin.Application/Common/Models");

        foreach (var file in Directory.GetFiles(modelsDir, "Ad*.cs"))
        {
            var source = File.ReadAllText(file);
            Assert.False(
                Regex.IsMatch(source, @"\bstring Message\b", RegexOptions.Multiline),
                $"Model {Path.GetFileName(file)} exposes user-facing Message property.");
        }
    }

    [Fact]
    public void AdManagementValidatorFiles_DoNotContainUserFacingTurkishLiterals()
    {
        var adManagementDir = Path.Combine(
            FindRepositoryRoot(),
            "backend/src/ITAdmin.Application/Common/AdManagement");

        foreach (var file in Directory.GetFiles(adManagementDir, "*.cs"))
        {
            var source = File.ReadAllText(file);
            foreach (var pattern in TurkishUserFacingPatterns)
            {
                Assert.False(
                    source.Contains(pattern, StringComparison.OrdinalIgnoreCase),
                    $"Validator file {Path.GetFileName(file)} contains Turkish user-facing literal '{pattern}'.");
            }
        }
    }

    [Fact]
    public void AdManagementSettingsService_DoesNotAssignRawSentenceMessageKeys()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Persistence/Services/AdManagementSettingsService.cs"));

        Assert.DoesNotContain("MissingRequiredFieldsMessage", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementApiMessageKeys.Settings.UpdateSucceeded", source, StringComparison.Ordinal);

        var rawMessageKeyMatch = Regex.Match(
            source,
            @"MessageKey\s*:\s*""(?!apiMessages\.)[^""]+""",
            RegexOptions.Multiline);
        Assert.False(
            rawMessageKeyMatch.Success,
            $"MessageKey assigned a raw sentence instead of apiMessages.* key: {rawMessageKeyMatch.Value}");
    }

    [Fact]
    public void CreateUserSuccess_UsesStableCreateSuccessMessageKey()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/ITAdmin.Infrastructure/Services/AdUserDirectoryService.Create.cs"));

        Assert.Contains("AdManagementApiMessageKeys.Users.CreateSuccess", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Kullanıcı oluşturuldu:", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AllDefinedApiMessageKeys_UseApiMessagesPrefix()
    {
        var keysType = typeof(AdManagementApiMessageKeys);
        foreach (var nested in keysType.GetNestedTypes())
        {
            foreach (var field in nested.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (field.FieldType != typeof(string))
                {
                    continue;
                }

                var value = (string?)field.GetRawConstantValue();
                Assert.False(string.IsNullOrWhiteSpace(value));
                Assert.StartsWith("apiMessages.", value, StringComparison.Ordinal);
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
