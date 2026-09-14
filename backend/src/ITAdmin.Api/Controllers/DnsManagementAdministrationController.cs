using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.DnsManagement;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppModels = ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/dns-management")]
[Authorize]
public sealed class DnsManagementAdministrationController(
    IDnsManagementAdministrationService service,
    IDnsServerConnectionTestService connectionTestService,
    IDnsInventorySyncService inventorySyncService,
    IDnsServerSettingsService serverSettingsService,
    IDnsPolicyManagementService policyManagementService,
    IDnssecManagementService dnssecManagementService,
    IDnsScavengingManagementService scavengingManagementService,
    IDnsNetworkConfigurationService networkConfigurationService,
    IDnsZoneTransferManagementService zoneTransferManagementService) : ControllerBase
{
    [HttpGet("settings")]
    [RequirePermission(DnsManagementPermissions.ManageSettings)]
    public async Task<ActionResult<DnsManagementSettingsResponse>> GetSettings(CancellationToken cancellationToken) =>
        Ok(Map(await service.GetSettingsAsync(cancellationToken)));

    [HttpPut("settings")]
    [RequirePermission(DnsManagementPermissions.ManageSettings)]
    public async Task<ActionResult<DnsManagementSettingsResponse>> UpdateSettings(
        UpdateDnsManagementSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateSettingsAsync(new(
            request.IsEnabled, request.AutomaticSyncEnabled, request.DefaultSyncIntervalMinutes,
            request.HealthCheckIntervalMinutes, request.CommandTimeoutSeconds, request.MaxParallelServers,
            request.SnapshotRetentionDays, request.SyncRecordInventory,
            request.PromptForFullSyncOnComparisonOpen, request.ComparisonSnapshotStaleAfterMinutes,
            DnsManagementActorResolver.Resolve(this)), cancellationToken);
        return result.IsSuccess && result.Value is not null ? Ok(Map(result.Value)) : BadRequest(new { message = result.Message });
    }

    [HttpGet("credential-profiles")]
    [RequirePermission(DnsManagementPermissions.ServersManage)]
    public async Task<ActionResult<IReadOnlyList<DnsCredentialProfileResponse>>> GetCredentials(CancellationToken cancellationToken) =>
        Ok((await service.GetCredentialProfilesAsync(cancellationToken)).Select(Map));

    [HttpPost("credential-profiles")]
    [RequirePermission(DnsManagementPermissions.ServersManage)]
    public Task<ActionResult<DnsCredentialProfileResponse>> CreateCredential(
        SaveDnsCredentialProfileRequest request, CancellationToken cancellationToken) => SaveCredential(null, request, cancellationToken);

    [HttpPut("credential-profiles/{id:guid}")]
    [RequirePermission(DnsManagementPermissions.ServersManage)]
    public Task<ActionResult<DnsCredentialProfileResponse>> UpdateCredential(
        Guid id, SaveDnsCredentialProfileRequest request, CancellationToken cancellationToken) => SaveCredential(id, request, cancellationToken);

    [HttpDelete("credential-profiles/{id:guid}")]
    [RequirePermission(DnsManagementPermissions.ServersManage)]
    public async Task<IActionResult> DeleteCredential(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.DeleteCredentialProfileAsync(id, DnsManagementActorResolver.Resolve(this), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { message = result.Message });
    }

    [HttpGet("servers")]
    [RequireAnyPermission(DnsManagementPermissions.ServersView, DnsManagementPermissions.ManageServerSettings, DnsManagementPermissions.ClearCache, DnsManagementPermissions.ManagePolicies, DnsManagementPermissions.ManageDnssec, DnsManagementPermissions.ManageScavenging, DnsManagementPermissions.ManageNetworkConfiguration, DnsManagementPermissions.ManageZoneTransfers)]
    public async Task<ActionResult<IReadOnlyList<DnsServerResponse>>> GetServers(CancellationToken cancellationToken) =>
        Ok((await service.GetServersAsync(cancellationToken)).Select(Map));

    [HttpPost("servers")]
    [RequirePermission(DnsManagementPermissions.ServersManage)]
    public Task<ActionResult<DnsServerResponse>> CreateServer(SaveDnsServerRequest request, CancellationToken cancellationToken) =>
        SaveServer(null, request, cancellationToken);

    [HttpPut("servers/{id:guid}")]
    [RequirePermission(DnsManagementPermissions.ServersManage)]
    public Task<ActionResult<DnsServerResponse>> UpdateServer(Guid id, SaveDnsServerRequest request, CancellationToken cancellationToken) =>
        SaveServer(id, request, cancellationToken);

    [HttpPost("servers/{id:guid}/test-connection")]
    [RequirePermission(DnsManagementPermissions.ServersTestConnection)]
    public async Task<ActionResult<DnsServerConnectionTestResponse>> TestServerConnection(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await connectionTestService.TestAsync(
            id, DnsManagementActorResolver.Resolve(this), cancellationToken);
        return result.IsSuccess && result.Value is not null
            ? Ok(Map(result.Value))
            : BadRequest(new { message = result.Message });
    }

    [HttpPost("servers/{id:guid}/synchronizations")]
    [RequirePermission(DnsManagementPermissions.Synchronize)]
    public async Task<ActionResult<DnsSyncJobResponse>> SynchronizeServer(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await inventorySyncService.EnqueueAsync(
            id, DnsManagementActorResolver.Resolve(this), cancellationToken);
        return result.IsSuccess && result.Value is not null
            ? Accepted(Map(result.Value))
            : BadRequest(new { message = result.Message });
    }

    [HttpGet("servers/{id:guid}/server-settings")]
    [RequirePermission(DnsManagementPermissions.ManageServerSettings)]
    public async Task<ActionResult<DnsServerSettingsResponse>> GetServerSettings(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await serverSettingsService.GetAsync(id, cancellationToken);
        return result.Success && result.Settings is not null
            ? Ok(Map(result.Settings))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpPut("servers/{id:guid}/server-settings")]
    [RequirePermission(DnsManagementPermissions.ManageServerSettings)]
    public async Task<ActionResult<DnsServerOperationResponse>> UpdateServerSettings(
        Guid id, UpdateDnsServerSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await serverSettingsService.UpdateAsync(new(
            id, request.ForwarderAddresses ?? [], request.ForwarderUseRootHint,
            request.ForwarderTimeoutSeconds, request.ForwarderEnableReordering,
            request.RecursionEnabled, request.RecursionAdditionalTimeoutSeconds,
            request.RecursionRetryIntervalSeconds, request.RecursionTimeoutSeconds,
            request.RecursionSecureResponse, request.ExpectedStateToken,
            DnsManagementActorResolver.Resolve(this)), cancellationToken);
        return result.Success
            ? Ok(Map(result))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpPost("servers/{id:guid}/cache/clear")]
    [RequirePermission(DnsManagementPermissions.ClearCache)]
    public async Task<ActionResult<DnsServerOperationResponse>> ClearServerCache(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await serverSettingsService.ClearCacheAsync(
            id, DnsManagementActorResolver.Resolve(this), cancellationToken);
        return result.Success
            ? Ok(Map(result))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpGet("servers/{id:guid}/policy-configuration")]
    [RequirePermission(DnsManagementPermissions.ManagePolicies)]
    public async Task<ActionResult<DnsPolicyConfigurationResponse>> GetPolicyConfiguration(Guid id, CancellationToken cancellationToken)
    {
        var result = await policyManagementService.GetAsync(id, cancellationToken);
        return result.Success && result.Configuration is not null ? Ok(Map(result.Configuration)) : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpPost("servers/{id:guid}/policy-configuration/mutations")]
    [RequirePermission(DnsManagementPermissions.ManagePolicies)]
    public async Task<ActionResult<DnsPolicyOperationResponse>> MutatePolicyConfiguration(Guid id, DnsPolicyMutationRequest request, CancellationToken cancellationToken)
    {
        static AppModels.DnsPolicyCriterionModel? Criterion(DnsPolicyCriterionRequest? x) => x is null ? null : new(x.Operator, x.Values ?? []);
        var result = await policyManagementService.MutateAsync(new(id, request.Action, request.Name, request.ZoneName,
            request.Ipv4Subnets ?? [], request.Ipv6Subnets ?? [], request.Level, request.Decision, request.Condition,
            request.ProcessingOrder, request.Enabled, Criterion(request.ClientSubnet), Criterion(request.Fqdn), Criterion(request.QueryType),
            Criterion(request.TransportProtocol), Criterion(request.InternetProtocol), Criterion(request.ServerInterfaceIp),
            request.ZoneScopes?.Select(x => new AppModels.DnsZoneScopeWeightModel(x.Name, x.Weight)).ToArray() ?? [],
            request.ExpectedStateToken, DnsManagementActorResolver.Resolve(this)), cancellationToken);
        return result.Success ? Ok(new DnsPolicyOperationResponse(true, null, result.Message, result.Configuration is null ? null : Map(result.Configuration)))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpGet("servers/{id:guid}/dnssec-configuration")]
    [RequirePermission(DnsManagementPermissions.ManageDnssec)]
    public async Task<ActionResult<DnssecConfigurationResponse>> GetDnssecConfiguration(Guid id, CancellationToken cancellationToken)
    {
        var result = await dnssecManagementService.GetAsync(id, cancellationToken);
        return result.Success && result.Configuration is not null
            ? Ok(Map(result.Configuration))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpPost("servers/{id:guid}/dnssec-configuration/mutations")]
    [RequirePermission(DnsManagementPermissions.ManageDnssec)]
    public async Task<ActionResult<DnssecOperationResponse>> MutateDnssecConfiguration(
        Guid id, DnssecMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await dnssecManagementService.MutateAsync(new(
            id, request.Action, request.ZoneName, request.KeyIds ?? [], request.ValidationEnabled,
            request.TrustPointName, request.TrustAnchorType, request.CryptoAlgorithm, request.KeyTag,
            request.DigestType, request.Digest, request.Base64Data, request.ExpectedStateToken,
            DnsManagementActorResolver.Resolve(this)), cancellationToken);
        return result.Success
            ? Ok(new DnssecOperationResponse(true, null, result.Message,
                result.Configuration is null ? null : Map(result.Configuration)))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpGet("servers/{id:guid}/scavenging-configuration")]
    [RequirePermission(DnsManagementPermissions.ManageScavenging)]
    public async Task<ActionResult<DnsScavengingConfigurationResponse>> GetScavengingConfiguration(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await scavengingManagementService.GetAsync(id, cancellationToken);
        return result.Success && result.Configuration is not null
            ? Ok(Map(result.Configuration))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpPost("servers/{id:guid}/scavenging-configuration/mutations")]
    [RequirePermission(DnsManagementPermissions.ManageScavenging)]
    public async Task<ActionResult<DnsScavengingOperationResponse>> MutateScavengingConfiguration(
        Guid id, DnsScavengingMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await scavengingManagementService.MutateAsync(new(id, request.Action,
            request.ScavengingEnabled, request.ScavengingIntervalHours, request.ZoneName,
            request.ZoneAgingEnabled, request.NoRefreshIntervalHours, request.RefreshIntervalHours,
            request.ScavengeServers ?? [], request.ExpectedStateToken,
            DnsManagementActorResolver.Resolve(this)), cancellationToken);
        return result.Success
            ? Ok(new DnsScavengingOperationResponse(true, null, result.Message,
                result.Configuration is null ? null : Map(result.Configuration)))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpGet("servers/{id:guid}/network-configuration")]
    [RequirePermission(DnsManagementPermissions.ManageNetworkConfiguration)]
    public async Task<ActionResult<DnsNetworkConfigurationResponse>> GetNetworkConfiguration(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await networkConfigurationService.GetAsync(id, cancellationToken);
        return result.Success && result.Configuration is not null
            ? Ok(Map(result.Configuration))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpPost("servers/{id:guid}/network-configuration/mutations")]
    [RequirePermission(DnsManagementPermissions.ManageNetworkConfiguration)]
    public async Task<ActionResult<DnsNetworkOperationResponse>> MutateNetworkConfiguration(
        Guid id, DnsNetworkMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await networkConfigurationService.MutateAsync(new(id, request.Action,
            request.ListeningIpAddresses ?? [], request.RootHintNameServer,
            request.RootHintIpAddresses ?? [], request.OriginalRootHintNameServer,
            request.ExpectedStateToken, DnsManagementActorResolver.Resolve(this)), cancellationToken);
        return result.Success
            ? Ok(new DnsNetworkOperationResponse(true, null, result.Message,
                result.Configuration is null ? null : Map(result.Configuration)))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpGet("servers/{id:guid}/zone-transfer-configuration")]
    [RequirePermission(DnsManagementPermissions.ManageZoneTransfers)]
    public async Task<ActionResult<DnsZoneTransferConfigurationResponse>> GetZoneTransferConfiguration(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await zoneTransferManagementService.GetAsync(id, cancellationToken);
        return result.Success && result.Configuration is not null
            ? Ok(Map(result.Configuration))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    [HttpPut("servers/{id:guid}/zone-transfer-configuration")]
    [RequirePermission(DnsManagementPermissions.ManageZoneTransfers)]
    public async Task<ActionResult<DnsZoneTransferOperationResponse>> UpdateZoneTransferConfiguration(
        Guid id, DnsZoneTransferMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await zoneTransferManagementService.UpdateAsync(new(id, request.ZoneName,
            request.TransferMode, request.SecondaryServers ?? [], request.NotifyMode,
            request.NotifyServers ?? [], request.ExpectedStateToken,
            DnsManagementActorResolver.Resolve(this)), cancellationToken);
        return result.Success
            ? Ok(new DnsZoneTransferOperationResponse(true, null, result.Message,
                result.Configuration is null ? null : Map(result.Configuration)))
            : BadRequest(new { code = result.ErrorCode, message = result.Message });
    }

    private async Task<ActionResult<DnsCredentialProfileResponse>> SaveCredential(
        Guid? id, SaveDnsCredentialProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SaveCredentialProfileAsync(new(id, request.Name, request.AuthenticationMode,
            request.UserName, request.Password, request.IsEnabled, DnsManagementActorResolver.Resolve(this)), cancellationToken);
        return result.IsSuccess && result.Value is not null ? Ok(Map(result.Value)) : BadRequest(new { message = result.Message });
    }

    private async Task<ActionResult<DnsServerResponse>> SaveServer(
        Guid? id, SaveDnsServerRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SaveServerAsync(new(id, request.DisplayName, request.HostName, request.Port,
            request.Environment, request.CredentialProfileId, request.IsEnabled, request.SyncIntervalMinutes,
            request.TlsCertificateThumbprint, request.Notes, DnsManagementActorResolver.Resolve(this)), cancellationToken);
        return result.IsSuccess && result.Value is not null ? Ok(Map(result.Value)) : BadRequest(new { message = result.Message });
    }

    private static DnsManagementSettingsResponse Map(AppModels.DnsManagementSettingsModel x) => new(
        x.IsEnabled, x.AutomaticSyncEnabled, x.DefaultSyncIntervalMinutes, x.HealthCheckIntervalMinutes,
        x.CommandTimeoutSeconds, x.MaxParallelServers, x.SnapshotRetentionDays, x.SyncRecordInventory,
        x.PromptForFullSyncOnComparisonOpen, x.ComparisonSnapshotStaleAfterMinutes, x.UpdatedAt, x.UpdatedBy);
    private static DnsCredentialProfileResponse Map(AppModels.DnsCredentialProfileModel x) => new(
        x.Id, x.Name, x.AuthenticationMode, x.UserName, x.HasPassword, x.IsEnabled,
        x.LastValidatedAt, x.LastValidationStatus, x.LastValidationMessage);
    private static DnsServerResponse Map(AppModels.DnsServerModel x) => new(
        x.Id, x.DisplayName, x.HostName, x.Port, x.Environment, x.CredentialProfileId,
        x.CredentialProfileName, x.IsEnabled, x.SyncIntervalMinutes, x.TlsCertificateThumbprint,
        x.Notes, x.OperatingSystemVersion, x.DnsServerVersion, x.LastSeenAt,
        x.LastSuccessfulSyncAt, x.LastSyncStatus, x.LastSyncMessage);
    private static DnsServerConnectionTestResponse Map(AppModels.DnsServerConnectionTestModel x) => new(
        x.ServerId, x.ServerDisplayName, x.Success, x.FailureKind, x.Message, x.HostAgentAvailable,
        x.NetworkReachable, x.TlsValidated, x.AuthenticationSucceeded, x.DnsModuleAvailable,
        x.DnsServiceReachable, x.OperatingSystemVersion, x.PowerShellVersion, x.DnsModuleVersion,
        x.DnsServerVersion, x.ZoneCount, x.Capabilities is null ? null : new(
            x.Capabilities.Zones, x.Capabilities.Records, x.Capabilities.ServerSettings,
            x.Capabilities.Dnssec, x.Capabilities.Policies, x.Capabilities.Scopes, x.Capabilities.Cache,
            x.Capabilities.NetworkConfiguration, x.Capabilities.ZoneTransfers),
        x.TestedAt);
    private static DnsSyncJobResponse Map(AppModels.DnsSyncJobModel x) => new(
        x.Id, x.BatchId, x.ServerId, x.ServerDisplayName, x.Scope, x.Trigger, x.Status,
        x.AttemptCount, x.RequestedAt, x.StartedAt, x.CompletedAt, x.ErrorCode, x.Message, x.AlreadyQueued);
    private static DnsServerSettingsResponse Map(AppModels.DnsServerSettingsModel x) => new(
        x.ForwarderAddresses, x.ForwarderUseRootHint, x.ForwarderTimeoutSeconds,
        x.ForwarderEnableReordering, x.RecursionEnabled, x.RecursionAdditionalTimeoutSeconds,
        x.RecursionRetryIntervalSeconds, x.RecursionTimeoutSeconds,
        x.RecursionSecureResponse, x.StateToken);
    private static DnsServerOperationResponse Map(AppModels.DnsServerSettingsOperationModel x) => new(
        x.Success, x.ErrorCode, x.Message, x.Settings is null ? null : Map(x.Settings));
    private static DnsPolicyConfigurationResponse Map(AppModels.DnsPolicyConfigurationModel x) => new(
        x.ClientSubnets.Select(v => new DnsClientSubnetResponse(v.Name, v.Ipv4Subnets, v.Ipv6Subnets)).ToArray(),
        x.ZoneScopes.Select(v => new DnsZoneScopeResponse(v.ZoneName, v.Name)).ToArray(),
        x.QueryPolicies.Select(v => new DnsQueryPolicyResponse(v.Name, v.Level, v.ZoneName, v.Action, v.Condition, v.ProcessingOrder,
            v.Enabled, v.ClientSubnet, v.Fqdn, v.QueryType, v.TransportProtocol, v.InternetProtocol, v.ServerInterfaceIp, v.ZoneScope)).ToArray(),
        x.StateToken);
    private static DnssecConfigurationResponse Map(AppModels.DnssecConfigurationModel x) => new(
        x.Zones.Select(zone => new DnssecZoneResponse(
            zone.Name, zone.ZoneType, zone.IsDsIntegrated, zone.IsAutoCreated, zone.IsSigned,
            zone.IsEligibleForSigning, zone.IneligibilityReason, zone.IsKeyMasterServer,
            zone.KeyMasterServer, zone.KeyMasterStatus, zone.DenialOfExistence,
            zone.Nsec3Iterations, zone.Nsec3OptOut, zone.DnsKeyRecordSetTtlSeconds,
            zone.DsRecordSetTtlSeconds, zone.DsRecordGenerationAlgorithms,
            zone.ParentHasSecureDelegation, zone.SigningKeys.Select(key => new DnssecSigningKeyResponse(
                key.KeyId, key.KeyType, key.CryptoAlgorithm, key.KeyLength, key.KeyStatus,
                key.KeyStorageProvider, key.IsRolloverEnabled, key.RolloverPeriodSeconds,
                key.NextRolloverAction, key.NextRolloverTime)).ToArray())).ToArray(),
        new DnssecResolverConfigurationResponse(x.Resolver.ValidationEnabled, x.Resolver.IsReadOnlyDomainController,
            x.Resolver.DirectoryServicesAvailable, x.Resolver.RootTrustAnchorsUrl,
            x.Resolver.TrustPoints.Select(point => new DnssecTrustPointResponse(point.Name, point.State,
                point.LastActiveRefreshTime, point.NextActiveRefreshTime,
                point.Anchors.Select(anchor => new DnssecTrustAnchorResponse(anchor.Type, anchor.State, anchor.Data)).ToArray())).ToArray()),
        x.StateToken);
    private static DnsScavengingConfigurationResponse Map(AppModels.DnsScavengingConfigurationModel x) => new(
        x.ScavengingEnabled, x.ScavengingIntervalSeconds, x.DefaultNoRefreshIntervalSeconds,
        x.DefaultRefreshIntervalSeconds, x.LastScavengeTime,
        x.Zones.Select(zone => new DnsZoneAgingResponse(zone.Name, zone.ZoneType, zone.AgingEnabled,
            zone.IsEligible, zone.IneligibilityReason, zone.NoRefreshIntervalSeconds,
            zone.RefreshIntervalSeconds, zone.AvailableForScavengeTime, zone.ScavengeServers)).ToArray(),
        x.StateToken);
    private static DnsNetworkConfigurationResponse Map(AppModels.DnsNetworkConfigurationModel x) => new(
        x.ListeningIpAddresses, x.AvailableIpAddresses,
        x.RootHints.Select(hint => new DnsRootHintResponse(hint.NameServer, hint.IpAddresses)).ToArray(),
        x.StateToken);
    private static DnsZoneTransferConfigurationResponse Map(AppModels.DnsZoneTransferConfigurationModel x) => new(
        x.Zones.Select(v => new DnsZoneTransferSettingResponse(v.ZoneName, v.IsDsIntegrated,
            v.TransferMode, v.SecondaryServers, v.NotifyMode, v.NotifyServers)).ToArray(), x.StateToken);
}
