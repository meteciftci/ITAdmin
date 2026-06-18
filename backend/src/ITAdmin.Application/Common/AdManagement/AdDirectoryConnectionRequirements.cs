namespace ITAdmin.Application.Common.AdManagement;

public static class AdDirectoryConnectionRequirements
{
    public static bool IsLdapsEnabled() => true;

    public static string? GetLdapsRequiredMessageKey() => null;
}
