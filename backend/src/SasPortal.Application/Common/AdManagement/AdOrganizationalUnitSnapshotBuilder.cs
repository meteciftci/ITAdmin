using System.Text.Json;
using System.Text.Json.Serialization;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdOrganizationalUnitSnapshotBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string BuildCreateRequestSummary(CreateAdOrganizationalUnitRequest request) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.OrganizationalUnitCreate,
                name = request.Name.Trim(),
                parentDistinguishedName = request.ParentDistinguishedName.Trim(),
            },
            SerializerOptions);

    public static string BuildRenameRequestSummary(RenameAdOrganizationalUnitRequest request, string? beforeDn) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.OrganizationalUnitRename,
                organizationalUnitId = request.OrganizationalUnitId.ToString("D"),
                name = request.Name.Trim(),
                beforeDistinguishedName = beforeDn,
            },
            SerializerOptions);

    public static string BuildMoveRequestSummary(
        MoveAdOrganizationalUnitRequest request,
        string? sourceDistinguishedName) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.OrganizationalUnitMove,
                organizationalUnitId = request.OrganizationalUnitId.ToString("D"),
                sourceDistinguishedName,
                targetParentDistinguishedName = request.TargetParentDistinguishedName.Trim(),
            },
            SerializerOptions);

    public static string BuildDeleteRequestSummary(
        DeleteAdOrganizationalUnitRequest request,
        AdOrganizationalUnitDetail? detail) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.OrganizationalUnitDelete,
                organizationalUnitId = request.OrganizationalUnitId.ToString("D"),
                distinguishedName = detail?.DistinguishedName,
            },
            SerializerOptions);

    public static string BuildSnapshot(AdOrganizationalUnitDetail? detail) =>
        detail is null
            ? "{}"
            : JsonSerializer.Serialize(CreateSnapshotBody(detail), SerializerOptions);

    public static string BuildOperationSnapshot(
        string operationType,
        AdOrganizationalUnitDetail? detail) =>
        detail is null
            ? "{}"
            : JsonSerializer.Serialize(
                new
                {
                    operation = operationType,
                    organizationalUnit = CreateSnapshotBody(detail),
                },
                SerializerOptions);

    private static object CreateSnapshotBody(AdOrganizationalUnitDetail detail) =>
        new
        {
            id = detail.ObjectGuid,
            name = detail.Name,
            ou = detail.Ou,
            displayName = detail.DisplayName,
            distinguishedName = detail.DistinguishedName,
            parentDistinguishedName = detail.ParentDistinguishedName,
            canonicalName = detail.CanonicalName,
            contentSummary = new
            {
                childOuCount = detail.ContentSummary.ChildOuCount,
                userCount = detail.ContentSummary.UserCount,
                groupCount = detail.ContentSummary.GroupCount,
                computerCount = detail.ContentSummary.ComputerCount,
            },
        };
}
