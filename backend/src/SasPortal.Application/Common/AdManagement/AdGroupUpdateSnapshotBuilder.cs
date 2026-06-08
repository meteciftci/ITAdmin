using System.Text.Json;
using System.Text.Json.Serialization;
using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Common.AdManagement;

public static class AdGroupUpdateSnapshotBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Build(AdGroupDetail? group) =>
        group is null
            ? "{}"
            : JsonSerializer.Serialize(
                new
                {
                    id = group.Id,
                    distinguishedName = group.DistinguishedName,
                    displayName = group.DisplayName,
                    name = group.Name,
                    cn = group.Cn,
                    samAccountName = group.SamAccountName,
                    description = group.Description,
                    groupScope = group.GroupScope,
                    securityEnabled = group.SecurityEnabled,
                    groupType = group.GroupType,
                },
                SerializerOptions);

    public static string BuildRequestSummary(UpdateAdGroupRequest request) =>
        JsonSerializer.Serialize(
            new
            {
                displayName = request.DisplayName,
                name = request.Name,
                samAccountName = request.SamAccountName,
                description = request.Description,
            },
            SerializerOptions);

    public static string BuildChangedAttributes(AdGroupUpdateChangePlan changePlan)
    {
        var attributes = changePlan.ScalarChanges
            .Select(static change => change.AttributeName)
            .ToList();

        if (changePlan.RequiresRename)
        {
            attributes.Add("cn");
        }

        return JsonSerializer.Serialize(attributes.Distinct(StringComparer.OrdinalIgnoreCase), SerializerOptions);
    }
}
