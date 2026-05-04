using System.Collections;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed class LdapService : ILdapService
{
    private const string MissingRequiredFieldsMessage = "Required LDAP fields are missing.";
    private const string ValidationSucceededMessage = "LDAP validation succeeded.";
    private const string ServiceAccountBindFailedMessage = "LDAP service account authentication failed.";
    private const string TestUserNotFoundMessage = "Test user could not be found.";
    private const string UserDistinguishedNameNotFoundMessage = "Test user distinguished name could not be resolved.";
    private const string TestUserBindFailedMessage = "Test user authentication failed.";
    private const string ValidationFailedMessage = "LDAP validation failed.";

    public Task<LdapValidationResult> ValidateAsync(LdapValidationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Host) ||
            request.Port <= 0 ||
            string.IsNullOrWhiteSpace(request.BaseDn) ||
            string.IsNullOrWhiteSpace(request.UserSearchFilter) ||
            string.IsNullOrWhiteSpace(request.BindUserName) ||
            string.IsNullOrWhiteSpace(request.BindPassword) ||
            string.IsNullOrWhiteSpace(request.TestUserName) ||
            string.IsNullOrWhiteSpace(request.TestPassword))
        {
            return Task.FromResult(new LdapValidationResult(false, MissingRequiredFieldsMessage));
        }

        try
        {
            var identifier = new LdapDirectoryIdentifier(request.Host, request.Port);
            var bindIdentity = BuildBindIdentity(request.BindUserName, request.BindUserDomain);
            if (string.IsNullOrWhiteSpace(bindIdentity))
            {
                return Task.FromResult(new LdapValidationResult(false, MissingRequiredFieldsMessage));
            }

            using var serviceConnection = CreateConnection(identifier, request.UseSsl, bindIdentity, request.BindPassword);
            try
            {
                serviceConnection.Bind();
            }
            catch (LdapException)
            {
                return Task.FromResult(new LdapValidationResult(false, ServiceAccountBindFailedMessage));
            }

            var escapedUserName = EscapeLdapFilterValue(request.TestUserName);
            var searchFilter = request.UserSearchFilter.Replace("{0}", escapedUserName, StringComparison.Ordinal);
            var searchBase = string.IsNullOrWhiteSpace(request.UserSearchBase) ? request.BaseDn : request.UserSearchBase;

            var searchRequest = new SearchRequest(
                searchBase,
                searchFilter,
                SearchScope.Subtree,
                "distinguishedName",
                "cn",
                "mail");

            SearchResponse searchResponse;
            try
            {
                searchResponse = (SearchResponse)serviceConnection.SendRequest(searchRequest);
            }
            catch (LdapException)
            {
                return Task.FromResult(new LdapValidationResult(false, ValidationFailedMessage));
            }

            if (searchResponse.Entries.Count == 0)
            {
                return Task.FromResult(new LdapValidationResult(false, TestUserNotFoundMessage));
            }

            var userDn = searchResponse.Entries[0].DistinguishedName;
            if (string.IsNullOrWhiteSpace(userDn))
            {
                return Task.FromResult(new LdapValidationResult(false, UserDistinguishedNameNotFoundMessage));
            }

            using var userConnection = CreateConnection(identifier, request.UseSsl, userDn, request.TestPassword);
            try
            {
                userConnection.Bind();
                return Task.FromResult(new LdapValidationResult(true, ValidationSucceededMessage));
            }
            catch (LdapException)
            {
                return Task.FromResult(new LdapValidationResult(false, TestUserBindFailedMessage));
            }
        }
        catch
        {
            return Task.FromResult(new LdapValidationResult(false, ValidationFailedMessage));
        }
    }

    public Task<LdapUserProfile?> GetUserProfileAsync(
        LdapUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(request.Host) ||
            request.Port <= 0 ||
            string.IsNullOrWhiteSpace(request.BaseDn) ||
            string.IsNullOrWhiteSpace(request.UserSearchFilter) ||
            string.IsNullOrWhiteSpace(request.BindUserName) ||
            string.IsNullOrWhiteSpace(request.BindPassword) ||
            string.IsNullOrWhiteSpace(request.UserName))
        {
            return Task.FromResult<LdapUserProfile?>(null);
        }

        try
        {
            var identifier = new LdapDirectoryIdentifier(request.Host, request.Port);
            var bindIdentity = BuildBindIdentity(request.BindUserName, request.BindUserDomain);
            if (string.IsNullOrWhiteSpace(bindIdentity))
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            using var serviceConnection = CreateConnection(identifier, request.UseSsl, bindIdentity, request.BindPassword);
            try
            {
                serviceConnection.Bind();
            }
            catch (LdapException)
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            var escapedUserName = EscapeLdapFilterValue(request.UserName);
            var searchFilter = request.UserSearchFilter.Replace("{0}", escapedUserName, StringComparison.Ordinal);
            var searchBase = string.IsNullOrWhiteSpace(request.UserSearchBase) ? request.BaseDn : request.UserSearchBase;

            var attributeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "objectGUID",
                "sAMAccountName",
                "displayName",
                "mail",
                "userPrincipalName"
            };

            if (!string.IsNullOrWhiteSpace(request.NationalIdAttribute))
            {
                attributeNames.Add(request.NationalIdAttribute.Trim());
            }

            var searchRequest = new SearchRequest(
                searchBase,
                searchFilter,
                SearchScope.Subtree,
                attributeNames.ToArray());

            SearchResponse searchResponse;
            try
            {
                searchResponse = (SearchResponse)serviceConnection.SendRequest(searchRequest);
            }
            catch (LdapException)
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            if (searchResponse.Entries.Count == 0)
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            var entry = searchResponse.Entries[0];

            var objectGuidAttr = TryGetDirectoryAttribute(entry, "objectGUID");
            var guidBytes = GetFirstByteArray(objectGuidAttr);
            if (guidBytes is null || guidBytes.Length == 0)
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            string directoryObjectId;
            try
            {
                directoryObjectId = new Guid(guidBytes).ToString("D");
            }
            catch
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            var sAm = NormalizeOptionalString(GetFirstString(TryGetDirectoryAttribute(entry, "sAMAccountName")));
            var upn = NormalizeOptionalString(GetFirstString(TryGetDirectoryAttribute(entry, "userPrincipalName")));
            var resolvedUserName = !string.IsNullOrEmpty(sAm) ? sAm : upn ?? string.Empty;
            if (string.IsNullOrEmpty(resolvedUserName))
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            var displayAttr = NormalizeOptionalString(GetFirstString(TryGetDirectoryAttribute(entry, "displayName")));
            var displayName = !string.IsNullOrEmpty(displayAttr) ? displayAttr : resolvedUserName;

            var email = NormalizeOptionalString(GetFirstString(TryGetDirectoryAttribute(entry, "mail")));

            string? nationalId = null;
            if (!string.IsNullOrWhiteSpace(request.NationalIdAttribute))
            {
                nationalId = NormalizeNationalIdCandidate(
                    GetFirstString(TryGetDirectoryAttribute(entry, request.NationalIdAttribute.Trim())));
            }

            return Task.FromResult<LdapUserProfile?>(
                new LdapUserProfile(directoryObjectId, resolvedUserName, displayName, email, nationalId));
        }
        catch
        {
            return Task.FromResult<LdapUserProfile?>(null);
        }
    }

    public Task<LdapUserProfile?> GetUserProfileByObjectIdAsync(
        LdapUserProfileByObjectIdRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(request.Host) ||
            request.Port <= 0 ||
            string.IsNullOrWhiteSpace(request.BaseDn) ||
            string.IsNullOrWhiteSpace(request.BindUserName) ||
            string.IsNullOrWhiteSpace(request.BindPassword) ||
            string.IsNullOrWhiteSpace(request.DirectoryObjectId))
        {
            return Task.FromResult<LdapUserProfile?>(null);
        }

        Guid directoryGuid;
        try
        {
            directoryGuid = Guid.Parse(request.DirectoryObjectId.Trim());
        }
        catch
        {
            return Task.FromResult<LdapUserProfile?>(null);
        }

        var escapedObjectGuidFilter = EscapeObjectGuidBinaryForLdapFilter(directoryGuid.ToByteArray());
        var searchFilter =
            $"(&(objectCategory=person)(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2))(objectGUID={escapedObjectGuidFilter}))";
        var searchBase = string.IsNullOrWhiteSpace(request.UserSearchBase) ? request.BaseDn : request.UserSearchBase;

        try
        {
            var identifier = new LdapDirectoryIdentifier(request.Host, request.Port);
            var bindIdentity = BuildBindIdentity(request.BindUserName, request.BindUserDomain);
            if (string.IsNullOrWhiteSpace(bindIdentity))
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            using var serviceConnection = CreateConnection(identifier, request.UseSsl, bindIdentity, request.BindPassword);
            try
            {
                serviceConnection.Bind();
            }
            catch (LdapException)
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            var attributeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "objectGUID",
                "sAMAccountName",
                "displayName",
                "mail",
                "userPrincipalName",
                "distinguishedName"
            };

            if (!string.IsNullOrWhiteSpace(request.NationalIdAttribute))
            {
                attributeNames.Add(request.NationalIdAttribute.Trim());
            }

            var searchRequest = new SearchRequest(
                searchBase,
                searchFilter,
                SearchScope.Subtree,
                attributeNames.ToArray());

            SearchResponse searchResponse;
            try
            {
                searchResponse = (SearchResponse)serviceConnection.SendRequest(searchRequest);
            }
            catch (LdapException)
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            if (searchResponse.Entries.Count == 0)
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            var entry = searchResponse.Entries[0];

            var objectGuidAttr = TryGetDirectoryAttribute(entry, "objectGUID");
            var guidBytes = GetFirstByteArray(objectGuidAttr);
            if (guidBytes is null || guidBytes.Length == 0)
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            string directoryObjectId;
            try
            {
                directoryObjectId = new Guid(guidBytes).ToString("D");
            }
            catch
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            var sAm = NormalizeOptionalString(GetFirstString(TryGetDirectoryAttribute(entry, "sAMAccountName")));
            var upn = NormalizeOptionalString(GetFirstString(TryGetDirectoryAttribute(entry, "userPrincipalName")));
            string? resolvedUserName = null;
            if (!string.IsNullOrEmpty(sAm))
            {
                resolvedUserName = sAm;
            }
            else if (!string.IsNullOrEmpty(upn))
            {
                resolvedUserName = upn;
            }

            if (resolvedUserName is null)
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            var displayAttr = NormalizeOptionalString(GetFirstString(TryGetDirectoryAttribute(entry, "displayName")));
            var displayName = !string.IsNullOrEmpty(displayAttr) ? displayAttr : resolvedUserName;

            var email = NormalizeOptionalString(GetFirstString(TryGetDirectoryAttribute(entry, "mail")));

            string? nationalId = null;
            if (!string.IsNullOrWhiteSpace(request.NationalIdAttribute))
            {
                nationalId = NormalizeNationalIdCandidate(
                    GetFirstString(TryGetDirectoryAttribute(entry, request.NationalIdAttribute.Trim())));
            }

            return Task.FromResult<LdapUserProfile?>(
                new LdapUserProfile(directoryObjectId, resolvedUserName, displayName, email, nationalId));
        }
        catch
        {
            return Task.FromResult<LdapUserProfile?>(null);
        }
    }

    public Task<IReadOnlyCollection<LdapUserLookupItem>> SearchUsersAsync(
        LdapUserLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(request.Host) ||
            request.Port <= 0 ||
            string.IsNullOrWhiteSpace(request.BaseDn) ||
            string.IsNullOrWhiteSpace(request.BindUserName) ||
            string.IsNullOrWhiteSpace(request.BindPassword))
        {
            return Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(Array.Empty<LdapUserLookupItem>());
        }

        var searchTrimmed = request.Search.Trim();
        if (searchTrimmed.Length == 0)
        {
            return Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(Array.Empty<LdapUserLookupItem>());
        }

        if (request.MaxResults < 1)
        {
            return Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(Array.Empty<LdapUserLookupItem>());
        }

        try
        {
            var identifier = new LdapDirectoryIdentifier(request.Host, request.Port);
            var bindIdentity = BuildBindIdentity(request.BindUserName, request.BindUserDomain);
            if (string.IsNullOrWhiteSpace(bindIdentity))
            {
                return Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(Array.Empty<LdapUserLookupItem>());
            }

            using var serviceConnection = CreateConnection(identifier, request.UseSsl, bindIdentity, request.BindPassword);
            try
            {
                serviceConnection.Bind();
            }
            catch (LdapException)
            {
                return Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(Array.Empty<LdapUserLookupItem>());
            }

            var escapedSearch = EscapeLdapFilterValue(searchTrimmed);
            var searchFilter =
                $"(&(objectCategory=person)(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2))(|(sAMAccountName=*{escapedSearch}*)(displayName=*{escapedSearch}*)(mail=*{escapedSearch}*)))";
            var searchBase = string.IsNullOrWhiteSpace(request.UserSearchBase) ? request.BaseDn : request.UserSearchBase;

            var attributeNames = new List<string>
            {
                "objectGUID",
                "sAMAccountName",
                "displayName",
                "mail",
                "userPrincipalName",
                "distinguishedName"
            };

            if (!string.IsNullOrWhiteSpace(request.NationalIdAttribute))
            {
                attributeNames.Add(request.NationalIdAttribute.Trim());
            }

            var searchRequest = new SearchRequest(
                searchBase,
                searchFilter,
                SearchScope.Subtree,
                attributeNames.ToArray())
            {
                SizeLimit = Math.Min(500, Math.Max(request.MaxResults * 5, request.MaxResults))
            };

            SearchResponse searchResponse;
            try
            {
                searchResponse = (SearchResponse)serviceConnection.SendRequest(searchRequest);
            }
            catch (LdapException)
            {
                return Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(Array.Empty<LdapUserLookupItem>());
            }

            var collected = new List<LdapUserLookupItem>(request.MaxResults);
            var seenObjectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenUserNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (SearchResultEntry entry in searchResponse.Entries)
            {
                if (collected.Count >= request.MaxResults)
                {
                    break;
                }

                var objectGuidAttr = TryGetDirectoryAttribute(entry, "objectGUID");
                var guidBytes = GetFirstByteArray(objectGuidAttr);
                if (guidBytes is null || guidBytes.Length == 0)
                {
                    continue;
                }

                string directoryObjectId;
                try
                {
                    directoryObjectId = new Guid(guidBytes).ToString("D");
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(directoryObjectId) || seenObjectIds.Contains(directoryObjectId))
                {
                    continue;
                }

                var sAm = NormalizeOptionalString(GetFirstString(TryGetDirectoryAttribute(entry, "sAMAccountName")));
                var upn = NormalizeOptionalString(GetFirstString(TryGetDirectoryAttribute(entry, "userPrincipalName")));
                var resolvedUserName = !string.IsNullOrEmpty(sAm)
                    ? sAm
                    : upn ?? string.Empty;
                if (string.IsNullOrEmpty(resolvedUserName) || seenUserNames.Contains(resolvedUserName))
                {
                    continue;
                }

                var displayAttr = NormalizeOptionalString(GetFirstString(TryGetDirectoryAttribute(entry, "displayName")));
                var displayName = !string.IsNullOrEmpty(displayAttr) ? displayAttr : resolvedUserName;

                var email = NormalizeOptionalString(GetFirstString(TryGetDirectoryAttribute(entry, "mail")));

                string? nationalId = null;
                if (!string.IsNullOrWhiteSpace(request.NationalIdAttribute))
                {
                    nationalId = NormalizeNationalIdCandidate(
                        GetFirstString(TryGetDirectoryAttribute(entry, request.NationalIdAttribute.Trim())));
                }

                seenObjectIds.Add(directoryObjectId);
                seenUserNames.Add(resolvedUserName);
                collected.Add(new LdapUserLookupItem(directoryObjectId, resolvedUserName, displayName, email, nationalId));
            }

            return Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(collected);
        }
        catch
        {
            return Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(Array.Empty<LdapUserLookupItem>());
        }
    }

    private static DirectoryAttribute? TryGetDirectoryAttribute(SearchResultEntry entry, string attributeName)
    {
        foreach (DictionaryEntry kv in entry.Attributes)
        {
            var keyText = kv.Key.ToString();
            if (string.Equals(keyText, attributeName, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value as DirectoryAttribute;
            }
        }

        return null;
    }

    private static byte[]? GetFirstByteArray(DirectoryAttribute? attribute)
    {
        if (attribute is null || attribute.Count == 0)
        {
            return null;
        }

        return attribute[0] as byte[];
    }

    private static string? GetFirstString(DirectoryAttribute? attribute)
    {
        if (attribute is null || attribute.Count == 0)
        {
            return null;
        }

        var raw = attribute[0];

        switch (raw)
        {
            case byte[] octets when octets.Length > 0:
                return Encoding.UTF8.GetString(octets).Trim();
            default:
                return raw.ToString()?.Trim();
        }
    }

    private static string? NormalizeOptionalString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeNationalIdCandidate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static LdapConnection CreateConnection(
        LdapDirectoryIdentifier identifier,
        bool useSsl,
        string userName,
        string password)
    {
        var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Basic,
            Credential = new NetworkCredential(userName, password)
        };

        connection.SessionOptions.ProtocolVersion = 3;
        if (useSsl)
        {
            connection.SessionOptions.SecureSocketLayer = true;
        }

        return connection;
    }

    private static string EscapeObjectGuidBinaryForLdapFilter(ReadOnlySpan<byte> bytes)
    {
        Span<char> buffer = stackalloc char[bytes.Length * 3];
        var pos = 0;
        foreach (var b in bytes)
        {
            buffer[pos++] = '\\';
            buffer[pos++] = ToHexChar(b >> 4);
            buffer[pos++] = ToHexChar(b & 0x0F);
        }

        return new string(buffer[..pos]);

        static char ToHexChar(int v) => (char)(v < 10 ? '0' + v : 'a' + (v - 10));
    }

    private static string EscapeLdapFilterValue(string value)
    {
        return value
            .Replace("\\", "\\5c", StringComparison.Ordinal)
            .Replace("*", "\\2a", StringComparison.Ordinal)
            .Replace("(", "\\28", StringComparison.Ordinal)
            .Replace(")", "\\29", StringComparison.Ordinal)
            .Replace("\0", "\\00", StringComparison.Ordinal);
    }

    private static string BuildBindIdentity(string bindUserName, string? bindUserDomain)
    {
        if (string.IsNullOrWhiteSpace(bindUserName))
        {
            return string.Empty;
        }

        if (bindUserName.Contains('\\') ||
            bindUserName.Contains('@') ||
            bindUserName.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
        {
            return bindUserName;
        }

        if (string.IsNullOrWhiteSpace(bindUserDomain))
        {
            return bindUserName;
        }

        return bindUserDomain.Contains('.')
            ? $"{bindUserName}@{bindUserDomain}"
            : $@"{bindUserDomain}\{bindUserName}";
    }
}
