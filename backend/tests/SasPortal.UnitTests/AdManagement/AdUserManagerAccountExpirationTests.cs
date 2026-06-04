using System.Text.Json;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdUserManagerAccountExpirationTests
{
    [Fact]
    public void UserManagerUpdate_RequestSummary_ContainsOperationUserAndManager()
    {
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var managerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var json = AdOperationLogSnapshotBuilder.BuildUserManagerUpdateRequestSummary(
            userId,
            managerUserId,
            clearManager: false);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(AdManagementOperationTypes.UserManagerUpdate, document.RootElement.GetProperty("operation").GetString());
        Assert.Equal(userId.ToString("D"), document.RootElement.GetProperty("userId").GetString());
        Assert.Equal(managerUserId.ToString("D"), document.RootElement.GetProperty("managerUserId").GetString());
        Assert.False(document.RootElement.GetProperty("clearManager").GetBoolean());
    }

    [Fact]
    public void UserManagerUpdate_ClearAfterSnapshot_ManagerIsNull()
    {
        var json = AdOperationLogSnapshotBuilder.BuildUserManagerUpdateAfterSnapshot(
            "11111111-1111-1111-1111-111111111111",
            "user1",
            "user1@domain.local",
            "CN=user1,OU=Users,DC=domain,DC=local",
            manager: null);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("manager").ValueKind);
    }

    [Fact]
    public void UserManagerUpdate_FailureDiagnostic_UsesAdUserManagerUpdateFailedCode()
    {
        var diagnosticJson = AdOperationErrorDiagnosticBuilder.BuildUserManagerUpdateFailureJson(
            "ModifyManager",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "CN=user1,OU=Users,DC=domain,DC=local");

        var extractedCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(diagnosticJson);
        Assert.Equal(AdOperationDiagnosticCodes.UserManagerUpdateFailed, extractedCode);

        using var document = JsonDocument.Parse(diagnosticJson);
        Assert.Equal(AdOperationDiagnosticCodes.UserManagerUpdateFailed, document.RootElement.GetProperty("code").GetString());
        Assert.Equal(AdManagementOperationTypes.UserManagerUpdate, document.RootElement.GetProperty("operation").GetString());
    }

    [Fact]
    public void UserAccountExpirationUpdate_RequestSummary_ContainsNeverExpiresAndDate()
    {
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var json = AdOperationLogSnapshotBuilder.BuildUserAccountExpirationUpdateRequestSummary(
            userId,
            neverExpires: false,
            expiresAt: "2026-12-31");

        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            AdManagementOperationTypes.UserAccountExpirationUpdate,
            document.RootElement.GetProperty("operation").GetString());
        Assert.Equal(userId.ToString("D"), document.RootElement.GetProperty("userId").GetString());
        Assert.False(document.RootElement.GetProperty("neverExpires").GetBoolean());
        Assert.Equal("2026-12-31", document.RootElement.GetProperty("expiresAt").GetString());
    }

    [Fact]
    public void UserAccountExpirationUpdate_FailureDiagnostic_UsesAdUserAccountExpirationUpdateFailedCode()
    {
        var diagnosticJson = AdOperationErrorDiagnosticBuilder.BuildUserAccountExpirationUpdateFailureJson(
            "ModifyAccountExpires",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "CN=user1,OU=Users,DC=domain,DC=local");

        var extractedCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(diagnosticJson);
        Assert.Equal(AdOperationDiagnosticCodes.UserAccountExpirationUpdateFailed, extractedCode);

        using var document = JsonDocument.Parse(diagnosticJson);
        Assert.Equal(
            AdOperationDiagnosticCodes.UserAccountExpirationUpdateFailed,
            document.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToAdFileTime_SetsExpectedValues(bool neverExpires)
    {
        if (neverExpires)
        {
            Assert.Equal(AdLdapValueConverter.NeverExpiresFileTime, AdLdapValueConverter.ToNeverExpiresFileTime());
            return;
        }

        var expiresAt = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var fileTime = AdLdapValueConverter.ToAdFileTime(expiresAt);
        var roundTrip = AdLdapValueConverter.FromAdFileTime(fileTime);
        Assert.NotNull(roundTrip);
        Assert.Equal(expiresAt.UtcDateTime.Date, roundTrip!.Value.UtcDateTime.Date);
    }

    [Fact]
    public void TryParseAccountExpirationDate_InvalidDate_ReturnsFalse()
    {
        var parsed = AdLdapValueConverter.TryParseAccountExpirationDate(
            "not-a-date",
            out _,
            out var errorMessage);

        Assert.False(parsed);
        Assert.NotNull(errorMessage);
    }

    [Fact]
    public void IsNeverExpiresFileTime_TreatsZeroAndMaxAsNever()
    {
        Assert.True(AdLdapValueConverter.IsNeverExpiresFileTime(0));
        Assert.True(AdLdapValueConverter.IsNeverExpiresFileTime(AdLdapValueConverter.NeverExpiresFileTime));
        Assert.False(AdLdapValueConverter.IsNeverExpiresFileTime(133038720000000000L));
    }

    [Fact]
    public void ResolveDefaultCode_UserManagerUpdate_ReturnsExpectedCode()
    {
        Assert.Equal(
            AdOperationDiagnosticCodes.UserManagerUpdateFailed,
            AdOperationErrorDiagnosticBuilder.ResolveDefaultCode(AdManagementOperationTypes.UserManagerUpdate));
    }
}
