using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private const string OrganizationalUnitCreateSuccessLoggingFailedMessage =
        "AD organizational unit create operation succeeded but logging failed.";
    private const string OrganizationalUnitCreateFailureLoggingFailedMessage =
        "AD organizational unit create operation failed but logging failed.";
    private const string OrganizationalUnitRenameSuccessLoggingFailedMessage =
        "AD organizational unit rename operation succeeded but logging failed.";
    private const string OrganizationalUnitRenameFailureLoggingFailedMessage =
        "AD organizational unit rename operation failed but logging failed.";
    private const string OrganizationalUnitMoveSuccessLoggingFailedMessage =
        "AD organizational unit move operation succeeded but logging failed.";
    private const string OrganizationalUnitMoveFailureLoggingFailedMessage =
        "AD organizational unit move operation failed but logging failed.";
    private const string OrganizationalUnitDeleteSuccessLoggingFailedMessage =
        "AD organizational unit delete operation succeeded but logging failed.";
    private const string OrganizationalUnitDeleteFailureLoggingFailedMessage =
        "AD organizational unit delete operation failed but logging failed.";

    public async Task<CreateAdOrganizationalUnitResult> CreateOrganizationalUnitAsync(
        CreateAdOrganizationalUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdOrganizationalUnitRequestValidator.TryValidateName(request.Name, out var nameMessageKey))
        {
            return new CreateAdOrganizationalUnitResult(
                false,
                nameMessageKey,
                null,
                AdDirectoryFailureKind.InvalidRequest);
        }

        if (!AdOrganizationalUnitRequestValidator.TryValidateParentDistinguishedName(
                request.ParentDistinguishedName,
                out var parentMessageKey))
        {
            return new CreateAdOrganizationalUnitResult(
                false,
                parentMessageKey,
                null,
                AdDirectoryFailureKind.InvalidRequest);
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new CreateAdOrganizationalUnitResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var connection = connectionResult.Context.Connection;
        var parentDn = request.ParentDistinguishedName.Trim();
        var ouName = request.Name.Trim();

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);

            if (!TryValidateParentExists(ldapConnection, parentDn))
            {
                return await FailOrganizationalUnitCreateAsync(
                    request,
                    connection,
                    AdManagementApiMessageKeys.OrganizationalUnits.InvalidParent,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildCreateFailureJson(
                        "ValidateParent",
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "The parent organizational unit could not be found."),
                    cancellationToken);
            }

            if (ExistsOrganizationalUnitWithNameUnderParent(ldapConnection, parentDn, ouName))
            {
                return await FailOrganizationalUnitCreateAsync(
                    request,
                    connection,
                    AdManagementApiMessageKeys.OrganizationalUnits.NameCollision,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildCreateFailureJson(
                        "Preflight",
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "An organizational unit with the same name already exists under the parent."),
                    cancellationToken);
            }

            var distinguishedName = AdLdapDnHelper.BuildOuDistinguishedName(ouName, parentDn);
            try
            {
                ExecuteCreateOrganizationalUnit(ldapConnection, distinguishedName, ouName);
            }
            catch (OrganizationalUnitLdapException ex)
            {
                return await FailOrganizationalUnitCreateAsync(
                    request,
                    connection,
                    ex.MessageKey,
                    ex.FailureKind,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildCreateFailureJson(
                        "CreateOrganizationalUnit",
                        ex.NormalizedReason,
                        ex.EnglishMessage,
                        distinguishedName,
                        ex.LdapResultCode,
                        ex.LdapExceptionErrorCode,
                        ex.LdapDiagnosticMessage),
                    cancellationToken);
            }

            if (!TryGetObjectGuidByDistinguishedName(ldapConnection, distinguishedName, out var objectGuid))
            {
                return await FailOrganizationalUnitCreateAsync(
                    request,
                    connection,
                    AdManagementApiMessageKeys.OrganizationalUnits.CreateFailed,
                    AdDirectoryFailureKind.ConnectionFailed,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildCreateFailureJson(
                        "ReloadOrganizationalUnit",
                        AdUserUpdateNormalizedReasons.Unknown,
                        "The organizational unit was created but could not be reloaded."),
                    cancellationToken);
            }

            var detailResult = await GetOrganizationalUnitByIdAsync(objectGuid, cancellationToken);
            if (!detailResult.IsSuccess || detailResult.OrganizationalUnit is null)
            {
                return await FailOrganizationalUnitCreateAsync(
                    request,
                    connection,
                    AdManagementApiMessageKeys.OrganizationalUnits.CreateFailed,
                    detailResult.FailureKind ?? AdDirectoryFailureKind.ConnectionFailed,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildCreateFailureJson(
                        "ReloadOrganizationalUnit",
                        AdUserUpdateNormalizedReasons.Unknown,
                        "The organizational unit was created but could not be reloaded."),
                    cancellationToken);
            }

            await WriteOrganizationalUnitCreateSuccessLogsAsync(
                request,
                connection,
                detailResult.OrganizationalUnit,
                cancellationToken);

            return new CreateAdOrganizationalUnitResult(true, string.Empty, detailResult.OrganizationalUnit);
        }
        catch (LdapException ex)
        {
            var messageKey = AdLdapErrorNormalizer.NormalizeMessageKey(ex.ErrorCode, ex.Message);
            return await FailOrganizationalUnitCreateAsync(
                request,
                connection,
                messageKey,
                AdDirectoryFailureKind.ConnectionFailed,
                AdOrganizationalUnitOperationDiagnosticBuilder.BuildCreateFailureJson(
                    "CreateOrganizationalUnit",
                    AdUserUpdateNormalizedReasons.ConnectionFailed,
                    "The organizational unit could not be created.",
                    ldapResultCode: ex.ErrorCode,
                    ldapExceptionErrorCode: ex.ErrorCode,
                    ldapDiagnosticMessage: ex.Message),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AD organizational unit create unexpected failure. ActorUserId={ActorUserId}", request.ActorUserId);
            return await FailOrganizationalUnitCreateAsync(
                request,
                connection,
                AdManagementApiMessageKeys.OrganizationalUnits.CreateFailed,
                AdDirectoryFailureKind.ConnectionFailed,
                AdOrganizationalUnitOperationDiagnosticBuilder.BuildCreateFailureJson(
                    "CreateOrganizationalUnit",
                    AdUserUpdateNormalizedReasons.Unknown,
                    "The organizational unit could not be created."),
                cancellationToken);
        }
    }

    public async Task<RenameAdOrganizationalUnitResult> RenameOrganizationalUnitAsync(
        RenameAdOrganizationalUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdOrganizationalUnitRequestValidator.TryValidateName(request.Name, out var nameMessageKey))
        {
            return new RenameAdOrganizationalUnitResult(
                false,
                nameMessageKey,
                null,
                null,
                AdDirectoryFailureKind.InvalidRequest);
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new RenameAdOrganizationalUnitResult(
                false,
                connectionResult.MessageKey,
                null,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var connection = connectionResult.Context.Connection;
        var searchBase = ResolveOrganizationalUnitsSearchBase(connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new RenameAdOrganizationalUnitResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            if (!TryLoadOrganizationalUnitDetail(
                    ldapConnection,
                    searchBase,
                    request.OrganizationalUnitId,
                    out var beforeDetail))
            {
                return await FailOrganizationalUnitRenameAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.NotFound,
                    AdDirectoryFailureKind.NotFound,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildRenameFailureJson(
                        "LoadOrganizationalUnit",
                        request.OrganizationalUnitId,
                        null,
                        AdUserUpdateNormalizedReasons.NoSuchObject,
                        "The organizational unit could not be found."),
                    cancellationToken);
            }

            var distinguishedName = beforeDetail.DistinguishedName;
            if (!IsOrganizationalUnitOperationAllowed(connection, distinguishedName))
            {
                return await FailOrganizationalUnitRenameAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.ProtectedObject,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildRenameFailureJson(
                        "ValidateProtected",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "The organizational unit is protected and cannot be renamed."),
                    cancellationToken);
            }

            var parentDn = beforeDetail.ParentDistinguishedName;
            if (string.IsNullOrWhiteSpace(parentDn))
            {
                return await FailOrganizationalUnitRenameAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.ProtectedObject,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildRenameFailureJson(
                        "ValidateParent",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "The organizational unit parent is missing."),
                    cancellationToken);
            }

            var newName = request.Name.Trim();
            if (ExistsOrganizationalUnitWithNameUnderParent(
                    ldapConnection,
                    parentDn,
                    newName,
                    distinguishedName))
            {
                return await FailOrganizationalUnitRenameAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.NameCollision,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildRenameFailureJson(
                        "Preflight",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "An organizational unit with the same name already exists under the parent."),
                    cancellationToken);
            }

            var newRdn = AdLdapDnHelper.BuildOuRdn(newName);
            try
            {
                ExecuteRenameOrganizationalUnit(ldapConnection, distinguishedName, parentDn, newRdn);
            }
            catch (OrganizationalUnitLdapException ex)
            {
                return await FailOrganizationalUnitRenameAsync(
                    request,
                    connection,
                    beforeDetail,
                    ex.MessageKey,
                    ex.FailureKind,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildRenameFailureJson(
                        "RenameOrganizationalUnit",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        ex.NormalizedReason,
                        ex.EnglishMessage,
                        ex.LdapResultCode,
                        ex.LdapExceptionErrorCode,
                        ex.LdapDiagnosticMessage),
                    cancellationToken);
            }

            var afterDetailResult = await GetOrganizationalUnitByIdAsync(request.OrganizationalUnitId, cancellationToken);
            if (!afterDetailResult.IsSuccess || afterDetailResult.OrganizationalUnit is null)
            {
                return await FailOrganizationalUnitRenameAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.RenameFailed,
                    afterDetailResult.FailureKind ?? AdDirectoryFailureKind.ConnectionFailed,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildRenameFailureJson(
                        "ReloadOrganizationalUnit",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        AdUserUpdateNormalizedReasons.Unknown,
                        "The organizational unit was renamed but could not be reloaded."),
                    cancellationToken);
            }

            await WriteOrganizationalUnitRenameSuccessLogsAsync(
                request,
                connection,
                beforeDetail,
                afterDetailResult.OrganizationalUnit,
                cancellationToken);

            return new RenameAdOrganizationalUnitResult(
                true,
                string.Empty,
                afterDetailResult.OrganizationalUnit,
                distinguishedName);
        }
        catch (LdapException ex)
        {
            var messageKey = AdLdapErrorNormalizer.NormalizeMessageKey(ex.ErrorCode, ex.Message);
            return await FailOrganizationalUnitRenameAsync(
                request,
                connection,
                null,
                messageKey,
                AdDirectoryFailureKind.ConnectionFailed,
                AdOrganizationalUnitOperationDiagnosticBuilder.BuildRenameFailureJson(
                    "RenameOrganizationalUnit",
                    request.OrganizationalUnitId,
                    null,
                    AdUserUpdateNormalizedReasons.ConnectionFailed,
                    "The organizational unit could not be renamed.",
                    ex.ErrorCode,
                    ex.ErrorCode,
                    ex.Message),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "AD organizational unit rename unexpected failure. OrganizationalUnitId={OrganizationalUnitId}",
                request.OrganizationalUnitId);
            return await FailOrganizationalUnitRenameAsync(
                request,
                connection,
                null,
                AdManagementApiMessageKeys.OrganizationalUnits.RenameFailed,
                AdDirectoryFailureKind.ConnectionFailed,
                AdOrganizationalUnitOperationDiagnosticBuilder.BuildRenameFailureJson(
                    "RenameOrganizationalUnit",
                    request.OrganizationalUnitId,
                    null,
                    AdUserUpdateNormalizedReasons.Unknown,
                    "The organizational unit could not be renamed."),
                cancellationToken);
        }
    }

    public async Task<MoveAdOrganizationalUnitResult> MoveOrganizationalUnitAsync(
        MoveAdOrganizationalUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdOrganizationalUnitRequestValidator.TryValidateTargetParentDistinguishedName(
                request.TargetParentDistinguishedName,
                out var parentMessageKey))
        {
            return new MoveAdOrganizationalUnitResult(
                false,
                parentMessageKey,
                null,
                null,
                request.TargetParentDistinguishedName,
                AdDirectoryFailureKind.InvalidRequest);
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new MoveAdOrganizationalUnitResult(
                false,
                connectionResult.MessageKey,
                null,
                null,
                request.TargetParentDistinguishedName,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var connection = connectionResult.Context.Connection;
        var searchBase = ResolveOrganizationalUnitsSearchBase(connection);
        var targetParentDn = request.TargetParentDistinguishedName.Trim();

        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new MoveAdOrganizationalUnitResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                null,
                targetParentDn,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            if (!TryLoadOrganizationalUnitDetail(
                    ldapConnection,
                    searchBase,
                    request.OrganizationalUnitId,
                    out var beforeDetail))
            {
                return await FailOrganizationalUnitMoveAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.NotFound,
                    targetParentDn,
                    AdDirectoryFailureKind.NotFound,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildMoveFailureJson(
                        "LoadOrganizationalUnit",
                        request.OrganizationalUnitId,
                        null,
                        AdUserUpdateNormalizedReasons.NoSuchObject,
                        "The organizational unit could not be found."),
                    cancellationToken);
            }

            var distinguishedName = beforeDetail.DistinguishedName;
            if (!IsOrganizationalUnitOperationAllowed(connection, distinguishedName))
            {
                return await FailOrganizationalUnitMoveAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.ProtectedObject,
                    targetParentDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildMoveFailureJson(
                        "ValidateProtected",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "The organizational unit is protected and cannot be moved."),
                    cancellationToken);
            }

            if (AdLdapDnHelper.AreDistinguishedNamesEqual(beforeDetail.ParentDistinguishedName, targetParentDn))
            {
                return await FailOrganizationalUnitMoveAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.AlreadyInTargetParent,
                    targetParentDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildMoveFailureJson(
                        "ValidateTargetParent",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "The organizational unit is already under the target parent."),
                    cancellationToken);
            }

            if (AdOrganizationalUnitGuard.IsInvalidMoveTarget(distinguishedName, targetParentDn))
            {
                return await FailOrganizationalUnitMoveAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.InvalidMoveTarget,
                    targetParentDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildMoveFailureJson(
                        "ValidateMoveTarget",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "The target parent cannot be the organizational unit itself or one of its descendants."),
                    cancellationToken);
            }

            if (!TryValidateParentExists(ldapConnection, targetParentDn))
            {
                return await FailOrganizationalUnitMoveAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.InvalidTargetParent,
                    targetParentDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildMoveFailureJson(
                        "ValidateTargetParent",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "The target parent organizational unit could not be found."),
                    cancellationToken);
            }

            var ouName = beforeDetail.Ou ?? beforeDetail.Name;
            if (!string.IsNullOrWhiteSpace(ouName)
                && ExistsOrganizationalUnitWithNameUnderParent(ldapConnection, targetParentDn, ouName))
            {
                return await FailOrganizationalUnitMoveAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.NameCollision,
                    targetParentDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildMoveFailureJson(
                        "Preflight",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "An organizational unit with the same name already exists under the target parent."),
                    cancellationToken);
            }

            var currentRdn = AdLdapDnHelper.GetRelativeDistinguishedName(distinguishedName);
            if (string.IsNullOrWhiteSpace(currentRdn))
            {
                return await FailOrganizationalUnitMoveAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.MoveFailed,
                    targetParentDn,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildMoveFailureJson(
                        "ValidateRdn",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "The organizational unit relative distinguished name is missing."),
                    cancellationToken);
            }

            try
            {
                ExecuteMoveOrganizationalUnit(ldapConnection, distinguishedName, targetParentDn, currentRdn);
            }
            catch (OrganizationalUnitLdapException ex)
            {
                return await FailOrganizationalUnitMoveAsync(
                    request,
                    connection,
                    beforeDetail,
                    ex.MessageKey,
                    targetParentDn,
                    ex.FailureKind,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildMoveFailureJson(
                        "MoveOrganizationalUnit",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        ex.NormalizedReason,
                        ex.EnglishMessage,
                        ex.LdapResultCode,
                        ex.LdapExceptionErrorCode,
                        ex.LdapDiagnosticMessage),
                    cancellationToken);
            }

            var afterDetailResult = await GetOrganizationalUnitByIdAsync(request.OrganizationalUnitId, cancellationToken);
            if (!afterDetailResult.IsSuccess || afterDetailResult.OrganizationalUnit is null)
            {
                return await FailOrganizationalUnitMoveAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.MoveFailed,
                    targetParentDn,
                    afterDetailResult.FailureKind ?? AdDirectoryFailureKind.ConnectionFailed,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildMoveFailureJson(
                        "ReloadOrganizationalUnit",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        AdUserUpdateNormalizedReasons.Unknown,
                        "The organizational unit was moved but could not be reloaded."),
                    cancellationToken);
            }

            await WriteOrganizationalUnitMoveSuccessLogsAsync(
                request,
                connection,
                beforeDetail,
                afterDetailResult.OrganizationalUnit,
                cancellationToken);

            return new MoveAdOrganizationalUnitResult(
                true,
                string.Empty,
                afterDetailResult.OrganizationalUnit,
                distinguishedName,
                targetParentDn);
        }
        catch (LdapException ex)
        {
            var messageKey = AdLdapErrorNormalizer.NormalizeMessageKey(ex.ErrorCode, ex.Message);
            return await FailOrganizationalUnitMoveAsync(
                request,
                connection,
                null,
                messageKey,
                targetParentDn,
                AdDirectoryFailureKind.ConnectionFailed,
                AdOrganizationalUnitOperationDiagnosticBuilder.BuildMoveFailureJson(
                    "MoveOrganizationalUnit",
                    request.OrganizationalUnitId,
                    null,
                    AdUserUpdateNormalizedReasons.ConnectionFailed,
                    "The organizational unit could not be moved.",
                    ex.ErrorCode,
                    ex.ErrorCode,
                    ex.Message),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "AD organizational unit move unexpected failure. OrganizationalUnitId={OrganizationalUnitId}",
                request.OrganizationalUnitId);
            return await FailOrganizationalUnitMoveAsync(
                request,
                connection,
                null,
                AdManagementApiMessageKeys.OrganizationalUnits.MoveFailed,
                targetParentDn,
                AdDirectoryFailureKind.ConnectionFailed,
                AdOrganizationalUnitOperationDiagnosticBuilder.BuildMoveFailureJson(
                    "MoveOrganizationalUnit",
                    request.OrganizationalUnitId,
                    null,
                    AdUserUpdateNormalizedReasons.Unknown,
                    "The organizational unit could not be moved."),
                cancellationToken);
        }
    }

    public async Task<DeleteAdOrganizationalUnitResult> DeleteOrganizationalUnitAsync(
        DeleteAdOrganizationalUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new DeleteAdOrganizationalUnitResult(
                false,
                connectionResult.MessageKey,
                null,
                connectionResult.FailureKind,
                connectionResult.MessageParams);
        }

        var connection = connectionResult.Context.Connection;
        var searchBase = ResolveOrganizationalUnitsSearchBase(connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new DeleteAdOrganizationalUnitResult(
                false,
                AdManagementApiMessageKeys.Common.NotConfigured,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            if (!TryLoadOrganizationalUnitDetail(
                    ldapConnection,
                    searchBase,
                    request.OrganizationalUnitId,
                    out var beforeDetail))
            {
                return await FailOrganizationalUnitDeleteAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.NotFound,
                    AdDirectoryFailureKind.NotFound,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildDeleteFailureJson(
                        "LoadOrganizationalUnit",
                        request.OrganizationalUnitId,
                        null,
                        AdUserUpdateNormalizedReasons.NoSuchObject,
                        "The organizational unit could not be found."),
                    cancellationToken);
            }

            var distinguishedName = beforeDetail.DistinguishedName;
            if (!IsOrganizationalUnitOperationAllowed(connection, distinguishedName))
            {
                return await FailOrganizationalUnitDeleteAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.ProtectedObject,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildDeleteFailureJson(
                        "ValidateProtected",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "The organizational unit is protected and cannot be deleted."),
                    cancellationToken);
            }

            if (OrganizationalUnitHasChildren(ldapConnection, distinguishedName))
            {
                return await FailOrganizationalUnitDeleteAsync(
                    request,
                    connection,
                    beforeDetail,
                    AdManagementApiMessageKeys.OrganizationalUnits.NotEmpty,
                    AdDirectoryFailureKind.InvalidRequest,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildDeleteFailureJson(
                        "Preflight",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        AdUserUpdateNormalizedReasons.InvalidRequest,
                        "The organizational unit is not empty."),
                    cancellationToken,
                    new Dictionary<string, object>
                    {
                        ["childOuCount"] = beforeDetail.ContentSummary.ChildOuCount,
                        ["userCount"] = beforeDetail.ContentSummary.UserCount,
                        ["groupCount"] = beforeDetail.ContentSummary.GroupCount,
                        ["computerCount"] = beforeDetail.ContentSummary.ComputerCount,
                    });
            }

            try
            {
                ExecuteDeleteOrganizationalUnit(ldapConnection, distinguishedName);
            }
            catch (OrganizationalUnitLdapException ex)
            {
                return await FailOrganizationalUnitDeleteAsync(
                    request,
                    connection,
                    beforeDetail,
                    ex.MessageKey,
                    ex.FailureKind,
                    AdOrganizationalUnitOperationDiagnosticBuilder.BuildDeleteFailureJson(
                        "DeleteOrganizationalUnit",
                        request.OrganizationalUnitId,
                        distinguishedName,
                        ex.NormalizedReason,
                        ex.EnglishMessage,
                        ex.LdapResultCode,
                        ex.LdapExceptionErrorCode,
                        ex.LdapDiagnosticMessage),
                    cancellationToken);
            }

            await WriteOrganizationalUnitDeleteSuccessLogsAsync(
                request,
                connection,
                beforeDetail,
                cancellationToken);

            return new DeleteAdOrganizationalUnitResult(
                true,
                string.Empty,
                beforeDetail.ObjectGuid);
        }
        catch (LdapException ex)
        {
            var messageKey = AdLdapErrorNormalizer.NormalizeMessageKey(ex.ErrorCode, ex.Message);
            return await FailOrganizationalUnitDeleteAsync(
                request,
                connection,
                null,
                messageKey,
                AdDirectoryFailureKind.ConnectionFailed,
                AdOrganizationalUnitOperationDiagnosticBuilder.BuildDeleteFailureJson(
                    "DeleteOrganizationalUnit",
                    request.OrganizationalUnitId,
                    null,
                    AdUserUpdateNormalizedReasons.ConnectionFailed,
                    "The organizational unit could not be deleted.",
                    ex.ErrorCode,
                    ex.ErrorCode,
                    ex.Message),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "AD organizational unit delete unexpected failure. OrganizationalUnitId={OrganizationalUnitId}",
                request.OrganizationalUnitId);
            return await FailOrganizationalUnitDeleteAsync(
                request,
                connection,
                null,
                AdManagementApiMessageKeys.OrganizationalUnits.DeleteFailed,
                AdDirectoryFailureKind.ConnectionFailed,
                AdOrganizationalUnitOperationDiagnosticBuilder.BuildDeleteFailureJson(
                    "DeleteOrganizationalUnit",
                    request.OrganizationalUnitId,
                    null,
                    AdUserUpdateNormalizedReasons.Unknown,
                    "The organizational unit could not be deleted."),
                cancellationToken);
        }
    }

    private bool IsOrganizationalUnitOperationAllowed(
        AdManagementConnectionParameters connection,
        string distinguishedName) =>
        AdOrganizationalUnitGuard.IsManagedOrganizationalUnit(distinguishedName)
        && !AdOrganizationalUnitGuard.IsConfiguredRootProtected(
            distinguishedName,
            connection.BaseDn,
            connection.DefaultNamingContext);

    private static void ExecuteCreateOrganizationalUnit(
        LdapConnection ldapConnection,
        string distinguishedName,
        string ouName)
    {
        var addRequest = new AddRequest(distinguishedName);
        addRequest.Attributes.Add(new DirectoryAttribute("objectClass", "top", "organizationalUnit"));
        addRequest.Attributes.Add(new DirectoryAttribute("ou", ouName));
        addRequest.Attributes.Add(new DirectoryAttribute("name", ouName));
        SendOrganizationalUnitRequest(ldapConnection, addRequest, "The organizational unit could not be created.");
    }

    private static void ExecuteRenameOrganizationalUnit(
        LdapConnection ldapConnection,
        string distinguishedName,
        string parentDistinguishedName,
        string newRdn)
    {
        var modifyDnRequest = new ModifyDNRequest(distinguishedName, parentDistinguishedName, newRdn)
        {
            DeleteOldRdn = true,
        };
        SendOrganizationalUnitRequest(ldapConnection, modifyDnRequest, "The organizational unit could not be renamed.");
    }

    private static void ExecuteMoveOrganizationalUnit(
        LdapConnection ldapConnection,
        string distinguishedName,
        string targetParentDistinguishedName,
        string currentRdn)
    {
        var modifyDnRequest = new ModifyDNRequest(
            distinguishedName,
            targetParentDistinguishedName,
            currentRdn)
        {
            DeleteOldRdn = true,
        };
        SendOrganizationalUnitRequest(ldapConnection, modifyDnRequest, "The organizational unit could not be moved.");
    }

    private static void ExecuteDeleteOrganizationalUnit(LdapConnection ldapConnection, string distinguishedName)
    {
        var deleteRequest = new DeleteRequest(distinguishedName);
        SendOrganizationalUnitRequest(ldapConnection, deleteRequest, "The organizational unit could not be deleted.");
    }

    private static void SendOrganizationalUnitRequest(
        LdapConnection ldapConnection,
        DirectoryRequest request,
        string englishMessage)
    {
        var response = (DirectoryResponse)ldapConnection.SendRequest(request);
        if (response.ResultCode == ResultCode.Success)
        {
            return;
        }

        var messageKey = AdLdapErrorNormalizer.NormalizeMessageKey(
            (int)response.ResultCode,
            response.ErrorMessage);
        throw new OrganizationalUnitLdapException(
            MapOrganizationalUnitFailureKind(response.ResultCode),
            ResolveOrganizationalUnitNormalizedReason(response.ResultCode),
            englishMessage,
            messageKey,
            (int)response.ResultCode,
            (int)response.ResultCode,
            response.ErrorMessage);
    }

    private static bool TryGetObjectGuidByDistinguishedName(
        LdapConnection ldapConnection,
        string distinguishedName,
        out Guid objectGuid)
    {
        objectGuid = Guid.Empty;
        var searchRequest = new SearchRequest(
            distinguishedName,
            "(objectClass=*)",
            SearchScope.Base,
            "objectGUID")
        {
            SizeLimit = 1,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        return TryGetObjectGuid(response.Entries[0], out objectGuid);
    }

    private static AdDirectoryFailureKind MapOrganizationalUnitFailureKind(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.NoSuchObject => AdDirectoryFailureKind.NotFound,
            ResultCode.EntryAlreadyExists or ResultCode.NamingViolation or ResultCode.ObjectClassViolation =>
                AdDirectoryFailureKind.InvalidRequest,
            ResultCode.Unavailable or ResultCode.TimeLimitExceeded or ResultCode.Busy =>
                AdDirectoryFailureKind.ConnectionFailed,
            _ => AdDirectoryFailureKind.InvalidRequest,
        };

    private static string ResolveOrganizationalUnitNormalizedReason(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.NoSuchObject => AdUserUpdateNormalizedReasons.NoSuchObject,
            ResultCode.EntryAlreadyExists => AdUserUpdateNormalizedReasons.DuplicateValue,
            ResultCode.InvalidDNSyntax or ResultCode.NamingViolation =>
                AdUserUpdateNormalizedReasons.InvalidDnSyntax,
            ResultCode.InsufficientAccessRights => AdUserUpdateNormalizedReasons.InsufficientAccessRights,
            ResultCode.Unavailable or ResultCode.TimeLimitExceeded or ResultCode.Busy =>
                AdUserUpdateNormalizedReasons.ConnectionFailed,
            _ => AdUserUpdateNormalizedReasons.Unknown,
        };

    private async Task<CreateAdOrganizationalUnitResult> FailOrganizationalUnitCreateAsync(
        CreateAdOrganizationalUnitRequest request,
        AdManagementConnectionParameters connection,
        string messageKey,
        AdDirectoryFailureKind failureKind,
        string operationDiagnosticJson,
        CancellationToken cancellationToken)
    {
        await WriteOrganizationalUnitCreateFailureLogsAsync(
            request,
            connection,
            operationDiagnosticJson,
            cancellationToken);
        return new CreateAdOrganizationalUnitResult(false, messageKey, null, failureKind);
    }

    private async Task<RenameAdOrganizationalUnitResult> FailOrganizationalUnitRenameAsync(
        RenameAdOrganizationalUnitRequest request,
        AdManagementConnectionParameters connection,
        AdOrganizationalUnitDetail? beforeDetail,
        string messageKey,
        AdDirectoryFailureKind failureKind,
        string operationDiagnosticJson,
        CancellationToken cancellationToken)
    {
        await WriteOrganizationalUnitRenameFailureLogsAsync(
            request,
            connection,
            beforeDetail,
            operationDiagnosticJson,
            cancellationToken);
        return new RenameAdOrganizationalUnitResult(
            false,
            messageKey,
            null,
            beforeDetail?.DistinguishedName,
            failureKind);
    }

    private async Task<MoveAdOrganizationalUnitResult> FailOrganizationalUnitMoveAsync(
        MoveAdOrganizationalUnitRequest request,
        AdManagementConnectionParameters connection,
        AdOrganizationalUnitDetail? beforeDetail,
        string messageKey,
        string targetParentDn,
        AdDirectoryFailureKind failureKind,
        string operationDiagnosticJson,
        CancellationToken cancellationToken)
    {
        await WriteOrganizationalUnitMoveFailureLogsAsync(
            request,
            connection,
            beforeDetail,
            operationDiagnosticJson,
            cancellationToken);
        return new MoveAdOrganizationalUnitResult(
            false,
            messageKey,
            null,
            beforeDetail?.DistinguishedName,
            targetParentDn,
            failureKind);
    }

    private async Task<DeleteAdOrganizationalUnitResult> FailOrganizationalUnitDeleteAsync(
        DeleteAdOrganizationalUnitRequest request,
        AdManagementConnectionParameters connection,
        AdOrganizationalUnitDetail? beforeDetail,
        string messageKey,
        AdDirectoryFailureKind failureKind,
        string operationDiagnosticJson,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        await WriteOrganizationalUnitDeleteFailureLogsAsync(
            request,
            connection,
            beforeDetail,
            operationDiagnosticJson,
            cancellationToken);
        return new DeleteAdOrganizationalUnitResult(false, messageKey, null, failureKind, messageParams);
    }

    private async Task WriteOrganizationalUnitCreateSuccessLogsAsync(
        CreateAdOrganizationalUnitRequest request,
        AdManagementConnectionParameters connection,
        AdOrganizationalUnitDetail detail,
        CancellationToken cancellationToken)
    {
        try
        {
            await adOperationLogService.WriteAsync(
                new AdOperationLogEntry
                {
                    OperationType = AdManagementOperationTypes.OrganizationalUnitCreate,
                    Status = AdManagementOperationStatuses.Succeeded,
                    TargetObjectType = AdManagementTargetOrganizationalUnitTypes.AdOrganizationalUnit,
                    TargetDistinguishedName = detail.DistinguishedName,
                    TargetObjectGuid = detail.ObjectGuid,
                    TargetSamAccountName = detail.Name,
                    RequestSummaryJson = AdOrganizationalUnitSnapshotBuilder.BuildCreateRequestSummary(request),
                    BeforeSnapshotJson = null,
                    AfterSnapshotJson = AdOrganizationalUnitSnapshotBuilder.BuildOperationSnapshot(
                        AdManagementOperationTypes.OrganizationalUnitCreate,
                        detail),
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                    DomainController = ResolvePrimaryHost(connection),
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, OrganizationalUnitCreateSuccessLoggingFailedMessage);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = "Create",
                    EntityName = "AdOrganizationalUnit",
                    EntityId = detail.ObjectGuid,
                    Description = $"AD organizational unit created: {detail.CanonicalName}",
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, OrganizationalUnitCreateSuccessLoggingFailedMessage);
        }
    }

    private async Task WriteOrganizationalUnitCreateFailureLogsAsync(
        CreateAdOrganizationalUnitRequest request,
        AdManagementConnectionParameters connection,
        string operationDiagnosticJson,
        CancellationToken cancellationToken)
    {
        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.OrganizationalUnitCreate,
                Status = AdManagementOperationStatuses.Failed,
                TargetObjectType = AdManagementTargetOrganizationalUnitTypes.AdOrganizationalUnit,
                RequestSummaryJson = AdOrganizationalUnitSnapshotBuilder.BuildCreateRequestSummary(request),
                ErrorCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(operationDiagnosticJson),
                ErrorMessage = operationDiagnosticJson,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
                DomainController = ResolvePrimaryHost(connection),
            },
            cancellationToken);
    }

    private async Task WriteOrganizationalUnitRenameSuccessLogsAsync(
        RenameAdOrganizationalUnitRequest request,
        AdManagementConnectionParameters connection,
        AdOrganizationalUnitDetail beforeDetail,
        AdOrganizationalUnitDetail afterDetail,
        CancellationToken cancellationToken)
    {
        try
        {
            await adOperationLogService.WriteAsync(
                new AdOperationLogEntry
                {
                    OperationType = AdManagementOperationTypes.OrganizationalUnitRename,
                    Status = AdManagementOperationStatuses.Succeeded,
                    TargetObjectType = AdManagementTargetOrganizationalUnitTypes.AdOrganizationalUnit,
                    TargetDistinguishedName = afterDetail.DistinguishedName,
                    TargetObjectGuid = afterDetail.ObjectGuid,
                    TargetSamAccountName = afterDetail.Name,
                    RequestSummaryJson = AdOrganizationalUnitSnapshotBuilder.BuildRenameRequestSummary(
                        request,
                        beforeDetail.DistinguishedName),
                    BeforeSnapshotJson = AdOrganizationalUnitSnapshotBuilder.BuildOperationSnapshot(
                        AdManagementOperationTypes.OrganizationalUnitRename,
                        beforeDetail),
                    AfterSnapshotJson = AdOrganizationalUnitSnapshotBuilder.BuildOperationSnapshot(
                        AdManagementOperationTypes.OrganizationalUnitRename,
                        afterDetail),
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                    DomainController = ResolvePrimaryHost(connection),
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, OrganizationalUnitRenameSuccessLoggingFailedMessage);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = "Update",
                    EntityName = "AdOrganizationalUnit",
                    EntityId = afterDetail.ObjectGuid,
                    Description = $"AD organizational unit renamed: {beforeDetail.CanonicalName} -> {afterDetail.CanonicalName}",
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, OrganizationalUnitRenameSuccessLoggingFailedMessage);
        }
    }

    private async Task WriteOrganizationalUnitRenameFailureLogsAsync(
        RenameAdOrganizationalUnitRequest request,
        AdManagementConnectionParameters connection,
        AdOrganizationalUnitDetail? beforeDetail,
        string operationDiagnosticJson,
        CancellationToken cancellationToken)
    {
        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.OrganizationalUnitRename,
                Status = AdManagementOperationStatuses.Failed,
                TargetObjectType = AdManagementTargetOrganizationalUnitTypes.AdOrganizationalUnit,
                TargetDistinguishedName = beforeDetail?.DistinguishedName,
                TargetObjectGuid = beforeDetail?.ObjectGuid ?? request.OrganizationalUnitId.ToString("D"),
                TargetSamAccountName = beforeDetail?.Name,
                RequestSummaryJson = AdOrganizationalUnitSnapshotBuilder.BuildRenameRequestSummary(
                    request,
                    beforeDetail?.DistinguishedName),
                BeforeSnapshotJson = AdOrganizationalUnitSnapshotBuilder.BuildOperationSnapshot(
                    AdManagementOperationTypes.OrganizationalUnitRename,
                    beforeDetail),
                ErrorCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(operationDiagnosticJson),
                ErrorMessage = operationDiagnosticJson,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
                DomainController = ResolvePrimaryHost(connection),
            },
            cancellationToken);
    }

    private async Task WriteOrganizationalUnitMoveSuccessLogsAsync(
        MoveAdOrganizationalUnitRequest request,
        AdManagementConnectionParameters connection,
        AdOrganizationalUnitDetail beforeDetail,
        AdOrganizationalUnitDetail afterDetail,
        CancellationToken cancellationToken)
    {
        try
        {
            await adOperationLogService.WriteAsync(
                new AdOperationLogEntry
                {
                    OperationType = AdManagementOperationTypes.OrganizationalUnitMove,
                    Status = AdManagementOperationStatuses.Succeeded,
                    TargetObjectType = AdManagementTargetOrganizationalUnitTypes.AdOrganizationalUnit,
                    TargetDistinguishedName = afterDetail.DistinguishedName,
                    TargetObjectGuid = afterDetail.ObjectGuid,
                    TargetSamAccountName = afterDetail.Name,
                    RequestSummaryJson = AdOrganizationalUnitSnapshotBuilder.BuildMoveRequestSummary(
                        request,
                        beforeDetail.DistinguishedName),
                    BeforeSnapshotJson = AdOrganizationalUnitSnapshotBuilder.BuildOperationSnapshot(
                        AdManagementOperationTypes.OrganizationalUnitMove,
                        beforeDetail),
                    AfterSnapshotJson = AdOrganizationalUnitSnapshotBuilder.BuildOperationSnapshot(
                        AdManagementOperationTypes.OrganizationalUnitMove,
                        afterDetail),
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                    DomainController = ResolvePrimaryHost(connection),
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, OrganizationalUnitMoveSuccessLoggingFailedMessage);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = "MoveOu",
                    EntityName = "AdOrganizationalUnit",
                    EntityId = afterDetail.ObjectGuid,
                    Description = $"AD organizational unit moved: {beforeDetail.CanonicalName} -> {afterDetail.CanonicalName}",
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, OrganizationalUnitMoveSuccessLoggingFailedMessage);
        }
    }

    private async Task WriteOrganizationalUnitMoveFailureLogsAsync(
        MoveAdOrganizationalUnitRequest request,
        AdManagementConnectionParameters connection,
        AdOrganizationalUnitDetail? beforeDetail,
        string operationDiagnosticJson,
        CancellationToken cancellationToken)
    {
        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.OrganizationalUnitMove,
                Status = AdManagementOperationStatuses.Failed,
                TargetObjectType = AdManagementTargetOrganizationalUnitTypes.AdOrganizationalUnit,
                TargetDistinguishedName = beforeDetail?.DistinguishedName,
                TargetObjectGuid = beforeDetail?.ObjectGuid ?? request.OrganizationalUnitId.ToString("D"),
                TargetSamAccountName = beforeDetail?.Name,
                RequestSummaryJson = AdOrganizationalUnitSnapshotBuilder.BuildMoveRequestSummary(
                    request,
                    beforeDetail?.DistinguishedName),
                BeforeSnapshotJson = AdOrganizationalUnitSnapshotBuilder.BuildOperationSnapshot(
                    AdManagementOperationTypes.OrganizationalUnitMove,
                    beforeDetail),
                ErrorCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(operationDiagnosticJson),
                ErrorMessage = operationDiagnosticJson,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
                DomainController = ResolvePrimaryHost(connection),
            },
            cancellationToken);
    }

    private async Task WriteOrganizationalUnitDeleteSuccessLogsAsync(
        DeleteAdOrganizationalUnitRequest request,
        AdManagementConnectionParameters connection,
        AdOrganizationalUnitDetail beforeDetail,
        CancellationToken cancellationToken)
    {
        try
        {
            await adOperationLogService.WriteAsync(
                new AdOperationLogEntry
                {
                    OperationType = AdManagementOperationTypes.OrganizationalUnitDelete,
                    Status = AdManagementOperationStatuses.Succeeded,
                    TargetObjectType = AdManagementTargetOrganizationalUnitTypes.AdOrganizationalUnit,
                    TargetDistinguishedName = beforeDetail.DistinguishedName,
                    TargetObjectGuid = beforeDetail.ObjectGuid,
                    TargetSamAccountName = beforeDetail.Name,
                    RequestSummaryJson = AdOrganizationalUnitSnapshotBuilder.BuildDeleteRequestSummary(
                        request,
                        beforeDetail),
                    BeforeSnapshotJson = AdOrganizationalUnitSnapshotBuilder.BuildOperationSnapshot(
                        AdManagementOperationTypes.OrganizationalUnitDelete,
                        beforeDetail),
                    AfterSnapshotJson = null,
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                    DomainController = ResolvePrimaryHost(connection),
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, OrganizationalUnitDeleteSuccessLoggingFailedMessage);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = "Delete",
                    EntityName = "AdOrganizationalUnit",
                    EntityId = beforeDetail.ObjectGuid,
                    Description = $"AD organizational unit deleted: {beforeDetail.CanonicalName}",
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, OrganizationalUnitDeleteSuccessLoggingFailedMessage);
        }
    }

    private async Task WriteOrganizationalUnitDeleteFailureLogsAsync(
        DeleteAdOrganizationalUnitRequest request,
        AdManagementConnectionParameters connection,
        AdOrganizationalUnitDetail? beforeDetail,
        string operationDiagnosticJson,
        CancellationToken cancellationToken)
    {
        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.OrganizationalUnitDelete,
                Status = AdManagementOperationStatuses.Failed,
                TargetObjectType = AdManagementTargetOrganizationalUnitTypes.AdOrganizationalUnit,
                TargetDistinguishedName = beforeDetail?.DistinguishedName,
                TargetObjectGuid = beforeDetail?.ObjectGuid ?? request.OrganizationalUnitId.ToString("D"),
                TargetSamAccountName = beforeDetail?.Name,
                RequestSummaryJson = AdOrganizationalUnitSnapshotBuilder.BuildDeleteRequestSummary(
                    request,
                    beforeDetail),
                BeforeSnapshotJson = AdOrganizationalUnitSnapshotBuilder.BuildOperationSnapshot(
                    AdManagementOperationTypes.OrganizationalUnitDelete,
                    beforeDetail),
                ErrorCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(operationDiagnosticJson),
                ErrorMessage = operationDiagnosticJson,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
                DomainController = ResolvePrimaryHost(connection),
            },
            cancellationToken);
    }

    private sealed class OrganizationalUnitLdapException(
        AdDirectoryFailureKind failureKind,
        string normalizedReason,
        string englishMessage,
        string messageKey,
        int? ldapResultCode = null,
        int? ldapExceptionErrorCode = null,
        string? ldapDiagnosticMessage = null) : Exception(englishMessage)
    {
        public string MessageKey { get; } = messageKey;
        public AdDirectoryFailureKind FailureKind { get; } = failureKind;
        public string NormalizedReason { get; } = normalizedReason;
        public string EnglishMessage { get; } = englishMessage;
        public int? LdapResultCode { get; } = ldapResultCode;
        public int? LdapExceptionErrorCode { get; } = ldapExceptionErrorCode;
        public string? LdapDiagnosticMessage { get; } = ldapDiagnosticMessage;
    }
}
