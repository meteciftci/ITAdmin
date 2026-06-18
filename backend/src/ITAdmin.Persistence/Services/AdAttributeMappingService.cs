using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services;

public sealed class AdAttributeMappingService(
    AppDbContext context,
    IAdOperationLogService adOperationLogService) : IAdAttributeMappingService
{
    private const int AuditDescriptionMaxLength = 2000;
    private const int AuditIpAddressMaxLength = 64;
    private const int AuditUserAgentMaxLength = 1024;
    private const int DisplayNameMaxLength = 150;

    internal static readonly Regex LogicalFieldRegex = new("^[a-z][a-zA-Z0-9]{1,63}$", RegexOptions.Compiled);
    internal static readonly Regex AttributeNameRegex = new("^[A-Za-z][A-Za-z0-9_-]{0,63}$", RegexOptions.Compiled);

    internal static readonly HashSet<string> AllowedValidationTypes = new(StringComparer.Ordinal)
    {
        "None",
        "NationalId",
        "Phone",
        "Email",
        "Text",
        "Number"
    };

    internal static readonly HashSet<string> AllowedMaskingStrategies = new(StringComparer.Ordinal)
    {
        "None",
        "Last4",
        "Phone",
        "Email",
        "Hidden"
    };

    public async Task<IReadOnlyList<AdAttributeMappingItem>> GetMappingsAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.AdAttributeMappings
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.LogicalField)
            .Select(x => new AdAttributeMappingItem(
                x.Id,
                x.LogicalField,
                x.DisplayName,
                x.AttributeName,
                x.IsEnabled,
                x.IsEditable,
                x.IsSensitive,
                x.IsSearchable,
                x.ValidationType,
                x.MaskingStrategy,
                x.SortOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdAttributeMappingResult> CreateAsync(
        CreateAdAttributeMappingRequest request,
        CancellationToken cancellationToken = default)
    {
        var logicalField = NormalizeNullable(request.LogicalField);
        var displayName = NormalizeNullable(request.DisplayName);
        var attributeName = NormalizeNullable(request.AttributeName);

        if (string.IsNullOrWhiteSpace(logicalField) || !LogicalFieldRegex.IsMatch(logicalField))
        {
            return new AdAttributeMappingResult(false, AdManagementApiMessageKeys.AttributeMappings.LogicalFieldInvalid, null);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return new AdAttributeMappingResult(false, AdManagementApiMessageKeys.AttributeMappings.DisplayNameRequired, null);
        }

        if (displayName.Length > DisplayNameMaxLength)
        {
            return new AdAttributeMappingResult(
                false,
                AdManagementApiMessageKeys.AttributeMappings.DisplayNameTooLong,
                null);
        }

        if (string.IsNullOrWhiteSpace(attributeName) || !AttributeNameRegex.IsMatch(attributeName))
        {
            return new AdAttributeMappingResult(false, AdManagementApiMessageKeys.AttributeMappings.AttributeNameInvalid, null);
        }

        if (AdReservedCoreAttributes.IsReserved(attributeName))
        {
            return new AdAttributeMappingResult(
                false,
                AdReservedCoreAttributes.ReservedAttributeMappingMessageKey,
                null);
        }

        var validationType = NormalizeOrDefault(request.ValidationType, "None");
        var maskingStrategy = ResolveMaskingStrategy(request.IsSensitive, request.MaskingStrategy);

        if (!AllowedValidationTypes.Contains(validationType))
        {
            return new AdAttributeMappingResult(false, AdManagementApiMessageKeys.AttributeMappings.ValidationTypeInvalid, null);
        }

        var duplicate = await context.AdAttributeMappings
            .AnyAsync(x => x.LogicalField == logicalField, cancellationToken);

        if (duplicate)
        {
            return new AdAttributeMappingResult(
                false,
                AdManagementApiMessageKeys.AttributeMappings.DuplicateLogicalField,
                null);
        }

        var now = DateTime.UtcNow;
        var entity = new AdAttributeMapping
        {
            LogicalField = logicalField,
            DisplayName = displayName,
            AttributeName = attributeName,
            IsEnabled = request.IsEnabled,
            IsEditable = request.IsEditable,
            IsSensitive = request.IsSensitive,
            IsSearchable = ResolveIsSearchable(request.IsSensitive, request.IsSearchable),
            ValidationType = validationType,
            MaskingStrategy = maskingStrategy,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            CreatedBy = request.ActorUserName ?? "system"
        };

        await context.AdAttributeMappings.AddAsync(entity, cancellationToken);

        var description = TruncateAuditDescription(
            $"AD attribute mapping created: {entity.LogicalField}. Attribute: {entity.AttributeName}.");

        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = "Create",
                EntityName = "AdAttributeMapping",
                EntityId = entity.Id.ToString(),
                Description = description,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = TruncateNullable(request.ActorIpAddress, AuditIpAddressMaxLength),
                UserAgent = TruncateNullable(request.ActorUserAgent, AuditUserAgentMaxLength),
                CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
            },
            cancellationToken);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.AttributeMappingCreated,
                Status = AdManagementOperationStatuses.Succeeded,
                TargetObjectType = AdManagementTargetObjectTypes.AdAttributeMapping,
                RequestSummaryJson = AdOperationLogSnapshotBuilder.BuildAttributeMappingCreateRequestSummary(request),
                BeforeSnapshotJson = null,
                AfterSnapshotJson = AdOperationLogSnapshotBuilder.BuildAttributeMappingSnapshot(entity),
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent
            },
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return new AdAttributeMappingResult(
            true,
            AdManagementApiMessageKeys.AttributeMappings.CreateSuccess,
            MapToItem(entity));
    }

    public async Task<AdAttributeMappingResult> UpdateAsync(
        UpdateAdAttributeMappingRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.AdAttributeMappings
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return new AdAttributeMappingResult(false, AdManagementApiMessageKeys.AttributeMappings.NotFound, null);
        }

        var displayName = NormalizeNullable(request.DisplayName);
        var attributeName = NormalizeNullable(request.AttributeName);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return new AdAttributeMappingResult(false, AdManagementApiMessageKeys.AttributeMappings.DisplayNameRequired, null);
        }

        if (displayName.Length > DisplayNameMaxLength)
        {
            return new AdAttributeMappingResult(
                false,
                AdManagementApiMessageKeys.AttributeMappings.DisplayNameTooLong,
                null);
        }

        if (string.IsNullOrWhiteSpace(attributeName) || !AttributeNameRegex.IsMatch(attributeName))
        {
            return new AdAttributeMappingResult(false, AdManagementApiMessageKeys.AttributeMappings.AttributeNameInvalid, null);
        }

        if (AdReservedCoreAttributes.IsReserved(attributeName))
        {
            return new AdAttributeMappingResult(
                false,
                AdReservedCoreAttributes.ReservedAttributeMappingMessageKey,
                null);
        }

        var validationType = NormalizeOrDefault(request.ValidationType, "None");
        var maskingStrategy = ResolveMaskingStrategy(request.IsSensitive, request.MaskingStrategy);

        if (!AllowedValidationTypes.Contains(validationType))
        {
            return new AdAttributeMappingResult(false, AdManagementApiMessageKeys.AttributeMappings.ValidationTypeInvalid, null);
        }

        var now = DateTime.UtcNow;
        var oldAttributeName = entity.AttributeName;
        var beforeSnapshotJson = AdOperationLogSnapshotBuilder.BuildAttributeMappingSnapshot(entity);
        var updateRequestSummaryJson = AdOperationLogSnapshotBuilder.BuildAttributeMappingUpdateRequestSummary(
            request,
            entity);

        entity.DisplayName = displayName;
        entity.AttributeName = attributeName;
        entity.IsEnabled = request.IsEnabled;
        entity.IsEditable = request.IsEditable;
        entity.IsSensitive = request.IsSensitive;
        entity.IsSearchable = ResolveIsSearchable(request.IsSensitive, request.IsSearchable);
        entity.ValidationType = validationType;
        entity.MaskingStrategy = maskingStrategy;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.ActorUserName ?? "system";

        var description = string.Equals(oldAttributeName, attributeName, StringComparison.Ordinal)
            ? $"AD attribute mapping updated: {entity.LogicalField}. Attribute: {entity.AttributeName}."
            : $"AD attribute mapping updated: {entity.LogicalField}. Attribute: {oldAttributeName} -> {entity.AttributeName}.";

        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = "Update",
                EntityName = "AdAttributeMapping",
                EntityId = entity.Id.ToString(),
                Description = TruncateAuditDescription(description),
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = TruncateNullable(request.ActorIpAddress, AuditIpAddressMaxLength),
                UserAgent = TruncateNullable(request.ActorUserAgent, AuditUserAgentMaxLength),
                CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
            },
            cancellationToken);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.AttributeMappingUpdated,
                Status = AdManagementOperationStatuses.Succeeded,
                TargetObjectType = AdManagementTargetObjectTypes.AdAttributeMapping,
                RequestSummaryJson = updateRequestSummaryJson,
                BeforeSnapshotJson = beforeSnapshotJson,
                AfterSnapshotJson = AdOperationLogSnapshotBuilder.BuildAttributeMappingSnapshot(entity),
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent
            },
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return new AdAttributeMappingResult(
            true,
            AdManagementApiMessageKeys.AttributeMappings.UpdateSuccess,
            MapToItem(entity));
    }

    public async Task<AdAttributeMappingResult> DeleteAsync(
        DeleteAdAttributeMappingRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.AdAttributeMappings
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return new AdAttributeMappingResult(false, AdManagementApiMessageKeys.AttributeMappings.NotFound, null);
        }

        var now = DateTime.UtcNow;
        var snapshot = MapToItem(entity);
        var beforeSnapshotJson = AdOperationLogSnapshotBuilder.BuildAttributeMappingSnapshot(entity);
        var deleteRequestSummaryJson = AdOperationLogSnapshotBuilder.BuildAttributeMappingDeleteRequestSummary(
            request,
            entity);

        context.AdAttributeMappings.Remove(entity);

        var description = TruncateAuditDescription(
            $"AD attribute mapping deleted: {entity.LogicalField}.");

        await context.AuditLogs.AddAsync(
            new AuditLog
            {
                Action = "Delete",
                EntityName = "AdAttributeMapping",
                EntityId = entity.Id.ToString(),
                Description = description,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = TruncateNullable(request.ActorIpAddress, AuditIpAddressMaxLength),
                UserAgent = TruncateNullable(request.ActorUserAgent, AuditUserAgentMaxLength),
                CreatedAt = new DateTimeOffset(now, TimeSpan.Zero)
            },
            cancellationToken);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.AttributeMappingDeleted,
                Status = AdManagementOperationStatuses.Succeeded,
                TargetObjectType = AdManagementTargetObjectTypes.AdAttributeMapping,
                RequestSummaryJson = deleteRequestSummaryJson,
                BeforeSnapshotJson = beforeSnapshotJson,
                AfterSnapshotJson = null,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent
            },
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return new AdAttributeMappingResult(true, AdManagementApiMessageKeys.AttributeMappings.DeleteSuccess, snapshot);
    }

    private static bool ResolveIsSearchable(bool isSensitive, bool isSearchable) =>
        isSensitive ? false : isSearchable;

    internal static string ResolveMaskingStrategy(bool isSensitive, string? maskingStrategy)
    {
        if (!isSensitive)
        {
            return "None";
        }

        var normalized = NormalizeOrDefault(maskingStrategy, "Hidden");
        if (!AllowedMaskingStrategies.Contains(normalized)
            || string.Equals(normalized, "None", StringComparison.Ordinal))
        {
            return "Hidden";
        }

        return normalized;
    }

    private static AdAttributeMappingItem MapToItem(AdAttributeMapping entity) =>
        new(
            entity.Id,
            entity.LogicalField,
            entity.DisplayName,
            entity.AttributeName,
            entity.IsEnabled,
            entity.IsEditable,
            entity.IsSensitive,
            entity.IsSearchable,
            entity.ValidationType,
            entity.MaskingStrategy,
            entity.SortOrder);

    private static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        var trimmed = NormalizeNullable(value);
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }

    private static string? TruncateNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string TruncateAuditDescription(string description) =>
        description.Length <= AuditDescriptionMaxLength
            ? description
            : description[..AuditDescriptionMaxLength];
}
