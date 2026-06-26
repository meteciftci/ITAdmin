using System.Collections;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed class LdapService(ILogger<LdapService> logger) : ILdapService
{
    private static readonly TimeSpan LdapOperationTimeout = TimeSpan.FromSeconds(10);

    private const string LdapOperationTimedOutMessage =
        "LDAP operation timed out. Verify host, network connectivity, and LDAPS access on port 636.";

    private const string MissingRequiredFieldsMessage = "Required LDAP fields are missing.";
    private const string ValidationSucceededMessage = "LDAP validation succeeded.";
    private const string DirectoryUserNotFoundMessage = "Directory user could not be found.";
    private const string DirectoryUserDistinguishedNameNotFoundMessage = "Directory user distinguished name could not be resolved.";
    private const string DirectoryUserBindFailedMessage = "Directory user authentication failed.";
    private const string BindValidationSucceededMessage = "LDAP bind validation succeeded.";
    private const string BaseDnCouldNotBeResolvedMessage = "LDAP base DN could not be resolved.";
    private const string UserSearchBaseCouldNotBeResolvedMessage = "LDAP user search base could not be resolved.";

    public Task<LdapValidationResult> ValidateBindAsync(LdapBindValidationRequest request, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(request.Host) ||
            string.IsNullOrWhiteSpace(request.BindUserName) ||
            string.IsNullOrWhiteSpace(request.BindPassword))
        {
            return Task.FromResult(new LdapValidationResult(false, MissingRequiredFieldsMessage));
        }

        try
        {
            var bindIdentity = BuildBindIdentity(request.BindUserName, request.BindUserDomain);
            if (string.IsNullOrWhiteSpace(bindIdentity))
            {
                return Task.FromResult(new LdapValidationResult(false, MissingRequiredFieldsMessage));
            }

            using var connection = CreateConnection(request.Host, bindIdentity, request.BindPassword);
            connection.Bind();
            return Task.FromResult(new LdapValidationResult(true, BindValidationSucceededMessage));
        }
        catch (LdapException exception) when (IsLikelyConnectionOrLdapTimeout(exception))
        {
            return Task.FromResult(new LdapValidationResult(false, LdapOperationTimedOutMessage));
        }
        catch (LdapException exception)
        {
            return Task.FromResult(
                new LdapValidationResult(false, LdapBindFailureMessageResolver.ResolveForServiceAccountBind(exception)));
        }
        catch (SocketException exception) when (exception.SocketErrorCode == SocketError.TimedOut)
        {
            return Task.FromResult(new LdapValidationResult(false, LdapOperationTimedOutMessage));
        }
        catch (TimeoutException)
        {
            return Task.FromResult(new LdapValidationResult(false, LdapOperationTimedOutMessage));
        }
        catch (Exception ex)
        {
            LogUnexpectedLdapFailure(ex);
            return Task.FromResult(new LdapValidationResult(false, LdapBindFailureMessageResolver.ValidationFailedMessage));
        }
    }

    public Task<LdapValidationResult> ValidateSearchBasesAsync(
        LdapSearchBasesValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(request.Host) ||
            string.IsNullOrWhiteSpace(request.BaseDn) ||
            string.IsNullOrWhiteSpace(request.BindUserName) ||
            string.IsNullOrWhiteSpace(request.BindPassword))
        {
            return Task.FromResult(new LdapValidationResult(false, MissingRequiredFieldsMessage));
        }

        try
        {
            var bindIdentity = BuildBindIdentity(request.BindUserName, request.BindUserDomain);
            if (string.IsNullOrWhiteSpace(bindIdentity))
            {
                return Task.FromResult(new LdapValidationResult(false, MissingRequiredFieldsMessage));
            }

            using var connection = CreateConnection(request.Host, bindIdentity, request.BindPassword);
            try
            {
                connection.Bind();
            }
            catch (LdapException exception) when (IsLikelyConnectionOrLdapTimeout(exception))
            {
                return Task.FromResult(new LdapValidationResult(false, LdapOperationTimedOutMessage));
            }
            catch (LdapException exception)
            {
                return Task.FromResult(
                    new LdapValidationResult(false, LdapBindFailureMessageResolver.ResolveForServiceAccountBind(exception)));
            }

            var baseDn = request.BaseDn.Trim();
            var baseResult = TryResolveLdapSearchBase(connection, baseDn, BaseDnCouldNotBeResolvedMessage);
            if (!baseResult.IsValid)
            {
                return Task.FromResult(baseResult);
            }

            var userSearchBase = string.IsNullOrWhiteSpace(request.UserSearchBase) ? string.Empty : request.UserSearchBase.Trim();
            if (!string.IsNullOrWhiteSpace(userSearchBase))
            {
                var userBaseResult = TryResolveLdapSearchBase(connection, userSearchBase, UserSearchBaseCouldNotBeResolvedMessage);
                if (!userBaseResult.IsValid)
                {
                    return Task.FromResult(userBaseResult);
                }
            }

            return Task.FromResult(new LdapValidationResult(true, BindValidationSucceededMessage));
        }
        catch (Exception exception) when (IsLikelyLdapNetworkTimeout(exception))
        {
            return Task.FromResult(new LdapValidationResult(false, LdapOperationTimedOutMessage));
        }
        catch (Exception ex)
        {
            LogUnexpectedLdapFailure(ex);
            return Task.FromResult(new LdapValidationResult(false, LdapBindFailureMessageResolver.ValidationFailedMessage));
        }
    }

    public Task<LdapValidationResult> ValidateAsync(LdapValidationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Host) ||
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
            var bindIdentity = BuildBindIdentity(request.BindUserName, request.BindUserDomain);
            if (string.IsNullOrWhiteSpace(bindIdentity))
            {
                return Task.FromResult(new LdapValidationResult(false, MissingRequiredFieldsMessage));
            }

            using var serviceConnection = CreateConnection(request.Host, bindIdentity, request.BindPassword);
            try
            {
                serviceConnection.Bind();
            }
            catch (LdapException exception) when (IsLikelyConnectionOrLdapTimeout(exception))
            {
                return Task.FromResult(new LdapValidationResult(false, LdapOperationTimedOutMessage));
            }
            catch (LdapException exception)
            {
                return Task.FromResult(
                    new LdapValidationResult(false, LdapBindFailureMessageResolver.ResolveForServiceAccountBind(exception)));
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
                "mail")
            {
                TimeLimit = LdapOperationTimeout
            };

            SearchResponse searchResponse;
            try
            {
                searchResponse = (SearchResponse)serviceConnection.SendRequest(searchRequest);
            }
            catch (LdapException exception) when (IsLikelyConnectionOrLdapTimeout(exception))
            {
                return Task.FromResult(new LdapValidationResult(false, LdapOperationTimedOutMessage));
            }
            catch (LdapException)
            {
                return Task.FromResult(new LdapValidationResult(false, LdapBindFailureMessageResolver.ValidationFailedMessage));
            }

            if (searchResponse.Entries.Count == 0)
            {
                return Task.FromResult(new LdapValidationResult(false, DirectoryUserNotFoundMessage));
            }

            var userDn = searchResponse.Entries[0].DistinguishedName;
            if (string.IsNullOrWhiteSpace(userDn))
            {
                return Task.FromResult(new LdapValidationResult(false, DirectoryUserDistinguishedNameNotFoundMessage));
            }

            using var userConnection = CreateConnection(request.Host, userDn, request.TestPassword);
            try
            {
                userConnection.Bind();
                return Task.FromResult(new LdapValidationResult(true, ValidationSucceededMessage));
            }
            catch (LdapException exception) when (IsLikelyConnectionOrLdapTimeout(exception))
            {
                return Task.FromResult(new LdapValidationResult(false, LdapOperationTimedOutMessage));
            }
            catch (LdapException)
            {
                return Task.FromResult(new LdapValidationResult(false, DirectoryUserBindFailedMessage));
            }
        }
        catch (Exception exception) when (IsLikelyLdapNetworkTimeout(exception))
        {
            return Task.FromResult(new LdapValidationResult(false, LdapOperationTimedOutMessage));
        }
        catch (Exception ex)
        {
            LogUnexpectedLdapFailure(ex);
            return Task.FromResult(new LdapValidationResult(false, LdapBindFailureMessageResolver.ValidationFailedMessage));
        }
    }

    public Task<LdapUserProfile?> GetUserProfileAsync(
        LdapUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(request.Host) ||
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
            var bindIdentity = BuildBindIdentity(request.BindUserName, request.BindUserDomain);
            if (string.IsNullOrWhiteSpace(bindIdentity))
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            using var serviceConnection = CreateConnection(request.Host, bindIdentity, request.BindPassword);
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

            var searchRequest = new SearchRequest(
                searchBase,
                searchFilter,
                SearchScope.Subtree,
                attributeNames.ToArray())
            {
                TimeLimit = LdapOperationTimeout
            };

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
            catch (Exception)
            {
                // Invalid objectGUID attribute value; treat as missing profile data.
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

            return Task.FromResult<LdapUserProfile?>(
                new LdapUserProfile(directoryObjectId, resolvedUserName, displayName, email));
        }
        catch (Exception exception) when (IsLikelyLdapNetworkTimeout(exception))
        {
            return Task.FromResult<LdapUserProfile?>(null);
        }
        catch (Exception ex)
        {
            LogUnexpectedLdapFailure(ex);
            return Task.FromResult<LdapUserProfile?>(null);
        }
    }

    public Task<LdapUserProfile?> GetUserProfileByObjectIdAsync(
        LdapUserProfileByObjectIdRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(request.Host) ||
            string.IsNullOrWhiteSpace(request.BaseDn) ||
            string.IsNullOrWhiteSpace(request.BindUserName) ||
            string.IsNullOrWhiteSpace(request.BindPassword) ||
            string.IsNullOrWhiteSpace(request.DirectoryObjectId))
        {
            return Task.FromResult<LdapUserProfile?>(null);
        }

        if (!Guid.TryParse(request.DirectoryObjectId.Trim(), out var directoryGuid))
        {
            return Task.FromResult<LdapUserProfile?>(null);
        }

        var escapedObjectGuidFilter = EscapeObjectGuidBinaryForLdapFilter(directoryGuid.ToByteArray());
        var searchFilter =
            $"(&(objectCategory=person)(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2))(objectGUID={escapedObjectGuidFilter}))";
        var searchBase = string.IsNullOrWhiteSpace(request.UserSearchBase) ? request.BaseDn : request.UserSearchBase;

        try
        {
            var bindIdentity = BuildBindIdentity(request.BindUserName, request.BindUserDomain);
            if (string.IsNullOrWhiteSpace(bindIdentity))
            {
                return Task.FromResult<LdapUserProfile?>(null);
            }

            using var serviceConnection = CreateConnection(request.Host, bindIdentity, request.BindPassword);
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

            var searchRequest = new SearchRequest(
                searchBase,
                searchFilter,
                SearchScope.Subtree,
                attributeNames.ToArray())
            {
                TimeLimit = LdapOperationTimeout
            };

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
            catch (Exception)
            {
                // Invalid objectGUID attribute value; treat as missing profile data.
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

            return Task.FromResult<LdapUserProfile?>(
                new LdapUserProfile(directoryObjectId, resolvedUserName, displayName, email));
        }
        catch (Exception exception) when (IsLikelyLdapNetworkTimeout(exception))
        {
            return Task.FromResult<LdapUserProfile?>(null);
        }
        catch (Exception ex)
        {
            LogUnexpectedLdapFailure(ex);
            return Task.FromResult<LdapUserProfile?>(null);
        }
    }

    public Task<IReadOnlyCollection<LdapUserLookupItem>> SearchUsersAsync(
        LdapUserLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(request.Host) ||
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
            var bindIdentity = BuildBindIdentity(request.BindUserName, request.BindUserDomain);
            if (string.IsNullOrWhiteSpace(bindIdentity))
            {
                return Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(Array.Empty<LdapUserLookupItem>());
            }

            using var serviceConnection = CreateConnection(request.Host, bindIdentity, request.BindPassword);
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

            var searchRequest = new SearchRequest(
                searchBase,
                searchFilter,
                SearchScope.Subtree,
                attributeNames.ToArray())
            {
                SizeLimit = Math.Min(500, Math.Max(request.MaxResults * 5, request.MaxResults)),
                TimeLimit = LdapOperationTimeout
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
                catch (Exception)
                {
                    // Skip directory entries with invalid objectGUID values.
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

                seenObjectIds.Add(directoryObjectId);
                seenUserNames.Add(resolvedUserName);
                collected.Add(new LdapUserLookupItem(
                    directoryObjectId,
                    resolvedUserName,
                    displayName,
                    email,
                    entry.DistinguishedName));
            }

            return Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(collected);
        }
        catch (Exception exception) when (IsLikelyLdapNetworkTimeout(exception))
        {
            return Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(Array.Empty<LdapUserLookupItem>());
        }
        catch (Exception ex)
        {
            LogUnexpectedLdapFailure(ex);
            return Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(Array.Empty<LdapUserLookupItem>());
        }
    }

    public Task<LdapOrganizationalUnitSearchResult> SearchOrganizationalUnitsAsync(
        LdapOrganizationalUnitSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(request.Host) ||
            string.IsNullOrWhiteSpace(request.BaseDn) ||
            string.IsNullOrWhiteSpace(request.BindUserName) ||
            string.IsNullOrWhiteSpace(request.BindPassword))
        {
            return Task.FromResult(new LdapOrganizationalUnitSearchResult(
                Array.Empty<LdapOrganizationalUnitListItem>(),
                false));
        }

        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search) && search.Length < SetupConstants.MinOuSearchLength)
        {
            return Task.FromResult(new LdapOrganizationalUnitSearchResult(
                Array.Empty<LdapOrganizationalUnitListItem>(),
                false));
        }

        try
        {
            var bindIdentity = BuildBindIdentity(request.BindUserName, request.BindUserDomain);
            if (string.IsNullOrWhiteSpace(bindIdentity))
            {
                return Task.FromResult(new LdapOrganizationalUnitSearchResult(
                    Array.Empty<LdapOrganizationalUnitListItem>(),
                    false));
            }

            using var connection = CreateConnection(request.Host, bindIdentity, request.BindPassword);
            try
            {
                connection.Bind();
            }
            catch (LdapException)
            {
                return Task.FromResult(new LdapOrganizationalUnitSearchResult(
                    Array.Empty<LdapOrganizationalUnitListItem>(),
                    false));
            }

            var searchBase = string.IsNullOrWhiteSpace(request.ParentDistinguishedName)
                ? request.BaseDn.Trim()
                : request.ParentDistinguishedName.Trim();

            var baseResult = TryResolveLdapSearchBase(connection, searchBase, BaseDnCouldNotBeResolvedMessage);
            if (!baseResult.IsValid)
            {
                return Task.FromResult(new LdapOrganizationalUnitSearchResult(
                    Array.Empty<LdapOrganizationalUnitListItem>(),
                    false));
            }

            var filter = BuildOrganizationalUnitSearchFilter(search);
            var scope = string.IsNullOrWhiteSpace(search) ? SearchScope.OneLevel : SearchScope.Subtree;
            var maxResults = request.MaxResults > 0 ? request.MaxResults : SetupConstants.MaxOuSearchResults;
            var fetchLimit = maxResults + 1;

            var searchRequest = new SearchRequest(
                searchBase,
                filter,
                scope,
                "distinguishedName",
                "displayName",
                "name",
                "ou")
            {
                SizeLimit = fetchLimit,
                TimeLimit = LdapOperationTimeout
            };

            SearchResponse searchResponse;
            try
            {
                searchResponse = (SearchResponse)connection.SendRequest(searchRequest);
            }
            catch (LdapException)
            {
                return Task.FromResult(new LdapOrganizationalUnitSearchResult(
                    Array.Empty<LdapOrganizationalUnitListItem>(),
                    false));
            }

            var collected = new List<LdapOrganizationalUnitListItem>(maxResults);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (SearchResultEntry entry in searchResponse.Entries)
            {
                if (collected.Count >= maxResults)
                {
                    break;
                }

                if (!TryMapOrganizationalUnit(entry, out var item))
                {
                    continue;
                }

                if (!seen.Add(item.DistinguishedName))
                {
                    continue;
                }

                collected.Add(item);
            }

            var hasMore = searchResponse.Entries.Count > maxResults || collected.Count >= maxResults;
            return Task.FromResult(new LdapOrganizationalUnitSearchResult(collected, hasMore));
        }
        catch (Exception exception) when (IsLikelyLdapNetworkTimeout(exception))
        {
            return Task.FromResult(new LdapOrganizationalUnitSearchResult(
                Array.Empty<LdapOrganizationalUnitListItem>(),
                false));
        }
        catch (Exception ex)
        {
            LogUnexpectedLdapFailure(ex);
            return Task.FromResult(new LdapOrganizationalUnitSearchResult(
                Array.Empty<LdapOrganizationalUnitListItem>(),
                false));
        }
    }

    private static string BuildOrganizationalUnitSearchFilter(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return "(objectClass=organizationalUnit)";
        }

        var escaped = AdLdapFilterHelper.EscapeFilterValue(search.Trim());
        return
            $"(&(objectClass=organizationalUnit)(|(displayName=*{escaped}*)(name=*{escaped}*)(ou=*{escaped}*)(distinguishedName=*{escaped}*)))";
    }

    private static bool TryMapOrganizationalUnit(
        SearchResultEntry entry,
        out LdapOrganizationalUnitListItem item)
    {
        item = null!;
        var distinguishedName = GetFirstString(TryGetDirectoryAttribute(entry, "distinguishedName"));
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            distinguishedName = entry.DistinguishedName;
        }

        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return false;
        }

        var displayName = GetFirstString(TryGetDirectoryAttribute(entry, "displayName"));
        var name = GetFirstString(TryGetDirectoryAttribute(entry, "name"));
        var ou = GetFirstString(TryGetDirectoryAttribute(entry, "ou"));
        item = new LdapOrganizationalUnitListItem(
            distinguishedName,
            name,
            displayName,
            ou,
            AdOrganizationalUnitLabelBuilder.Build(distinguishedName, displayName, name, ou));
        return true;
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

    private static bool IsLikelyConnectionOrLdapTimeout(LdapException exception)
    {
        var code = exception.ErrorCode;
        if (code == (int)ResultCode.TimeLimitExceeded)
        {
            return true;
        }

        if (code == (int)ResultCode.Unavailable || code == (int)ResultCode.Busy)
        {
            return true;
        }

        // LDAP_SERVER_DOWN (81) — not exposed on ResultCode on all targets.
        return code == 81;
    }

    private static bool IsLikelyLdapNetworkTimeout(Exception exception)
    {
        return exception switch
        {
            LdapException ldap => IsLikelyConnectionOrLdapTimeout(ldap),
            SocketException socket => socket.SocketErrorCode == SocketError.TimedOut,
            TimeoutException => true,
            _ => false,
        };
    }

    private static LdapConnection CreateConnection(string host, string userName, string password)
    {
        var identifier = new LdapDirectoryIdentifier(host, LdapConnectionDefaults.StandardLdapsPort);
        var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Basic,
            Credential = new NetworkCredential(userName, password)
        };

        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = true;
        connection.Timeout = LdapOperationTimeout;

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

    private static LdapValidationResult TryResolveLdapSearchBase(LdapConnection connection, string distinguishedName, string failureMessage)
    {
        try
        {
            var searchRequest = new SearchRequest(
                distinguishedName,
                "(objectClass=*)",
                SearchScope.Base,
                "distinguishedName")
            {
                SizeLimit = 1,
                TimeLimit = LdapOperationTimeout
            };

            var searchResponse = (SearchResponse)connection.SendRequest(searchRequest);
            if (searchResponse.ResultCode != ResultCode.Success || searchResponse.Entries.Count == 0)
            {
                return new LdapValidationResult(false, failureMessage);
            }

            return new LdapValidationResult(true, BindValidationSucceededMessage);
        }
        catch (LdapException exception) when (IsLikelyConnectionOrLdapTimeout(exception))
        {
            return new LdapValidationResult(false, LdapOperationTimedOutMessage);
        }
        catch (LdapException)
        {
            return new LdapValidationResult(false, failureMessage);
        }
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

    private void LogUnexpectedLdapFailure(
        Exception ex,
        [CallerMemberName] string operationName = "")
    {
        logger.LogWarning(
            ex,
            "LDAP operation failed unexpectedly. Operation={OperationName}",
            operationName);
    }
}
