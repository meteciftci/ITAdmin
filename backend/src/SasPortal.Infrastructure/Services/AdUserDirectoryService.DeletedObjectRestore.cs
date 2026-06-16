using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService : IAdDeletedObjectRestoreService
{
    private const string DeletedObjectRestoreSuccessMessage = "Silinen nesne geri yüklendi.";
    private const string DeletedObjectRestoreFailedMessage = "Silinen nesne geri yüklenemedi.";
    private const string DeletedObjectRestoreUnsupportedTypeMessage = "Bu nesne türü geri yüklenemez.";
    private const string DeletedObjectRestoreMissingTargetMessage =
        "Bu nesne için son bilinen konum bilgisi bulunamadı.";
    private const string DeletedObjectRestoreMissingRdnMessage =
        "Bu nesne için son bilinen RDN bilgisi bulunamadı.";
    private const string DeletedObjectRestoreInvalidRdnMessage =
        "Bu nesne için geçerli geri yükleme RDN bilgisi bulunamadı.";
    private const string DeletedObjectRestoreParentNotFoundMessage = "Geri yükleme hedefi bulunamadı.";
    private const string DeletedObjectRestoreConflictMessage =
        "Hedef konumda aynı ada sahip bir nesne bulunuyor.";
    private const string DeletedObjectRestoreInvalidTargetMessage =
        "Geri yükleme hedefi geçerli değil.";
    private const string DeletedObjectRestoreVerifyFailedMessage =
        "Silinen nesne geri yükleme işlemi doğrulanamadı.";
    private const string DeletedObjectRestoreSuccessLoggingFailedMessage =
        "AD deleted object restore operation succeeded but logging failed.";
    private const string DeletedObjectRestoreFailureLoggingFailedMessage =
        "AD deleted object restore operation failed but logging failed.";
    private const string DeletedObjectRestoreOperationMode = "ModifyRequestUndelete";

    private static class AdDeletedObjectRestoreSteps
    {
        public const string LoadDeletedObject = "LoadDeletedObject";
        public const string ValidateRestoreTarget = "ValidateRestoreTarget";
        public const string CheckParentExists = "CheckParentExists";
        public const string CheckConflict = "CheckConflict";
        public const string RestoreObject = "RestoreObject";
        public const string VerifyRestored = "VerifyRestored";
    }

    private static readonly string[] DeletedObjectRestoreLookupAttributes =
    [
        "objectGUID",
        "objectClass",
        "name",
        "displayName",
        "sAMAccountName",
        "userPrincipalName",
        "distinguishedName",
        "lastKnownParent",
        "msDS-LastKnownRDN",
        "whenChanged",
        "whenDeleted",
    ];

    private static readonly string[] RestoredObjectVerifyAttributes =
    [
        "objectGUID",
        "distinguishedName",
        "name",
        "sAMAccountName",
    ];

    public async Task<AdDeletedObjectRestoreResult> RestoreDeletedObjectAsync(
        AdDeletedObjectRestoreRequest request,
        CancellationToken cancellationToken = default)
    {
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return await FailDeletedObjectRestoreAsync(
                request,
                connectionResult.Message,
                connectionResult.Context?.Connection,
                beforeState: null,
                connectionResult.FailureKind,
                BuildDeletedObjectRestoreFailureDiagnostic(
                    AdDeletedObjectRestoreSteps.LoadDeletedObject,
                    request.ObjectGuid,
                    sourceDeletedDistinguishedName: null,
                    restoredDistinguishedName: null,
                    englishMessageOverride: connectionResult.Message),
                cancellationToken);
        }

        var context = connectionResult.Context;
        var namingContext = ResolveDefaultNamingContext(context.Connection);
        var deletedObjectsSearchBase = ResolveDeletedObjectsSearchBase(context.Connection);
        if (string.IsNullOrWhiteSpace(deletedObjectsSearchBase) || string.IsNullOrWhiteSpace(namingContext))
        {
            return await FailDeletedObjectRestoreAsync(
                request,
                AdManagementNotConfiguredMessage,
                context.Connection,
                beforeState: null,
                AdDirectoryFailureKind.NotConfigured,
                BuildDeletedObjectRestoreFailureDiagnostic(
                    AdDeletedObjectRestoreSteps.LoadDeletedObject,
                    request.ObjectGuid,
                    sourceDeletedDistinguishedName: null,
                    restoredDistinguishedName: null,
                    englishMessageOverride: AdManagementNotConfiguredMessage,
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                cancellationToken);
        }

        AdDeletedObjectRestoreState? loadedBeforeState = null;

        try
        {
            using var ldapConnection = CreateBoundConnection(context);

            if (!TryLoadDeletedObjectForRestore(
                    ldapConnection,
                    deletedObjectsSearchBase,
                    request.ObjectGuid,
                    out var beforeState))
            {
                return await FailDeletedObjectRestoreAsync(
                    request,
                    DeletedObjectNotFoundMessage,
                    context.Connection,
                    beforeState: null,
                    AdDirectoryFailureKind.NotFound,
                    BuildDeletedObjectRestoreFailureDiagnostic(
                        AdDeletedObjectRestoreSteps.LoadDeletedObject,
                        request.ObjectGuid,
                        sourceDeletedDistinguishedName: null,
                    restoredDistinguishedName: null,
                        englishMessageOverride: "The deleted AD object could not be found.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    cancellationToken);
            }

            loadedBeforeState = beforeState;

            if (beforeState.ObjectType == AdDeletedObjectType.Unknown)
            {
                return await FailDeletedObjectRestoreAsync(
                    request,
                    DeletedObjectRestoreUnsupportedTypeMessage,
                    context.Connection,
                    beforeState,
                    AdDirectoryFailureKind.InvalidRequest,
                    BuildDeletedObjectRestoreFailureDiagnostic(
                        AdDeletedObjectRestoreSteps.ValidateRestoreTarget,
                        request.ObjectGuid,
                        sourceDeletedDistinguishedName: beforeState.DistinguishedName,
                        restoredDistinguishedName: null,
                        englishMessageOverride: "The deleted AD object type is not supported for restore.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    cancellationToken);
            }

            var lastKnownParent = beforeState.LastKnownParent?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(lastKnownParent))
            {
                return await FailDeletedObjectRestoreAsync(
                    request,
                    DeletedObjectRestoreMissingTargetMessage,
                    context.Connection,
                    beforeState,
                    AdDirectoryFailureKind.InvalidRequest,
                    BuildDeletedObjectRestoreFailureDiagnostic(
                        AdDeletedObjectRestoreSteps.ValidateRestoreTarget,
                        request.ObjectGuid,
                        sourceDeletedDistinguishedName: beforeState.DistinguishedName,
                        restoredDistinguishedName: null,
                        englishMessageOverride: "The deleted AD object is missing last known parent information.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    cancellationToken);
            }

            var lastKnownRdn = beforeState.LastKnownRdn?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(lastKnownRdn))
            {
                return await FailDeletedObjectRestoreAsync(
                    request,
                    DeletedObjectRestoreMissingRdnMessage,
                    context.Connection,
                    beforeState,
                    AdDirectoryFailureKind.InvalidRequest,
                    BuildDeletedObjectRestoreFailureDiagnostic(
                        AdDeletedObjectRestoreSteps.ValidateRestoreTarget,
                        request.ObjectGuid,
                        sourceDeletedDistinguishedName: beforeState.DistinguishedName,
                        restoredDistinguishedName: null,
                        englishMessageOverride: "The deleted AD object is missing last known RDN information.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    cancellationToken);
            }

            var originalLastKnownRdn = lastKnownRdn;

            if (ContainsDeletedObjectRestoreRdnMarker(originalLastKnownRdn))
            {
                return await FailDeletedObjectRestoreAsync(
                    request,
                    DeletedObjectRestoreInvalidRdnMessage,
                    context.Connection,
                    beforeState,
                    AdDirectoryFailureKind.InvalidRequest,
                    BuildDeletedObjectRestoreFailureDiagnostic(
                        AdDeletedObjectRestoreSteps.ValidateRestoreTarget,
                        request.ObjectGuid,
                        sourceDeletedDistinguishedName: beforeState.DistinguishedName,
                        restoredDistinguishedName: null,
                        englishMessageOverride:
                            $"The deleted AD object restore RDN contains a deleted marker. Original msDS-LastKnownRDN: {originalLastKnownRdn}",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidDnSyntax),
                    cancellationToken);
            }

            var restoreRdn = NormalizeDeletedObjectRestoreRdn(originalLastKnownRdn);
            if (string.IsNullOrWhiteSpace(restoreRdn) || !IsValidDeletedObjectRestoreRdn(restoreRdn))
            {
                return await FailDeletedObjectRestoreAsync(
                    request,
                    DeletedObjectRestoreInvalidRdnMessage,
                    context.Connection,
                    beforeState,
                    AdDirectoryFailureKind.InvalidRequest,
                    BuildDeletedObjectRestoreFailureDiagnostic(
                        AdDeletedObjectRestoreSteps.ValidateRestoreTarget,
                        request.ObjectGuid,
                        sourceDeletedDistinguishedName: beforeState.DistinguishedName,
                        restoredDistinguishedName: null,
                        englishMessageOverride:
                            $"The deleted AD object restore RDN is invalid. Original msDS-LastKnownRDN: {originalLastKnownRdn}",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidDnSyntax),
                    cancellationToken);
            }

            if (!IsValidRestoreTargetDistinguishedName(lastKnownParent)
                || !AdLdapDnHelper.IsEqualOrDescendantOf(lastKnownParent, namingContext))
            {
                return await FailDeletedObjectRestoreAsync(
                    request,
                    DeletedObjectRestoreInvalidTargetMessage,
                    context.Connection,
                    beforeState,
                    AdDirectoryFailureKind.InvalidRequest,
                    BuildDeletedObjectRestoreFailureDiagnostic(
                        AdDeletedObjectRestoreSteps.ValidateRestoreTarget,
                        request.ObjectGuid,
                        sourceDeletedDistinguishedName: beforeState.DistinguishedName,
                        restoredDistinguishedName: null,
                        englishMessageOverride: "The restore target is outside the domain naming context.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidDnSyntax),
                    cancellationToken);
            }

            if (!TryLoadDirectoryObjectByDn(ldapConnection, lastKnownParent))
            {
                return await FailDeletedObjectRestoreAsync(
                    request,
                    DeletedObjectRestoreParentNotFoundMessage,
                    context.Connection,
                    beforeState,
                    AdDirectoryFailureKind.InvalidRequest,
                    BuildDeletedObjectRestoreFailureDiagnostic(
                        AdDeletedObjectRestoreSteps.CheckParentExists,
                        request.ObjectGuid,
                        sourceDeletedDistinguishedName: beforeState.DistinguishedName,
                        restoredDistinguishedName: null,
                        englishMessageOverride: "The restore target parent could not be found.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject),
                    cancellationToken);
            }

            var restoredDistinguishedName = $"{restoreRdn},{lastKnownParent}";
            if (TryLoadDirectoryObjectByDn(ldapConnection, restoredDistinguishedName))
            {
                return await FailDeletedObjectRestoreAsync(
                    request,
                    DeletedObjectRestoreConflictMessage,
                    context.Connection,
                    beforeState,
                    AdDirectoryFailureKind.InvalidRequest,
                    BuildDeletedObjectRestoreFailureDiagnostic(
                        AdDeletedObjectRestoreSteps.CheckConflict,
                        request.ObjectGuid,
                        sourceDeletedDistinguishedName: beforeState.DistinguishedName,
                        restoredDistinguishedName,
                        englishMessageOverride: "An object with the same name already exists in the target location.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.InvalidRequest),
                    cancellationToken);
            }

            try
            {
                ExecuteRestoreDeletedObject(
                    ldapConnection,
                    beforeState.DistinguishedName,
                    restoredDistinguishedName);
            }
            catch (RestoreDeletedObjectLdapException ex)
            {
                return await FailDeletedObjectRestoreAsync(
                    request,
                    ex.UserMessage,
                    context.Connection,
                    beforeState,
                    ex.FailureKind,
                    BuildDeletedObjectRestoreFailureDiagnostic(
                        ex.Step,
                        request.ObjectGuid,
                        sourceDeletedDistinguishedName: beforeState.DistinguishedName,
                        restoredDistinguishedName,
                        englishMessageOverride: ex.EnglishMessage,
                        ldapResultCode: ex.LdapResultCode,
                        ldapExceptionErrorCode: ex.LdapExceptionErrorCode,
                        ldapDiagnosticMessage: ex.LdapDiagnosticMessage,
                        normalizedReasonOverride: ex.NormalizedReason),
                    cancellationToken);
            }

            if (!TryVerifyRestoredObject(
                    ldapConnection,
                    namingContext,
                    request.ObjectGuid,
                    restoredDistinguishedName,
                    out var restoredState))
            {
                return await FailDeletedObjectRestoreAsync(
                    request,
                    DeletedObjectRestoreVerifyFailedMessage,
                    context.Connection,
                    beforeState,
                    AdDirectoryFailureKind.InvalidRequest,
                    BuildDeletedObjectRestoreFailureDiagnostic(
                        AdDeletedObjectRestoreSteps.VerifyRestored,
                        request.ObjectGuid,
                        sourceDeletedDistinguishedName: beforeState.DistinguishedName,
                        restoredDistinguishedName,
                        englishMessageOverride: "The restored AD object could not be verified.",
                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.Unknown),
                    cancellationToken);
            }

            await WriteDeletedObjectRestoreSuccessLogsAsync(
                request,
                context.Connection,
                beforeState,
                restoredState,
                lastKnownParent,
                originalLastKnownRdn,
                restoreRdn,
                beforeState.DistinguishedName,
                restoredDistinguishedName,
                cancellationToken);

            return new AdDeletedObjectRestoreResult(
                true,
                DeletedObjectRestoreSuccessMessage,
                new AdDeletedObjectRestoreItem(
                    restoredState.ObjectId,
                    restoredState.ObjectType,
                    restoredState.Name,
                    restoredState.SamAccountName,
                    restoredState.DistinguishedName,
                    lastKnownParent,
                    restoreRdn));
        }
        catch (DirectoryOperationException ex)
        {
            var ldapFailure = CreateRestoreDeletedObjectLdapExceptionFromDirectoryOperation(
                ex,
                AdDeletedObjectRestoreSteps.RestoreObject,
                "The deleted AD object could not be restored.");

            return await FailDeletedObjectRestoreAsync(
                request,
                ldapFailure.UserMessage,
                context.Connection,
                loadedBeforeState,
                ldapFailure.FailureKind,
                BuildDeletedObjectRestoreFailureDiagnostic(
                    ldapFailure.Step,
                    request.ObjectGuid,
                    sourceDeletedDistinguishedName: loadedBeforeState?.DistinguishedName,
                    restoredDistinguishedName: null,
                    englishMessageOverride: ldapFailure.EnglishMessage,
                    ldapResultCode: ldapFailure.LdapResultCode,
                    ldapExceptionErrorCode: ldapFailure.LdapExceptionErrorCode,
                    ldapDiagnosticMessage: ldapFailure.LdapDiagnosticMessage,
                    normalizedReasonOverride: ldapFailure.NormalizedReason),
                cancellationToken);
        }
        catch (LdapException ex)
        {
            var failureKind = MapDeletedObjectRestoreFailureKindFromLdapErrorCode(ex.ErrorCode);
            var failureStep = loadedBeforeState is null
                ? AdDeletedObjectRestoreSteps.LoadDeletedObject
                : AdDeletedObjectRestoreSteps.RestoreObject;

            return await FailDeletedObjectRestoreAsync(
                request,
                SanitizeDeletedObjectRestoreLdapError(ex),
                context.Connection,
                loadedBeforeState,
                failureKind,
                BuildDeletedObjectRestoreFailureDiagnostic(
                    failureStep,
                    request.ObjectGuid,
                    sourceDeletedDistinguishedName: loadedBeforeState?.DistinguishedName,
                    restoredDistinguishedName: null,
                    englishMessageOverride: ResolveDeletedObjectRestoreEnglishMessageFromLdapErrorCode(ex.ErrorCode),
                    ldapResultCode: ex.ErrorCode,
                    ldapExceptionErrorCode: ex.ErrorCode,
                    ldapDiagnosticMessage: ex.Message,
                    normalizedReasonOverride: ResolveDeletedObjectRestoreNormalizedReasonFromLdapErrorCode(ex.ErrorCode)),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "AD deleted object restore unexpected failure. ObjectGuid={ObjectGuid} ActorUserId={ActorUserId}",
                request.ObjectGuid,
                request.ActorUserId);

            return await FailDeletedObjectRestoreAsync(
                request,
                DeletedObjectRestoreFailedMessage,
                context.Connection,
                loadedBeforeState,
                AdDirectoryFailureKind.InvalidRequest,
                BuildDeletedObjectRestoreFailureDiagnostic(
                    AdDeletedObjectRestoreSteps.RestoreObject,
                    request.ObjectGuid,
                    sourceDeletedDistinguishedName: loadedBeforeState?.DistinguishedName,
                    restoredDistinguishedName: null,
                    englishMessageOverride: "The deleted AD object could not be restored.",
                    normalizedReasonOverride: AdUserUpdateNormalizedReasons.Unknown),
                cancellationToken);
        }
    }

    private static string? ResolveDefaultNamingContext(AdManagementConnectionParameters connection) =>
        string.IsNullOrWhiteSpace(connection.DefaultNamingContext)
            ? connection.BaseDn
            : connection.DefaultNamingContext;

    private static bool IsValidRestoreTargetDistinguishedName(string distinguishedName) =>
        !string.IsNullOrWhiteSpace(distinguishedName)
        && distinguishedName.Contains('=', StringComparison.Ordinal);

    private bool TryLoadDeletedObjectForRestore(
        LdapConnection ldapConnection,
        string deletedObjectsSearchBase,
        Guid objectGuid,
        out AdDeletedObjectRestoreState state)
    {
        state = null!;
        var filter = AdLdapDeletedObjectFilterHelper.BuildDeletedObjectGuidFilter(objectGuid);
        var searchRequest = new SearchRequest(
            deletedObjectsSearchBase,
            filter,
            SearchScope.Subtree,
            DeletedObjectRestoreLookupAttributes)
        {
            SizeLimit = 1,
            TimeLimit = LdapOperationTimeout,
        };

        searchRequest.Controls.Add(
            new DirectoryControl(
                AdLdapDeletedObjectFilterHelper.ShowDeletedControlOid,
                null,
                true,
                true));

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        var entry = response.Entries[0];
        if (!TryGetObjectGuid(entry, out var resolvedGuid))
        {
            return false;
        }

        var distinguishedName = GetFirstString(entry, "distinguishedName");
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return false;
        }

        var objectClasses = GetAllStrings(entry, "objectClass");
        state = new AdDeletedObjectRestoreState(
            resolvedGuid.ToString("D"),
            ResolveDeletedObjectType(objectClasses),
            GetFirstString(entry, "name"),
            GetFirstString(entry, "displayName"),
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "userPrincipalName"),
            distinguishedName,
            GetFirstString(entry, "lastKnownParent"),
            GetFirstString(entry, "msDS-LastKnownRDN"),
            objectClasses,
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenChanged")),
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenDeleted"))
            ?? AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenChanged")));

        return true;
    }

    private static bool TryLoadDirectoryObjectByDn(LdapConnection ldapConnection, string distinguishedName)
    {
        var searchRequest = new SearchRequest(
            distinguishedName.Trim(),
            "(objectClass=*)",
            SearchScope.Base,
            "distinguishedName")
        {
            SizeLimit = 1,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        return response.ResultCode == ResultCode.Success && response.Entries.Count > 0;
    }

    private static void ExecuteRestoreDeletedObject(
        LdapConnection ldapConnection,
        string deletedDistinguishedName,
        string restoredDistinguishedName)
    {
        var modifyRequest = new ModifyRequest(deletedDistinguishedName);

        modifyRequest.Controls.Add(
            new DirectoryControl(
                AdLdapDeletedObjectFilterHelper.ShowDeletedControlOid,
                null,
                true,
                true));

        var isDeletedModification = new DirectoryAttributeModification
        {
            Name = "isDeleted",
            Operation = DirectoryAttributeOperation.Delete,
        };

        var distinguishedNameModification = new DirectoryAttributeModification
        {
            Name = "distinguishedName",
            Operation = DirectoryAttributeOperation.Replace,
        };
        distinguishedNameModification.Add(restoredDistinguishedName);

        modifyRequest.Modifications.Add(isDeletedModification);
        modifyRequest.Modifications.Add(distinguishedNameModification);

        DirectoryResponse response;
        try
        {
            response = (DirectoryResponse)ldapConnection.SendRequest(modifyRequest);
        }
        catch (DirectoryOperationException ex)
        {
            throw CreateRestoreDeletedObjectLdapExceptionFromDirectoryOperation(
                ex,
                AdDeletedObjectRestoreSteps.RestoreObject,
                "The deleted AD object could not be restored.");
        }

        if (response.ResultCode != ResultCode.Success)
        {
            var userMessage = AdLdapErrorNormalizer.Normalize((int)response.ResultCode, response.ErrorMessage);
            throw new RestoreDeletedObjectLdapException(
                userMessage,
                MapDeletedObjectRestoreFailureKind(response.ResultCode),
                ResolveDeletedObjectRestoreNormalizedReason(response.ResultCode),
                ResolveDeletedObjectRestoreEnglishMessage(response.ResultCode),
                AdDeletedObjectRestoreSteps.RestoreObject,
                (int)response.ResultCode,
                (int)response.ResultCode,
                response.ErrorMessage);
        }
    }

    private bool TryVerifyRestoredObject(
        LdapConnection ldapConnection,
        string namingContext,
        Guid objectGuid,
        string restoredDistinguishedName,
        out AdDeletedObjectRestoreState state)
    {
        state = null!;
        var filter = AdLdapFilterHelper.FormatObjectGuidFilter(objectGuid);
        var searchRequest = new SearchRequest(
            namingContext,
            $"(&(objectGUID={filter}))",
            SearchScope.Subtree,
            RestoredObjectVerifyAttributes)
        {
            SizeLimit = 1,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        var entry = response.Entries[0];
        if (!TryGetObjectGuid(entry, out var resolvedGuid) || resolvedGuid != objectGuid)
        {
            return false;
        }

        var distinguishedName = GetFirstString(entry, "distinguishedName");
        if (string.IsNullOrWhiteSpace(distinguishedName)
            || !AdLdapDnHelper.AreDistinguishedNamesEqual(distinguishedName, restoredDistinguishedName))
        {
            return false;
        }

        var objectClasses = GetAllStrings(entry, "objectClass");
        state = new AdDeletedObjectRestoreState(
            resolvedGuid.ToString("D"),
            ResolveDeletedObjectType(objectClasses),
            GetFirstString(entry, "name"),
            null,
            GetFirstString(entry, "sAMAccountName"),
            null,
            distinguishedName,
            null,
            null,
            objectClasses,
            null,
            null);

        return true;
    }

    private static AdDirectoryFailureKind MapDeletedObjectRestoreFailureKind(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.NoSuchObject => AdDirectoryFailureKind.NotFound,
            ResultCode.EntryAlreadyExists
                or ResultCode.AttributeOrValueExists
                or ResultCode.ConstraintViolation
                or ResultCode.InvalidDNSyntax
                or ResultCode.NamingViolation
                or ResultCode.UnwillingToPerform => AdDirectoryFailureKind.InvalidRequest,
            ResultCode.InsufficientAccessRights => AdDirectoryFailureKind.InvalidRequest,
            ResultCode.Unavailable
                or ResultCode.TimeLimitExceeded
                or ResultCode.Busy => AdDirectoryFailureKind.ConnectionFailed,
            _ => AdDirectoryFailureKind.InvalidRequest,
        };

    private static AdDirectoryFailureKind MapDeletedObjectRestoreFailureKindFromLdapErrorCode(int ldapErrorCode)
    {
        if (ldapErrorCode is 81 or 91 or 52 or 85)
        {
            return AdDirectoryFailureKind.ConnectionFailed;
        }

        return MapDeletedObjectRestoreFailureKind((ResultCode)ldapErrorCode);
    }

    private static string ResolveDeletedObjectRestoreNormalizedReasonFromLdapErrorCode(int ldapErrorCode) =>
        ResolveDeletedObjectRestoreNormalizedReason((ResultCode)ldapErrorCode);

    private static string ResolveDeletedObjectRestoreEnglishMessageFromLdapErrorCode(int ldapErrorCode) =>
        ResolveDeletedObjectRestoreEnglishMessage((ResultCode)ldapErrorCode);

    private static string ResolveDeletedObjectRestoreNormalizedReason(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.NoSuchObject => AdUserUpdateNormalizedReasons.NoSuchObject,
            ResultCode.EntryAlreadyExists
                or ResultCode.AttributeOrValueExists
                or ResultCode.NamingViolation => AdUserUpdateNormalizedReasons.InvalidRequest,
            ResultCode.InvalidDNSyntax => AdUserUpdateNormalizedReasons.InvalidDnSyntax,
            ResultCode.InsufficientAccessRights => AdUserUpdateNormalizedReasons.InsufficientAccessRights,
            ResultCode.Unavailable or ResultCode.TimeLimitExceeded or ResultCode.Busy =>
                AdUserUpdateNormalizedReasons.ConnectionFailed,
            _ => AdUserUpdateNormalizedReasons.Unknown,
        };

    private static string ResolveDeletedObjectRestoreEnglishMessage(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.NoSuchObject => "The deleted AD object could not be found for restore.",
            ResultCode.EntryAlreadyExists or ResultCode.AttributeOrValueExists or ResultCode.NamingViolation =>
                "An object with the same name already exists in the target location.",
            ResultCode.InsufficientAccessRights =>
                "The AD service account does not have permission to restore this object.",
            ResultCode.UnwillingToPerform =>
                "Active Directory rejected the restore operation.",
            _ => "The deleted AD object could not be restored.",
        };

    private async Task<AdDeletedObjectRestoreResult> FailDeletedObjectRestoreAsync(
        AdDeletedObjectRestoreRequest request,
        string message,
        AdManagementConnectionParameters? connection,
        AdDeletedObjectRestoreState? beforeState,
        AdDirectoryFailureKind? failureKind,
        string operationDiagnosticJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteDeletedObjectRestoreFailureLogsAsync(
                request,
                connection,
                beforeState,
                operationDiagnosticJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} ObjectGuid={ObjectGuid} ActorUserId={ActorUserId}",
                DeletedObjectRestoreFailureLoggingFailedMessage,
                request.ObjectGuid,
                request.ActorUserId);
        }

        return new AdDeletedObjectRestoreResult(false, message, null, failureKind);
    }

    private async Task WriteDeletedObjectRestoreSuccessLogsAsync(
        AdDeletedObjectRestoreRequest request,
        AdManagementConnectionParameters connection,
        AdDeletedObjectRestoreState beforeState,
        AdDeletedObjectRestoreState restoredState,
        string lastKnownParent,
        string originalLastKnownRdn,
        string restoreRdn,
        string sourceDeletedDistinguishedName,
        string restoredDistinguishedName,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteDeletedObjectRestoreOperationLogAsync(
                request,
                connection,
                AdManagementOperationStatuses.Succeeded,
                beforeState,
                restoredState,
                lastKnownParent,
                originalLastKnownRdn,
                restoreRdn,
                sourceDeletedDistinguishedName,
                restoredDistinguishedName,
                errorDiagnosticJson: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} ObjectGuid={ObjectGuid} ActorUserId={ActorUserId}",
                DeletedObjectRestoreSuccessLoggingFailedMessage,
                beforeState.ObjectId,
                request.ActorUserId);
        }
    }

    private async Task WriteDeletedObjectRestoreFailureLogsAsync(
        AdDeletedObjectRestoreRequest request,
        AdManagementConnectionParameters? connection,
        AdDeletedObjectRestoreState? beforeState,
        string operationDiagnosticJson,
        CancellationToken cancellationToken)
    {
        await WriteDeletedObjectRestoreOperationLogAsync(
            request,
            connection,
            AdManagementOperationStatuses.Failed,
            beforeState,
            restoredState: null,
            lastKnownParent: beforeState?.LastKnownParent,
            originalLastKnownRdn: beforeState?.LastKnownRdn,
            restoreRdn: beforeState?.LastKnownRdn is null
                ? null
                : NormalizeDeletedObjectRestoreRdn(beforeState.LastKnownRdn),
            sourceDeletedDistinguishedName: beforeState?.DistinguishedName,
            restoredDistinguishedName: beforeState?.LastKnownParent is not null
                && beforeState.LastKnownRdn is not null
                && NormalizeDeletedObjectRestoreRdn(beforeState.LastKnownRdn) is { } normalizedRestoreRdn
                ? $"{normalizedRestoreRdn},{beforeState.LastKnownParent.Trim()}"
                : null,
            operationDiagnosticJson,
            cancellationToken);
    }

    private async Task WriteDeletedObjectRestoreOperationLogAsync(
        AdDeletedObjectRestoreRequest request,
        AdManagementConnectionParameters? connection,
        string status,
        AdDeletedObjectRestoreState? beforeState,
        AdDeletedObjectRestoreState? restoredState,
        string? lastKnownParent,
        string? originalLastKnownRdn,
        string? restoreRdn,
        string? sourceDeletedDistinguishedName,
        string? restoredDistinguishedName,
        string? errorDiagnosticJson,
        CancellationToken cancellationToken)
    {
        var beforeSnapshot = beforeState is null
            ? null
            : AdOperationLogSnapshotBuilder.BuildDeletedObjectRestoreBeforeSnapshot(
                beforeState.ObjectId,
                beforeState.ObjectType,
                beforeState.Name,
                beforeState.DisplayName,
                beforeState.SamAccountName,
                beforeState.UserPrincipalName,
                beforeState.DistinguishedName,
                beforeState.LastKnownParent,
                beforeState.LastKnownRdn,
                beforeState.ObjectClass,
                beforeState.WhenChanged,
                beforeState.DeletedAt);

        var afterSnapshot = string.Equals(status, AdManagementOperationStatuses.Succeeded, StringComparison.Ordinal)
            && restoredState is not null
            && !string.IsNullOrWhiteSpace(lastKnownParent)
            && !string.IsNullOrWhiteSpace(restoreRdn)
            ? AdOperationLogSnapshotBuilder.BuildDeletedObjectRestoreAfterSnapshot(
                restoredState.ObjectId,
                restoredState.ObjectType,
                restoredState.Name,
                restoredState.SamAccountName,
                restoredState.DistinguishedName,
                lastKnownParent,
                restoreRdn)
            : null;

        var isSuccess = string.Equals(status, AdManagementOperationStatuses.Succeeded, StringComparison.Ordinal);
        var effectiveRestoreRdn = restoreRdn
            ?? NormalizeDeletedObjectRestoreRdn(originalLastKnownRdn)
            ?? string.Empty;
        var requestSummary = !string.IsNullOrWhiteSpace(lastKnownParent)
            && !string.IsNullOrWhiteSpace(restoredDistinguishedName)
            && (!string.IsNullOrWhiteSpace(effectiveRestoreRdn) || !isSuccess)
            ? AdOperationLogSnapshotBuilder.BuildDeletedObjectRestoreRequestSummary(
                request.ObjectGuid,
                lastKnownParent,
                effectiveRestoreRdn,
                restoredDistinguishedName,
                originalLastKnownRdn,
                sourceDeletedDistinguishedName,
                DeletedObjectRestoreOperationMode)
            : !isSuccess
                ? AdOperationLogSnapshotBuilder.BuildDeletedObjectRestoreRequestSummary(
                    request.ObjectGuid,
                    lastKnownParent ?? string.Empty,
                    effectiveRestoreRdn,
                    restoredDistinguishedName ?? request.ObjectGuid.ToString("D"),
                    originalLastKnownRdn,
                    sourceDeletedDistinguishedName,
                    DeletedObjectRestoreOperationMode)
                : null;

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.DeletedObjectRestore,
                Status = status,
                TargetObjectType = ResolveDeletedObjectRestoreTargetType(beforeState?.ObjectType ?? restoredState?.ObjectType),
                TargetDistinguishedName = restoredState?.DistinguishedName ?? beforeState?.DistinguishedName,
                TargetObjectGuid = beforeState?.ObjectId ?? request.ObjectGuid.ToString("D"),
                TargetSamAccountName = restoredState?.SamAccountName ?? beforeState?.SamAccountName,
                ErrorCode = isSuccess
                    ? null
                    : AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(errorDiagnosticJson),
                ErrorMessage = isSuccess ? null : errorDiagnosticJson,
                RequestSummaryJson = requestSummary,
                BeforeSnapshotJson = beforeSnapshot,
                AfterSnapshotJson = afterSnapshot,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
                DomainController = connection is null ? null : ResolvePrimaryHost(connection),
            },
            cancellationToken);
    }

    private static string? ResolveDeletedObjectRestoreTargetType(AdDeletedObjectType? objectType) =>
        objectType switch
        {
            AdDeletedObjectType.User => AdManagementTargetUserTypes.AdUser,
            AdDeletedObjectType.Group => AdManagementTargetGroupTypes.AdGroup,
            AdDeletedObjectType.Computer => AdManagementTargetComputerTypes.AdComputer,
            _ => null,
        };

    private static string BuildDeletedObjectRestoreFailureDiagnostic(
        string step,
        Guid objectGuid,
        string? sourceDeletedDistinguishedName,
        string? restoredDistinguishedName,
        string? englishMessageOverride = null,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null,
        string? normalizedReasonOverride = null) =>
        AdOperationErrorDiagnosticBuilder.BuildDeletedObjectRestoreFailureJson(
            step,
            objectGuid,
            sourceDeletedDistinguishedName,
            restoredDistinguishedName,
            DeletedObjectRestoreOperationMode,
            englishMessageOverride,
            ldapResultCode,
            ldapExceptionErrorCode,
            ldapDiagnosticMessage,
            normalizedReasonOverride);

    private static string? NormalizeDeletedObjectRestoreRdn(string? lastKnownRdn)
    {
        if (string.IsNullOrWhiteSpace(lastKnownRdn))
        {
            return null;
        }

        var trimmed = lastKnownRdn.Trim();
        if (ContainsDeletedObjectRestoreRdnMarker(trimmed))
        {
            return null;
        }

        if (HasDeletedObjectRestoreRdnAttributePrefix(trimmed))
        {
            return trimmed;
        }

        return AdLdapDnHelper.BuildCommonNameRdn(trimmed);
    }

    private static bool ContainsDeletedObjectRestoreRdnMarker(string value) =>
        value.Contains('\0', StringComparison.Ordinal)
        || value.Contains("ADEL:", StringComparison.OrdinalIgnoreCase)
        || value.Contains(@"\0ADEL", StringComparison.OrdinalIgnoreCase);

    private static bool HasDeletedObjectRestoreRdnAttributePrefix(string value)
    {
        var equalsIndex = value.IndexOf('=');
        if (equalsIndex <= 0)
        {
            return false;
        }

        var attributeType = value[..equalsIndex].Trim();
        if (attributeType.Length == 0)
        {
            return false;
        }

        foreach (var character in attributeType)
        {
            if (!char.IsLetterOrDigit(character) && character != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidDeletedObjectRestoreRdn(string restoreRdn)
    {
        if (string.IsNullOrWhiteSpace(restoreRdn))
        {
            return false;
        }

        var trimmed = restoreRdn.Trim();
        if (!trimmed.Contains('=', StringComparison.Ordinal))
        {
            return false;
        }

        if (!HasDeletedObjectRestoreRdnAttributePrefix(trimmed))
        {
            return false;
        }

        return !ContainsUnescapedRdnComponentSeparator(trimmed);
    }

    private static bool ContainsUnescapedRdnComponentSeparator(string rdn)
    {
        var inQuotes = false;

        for (var index = 0; index < rdn.Length; index++)
        {
            var character = rdn[index];
            if (character == '\\' && index + 1 < rdn.Length)
            {
                index++;
                continue;
            }

            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && character == ',')
            {
                return true;
            }
        }

        return false;
    }

    private static string SanitizeDeletedObjectRestoreLdapError(LdapException exception) =>
        AdLdapErrorNormalizer.Normalize(exception.ErrorCode, exception.Message);

    private static RestoreDeletedObjectLdapException CreateRestoreDeletedObjectLdapExceptionFromDirectoryOperation(
        DirectoryOperationException exception,
        string step,
        string englishMessageFallback)
    {
        var response = exception.Response;
        var ldapResultCode = response is not null ? (int)response.ResultCode : (int?)null;
        var diagnosticMessage = response?.ErrorMessage ?? exception.Message;
        var userMessage = ldapResultCode is not null
            ? AdLdapErrorNormalizer.Normalize(ldapResultCode.Value, diagnosticMessage)
            : DeletedObjectRestoreFailedMessage;

        return new RestoreDeletedObjectLdapException(
            userMessage,
            ldapResultCode is not null
                ? MapDeletedObjectRestoreFailureKind(response!.ResultCode)
                : AdDirectoryFailureKind.InvalidRequest,
            ldapResultCode is not null
                ? ResolveDeletedObjectRestoreNormalizedReason(response!.ResultCode)
                : AdUserUpdateNormalizedReasons.Unknown,
            ldapResultCode is not null
                ? ResolveDeletedObjectRestoreEnglishMessage(response!.ResultCode)
                : englishMessageFallback,
            step,
            ldapResultCode,
            ldapResultCode,
            diagnosticMessage);
    }

    private sealed record AdDeletedObjectRestoreState(
        string ObjectId,
        AdDeletedObjectType ObjectType,
        string? Name,
        string? DisplayName,
        string? SamAccountName,
        string? UserPrincipalName,
        string DistinguishedName,
        string? LastKnownParent,
        string? LastKnownRdn,
        IReadOnlyList<string> ObjectClass,
        DateTimeOffset? WhenChanged,
        DateTimeOffset? DeletedAt);

    private sealed class RestoreDeletedObjectLdapException(
        string userMessage,
        AdDirectoryFailureKind failureKind,
        string normalizedReason,
        string englishMessage,
        string step,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null) : Exception(userMessage)
    {
        public string UserMessage { get; } = userMessage;
        public AdDirectoryFailureKind FailureKind { get; } = failureKind;
        public string NormalizedReason { get; } = normalizedReason;
        public string EnglishMessage { get; } = englishMessage;
        public string Step { get; } = step;
        public int? LdapResultCode { get; } = ldapResultCode;
        public int? LdapExceptionErrorCode { get; } = ldapExceptionErrorCode;
        public string? LdapDiagnosticMessage { get; } = ldapDiagnosticMessage;
    }
}
