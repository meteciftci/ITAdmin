using System.Text.Json;
using System.Text.Json.Serialization;
using ITAdmin.Application.Common.Constants;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdGroupMemberOperationDiagnosticBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string BuildJson(AdGroupMemberOperationFailureContext context)
    {
        var normalizedReason = context.NormalizedReasonOverride
            ?? AdUserUpdateOperationDiagnosticBuilder.ResolveNormalizedReason(
                context.LdapResultCode,
                context.LdapExceptionErrorCode,
                context.LdapDiagnosticMessage,
                attributeName: null);

        var message = context.EnglishMessageOverride
            ?? ResolveEnglishMessage(context.OperationType, normalizedReason);

        var code = context.DiagnosticCode
            ?? ResolveDefaultCode(context.OperationType, context.IsPreflight)
            ?? AdOperationDiagnosticCodes.GroupMemberAddFailed;

        var payload = new
        {
            code,
            operation = context.OperationType,
            step = context.Step,
            normalizedReason,
            ldapResultCode = context.LdapResultCode,
            ldapExceptionErrorCode = context.LdapExceptionErrorCode,
            message,
            ldapDiagnosticMessage = AdLdapDiagnosticSanitizer.SanitizeLdapDiagnosticMessage(
                context.LdapDiagnosticMessage),
            targetObjectGuid = context.TargetObjectGuid?.ToString("D"),
            targetDistinguishedName = AdLdapDiagnosticSanitizer.SanitizeDistinguishedName(
                context.TargetDistinguishedName),
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    public static string BuildNotFoundJson(string operationType, string step, Guid targetObjectGuid) =>
        BuildJson(
            new AdGroupMemberOperationFailureContext(
                operationType,
                step,
                TargetObjectGuid: targetObjectGuid,
                NormalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject,
                EnglishMessageOverride: "The AD security group could not be found."));

    public static string BuildPreflightJson(
        string operationType,
        string step,
        string normalizedReason,
        string englishMessage,
        Guid targetObjectGuid,
        string? targetDistinguishedName = null) =>
        BuildJson(
            new AdGroupMemberOperationFailureContext(
                operationType,
                step,
                TargetObjectGuid: targetObjectGuid,
                TargetDistinguishedName: targetDistinguishedName,
                IsPreflight: true,
                NormalizedReasonOverride: normalizedReason,
                EnglishMessageOverride: englishMessage));

    public static string BuildGenericFailureJson(
        string operationType,
        string step,
        string normalizedReason,
        string englishMessage,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null) =>
        BuildJson(
            new AdGroupMemberOperationFailureContext(
                operationType,
                step,
                TargetObjectGuid: targetObjectGuid,
                TargetDistinguishedName: targetDistinguishedName,
                NormalizedReasonOverride: normalizedReason,
                EnglishMessageOverride: englishMessage,
                LdapResultCode: ldapResultCode,
                LdapExceptionErrorCode: ldapExceptionErrorCode,
                LdapDiagnosticMessage: ldapDiagnosticMessage));

    private static string? ResolveDefaultCode(string operationType, bool isPreflight) =>
        operationType switch
        {
            AdManagementOperationTypes.GroupMemberAdd => isPreflight
                ? AdOperationDiagnosticCodes.GroupMemberAddPreflightFailed
                : AdOperationDiagnosticCodes.GroupMemberAddFailed,
            AdManagementOperationTypes.GroupMemberRemove => isPreflight
                ? AdOperationDiagnosticCodes.GroupMemberRemovePreflightFailed
                : AdOperationDiagnosticCodes.GroupMemberRemoveFailed,
            _ => null,
        };

    private static string ResolveEnglishMessage(string operationType, string normalizedReason) =>
        normalizedReason switch
        {
            AdUserUpdateNormalizedReasons.NoSuchObject =>
                operationType switch
                {
                    AdManagementOperationTypes.GroupMemberAdd =>
                        "The AD security group or member object could not be found.",
                    AdManagementOperationTypes.GroupMemberRemove =>
                        "The AD security group or member object could not be found.",
                    _ => "The requested AD object could not be found.",
                },
            AdUserUpdateNormalizedReasons.AlreadyMember =>
                "The member is already a direct member of this group.",
            AdUserUpdateNormalizedReasons.NotDirectMember =>
                "The member is not a direct member of this group.",
            AdUserUpdateNormalizedReasons.InvalidRequest =>
                "The AD group membership request is invalid.",
            AdUserUpdateNormalizedReasons.LdapsRequired =>
                "LDAPS is required for AD write operations.",
            AdUserUpdateNormalizedReasons.InsufficientAccessRights =>
                "The AD service account does not have permission to modify this group membership.",
            AdUserUpdateNormalizedReasons.ConnectionFailed =>
                "The LDAP connection failed.",
            _ =>
                operationType switch
                {
                    AdManagementOperationTypes.GroupMemberAdd =>
                        "The AD security group member add operation failed.",
                    AdManagementOperationTypes.GroupMemberRemove =>
                        "The AD security group member remove operation failed.",
                    _ => "The AD operation failed.",
                },
        };
}

public sealed record AdGroupMemberOperationFailureContext(
    string OperationType,
    string Step,
    string? DiagnosticCode = null,
    string? NormalizedReasonOverride = null,
    string? EnglishMessageOverride = null,
    int? LdapResultCode = null,
    int? LdapExceptionErrorCode = null,
    string? LdapDiagnosticMessage = null,
    Guid? TargetObjectGuid = null,
    string? TargetDistinguishedName = null,
    bool IsPreflight = false);
