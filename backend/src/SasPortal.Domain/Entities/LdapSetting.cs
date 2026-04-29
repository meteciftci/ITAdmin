using SasPortal.Domain.Common;

namespace SasPortal.Domain.Entities;

public class LdapSetting : SoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; }
    public string BaseDn { get; set; } = string.Empty;
    public string UserSearchBase { get; set; } = string.Empty;
    public string UserSearchFilter { get; set; } = "(sAMAccountName={0})";
    public string BindDn { get; set; } = string.Empty;
    public string EncryptedBindPassword { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}
