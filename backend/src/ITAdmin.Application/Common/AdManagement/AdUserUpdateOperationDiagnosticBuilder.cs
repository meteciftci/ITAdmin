using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdUserUpdateOperationDiagnosticBuilder
{
    private const string OperationName = "UserUpdate";
    private const string PreflightStep = "Preflight";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string BuildJson(AdUserUpdateFailureContext context)
    {
        var normalizedReason = context.NormalizedReasonOverride
            ?? ResolveNormalizedReason(
                context.LdapResultCode,
                context.LdapExceptionErrorCode,
                context.LdapDiagnosticMessage,
                context.AttributeName);

        var message = context.EnglishMessageOverride
            ?? ResolveEnglishMessage(
                normalizedReason,
                context.AttributeName,
                context.LdapResultCode,
                context.LdapExceptionErrorCode,
                context.LdapDiagnosticMessage);

        var code = context.DiagnosticCode ?? ResolveDefaultCode(context);

        var payload = new AdUserUpdateOperationDiagnosticPayload
        {
            Code = code,
            Operation = OperationName,
            Step = context.Step,
            Attribute = string.IsNullOrWhiteSpace(context.AttributeName) ? null : context.AttributeName,
            NormalizedReason = normalizedReason,
            LdapResultCode = context.LdapResultCode,
            LdapExceptionErrorCode = context.LdapExceptionErrorCode,
            Message = message,
            LdapDiagnosticMessage = AdLdapDiagnosticSanitizer.SanitizeLdapDiagnosticMessage(
                context.LdapDiagnosticMessage),
            TargetObjectGuid = context.TargetObjectGuid?.ToString("D"),
            TargetDistinguishedName = AdLdapDiagnosticSanitizer.SanitizeDistinguishedName(
                context.TargetDistinguishedName),
            PartialUpdate = context.RollbackStatus is not null ? context.PartialUpdate : null,
            RollbackStatus = context.RollbackStatus,
            AppliedChanges = context.AppliedChanges?.Count > 0 ? context.AppliedChanges : null,
            RolledBackChanges = context.RolledBackChanges?.Count > 0 ? context.RolledBackChanges : null,
            RollbackErrors = context.RollbackErrors?.Count > 0
                ? context.RollbackErrors
                    .Select(static error => new AdUserUpdateOperationDiagnosticRollbackError
                    {
                        Attribute = error.Attribute,
                        Message = error.Message,
                    })
                    .ToList()
                : null,
            AfterReloadFailed = context.AfterReloadFailed == true ? true : null,
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    public static string BuildPreflightDuplicateJson(
        string attributeName,
        string englishMessage,
        Guid targetObjectGuid) =>
        BuildJson(
            new AdUserUpdateFailureContext(
                PreflightStep,
                AttributeName: attributeName,
                TargetObjectGuid: targetObjectGuid,
                DiagnosticCode: AdUserUpdateDiagnosticCodes.PreflightFailed,
                NormalizedReasonOverride: AdUserUpdateNormalizedReasons.DuplicateValue,
                EnglishMessageOverride: englishMessage,
                PartialUpdate: false,
                RollbackStatus: AdUserUpdateRollbackStatus.NotRequired));

    public static string BuildWithRollback(
        AdUserUpdateFailureContext failureContext,
        AdUserUpdateRollbackResult rollbackResult,
        IReadOnlyList<string> appliedChangeNames)
    {
        var partialUpdate = rollbackResult.Status is AdUserUpdateRollbackStatus.Failed
            or AdUserUpdateRollbackStatus.PartiallySucceeded;

        var code = rollbackResult.Status switch
        {
            AdUserUpdateRollbackStatus.Succeeded => AdUserUpdateDiagnosticCodes.UpdateFailedRollbackSucceeded,
            AdUserUpdateRollbackStatus.Failed or AdUserUpdateRollbackStatus.PartiallySucceeded =>
                AdUserUpdateDiagnosticCodes.UpdateFailedRollbackFailed,
            _ => failureContext.DiagnosticCode ?? AdUserUpdateDiagnosticCodes.UpdateFailed,
        };

        return BuildJson(
            failureContext with
            {
                DiagnosticCode = code,
                PartialUpdate = partialUpdate,
                RollbackStatus = rollbackResult.Status,
                AppliedChanges = appliedChangeNames,
                RolledBackChanges = rollbackResult.RolledBackChanges,
                RollbackErrors = rollbackResult.Errors,
            });
    }

    public static string BuildNotFoundJson(string step, Guid? targetObjectGuid = null) =>
        BuildJson(
            new AdUserUpdateFailureContext(
                step,
                TargetObjectGuid: targetObjectGuid,
                NormalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject,
                EnglishMessageOverride: "The AD user could not be found.",
                RollbackStatus: AdUserUpdateRollbackStatus.NotRequired));

    public static string BuildValidationJson(string step, string englishMessage) =>
        BuildJson(
            new AdUserUpdateFailureContext(
                step,
                DiagnosticCode: AdUserUpdateDiagnosticCodes.ValidationFailed,
                NormalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest,
                EnglishMessageOverride: englishMessage,
                RollbackStatus: AdUserUpdateRollbackStatus.NotRequired));

    public static string BuildGenericFailureJson(
        string step,
        string normalizedReason,
        string englishMessage,
        Guid? targetObjectGuid = null,
        string? targetDistinguishedName = null,
        bool? afterReloadFailed = null) =>
        BuildJson(
            new AdUserUpdateFailureContext(
                step,
                TargetObjectGuid: targetObjectGuid,
                TargetDistinguishedName: targetDistinguishedName,
                NormalizedReasonOverride: normalizedReason,
                EnglishMessageOverride: englishMessage,
                RollbackStatus: AdUserUpdateRollbackStatus.NotRequired,
                AfterReloadFailed: afterReloadFailed));

    private static string ResolveDefaultCode(AdUserUpdateFailureContext context)
    {
        if (string.Equals(context.DiagnosticCode, AdUserUpdateDiagnosticCodes.PreflightFailed, StringComparison.Ordinal))
        {
            return AdUserUpdateDiagnosticCodes.PreflightFailed;
        }

        return AdUserUpdateDiagnosticCodes.UpdateFailed;
    }

    public static string ResolveNormalizedReason(
        int? ldapResultCode,
        int? ldapExceptionErrorCode,
        string? ldapDiagnosticMessage,
        string? attributeName)
    {
        var code = ldapResultCode ?? ldapExceptionErrorCode;
        var diagnostic = ldapDiagnosticMessage;

        if (IsDuplicateFailure(code, diagnostic))
        {
            return AdUserUpdateNormalizedReasons.DuplicateValue;
        }

        if (code is 34 or 64 or 65 or 66 or 67)
        {
            return AdUserUpdateNormalizedReasons.InvalidDnSyntax;
        }

        if (code is 50)
        {
            return AdUserUpdateNormalizedReasons.InsufficientAccessRights;
        }

        if (code is 53)
        {
            return AdUserUpdateNormalizedReasons.UnwillingToPerform;
        }

        if (code is 32)
        {
            return AdUserUpdateNormalizedReasons.NoSuchObject;
        }

        if (code is 19 or 23)
        {
            return AdUserUpdateNormalizedReasons.ConstraintViolation;
        }

        if (IsConnectionFailure(code, diagnostic))
        {
            return AdUserUpdateNormalizedReasons.ConnectionFailed;
        }

        if (MatchesInvalidDn(diagnostic))
        {
            return AdUserUpdateNormalizedReasons.InvalidDnSyntax;
        }

        if (MatchesInsufficientAccess(diagnostic))
        {
            return AdUserUpdateNormalizedReasons.InsufficientAccessRights;
        }

        if (MatchesUnwilling(diagnostic))
        {
            return AdUserUpdateNormalizedReasons.UnwillingToPerform;
        }

        if (MatchesNoSuchObject(diagnostic))
        {
            return AdUserUpdateNormalizedReasons.NoSuchObject;
        }

        if (MatchesConstraint(diagnostic))
        {
            return AdUserUpdateNormalizedReasons.ConstraintViolation;
        }

        if (MatchesDuplicate(diagnostic))
        {
            return AdUserUpdateNormalizedReasons.DuplicateValue;
        }

        return AdUserUpdateNormalizedReasons.Unknown;
    }

    public static string ResolveEnglishMessage(
        string normalizedReason,
        string? attributeName,
        int? ldapResultCode,
        int? ldapExceptionErrorCode,
        string? ldapDiagnosticMessage)
    {
        if (normalizedReason == AdUserUpdateNormalizedReasons.DuplicateValue)
        {
            return ResolveDuplicateEnglishMessage(attributeName, ldapDiagnosticMessage);
        }

        return normalizedReason switch
        {
            AdUserUpdateNormalizedReasons.ConstraintViolation =>
                "Active Directory rejected the requested attribute change.",
            AdUserUpdateNormalizedReasons.InvalidDnSyntax =>
                "The display name or distinguished name is not valid for Active Directory.",
            AdUserUpdateNormalizedReasons.InsufficientAccessRights =>
                "The AD service account does not have permission to update this attribute.",
            AdUserUpdateNormalizedReasons.UnwillingToPerform =>
                "Active Directory rejected the requested attribute change.",
            AdUserUpdateNormalizedReasons.NoSuchObject =>
                "The AD user could not be found.",
            AdUserUpdateNormalizedReasons.ConnectionFailed =>
                "The LDAP connection failed.",
            AdUserUpdateNormalizedReasons.InvalidRequest =>
                "The AD user update request is invalid.",
            _ => "The AD user update failed.",
        };
    }

    private static string ResolveDuplicateEnglishMessage(string? attributeName, string? ldapDiagnosticMessage)
    {
        if (IsCnAttribute(attributeName)
            || DiagnosticMentionsAttribute(ldapDiagnosticMessage, "cn"))
        {
            return "The CN value is already used by another AD object in the target OU.";
        }

        if (IsSamAccountNameAttribute(attributeName)
            || DiagnosticMentionsAttribute(ldapDiagnosticMessage, "samaccountname"))
        {
            return "The sAMAccountName value is already used by another AD object.";
        }

        if (IsUserPrincipalNameAttribute(attributeName)
            || DiagnosticMentionsAttribute(ldapDiagnosticMessage, "userprincipalname"))
        {
            return "The userPrincipalName value is already used by another AD object.";
        }

        return "The CN, sAMAccountName, or userPrincipalName value is already used by another AD object.";
    }

    private static bool IsDuplicateFailure(int? ldapResultCode, string? diagnostic) =>
        ldapResultCode is 68 or 20
        || MatchesDuplicate(diagnostic);

    private static bool IsConnectionFailure(int? ldapResultCode, string? diagnostic)
    {
        if (ldapResultCode is 52 or 81 or 85 or 91 or 51 or 1 or 3)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(diagnostic))
        {
            return false;
        }

        return diagnostic.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Contains("server down", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Contains("connection", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDuplicate(string? message) =>
        ContainsAny(
            message,
            "entryalreadyexists",
            "entry already exists",
            "object already exists",
            "already exists",
            "attributeorvalueexists",
            "attribute or value exists",
            "entry_exists",
            "0000208f",
            "00002071",
            "000021c7",
            "constraint violation");

    private static bool MatchesConstraint(string? message) =>
        ContainsAny(message, "constraintviolation", "constraint violation", "0000052d");

    private static bool MatchesInvalidDn(string? message) =>
        ContainsAny(
            message,
            "invaliddnsyntax",
            "invalid dn",
            "namingviolation",
            "0000207d",
            "name reference is invalid");

    private static bool MatchesInsufficientAccess(string? message) =>
        ContainsAny(message, "insufficientaccessrights", "insufficient access", "00002098", "00002089");

    private static bool MatchesUnwilling(string? message) =>
        ContainsAny(message, "unwillingtoperform", "unwilling to perform", "00002056");

    private static bool MatchesNoSuchObject(string? message) =>
        ContainsAny(message, "nosuchobject", "no such object", "00002030");

    private static bool IsCnAttribute(string? attributeName) =>
        string.Equals(attributeName, "cn", StringComparison.OrdinalIgnoreCase);

    private static bool IsSamAccountNameAttribute(string? attributeName) =>
        string.Equals(attributeName, "sAMAccountName", StringComparison.OrdinalIgnoreCase);

    private static bool IsUserPrincipalNameAttribute(string? attributeName) =>
        string.Equals(attributeName, "userPrincipalName", StringComparison.OrdinalIgnoreCase);

    private static bool DiagnosticMentionsAttribute(string? diagnostic, string attributeToken) =>
        !string.IsNullOrWhiteSpace(diagnostic)
        && diagnostic.Contains(attributeToken, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string? message, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (message.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class AdUserUpdateOperationDiagnosticPayload
    {
        public string Code { get; init; } = string.Empty;
        public string Operation { get; init; } = string.Empty;
        public string Step { get; init; } = string.Empty;
        public string? Attribute { get; init; }
        public string NormalizedReason { get; init; } = string.Empty;
        public int? LdapResultCode { get; init; }
        public int? LdapExceptionErrorCode { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? LdapDiagnosticMessage { get; init; }
        public string? TargetObjectGuid { get; init; }
        public string? TargetDistinguishedName { get; init; }
        public bool? PartialUpdate { get; init; }
        public string? RollbackStatus { get; init; }
        public IReadOnlyList<string>? AppliedChanges { get; init; }
        public IReadOnlyList<string>? RolledBackChanges { get; init; }
        public IReadOnlyList<AdUserUpdateOperationDiagnosticRollbackError>? RollbackErrors { get; init; }
        public bool? AfterReloadFailed { get; init; }
    }

    private sealed class AdUserUpdateOperationDiagnosticRollbackError
    {
        public string Attribute { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }
}
