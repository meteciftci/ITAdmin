using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdGroupUpdateOperationDiagnosticBuilder
{
    private const string OperationName = "GroupUpdate";
    private const string PreflightStep = "Preflight";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string BuildJson(AdGroupUpdateFailureContext context)
    {
        var normalizedReason = context.NormalizedReasonOverride
            ?? ResolveNormalizedReason(
                context.LdapResultCode,
                context.LdapDiagnosticMessage,
                context.AttributeName);

        var message = context.EnglishMessageOverride
            ?? ResolveEnglishMessage(normalizedReason, context.AttributeName);

        var code = context.DiagnosticCode ?? AdGroupUpdateDiagnosticCodes.UpdateFailed;

        var payload = new
        {
            code,
            operation = OperationName,
            step = context.Step,
            attribute = string.IsNullOrWhiteSpace(context.AttributeName) ? null : context.AttributeName,
            normalizedReason,
            ldapResultCode = context.LdapResultCode,
            ldapExceptionErrorCode = context.LdapExceptionErrorCode,
            message,
            ldapDiagnosticMessage = AdLdapDiagnosticSanitizer.SanitizeLdapDiagnosticMessage(
                context.LdapDiagnosticMessage),
            targetObjectGuid = context.TargetObjectGuid?.ToString("D"),
            targetDistinguishedName = AdLdapDiagnosticSanitizer.SanitizeDistinguishedName(
                context.TargetDistinguishedName),
            partialUpdate = context.RollbackStatus is not null ? context.PartialUpdate : null,
            rollbackStatus = context.RollbackStatus,
            appliedChanges = context.AppliedChanges?.Count > 0 ? context.AppliedChanges : null,
            rolledBackChanges = context.RolledBackChanges?.Count > 0 ? context.RolledBackChanges : null,
            rollbackErrors = context.RollbackErrors?.Count > 0
                ? context.RollbackErrors
                    .Select(static error => new { attribute = error.Attribute, message = error.Message })
                    .ToList()
                : null,
            afterReloadFailed = context.AfterReloadFailed == true ? (bool?)true : null,
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    public static string BuildPreflightDuplicateJson(
        string attributeName,
        string englishMessage,
        Guid targetObjectGuid) =>
        BuildJson(
            new AdGroupUpdateFailureContext(
                PreflightStep,
                AttributeName: attributeName,
                TargetObjectGuid: targetObjectGuid,
                DiagnosticCode: AdGroupUpdateDiagnosticCodes.PreflightFailed,
                NormalizedReasonOverride: AdUserUpdateNormalizedReasons.DuplicateValue,
                EnglishMessageOverride: englishMessage));

    public static string BuildNotFoundJson(string step, Guid targetObjectGuid) =>
        BuildJson(
            new AdGroupUpdateFailureContext(
                step,
                TargetObjectGuid: targetObjectGuid,
                NormalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject,
                EnglishMessageOverride: "The AD security group could not be found."));

    public static string BuildGenericFailureJson(
        string step,
        string normalizedReason,
        string englishMessage,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        bool afterReloadFailed = false) =>
        BuildJson(
            new AdGroupUpdateFailureContext(
                step,
                TargetObjectGuid: targetObjectGuid,
                TargetDistinguishedName: targetDistinguishedName,
                NormalizedReasonOverride: normalizedReason,
                EnglishMessageOverride: englishMessage,
                AfterReloadFailed: afterReloadFailed));

    private static string ResolveNormalizedReason(
        int? ldapResultCode,
        string? diagnosticMessage,
        string? attributeName)
    {
        if (ldapResultCode is 68 or 20
            || (!string.IsNullOrWhiteSpace(diagnosticMessage)
                && diagnosticMessage.Contains("already exists", StringComparison.OrdinalIgnoreCase)))
        {
            return AdUserUpdateNormalizedReasons.DuplicateValue;
        }

        if (ldapResultCode is 34 or 64)
        {
            return AdUserUpdateNormalizedReasons.InvalidDnSyntax;
        }

        if (ldapResultCode is 32)
        {
            return AdUserUpdateNormalizedReasons.NoSuchObject;
        }

        if (ldapResultCode is 50)
        {
            return AdUserUpdateNormalizedReasons.InsufficientAccessRights;
        }

        return string.IsNullOrWhiteSpace(attributeName)
            ? AdUserUpdateNormalizedReasons.Unknown
            : AdUserUpdateNormalizedReasons.Unknown;
    }

    private static string ResolveEnglishMessage(string normalizedReason, string? attributeName) =>
        normalizedReason switch
        {
            AdUserUpdateNormalizedReasons.DuplicateValue when string.Equals(
                attributeName,
                "sAMAccountName",
                StringComparison.OrdinalIgnoreCase) =>
                "The sAMAccountName value is already used by another AD group.",
            AdUserUpdateNormalizedReasons.DuplicateValue when string.Equals(
                attributeName,
                "cn",
                StringComparison.OrdinalIgnoreCase) =>
                "A group with the same technical name already exists in the target OU.",
            AdUserUpdateNormalizedReasons.InvalidDnSyntax =>
                "The technical name or distinguished name is not valid for Active Directory.",
            AdUserUpdateNormalizedReasons.NoSuchObject => "The AD security group could not be found.",
            AdUserUpdateNormalizedReasons.ConnectionFailed =>
                "The AD security group could not be updated because the directory connection failed.",
            _ => "The AD security group update failed.",
        };
}
