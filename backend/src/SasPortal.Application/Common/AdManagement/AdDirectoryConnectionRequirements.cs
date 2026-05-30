namespace SasPortal.Application.Common.AdManagement;

public static class AdDirectoryConnectionRequirements
{
    public const string LdapsRequiredMessage =
        "AD işlemleri için LDAPS bağlantısı zorunludur. Lütfen AD yönetim ayarlarında SSL'i etkinleştirin.";

    public static bool IsLdapsEnabled(bool useSsl) => useSsl;

    public static string? GetLdapsRequiredErrorMessage(bool useSsl) =>
        IsLdapsEnabled(useSsl) ? null : LdapsRequiredMessage;
}
