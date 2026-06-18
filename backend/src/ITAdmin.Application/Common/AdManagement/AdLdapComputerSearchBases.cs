using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdLdapComputerSearchBases
{
    public static string? ResolveRequiredComputersSearchBase(AdManagementConnectionParameters connection) =>
        string.IsNullOrWhiteSpace(connection.ComputersSearchBase)
            ? null
            : connection.ComputersSearchBase.Trim();
}
