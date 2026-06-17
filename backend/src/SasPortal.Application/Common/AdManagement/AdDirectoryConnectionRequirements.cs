using SasPortal.Application.Common.Constants;

namespace SasPortal.Application.Common.AdManagement;

public static class AdDirectoryConnectionRequirements
{
    public static bool IsLdapsEnabled(bool useSsl) => useSsl;

    public static string? GetLdapsRequiredMessageKey(bool useSsl) =>
        IsLdapsEnabled(useSsl) ? null : AdManagementApiMessageKeys.Common.LdapsRequired;
}
