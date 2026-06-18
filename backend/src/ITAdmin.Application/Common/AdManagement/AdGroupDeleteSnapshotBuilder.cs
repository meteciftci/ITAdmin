using System.Text.Json;
using System.Text.Json.Serialization;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdGroupDeleteSnapshotBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Build(AdGroupDetail? group) =>
        AdOperationLogSnapshotBuilder.BuildGroupOperationSnapshot(
            AdManagementOperationTypes.GroupDelete,
            group);

    public static string BuildRequestSummary(DeleteAdGroupRequest request, AdGroupDetail? group) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.GroupDelete,
                groupId = request.GroupId.ToString("D"),
                samAccountName = group?.SamAccountName,
                name = group?.Name,
                distinguishedName = group?.DistinguishedName,
            },
            SerializerOptions);
}
