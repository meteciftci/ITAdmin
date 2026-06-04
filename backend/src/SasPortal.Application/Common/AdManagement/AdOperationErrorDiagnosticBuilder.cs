using System.Text.Json;
using System.Text.Json.Serialization;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdOperationErrorDiagnosticBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string BuildJson(AdOperationFailureContext context)
    {
        var normalizedReason = context.NormalizedReasonOverride
            ?? AdUserUpdateOperationDiagnosticBuilder.ResolveNormalizedReason(
                context.LdapResultCode,
                context.LdapExceptionErrorCode,
                context.LdapDiagnosticMessage,
                attributeName: null);

        var message = context.EnglishMessageOverride
            ?? ResolveEnglishMessage(context.Operation, normalizedReason);

        var code = context.DiagnosticCode ?? ResolveDefaultCode(context.Operation) ?? "AD_OPERATION_FAILED";

        var payload = new AdOperationDiagnosticPayload
        {
            Code = code,
            Operation = context.Operation,
            Step = context.Step,
            NormalizedReason = normalizedReason,
            LdapResultCode = context.LdapResultCode,
            LdapExceptionErrorCode = context.LdapExceptionErrorCode,
            Message = message,
            LdapDiagnosticMessage = AdLdapDiagnosticSanitizer.SanitizeLdapDiagnosticMessage(
                context.LdapDiagnosticMessage),
            TargetObjectGuid = context.TargetObjectGuid?.ToString("D"),
            TargetDistinguishedName = AdLdapDiagnosticSanitizer.SanitizeDistinguishedName(
                context.TargetDistinguishedName),
            PartialUpdate = context.PartialUpdate,
            RollbackStatus = context.RollbackStatus ?? AdUserUpdateRollbackStatus.NotRequired,
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    public static string BuildGroupMembershipFailureJson(
        string operationType,
        string step,
        Guid? targetObjectGuid,
        string? targetDistinguishedName,
        string? englishMessageOverride = null,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null,
        string? normalizedReasonOverride = null) =>
        BuildJson(
            new AdOperationFailureContext(
                operationType,
                step,
                DiagnosticCode: ResolveDefaultCode(operationType),
                NormalizedReasonOverride: normalizedReasonOverride,
                EnglishMessageOverride: englishMessageOverride,
                LdapResultCode: ldapResultCode,
                LdapExceptionErrorCode: ldapExceptionErrorCode,
                LdapDiagnosticMessage: ldapDiagnosticMessage,
                TargetObjectGuid: targetObjectGuid,
                TargetDistinguishedName: targetDistinguishedName,
                PartialUpdate: false,
                RollbackStatus: AdUserUpdateRollbackStatus.NotRequired));

    public static string BuildAccountOperationFailureJson(
        string operationType,
        string step,
        Guid targetObjectGuid,
        string? targetDistinguishedName,
        string? englishMessageOverride = null,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null,
        string? normalizedReasonOverride = null) =>
        BuildJson(
            new AdOperationFailureContext(
                operationType,
                step,
                DiagnosticCode: ResolveDefaultCode(operationType),
                NormalizedReasonOverride: normalizedReasonOverride,
                EnglishMessageOverride: englishMessageOverride,
                LdapResultCode: ldapResultCode,
                LdapExceptionErrorCode: ldapExceptionErrorCode,
                LdapDiagnosticMessage: ldapDiagnosticMessage,
                TargetObjectGuid: targetObjectGuid,
                TargetDistinguishedName: targetDistinguishedName,
                PartialUpdate: false,
                RollbackStatus: AdUserUpdateRollbackStatus.NotRequired));

    public static string BuildUserOuMoveFailureJson(
        string step,
        Guid? targetObjectGuid,
        string? targetDistinguishedName,
        string? englishMessageOverride = null,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null,
        string? normalizedReasonOverride = null) =>
        BuildJson(
            new AdOperationFailureContext(
                AdManagementOperationTypes.UserOuMove,
                step,
                DiagnosticCode: AdOperationDiagnosticCodes.UserOuMoveFailed,
                NormalizedReasonOverride: normalizedReasonOverride,
                EnglishMessageOverride: englishMessageOverride,
                LdapResultCode: ldapResultCode,
                LdapExceptionErrorCode: ldapExceptionErrorCode,
                LdapDiagnosticMessage: ldapDiagnosticMessage,
                TargetObjectGuid: targetObjectGuid,
                TargetDistinguishedName: targetDistinguishedName,
                PartialUpdate: false,
                RollbackStatus: AdUserUpdateRollbackStatus.NotRequired));

    public static string BuildCreateUserFailureJson(
        string step,
        string? englishMessageOverride = null,
        string? normalizedReasonOverride = null,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null) =>
        BuildJson(
            new AdOperationFailureContext(
                AdManagementOperationTypes.CreateUser,
                step,
                DiagnosticCode: AdOperationDiagnosticCodes.UserCreateFailed,
                NormalizedReasonOverride: normalizedReasonOverride,
                EnglishMessageOverride: englishMessageOverride,
                LdapResultCode: ldapResultCode,
                LdapExceptionErrorCode: ldapExceptionErrorCode,
                LdapDiagnosticMessage: ldapDiagnosticMessage,
                PartialUpdate: false,
                RollbackStatus: AdUserUpdateRollbackStatus.NotRequired));

    public static string BuildSettingsValidationFailureJson(AdManagementValidationResult result)
    {
        var failedDetail = result.Details.FirstOrDefault(static detail =>
            string.Equals(detail.Status, AdManagementValidationStatuses.Failed, StringComparison.OrdinalIgnoreCase));

        var step = MapValidationKeyToStep(failedDetail?.Key);
        var normalizedReason = ResolveValidationNormalizedReason(failedDetail?.Key);

        return BuildJson(
            new AdOperationFailureContext(
                AdManagementOperationTypes.SettingsValidated,
                step,
                DiagnosticCode: AdOperationDiagnosticCodes.SettingsValidationFailed,
                NormalizedReasonOverride: normalizedReason,
                EnglishMessageOverride: "AD management settings validation failed.",
                LdapDiagnosticMessage: result.Message,
                PartialUpdate: false,
                RollbackStatus: AdUserUpdateRollbackStatus.NotRequired));
    }

    public static string? ResolveDefaultCode(string operationType) =>
        operationType switch
        {
            AdManagementOperationTypes.UserGroupAdd => AdOperationDiagnosticCodes.UserGroupAddFailed,
            AdManagementOperationTypes.UserGroupRemove => AdOperationDiagnosticCodes.UserGroupRemoveFailed,
            AdManagementOperationTypes.UserEnable => AdOperationDiagnosticCodes.UserEnableFailed,
            AdManagementOperationTypes.UserDisable => AdOperationDiagnosticCodes.UserDisableFailed,
            AdManagementOperationTypes.UserUnlock => AdOperationDiagnosticCodes.UserUnlockFailed,
            AdManagementOperationTypes.CreateUser => AdOperationDiagnosticCodes.UserCreateFailed,
            AdManagementOperationTypes.UserOuMove => AdOperationDiagnosticCodes.UserOuMoveFailed,
            AdManagementOperationTypes.SettingsValidated => AdOperationDiagnosticCodes.SettingsValidationFailed,
            AdManagementOperationTypes.AttributeMappingCreated => AdOperationDiagnosticCodes.AttributeMappingCreateFailed,
            AdManagementOperationTypes.AttributeMappingUpdated => AdOperationDiagnosticCodes.AttributeMappingUpdateFailed,
            AdManagementOperationTypes.AttributeMappingDeleted => AdOperationDiagnosticCodes.AttributeMappingDeleteFailed,
            _ => null,
        };

    private static string MapValidationKeyToStep(string? key) =>
        key switch
        {
            "serviceAccountBind" => "ValidateConnection",
            "domainFqdn" => "ValidateDomainFqdn",
            "defaultNamingContext" => "ValidateDefaultNamingContext",
            "baseDn" => "ValidateBaseDn",
            "usersRootOu" => "ValidateUsersRootOu",
            "disabledUsersOu" => "ValidateDisabledUsersOu",
            "groupsSearchBase" => "ValidateGroupsSearchBase",
            "computersSearchBase" => "ValidateComputersSearchBase",
            "preferredDomainControllers" => "ValidatePreferredDomainControllers",
            _ => "ValidateConnection",
        };

    private static string ResolveValidationNormalizedReason(string? failedDetailKey) =>
        failedDetailKey switch
        {
            "serviceAccountBind" or "domainFqdn" or "preferredDomainControllers" =>
                AdUserUpdateNormalizedReasons.ConnectionFailed,
            _ => AdUserUpdateNormalizedReasons.Unknown,
        };

    private static string ResolveEnglishMessage(string operation, string normalizedReason) =>
        normalizedReason switch
        {
            AdUserUpdateNormalizedReasons.NoSuchObject =>
                operation switch
                {
                    AdManagementOperationTypes.UserGroupAdd or AdManagementOperationTypes.UserGroupRemove =>
                        "The AD user or group could not be found.",
                    AdManagementOperationTypes.UserEnable or AdManagementOperationTypes.UserDisable
                        or AdManagementOperationTypes.UserUnlock =>
                        "The AD user could not be found.",
                    AdManagementOperationTypes.CreateUser =>
                        "The AD user could not be created because a required object was not found.",
                    _ => "The requested AD object could not be found.",
                },
            AdUserUpdateNormalizedReasons.InsufficientAccessRights =>
                operation switch
                {
                    AdManagementOperationTypes.UserGroupAdd or AdManagementOperationTypes.UserGroupRemove =>
                        "The AD service account does not have permission to modify this group membership.",
                    AdManagementOperationTypes.UserEnable or AdManagementOperationTypes.UserDisable =>
                        "The AD service account does not have permission to modify this account state.",
                    AdManagementOperationTypes.UserUnlock =>
                        "The AD service account does not have permission to unlock this account.",
                    AdManagementOperationTypes.CreateUser =>
                        "The AD service account does not have permission to create this user.",
                    _ => "The AD service account does not have permission to perform this operation.",
                },
            AdUserUpdateNormalizedReasons.ConnectionFailed =>
                "The LDAP connection failed.",
            AdUserUpdateNormalizedReasons.InvalidRequest =>
                "The AD operation request is invalid.",
            _ =>
                operation switch
                {
                    AdManagementOperationTypes.UserGroupAdd =>
                        "The AD group membership add operation failed.",
                    AdManagementOperationTypes.UserGroupRemove =>
                        "The AD group membership remove operation failed.",
                    AdManagementOperationTypes.UserEnable =>
                        "The AD user enable operation failed.",
                    AdManagementOperationTypes.UserDisable =>
                        "The AD user disable operation failed.",
                    AdManagementOperationTypes.UserUnlock =>
                        "The AD user unlock operation failed.",
                    AdManagementOperationTypes.CreateUser =>
                        "The AD user create operation failed.",
                    AdManagementOperationTypes.UserOuMove =>
                        "The AD user OU move operation failed.",
                    _ => "The AD operation failed.",
                },
        };

    private sealed class AdOperationDiagnosticPayload
    {
        public string Code { get; init; } = string.Empty;
        public string Operation { get; init; } = string.Empty;
        public string Step { get; init; } = string.Empty;
        public string NormalizedReason { get; init; } = string.Empty;
        public int? LdapResultCode { get; init; }
        public int? LdapExceptionErrorCode { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? LdapDiagnosticMessage { get; init; }
        public string? TargetObjectGuid { get; init; }
        public string? TargetDistinguishedName { get; init; }
        public bool? PartialUpdate { get; init; }
        public string? RollbackStatus { get; init; }
    }
}
