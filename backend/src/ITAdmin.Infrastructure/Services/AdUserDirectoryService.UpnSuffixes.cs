using System.DirectoryServices.Protocols;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed partial class AdUsersDirectoryService
{
    public async Task<AdUpnSuffixesResult> GetUpnSuffixesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return BuildDomainOnlyFallback(settings.DomainFqdn, settings.DefaultNamingContext, settings.BaseDn);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context, cancellationToken);
            var connection = connectionResult.Context.Connection;
            var directoryRead = ReadUpnSuffixesFromDirectory(ldapConnection);

            var defaultNamingContext = directoryRead.DefaultNamingContext
                ?? connection.DefaultNamingContext;
            var buildResult = AdUpnSuffixListBuilder.Build(
                directoryRead.ForestSuffixes,
                connection.DomainFqdn,
                AdLdapDnHelper.ConvertNamingContextToDnsSuffix(defaultNamingContext),
                AdLdapDnHelper.ConvertNamingContextToDnsSuffix(connection.BaseDn),
                settingsOnlyFallback: false);

            if (buildResult.Items.Count == 0)
            {
                if (directoryRead.PartitionsReadSucceeded)
                {
                    return new AdUpnSuffixesResult(
                        false,
                        AdManagementApiMessageKeys.Users.MissingUpnSuffix,
                        null,
                        null,
                        AdDirectoryFailureKind.NotConfigured);
                }

                return BuildDomainOnlyFallback(
                    connection.DomainFqdn ?? settings.DomainFqdn,
                    defaultNamingContext ?? settings.DefaultNamingContext,
                    connection.BaseDn ?? settings.BaseDn);
            }

            return new AdUpnSuffixesResult(true, string.Empty, buildResult.Items, buildResult.Warning);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LdapException)
        {
            return BuildDomainOnlyFallback(
                connectionResult.Context.Connection.DomainFqdn ?? settings.DomainFqdn,
                connectionResult.Context.Connection.DefaultNamingContext ?? settings.DefaultNamingContext,
                connectionResult.Context.Connection.BaseDn ?? settings.BaseDn);
        }
        catch (DirectoryOperationException)
        {
            return BuildDomainOnlyFallback(
                connectionResult.Context.Connection.DomainFqdn ?? settings.DomainFqdn,
                connectionResult.Context.Connection.DefaultNamingContext ?? settings.DefaultNamingContext,
                connectionResult.Context.Connection.BaseDn ?? settings.BaseDn);
        }
        catch (Exception ex)
        {
            LogUnexpectedDirectoryFailure(ex);
            return BuildDomainOnlyFallback(
                connectionResult.Context.Connection.DomainFqdn ?? settings.DomainFqdn,
                connectionResult.Context.Connection.DefaultNamingContext ?? settings.DefaultNamingContext,
                connectionResult.Context.Connection.BaseDn ?? settings.BaseDn);
        }
    }

    private AdUpnSuffixesResult BuildDomainOnlyFallback(
        string? domainFqdn,
        string? defaultNamingContext,
        string? baseDn)
    {
        var buildResult = AdUpnSuffixListBuilder.Build(
            Array.Empty<DiscoveredUpnSuffix>(),
            domainFqdn,
            AdLdapDnHelper.ConvertNamingContextToDnsSuffix(defaultNamingContext),
            AdLdapDnHelper.ConvertNamingContextToDnsSuffix(baseDn),
            settingsOnlyFallback: true);

        if (buildResult.Items.Count == 0)
        {
            return new AdUpnSuffixesResult(
                false,
                AdManagementApiMessageKeys.Users.MissingUpnSuffix,
                null,
                AdUpnSuffixListBuilder.ForestReadFallbackWarning,
                AdDirectoryFailureKind.NotConfigured);
        }

        return new AdUpnSuffixesResult(
            true,
            string.Empty,
            buildResult.Items,
            buildResult.Warning);
    }

    private static UpnSuffixDirectoryReadResult ReadUpnSuffixesFromDirectory(LdapConnection ldapConnection)
    {
        ldapConnection.SessionOptions.ReferralChasing = ReferralChasingOptions.All;

        var forestSuffixes = new List<DiscoveredUpnSuffix>();
        var rootDse = ReadRootDse(ldapConnection);
        if (rootDse is null || string.IsNullOrWhiteSpace(rootDse.ConfigurationNamingContext))
        {
            return new UpnSuffixDirectoryReadResult(
                forestSuffixes,
                rootDse?.DefaultNamingContext,
                RootDseReadSucceeded: rootDse is not null,
                PartitionsReadSucceeded: false);
        }

        var partitionsDn = $"CN=Partitions,{rootDse.ConfigurationNamingContext}";
        var partitionsReadSucceeded = TryReadSuffixAttributeValues(
            ldapConnection,
            partitionsDn,
            "(objectClass=*)",
            SearchScope.Base,
            "uPNSuffixes",
            out var forestValues);

        if (partitionsReadSucceeded)
        {
            foreach (var value in forestValues)
            {
                forestSuffixes.Add(new DiscoveredUpnSuffix(value, AdUpnSuffixSources.Forest));
            }
        }

        return new UpnSuffixDirectoryReadResult(
            forestSuffixes,
            rootDse.DefaultNamingContext,
            RootDseReadSucceeded: true,
            PartitionsReadSucceeded: partitionsReadSucceeded);
    }

    private static bool TryReadSuffixAttributeValues(
        LdapConnection ldapConnection,
        string searchBase,
        string filter,
        SearchScope scope,
        string attributeName,
        out List<string> values)
    {
        values = [];

        try
        {
            var searchRequest = new SearchRequest(
                searchBase,
                filter,
                scope,
                attributeName)
            {
                SizeLimit = 100,
                TimeLimit = LdapOperationTimeout,
            };

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success)
            {
                return false;
            }

            foreach (SearchResultEntry entry in response.Entries)
            {
                values.AddRange(GetAllStrings(entry, attributeName));
            }

            return true;
        }
        catch (LdapException)
        {
            values = [];
            return false;
        }
        catch (DirectoryOperationException)
        {
            values = [];
            return false;
        }
        catch (Exception)
        {
            // Unexpected LDAP attribute read failure falls back to empty suffix values.
            values = [];
            return false;
        }
    }

    private static RootDseAttributes? ReadRootDse(LdapConnection ldapConnection)
    {
        try
        {
            var searchRequest = new SearchRequest(
                string.Empty,
                "(objectClass=*)",
                SearchScope.Base,
                "defaultNamingContext",
                "configurationNamingContext",
                "dnsHostName",
                "rootDomainNamingContext")
            {
                TimeLimit = LdapOperationTimeout,
            };

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
            {
                return null;
            }

            var entry = response.Entries[0];
            return new RootDseAttributes(
                GetFirstString(entry, "defaultNamingContext"),
                GetFirstString(entry, "configurationNamingContext"),
                GetFirstString(entry, "dnsHostName"),
                GetFirstString(entry, "rootDomainNamingContext"));
        }
        catch (LdapException)
        {
            return null;
        }
        catch (DirectoryOperationException)
        {
            return null;
        }
        catch (Exception)
        {
            // Unexpected rootDSE read failure falls back to null metadata.
            return null;
        }
    }

    private sealed record UpnSuffixDirectoryReadResult(
        List<DiscoveredUpnSuffix> ForestSuffixes,
        string? DefaultNamingContext,
        bool RootDseReadSucceeded,
        bool PartitionsReadSucceeded);

    private sealed record RootDseAttributes(
        string? DefaultNamingContext,
        string? ConfigurationNamingContext,
        string? DnsHostName,
        string? RootDomainNamingContext);
}
