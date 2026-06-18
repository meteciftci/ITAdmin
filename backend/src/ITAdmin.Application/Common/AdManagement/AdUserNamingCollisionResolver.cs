namespace ITAdmin.Application.Common.AdManagement;

public sealed record AdUserNamingCandidate(
    string CommonName,
    string DisplayName,
    string SamAccountName,
    string UserPrincipalName);

public sealed record ResolvedAdUserNames(
    string DisplayName,
    string CommonName,
    string SamAccountName,
    string UserPrincipalName,
    bool NamingCollisionResolved,
    int? GeneratedSuffix);

public static class AdUserNamingCollisionResolver
{
    public const int DefaultMaxAttempts = 50;

    public static ResolvedAdUserNames? Resolve(
        string givenName,
        string surname,
        string? requestedSamAccountName,
        string upnSuffix,
        Func<AdUserNamingCandidate, bool> hasCollision,
        int maxAttempts = DefaultMaxAttempts)
    {
        var baseSam = AdUserNameNormalizer.NormalizeSamAccountName(requestedSamAccountName)
            ?? AdUserNameNormalizer.NormalizeUserName(givenName, surname);

        if (string.IsNullOrWhiteSpace(baseSam))
        {
            return null;
        }

        var normalizedSuffix = AdDefaultUpnSuffixNormalizer.Normalize(upnSuffix);
        if (string.IsNullOrWhiteSpace(normalizedSuffix)
            || !AdDefaultUpnSuffixNormalizer.IsValidFormat(normalizedSuffix))
        {
            return null;
        }

        var manualSam = !string.IsNullOrWhiteSpace(requestedSamAccountName);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var displayName = AdUserNameNormalizer.BuildDisplayName(givenName, surname, attempt);
            var commonName = displayName;
            var samAccountName = manualSam && attempt == 1
                ? baseSam
                : AdUserNameNormalizer.BuildSamAccountNameWithSuffix(baseSam, attempt);

            var userPrincipalName = AdUserNameNormalizer.BuildUserPrincipalNameWithSuffix(
                baseSam,
                normalizedSuffix,
                attempt);

            var candidate = new AdUserNamingCandidate(
                commonName,
                displayName,
                samAccountName,
                userPrincipalName);

            if (!hasCollision(candidate))
            {
                return new ResolvedAdUserNames(
                    displayName,
                    commonName,
                    samAccountName,
                    userPrincipalName,
                    attempt > 1,
                    attempt > 1 ? attempt : null);
            }
        }

        return null;
    }
}
