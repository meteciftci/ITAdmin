using System.DirectoryServices.Protocols;
using System.Net;
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
            string.IsNullOrWhiteSpace(request.BindDn) ||
            string.IsNullOrWhiteSpace(request.BindPassword) ||
            string.IsNullOrWhiteSpace(request.TestUserName) ||
            string.IsNullOrWhiteSpace(request.TestPassword))
        {
            return Task.FromResult(new LdapValidationResult(false, MissingRequiredFieldsMessage));
        }

        try
        {
            var identifier = new LdapDirectoryIdentifier(request.Host, request.Port);

            using var serviceConnection = CreateConnection(identifier, request.UseSsl, request.BindDn, request.BindPassword);
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

    private static string EscapeLdapFilterValue(string value)
    {
        return value
            .Replace("\\", "\\5c", StringComparison.Ordinal)
            .Replace("*", "\\2a", StringComparison.Ordinal)
            .Replace("(", "\\28", StringComparison.Ordinal)
            .Replace(")", "\\29", StringComparison.Ordinal)
            .Replace("\0", "\\00", StringComparison.Ordinal);
    }
}
