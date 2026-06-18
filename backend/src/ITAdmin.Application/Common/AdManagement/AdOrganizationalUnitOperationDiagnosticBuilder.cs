using System.Text.Json;
using System.Text.Json.Serialization;
using ITAdmin.Application.Common.Constants;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdOrganizationalUnitOperationDiagnosticBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string BuildCreateFailureJson(
        string step,
        string normalizedReason,
        string englishMessage,
        string? distinguishedName = null,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null) =>
        BuildFailureJson(
            AdOperationDiagnosticCodes.OrganizationalUnitCreateFailed,
            AdManagementOperationTypes.OrganizationalUnitCreate,
            step,
            normalizedReason,
            englishMessage,
            distinguishedName,
            ldapResultCode,
            ldapExceptionErrorCode,
            ldapDiagnosticMessage);

    public static string BuildRenameFailureJson(
        string step,
        Guid organizationalUnitId,
        string? distinguishedName,
        string normalizedReason,
        string englishMessage,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null) =>
        JsonSerializer.Serialize(
            new
            {
                code = AdOperationDiagnosticCodes.OrganizationalUnitRenameFailed,
                operation = AdManagementOperationTypes.OrganizationalUnitRename,
                step,
                normalizedReason,
                englishMessage,
                organizationalUnitId = organizationalUnitId.ToString("D"),
                distinguishedName,
                ldapResultCode,
                ldapExceptionErrorCode,
                ldapDiagnosticMessage,
            },
            SerializerOptions);

    public static string BuildMoveFailureJson(
        string step,
        Guid organizationalUnitId,
        string? distinguishedName,
        string normalizedReason,
        string englishMessage,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null) =>
        JsonSerializer.Serialize(
            new
            {
                code = AdOperationDiagnosticCodes.OrganizationalUnitMoveFailed,
                operation = AdManagementOperationTypes.OrganizationalUnitMove,
                step,
                normalizedReason,
                englishMessage,
                organizationalUnitId = organizationalUnitId.ToString("D"),
                distinguishedName,
                ldapResultCode,
                ldapExceptionErrorCode,
                ldapDiagnosticMessage,
            },
            SerializerOptions);

    public static string BuildDeleteFailureJson(
        string step,
        Guid organizationalUnitId,
        string? distinguishedName,
        string normalizedReason,
        string englishMessage,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null) =>
        JsonSerializer.Serialize(
            new
            {
                code = AdOperationDiagnosticCodes.OrganizationalUnitDeleteFailed,
                operation = AdManagementOperationTypes.OrganizationalUnitDelete,
                step,
                normalizedReason,
                englishMessage,
                organizationalUnitId = organizationalUnitId.ToString("D"),
                distinguishedName,
                ldapResultCode,
                ldapExceptionErrorCode,
                ldapDiagnosticMessage,
            },
            SerializerOptions);

    private static string BuildFailureJson(
        string code,
        string operation,
        string step,
        string normalizedReason,
        string englishMessage,
        string? distinguishedName,
        int? ldapResultCode,
        int? ldapExceptionErrorCode,
        string? ldapDiagnosticMessage) =>
        JsonSerializer.Serialize(
            new
            {
                code,
                operation,
                step,
                normalizedReason,
                englishMessage,
                distinguishedName,
                ldapResultCode,
                ldapExceptionErrorCode,
                ldapDiagnosticMessage,
            },
            SerializerOptions);
}
