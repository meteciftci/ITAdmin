using ITAdmin.Domain.Common;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Domain.Entities;

public sealed class DnsCredentialProfile : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public DnsAuthenticationMode AuthenticationMode { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastValidatedAt { get; set; }
    public string? LastValidationStatus { get; set; }
    public string? LastValidationMessage { get; set; }

    public ICollection<DnsServer> Servers { get; set; } = new List<DnsServer>();
}
