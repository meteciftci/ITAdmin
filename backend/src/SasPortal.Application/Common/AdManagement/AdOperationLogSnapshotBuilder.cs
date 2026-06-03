using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdOperationLogSnapshotBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string BuildGroupMembershipRequestSummary(
        string operationType,
        Guid userId,
        string groupDistinguishedName,
        string? groupName = null) =>
        JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                userId = userId.ToString("D"),
                groupDistinguishedName,
                groupName,
            },
            SerializerOptions);

    public static string BuildGroupMembershipBeforeSnapshot(
        string operationType,
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        string? groupName,
        string? groupDistinguishedName,
        bool isDirectMember)
    {
        if (operationType == AdManagementOperationTypes.UserGroupRemove)
        {
            return JsonSerializer.Serialize(
                new
                {
                    membership = new { isDirectMember },
                },
                SerializerOptions);
        }

        return JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                group = BuildGroupSnapshot(groupName, groupDistinguishedName),
                membership = new { isDirectMember },
            },
            SerializerOptions);
    }

    public static string BuildGroupMembershipAfterSnapshot(
        string operationType,
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        string? groupName,
        string? groupDistinguishedName,
        bool isDirectMember)
    {
        if (operationType == AdManagementOperationTypes.UserGroupRemove)
        {
            return JsonSerializer.Serialize(
                new
                {
                    membership = new { isDirectMember },
                },
                SerializerOptions);
        }

        return JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                group = BuildGroupSnapshot(groupName, groupDistinguishedName),
                membership = new { isDirectMember },
            },
            SerializerOptions);
    }

    public static string BuildAccountRequestSummary(
        string operationType,
        Guid userId,
        bool? requestedEnabled = null)
    {
        if (requestedEnabled is null)
        {
            return JsonSerializer.Serialize(
                new { operation = operationType, userId = userId.ToString("D") },
                SerializerOptions);
        }

        return JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                userId = userId.ToString("D"),
                requestedEnabled,
            },
            SerializerOptions);
    }

    public static string BuildAccountBeforeSnapshot(
        string operationType,
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        bool isEnabled,
        bool isLockedOut,
        int? userAccountControl,
        long? lockoutTime)
    {
        if (operationType == AdManagementOperationTypes.UserUnlock)
        {
            return JsonSerializer.Serialize(
                new
                {
                    operation = operationType,
                    user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                    account = new
                    {
                        isLocked = isLockedOut,
                        lockoutTime = FormatLockoutTime(lockoutTime),
                    },
                },
                SerializerOptions);
        }

        return JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                account = new
                {
                    isEnabled,
                    isLocked = isLockedOut,
                    userAccountControl,
                },
            },
            SerializerOptions);
    }

    public static string BuildAccountAfterSnapshot(
        string operationType,
        bool isEnabled,
        bool isLockedOut,
        int? userAccountControl,
        long? lockoutTime)
    {
        if (operationType == AdManagementOperationTypes.UserUnlock)
        {
            return new JsonObject
            {
                ["account"] = new JsonObject
                {
                    ["isLocked"] = isLockedOut,
                    ["lockoutTime"] = FormatLockoutTime(lockoutTime),
                },
            }.ToJsonString(SerializerOptions);
        }

        return JsonSerializer.Serialize(
            new
            {
                account = new
                {
                    isEnabled,
                    isLocked = isLockedOut,
                    userAccountControl,
                },
            },
            SerializerOptions);
    }

    public static string BuildCreateRequestSummary(CreateAdUserRequest request)
    {
        var mappedAttributeFields = request.MappedAttributes
            .Select(static attribute => attribute.LogicalField.Trim())
            .Where(static field => !string.IsNullOrWhiteSpace(field))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static field => field, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.CreateUser,
                givenName = request.GivenName.Trim(),
                surname = request.Surname.Trim(),
                samAccountName = string.IsNullOrWhiteSpace(request.SamAccountName)
                    ? null
                    : request.SamAccountName.Trim(),
                upnSuffix = request.UpnSuffix.Trim(),
                targetOuDistinguishedName = request.TargetOuDistinguishedName.Trim(),
                isEnabled = request.IsEnabled,
                mustChangePasswordAtNextLogon = request.MustChangePasswordAtNextLogon,
                mappedAttributeFields,
            },
            SerializerOptions);
    }

    public static string BuildCreateAfterSnapshot(
        CreateAdUserResponse response,
        bool isEnabled,
        IReadOnlyList<CreateAdUserMappedAttributeRequest> mappedAttributeRequests,
        IReadOnlyList<AdAttributeMappingItem> mappings)
    {
        var mappedAttributes = BuildCreateMappedAttributesForSnapshot(mappedAttributeRequests, mappings);

        return JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.CreateUser,
                user = new
                {
                    id = response.Id,
                    samAccountName = response.SamAccountName,
                    userPrincipalName = response.UserPrincipalName,
                    displayName = response.DisplayName,
                    distinguishedName = response.DistinguishedName,
                },
                account = new { isEnabled },
                mappedAttributes,
            },
            SerializerOptions);
    }

    private static IReadOnlyList<object> BuildCreateMappedAttributesForSnapshot(
        IReadOnlyList<CreateAdUserMappedAttributeRequest> mappedAttributeRequests,
        IReadOnlyList<AdAttributeMappingItem> mappings)
    {
        var mappingByField = mappings
            .Where(static mapping => mapping.IsEnabled)
            .ToDictionary(static mapping => mapping.LogicalField, StringComparer.OrdinalIgnoreCase);

        var result = new List<object>();
        foreach (var mappedAttribute in mappedAttributeRequests)
        {
            var logicalField = mappedAttribute.LogicalField.Trim();
            if (string.IsNullOrWhiteSpace(logicalField))
            {
                continue;
            }

            if (!mappingByField.TryGetValue(logicalField, out var mapping))
            {
                continue;
            }

            var rawValue = ExtractMappedAttributeValue(mappedAttribute.Value);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            var maskedValues = AdAttributeValueMasker.MaskValues(
                [rawValue],
                mapping.IsSensitive,
                mapping.MaskingStrategy);

            result.Add(new
            {
                logicalField,
                values = maskedValues,
            });
        }

        return result;
    }

    private static string? ExtractMappedAttributeValue(object? value) =>
        value switch
        {
            null => null,
            string text => string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            IEnumerable<string> values => values.FirstOrDefault(static item => !string.IsNullOrWhiteSpace(item))?.Trim(),
            _ => string.IsNullOrWhiteSpace(value.ToString()) ? null : value.ToString()!.Trim(),
        };

    private static object BuildUserSnapshot(
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName) =>
        new
        {
            id = userId,
            samAccountName,
            userPrincipalName,
            distinguishedName,
        };

    private static object BuildGroupSnapshot(string? groupName, string? groupDistinguishedName) =>
        new
        {
            name = groupName,
            distinguishedName = groupDistinguishedName,
        };

    private static string? FormatLockoutTime(long? lockoutTime) =>
        lockoutTime is null or 0 ? null : lockoutTime.Value.ToString();
}
