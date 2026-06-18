using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdGroupDeleteOperationDiagnosticBuilder
{
    private const string OperationName = "GroupDelete";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string BuildJson(AdGroupDeleteFailureContext context)
    {
        var normalizedReason = context.NormalizedReasonOverride
            ?? ResolveNormalizedReason(
                context.LdapResultCode,
                context.LdapDiagnosticMessage);

        var message = context.EnglishMessageOverride
            ?? ResolveEnglishMessage(normalizedReason);

        var code = context.DiagnosticCode ?? AdGroupDeleteDiagnosticCodes.DeleteFailed;

        var payload = new
        {
            code,
            operation = OperationName,
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

    public static string BuildNotFoundJson(string step, Guid targetObjectGuid) =>
        BuildJson(
            new AdGroupDeleteFailureContext(
                step,
                TargetObjectGuid: targetObjectGuid,
                NormalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject,
                EnglishMessageOverride: "The AD security group could not be found."));

    public static string BuildPreflightJson(
        string step,
        string normalizedReason,
        string englishMessage,
        Guid targetObjectGuid,
        string? targetDistinguishedName = null) =>
        BuildJson(
            new AdGroupDeleteFailureContext(
                step,
                TargetObjectGuid: targetObjectGuid,
                TargetDistinguishedName: targetDistinguishedName,
                DiagnosticCode: AdGroupDeleteDiagnosticCodes.PreflightFailed,
                NormalizedReasonOverride: normalizedReason,
                EnglishMessageOverride: englishMessage));

    public static string BuildGenericFailureJson(
        string step,
        string normalizedReason,
        string englishMessage,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null) =>
        BuildJson(
            new AdGroupDeleteFailureContext(
                step,
                TargetObjectGuid: targetObjectGuid,
                TargetDistinguishedName: targetDistinguishedName,
                NormalizedReasonOverride: normalizedReason,
                EnglishMessageOverride: englishMessage,
                LdapResultCode: ldapResultCode,
                LdapExceptionErrorCode: ldapExceptionErrorCode,
                LdapDiagnosticMessage: ldapDiagnosticMessage));

    private static string ResolveNormalizedReason(int? ldapResultCode, string? diagnosticMessage)
    {
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

        if (ldapResultCode is 81 or 85 or 91 or 52 or 51)
        {
            return AdUserUpdateNormalizedReasons.ConnectionFailed;
        }

        if (!string.IsNullOrWhiteSpace(diagnosticMessage)
            && diagnosticMessage.Contains("SSL", StringComparison.OrdinalIgnoreCase))
        {
            return AdUserUpdateNormalizedReasons.LdapsRequired;
        }

        return AdUserUpdateNormalizedReasons.Unknown;
    }

    private static string ResolveEnglishMessage(string normalizedReason) =>
        normalizedReason switch
        {
            AdUserUpdateNormalizedReasons.NoSuchObject => "The AD security group could not be found.",
            AdUserUpdateNormalizedReasons.InvalidDnSyntax => "The group distinguished name is invalid.",
            AdUserUpdateNormalizedReasons.InsufficientAccessRights =>
                "The AD service account does not have permission to delete this group.",
            AdUserUpdateNormalizedReasons.ConnectionFailed => "The AD connection failed.",
            AdUserUpdateNormalizedReasons.LdapsRequired =>
                "LDAPS is required for AD write operations.",
            _ => "The AD security group could not be deleted.",
        };
}

public sealed record AdGroupDeleteFailureContext(
    string Step,
    Guid? TargetObjectGuid = null,
    string? TargetDistinguishedName = null,
    string? DiagnosticCode = null,
    string? NormalizedReasonOverride = null,
    string? EnglishMessageOverride = null,
    int? LdapResultCode = null,
    int? LdapExceptionErrorCode = null,
    string? LdapDiagnosticMessage = null);
