namespace ITAdmin.Application.Common.AdManagement;

public static class AdUserIdentityResolver
{
    public static string ResolvePublicUserId(string? objectGuid, string samAccountName)
    {
        if (Guid.TryParse(objectGuid, out _))
        {
            return objectGuid!;
        }

        return samAccountName;
    }

    public static string? ResolveAuditEntityId(string? candidateId, string samAccountName)
    {
        if (Guid.TryParse(candidateId, out _))
        {
            return candidateId;
        }

        if (!string.IsNullOrWhiteSpace(samAccountName))
        {
            return samAccountName;
        }

        return null;
    }

    public static bool LooksLikeDistinguishedName(string value) =>
        value.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)
        || value.Contains(",DC=", StringComparison.OrdinalIgnoreCase);
}
