using System.Text.Json;
using SasPortal.Application.Common.AdManagement;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdUserUpdateOperationDiagnosticBuilderTests
{
    [Fact]
    public void BuildJson_LdapFailure_IncludesRequiredFields()
    {
        var json = AdUserUpdateOperationDiagnosticBuilder.BuildJson(
            new AdUserUpdateFailureContext(
                "UpdateBasicAttribute",
                AttributeName: "department",
                LdapResultCode: 50,
                LdapDiagnosticMessage: "insufficient access rights",
                TargetObjectGuid: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(AdUserUpdateDiagnosticCodes.UpdateFailed, root.GetProperty("code").GetString());
        Assert.Equal("UserUpdate", root.GetProperty("operation").GetString());
        Assert.Equal("UpdateBasicAttribute", root.GetProperty("step").GetString());
        Assert.Equal("department", root.GetProperty("attribute").GetString());
        Assert.Equal(
            AdUserUpdateNormalizedReasons.InsufficientAccessRights,
            root.GetProperty("normalizedReason").GetString());
        Assert.Equal(50, root.GetProperty("ldapResultCode").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
    }

    [Fact]
    public void BuildJson_DuplicateSamAccountName_UsesAttributeSpecificMessage()
    {
        var json = AdUserUpdateOperationDiagnosticBuilder.BuildJson(
            new AdUserUpdateFailureContext(
                "UpdateBasicAttribute",
                AttributeName: "sAMAccountName",
                LdapResultCode: 68,
                LdapDiagnosticMessage: "00002071: entry already exists"));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(AdUserUpdateNormalizedReasons.DuplicateValue, root.GetProperty("normalizedReason").GetString());
        Assert.Equal(
            "The sAMAccountName value is already used by another AD object.",
            root.GetProperty("message").GetString());
    }

    [Fact]
    public void BuildJson_DuplicateUserPrincipalName_UsesAttributeSpecificMessage()
    {
        var json = AdUserUpdateOperationDiagnosticBuilder.BuildJson(
            new AdUserUpdateFailureContext(
                "UpdateBasicAttribute",
                AttributeName: "userPrincipalName",
                LdapResultCode: 68,
                LdapDiagnosticMessage: "attributeOrValueExists"));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(AdUserUpdateNormalizedReasons.DuplicateValue, root.GetProperty("normalizedReason").GetString());
        Assert.Equal(
            "The userPrincipalName value is already used by another AD object.",
            root.GetProperty("message").GetString());
    }

    [Fact]
    public void BuildJson_DuplicateWithoutAttribute_UsesGenericDuplicateMessage()
    {
        var json = AdUserUpdateOperationDiagnosticBuilder.BuildJson(
            new AdUserUpdateFailureContext(
                "RenameCn",
                LdapResultCode: 68,
                LdapDiagnosticMessage: "entry already exists"));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(
            "The CN, sAMAccountName, or userPrincipalName value is already used by another AD object.",
            root.GetProperty("message").GetString());
    }

    [Fact]
    public void BuildJson_SanitizesSensitiveDiagnosticContent()
    {
        var json = AdUserUpdateOperationDiagnosticBuilder.BuildJson(
            new AdUserUpdateFailureContext(
                "UpdateBasicAttribute",
                AttributeName: "mail",
                LdapResultCode: 1,
                LdapDiagnosticMessage: "bind failed: password is invalid for service account"));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("[redacted]", root.GetProperty("ldapDiagnosticMessage").GetString());
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildJson_TruncatesLongDiagnosticMessage()
    {
        var longDiagnostic = new string('x', 600);
        var json = AdUserUpdateOperationDiagnosticBuilder.BuildJson(
            new AdUserUpdateFailureContext(
                "UpdateBasicAttribute",
                LdapDiagnosticMessage: longDiagnostic));

        using var document = JsonDocument.Parse(json);
        var diagnostic = document.RootElement.GetProperty("ldapDiagnosticMessage").GetString();

        Assert.NotNull(diagnostic);
        Assert.True(diagnostic.Length <= 501);
        Assert.EndsWith("…", diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildNotFoundJson_UsesNoSuchObjectReason()
    {
        var json = AdUserUpdateOperationDiagnosticBuilder.BuildNotFoundJson("LoadUser");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(AdUserUpdateNormalizedReasons.NoSuchObject, root.GetProperty("normalizedReason").GetString());
        Assert.Equal("The AD user could not be found.", root.GetProperty("message").GetString());
    }
}
