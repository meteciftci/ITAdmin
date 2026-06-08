using System.Text.Json;
using System.Text.Json.Serialization;
using SasPortal.Application.Common.Constants;
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
        AdOperationLogSnapshotBuilder.BuildGroupOperationSnapshot(
            AdManagementOperationTypes.GroupUpdate,
            group);

    public static string BuildRequestSummary(UpdateAdGroupRequest request) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.GroupUpdate,
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
