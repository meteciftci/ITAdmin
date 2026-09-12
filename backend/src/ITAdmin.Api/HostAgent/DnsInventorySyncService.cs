using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models.DnsManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.HostAgent.Contracts;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.Api.HostAgent;

public sealed class DnsInventorySyncService(
    AppDbContext context,
    ISecretProtector secretProtector,
    IHostAgentClient hostAgentClient,
    ILogger<DnsInventorySyncService> logger) : IDnsInventorySyncService
{
    private const int PageSize = 250;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);

    public Task<DnsAdministrationResult<DnsSyncJobModel>> EnqueueAsync(
        Guid serverId, DnsActorContext actor, CancellationToken cancellationToken = default) =>
        EnqueueInternalAsync(serverId, actor, DnsSyncTrigger.Manual, 100, Guid.NewGuid(), cancellationToken);

    public Task<DnsAdministrationResult<DnsSyncJobModel>> EnqueuePostMutationAsync(
        Guid serverId, DnsActorContext actor, CancellationToken cancellationToken = default) =>
        EnqueueInternalAsync(serverId, actor, DnsSyncTrigger.PostMutation, 200, Guid.NewGuid(), cancellationToken);

    public async Task<DnsSyncBatchModel> EnqueueAllEnabledAsync(
        DnsActorContext actor, CancellationToken cancellationToken = default)
    {
        var serverIds = await context.DnsServers.AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.DisplayName)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var batchId = Guid.NewGuid();
        var jobs = new List<DnsSyncJobModel>();
        var queued = 0;
        var alreadyQueued = 0;
        var failed = 0;
        foreach (var serverId in serverIds)
        {
            var result = await EnqueueInternalAsync(
                serverId, actor, DnsSyncTrigger.Manual, 100, batchId, cancellationToken);
            if (!result.IsSuccess || result.Value is null)
            {
                failed++;
                continue;
            }

            jobs.Add(result.Value);
            if (result.Value.AlreadyQueued) alreadyQueued++;
            else queued++;
        }

        return new DnsSyncBatchModel(
            batchId, serverIds.Count, queued, alreadyQueued, failed, jobs);
    }

    private async Task<DnsAdministrationResult<DnsSyncJobModel>> EnqueueInternalAsync(
        Guid serverId, DnsActorContext actor, DnsSyncTrigger trigger, int priority, Guid batchId,
        CancellationToken cancellationToken)
    {
        var server = await context.DnsServers.Include(x => x.CredentialProfile)
            .SingleOrDefaultAsync(x => x.Id == serverId, cancellationToken);
        if (server is null) return new(false, "DNS server was not found.");
        if (!server.IsEnabled) return new(false, "The DNS server is disabled.");
        if (!server.CredentialProfile.IsEnabled) return new(false, "The assigned credential profile is disabled.");
        var settings = await context.DnsManagementSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (settings is null || !settings.IsEnabled) return new(false, "DNS Management is disabled.");
        if (trigger == DnsSyncTrigger.Scheduled && !settings.AutomaticSyncEnabled)
            return new(false, "Automatic DNS inventory synchronization is disabled.");

        var scope = settings.SyncRecordInventory ? DnsSyncScope.FullInventory : DnsSyncScope.Zones;
        var dedupeKey = $"{server.Id:N}:inventory:*";
        var existing = await context.DnsSyncJobs.AsNoTracking()
            .Where(x => x.DedupeKey == dedupeKey
                        && (x.Status == DnsSyncStatus.Pending || x.Status == DnsSyncStatus.Running))
            .OrderByDescending(x => x.RequestedAt).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return new(true, "A synchronization is already queued.", Map(existing, server.DisplayName, true));

        var now = DateTime.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");
        var job = new DnsSyncJob
        {
            BatchId = batchId,
            DnsServerId = server.Id,
            Scope = scope,
            Trigger = trigger,
            Status = DnsSyncStatus.Pending,
            DedupeKey = dedupeKey,
            Priority = priority,
            RequestedAt = now,
            RequestedByUserId = actor.UserId,
            RequestedByUserName = Limit(actor.UserName, 100),
            CorrelationId = correlationId,
            Message = "Synchronization queued.",
        };
        context.DnsSyncJobs.Add(job);
        server.LastSyncStatus = DnsSyncStatus.Pending.ToString();
        server.LastSyncMessage = "Synchronization queued.";
        context.AuditLogs.Add(new AuditLog
        {
            Action = "DnsInventorySyncQueued",
            EntityName = "DnsServer",
            EntityId = server.Id.ToString(),
            Description = $"DNS inventory synchronization queued for '{server.DisplayName}'.",
            ActorUserId = actor.UserId,
            ActorUserName = Limit(actor.UserName, 100),
            IpAddress = Limit(actor.IpAddress, 64),
            UserAgent = Limit(actor.UserAgent, 1024),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        context.DnsOperationLogs.Add(new DnsOperationLog
        {
            DnsServerId = server.Id,
            ServerDisplayName = server.DisplayName,
            OperationType = "InventorySynchronization",
            Status = "Pending",
            RequestSummaryJson = JsonSerializer.Serialize(new
            {
                scope = scope.ToString(),
                trigger = trigger.ToString(),
            }),
            ActorUserId = actor.UserId,
            ActorUserName = Limit(actor.UserName, 100),
            IpAddress = Limit(actor.IpAddress, 64),
            UserAgent = Limit(actor.UserAgent, 1024),
            CorrelationId = correlationId,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            existing = await context.DnsSyncJobs.AsNoTracking()
                .Where(x => x.DedupeKey == dedupeKey
                            && (x.Status == DnsSyncStatus.Pending || x.Status == DnsSyncStatus.Running))
                .OrderByDescending(x => x.RequestedAt).FirstOrDefaultAsync(cancellationToken);
            if (existing is null) throw;
            return new(true, "A synchronization is already queued.", Map(existing, server.DisplayName, true));
        }
        return new(true, "Synchronization queued.", Map(job, server.DisplayName, false));
    }

    public async Task<int> EnqueueDueAutomaticAsync(
        DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var settings = await context.DnsManagementSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (settings is null || !settings.IsEnabled || !settings.AutomaticSyncEnabled) return 0;

        var servers = await context.DnsServers.AsNoTracking()
            .Where(x => x.IsEnabled && x.CredentialProfile.IsEnabled)
            .Select(x => new
            {
                x.Id,
                x.SyncIntervalMinutes,
                x.LastSuccessfulSyncAt,
            })
            .ToListAsync(cancellationToken);
        if (servers.Count == 0) return 0;

        var serverIds = servers.Select(x => x.Id).ToArray();
        var lastJobActivities = await context.DnsSyncJobs.AsNoTracking()
            .Where(x => serverIds.Contains(x.DnsServerId))
            .GroupBy(x => x.DnsServerId)
            .Select(x => new { ServerId = x.Key, LastActivityAt = x.Max(y => y.CompletedAt ?? y.RequestedAt) })
            .ToDictionaryAsync(x => x.ServerId, x => x.LastActivityAt, cancellationToken);

        var batchId = Guid.NewGuid();
        var actor = new DnsActorContext(null, "dns-scheduler", null, null);
        var queued = 0;
        foreach (var server in servers)
        {
            var interval = server.SyncIntervalMinutes ?? settings.DefaultSyncIntervalMinutes;
            lastJobActivities.TryGetValue(server.Id, out var lastJobActivityAt);
            var lastActivity = Latest(server.LastSuccessfulSyncAt,
                lastJobActivityAt == default ? null : lastJobActivityAt);
            if (lastActivity is not null && lastActivity > utcNow.AddMinutes(-interval)) continue;

            var result = await EnqueueInternalAsync(
                server.Id, actor, DnsSyncTrigger.Scheduled, 10, batchId, cancellationToken);
            if (result.IsSuccess && result.Value is { AlreadyQueued: false }) queued++;
        }
        return queued;
    }

    public async Task<int> PurgeExpiredSnapshotsAsync(
        DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var retentionDays = await context.DnsManagementSettings.AsNoTracking()
            .Select(x => (int?)x.SnapshotRetentionDays).SingleOrDefaultAsync(cancellationToken);
        if (retentionDays is null) return 0;

        var cutoff = utcNow.AddDays(-Math.Clamp(retentionDays.Value, 1, 3650));
        var expired = context.DnsInventorySnapshots.Where(x =>
            !x.IsActive && x.Status != DnsSyncStatus.Running && x.CompletedAt < cutoff);
        if (context.Database.IsRelational())
            return await expired.ExecuteDeleteAsync(cancellationToken);

        var entities = await expired.ToListAsync(cancellationToken);
        context.DnsInventorySnapshots.RemoveRange(entities);
        await context.SaveChangesAsync(cancellationToken);
        return entities.Count;
    }

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        await RecoverExpiredLeasesAsync(cancellationToken);
        var jobId = await context.DnsSyncJobs.AsNoTracking()
            .Where(x => x.Status == DnsSyncStatus.Pending)
            .OrderByDescending(x => x.Priority).ThenBy(x => x.RequestedAt)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (jobId is null) return false;

        var now = DateTime.UtcNow;
        int claimed;
        if (context.Database.IsRelational())
        {
            claimed = await context.DnsSyncJobs
                .Where(x => x.Id == jobId && x.Status == DnsSyncStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, DnsSyncStatus.Running)
                    .SetProperty(x => x.StartedAt, now)
                    .SetProperty(x => x.LeaseExpiresAt, now + LeaseDuration)
                    .SetProperty(x => x.LeaseOwner, Environment.MachineName)
                    .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                    .SetProperty(x => x.Message, "Synchronization is running."), cancellationToken);
        }
        else
        {
            var candidate = await context.DnsSyncJobs.SingleAsync(x => x.Id == jobId, cancellationToken);
            if (candidate.Status != DnsSyncStatus.Pending) return true;
            candidate.Status = DnsSyncStatus.Running; candidate.StartedAt = now;
            candidate.LeaseExpiresAt = now + LeaseDuration; candidate.LeaseOwner = Environment.MachineName;
            candidate.AttemptCount++; candidate.Message = "Synchronization is running.";
            await context.SaveChangesAsync(cancellationToken);
            claimed = 1;
        }
        if (claimed == 0) return true;
        context.ChangeTracker.Clear();
        await ProcessJobAsync(jobId.Value, cancellationToken);
        return true;
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await context.DnsSyncJobs.Include(x => x.DnsServer).ThenInclude(x => x.CredentialProfile)
            .SingleAsync(x => x.Id == jobId, cancellationToken);
        var server = job.DnsServer;
        server.LastSyncStatus = DnsSyncStatus.Running.ToString();
        server.LastSyncMessage = "Synchronization is running.";
        DnsInventorySnapshot? snapshot = null;
        try
        {
            if (!server.IsEnabled || !server.CredentialProfile.IsEnabled)
                throw new DnsInventoryException("ServerDisabled", "The server or credential profile is disabled.");
            var settings = await context.DnsManagementSettings.AsNoTracking().SingleAsync(cancellationToken);
            var password = secretProtector.Unprotect(server.CredentialProfile.EncryptedPassword);
            snapshot = new DnsInventorySnapshot
            {
                DnsServerId = server.Id,
                Scope = job.Scope,
                Trigger = job.Trigger,
                Status = DnsSyncStatus.Running,
                IsActive = false,
                StartedAt = DateTime.UtcNow,
                RequestedByUserId = job.RequestedByUserId,
                RequestedByUserName = job.RequestedByUserName,
                CorrelationId = job.CorrelationId,
            };
            context.DnsInventorySnapshots.Add(snapshot);
            await context.SaveChangesAsync(cancellationToken);

            var request = CreateBaseRequest(server, settings.CommandTimeoutSeconds, password, job.CorrelationId);
            var zones = await ReadZonesAsync(request, snapshot, job, cancellationToken);
            var recordCount = 0;
            if (job.Scope == DnsSyncScope.FullInventory)
            {
                foreach (var zone in zones)
                {
                    recordCount += await ReadRecordsAsync(request, zone, job, cancellationToken);
                }
            }

            await ActivateSnapshotAsync(snapshot, job, server, zones.Count, recordCount, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var code = exception is DnsInventoryException known ? known.Code : "InventorySynchronizationFailed";
            var message = exception is DnsInventoryException ? exception.Message : "DNS inventory synchronization failed.";
            logger.LogError(exception, "DNS inventory synchronization failed for server {DnsServerId} ({ErrorCode}).",
                server.Id, code);
            await MarkFailedAsync(snapshot, job, server, code, message, cancellationToken);
        }
    }

    private async Task<List<ZoneWorkItem>> ReadZonesAsync(HostAgentRequest request,
        DnsInventorySnapshot snapshot, DnsSyncJob job, CancellationToken cancellationToken)
    {
        var result = new List<ZoneWorkItem>();
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var offset = 0;
        while (true)
        {
            var page = await ReadPageAsync(request with
            {
                DnsInventoryKind = HostAgentDnsInventoryKind.Zones,
                DnsInventoryOffset = offset,
                DnsInventoryPageSize = PageSize,
            }, cancellationToken);
            if (page.Zones.Count == 0 && page.HasMore)
                throw new DnsInventoryException("InvalidInventoryPage", "The DNS server returned an invalid zone page.");
            foreach (var source in page.Zones)
            {
                var name = Required(source.Name, 253, "zone name");
                var instance = Limit(source.VirtualizationInstance, 128);
                if (!identities.Add($"{instance}\n{name}"))
                    throw new DnsInventoryException("DuplicateZone", "The DNS server returned a duplicate zone identity.");
                var scopes = source.ZoneScopes
                    .Select(x => Limit(x, 128)).Where(x => x is not null)
                    .Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var entity = new DnsZoneSnapshot
                {
                    DnsInventorySnapshotId = snapshot.Id,
                    Name = name,
                    ZoneType = Required(source.ZoneType, 64, "zone type"),
                    IsReverseLookupZone = source.IsReverseLookupZone,
                    IsDsIntegrated = source.IsDsIntegrated,
                    IsSigned = source.IsSigned,
                    IsPaused = source.IsPaused,
                    DynamicUpdate = Limit(source.DynamicUpdate, 64),
                    ReplicationScope = Limit(source.ReplicationScope, 64),
                    DirectoryPartitionName = Limit(source.DirectoryPartitionName, 512),
                    ZoneFile = Limit(source.ZoneFile, 512),
                    VirtualizationInstance = instance,
                    PropertiesJson = JsonSerializer.Serialize(new { zoneScopes = scopes }),
                };
                context.DnsZoneSnapshots.Add(entity);
                result.Add(new(entity, scopes));
            }
            await HeartbeatAsync(job, cancellationToken);
            if (!page.HasMore) break;
            offset = CheckedNextOffset(offset, page.Zones.Count);
        }
        return result;
    }

    private async Task<int> ReadRecordsAsync(HostAgentRequest request, ZoneWorkItem zone,
        DnsSyncJob job, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var scope in new string?[] { null }.Concat(zone.Scopes))
        {
            var offset = 0;
            while (true)
            {
                var page = await ReadPageAsync(request with
                {
                    DnsInventoryKind = HostAgentDnsInventoryKind.Records,
                    DnsZoneName = zone.Entity.Name,
                    DnsZoneScope = scope,
                    DnsVirtualizationInstance = zone.Entity.VirtualizationInstance,
                    DnsInventoryOffset = offset,
                    DnsInventoryPageSize = PageSize,
                }, cancellationToken);
                if (page.Records.Count == 0 && page.HasMore)
                    throw new DnsInventoryException("InvalidInventoryPage", "The DNS server returned an invalid record page.");
                var entities = page.Records.Select(x => MapRecord(zone.Entity, x, scope)).ToArray();
                context.DnsRecordSnapshots.AddRange(entities);
                count = checked(count + entities.Length);
                await HeartbeatAsync(job, cancellationToken);
                foreach (var entity in entities) context.Entry(entity).State = EntityState.Detached;
                if (!page.HasMore) break;
                offset = CheckedNextOffset(offset, page.Records.Count);
            }
        }
        return count;
    }

    private async Task<HostAgentDnsInventoryPage> ReadPageAsync(
        HostAgentRequest request, CancellationToken cancellationToken)
    {
        HostAgentResponse response;
        try { response = await hostAgentClient.SendAsync(request, cancellationToken); }
        catch (HostAgentUnavailableException)
        {
            throw new DnsInventoryException("HostAgentUnavailable", "The ITAdmin Host Agent is unavailable.");
        }
        if (response.Status != HostAgentResponseStatus.Ok || response.DnsInventoryPage is null)
            throw new DnsInventoryException("HostAgentRejected", "The ITAdmin Host Agent rejected the inventory request.");
        if (!response.DnsInventoryPage.Success)
            throw new DnsInventoryException(response.DnsInventoryPage.FailureKind ?? "DnsInventoryReadFailed",
                Limit(response.DnsInventoryPage.Message, 2000) ?? "The DNS inventory page could not be read.");
        return response.DnsInventoryPage;
    }

    private async Task HeartbeatAsync(DnsSyncJob job, CancellationToken cancellationToken)
    {
        job.LeaseExpiresAt = DateTime.UtcNow + LeaseDuration;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ActivateSnapshotAsync(DnsInventorySnapshot snapshot, DnsSyncJob job,
        DnsServer server, int zoneCount, int recordCount, CancellationToken cancellationToken)
    {
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
        if (context.Database.IsRelational())
        {
            await context.DnsInventorySnapshots
                .Where(x => x.DnsServerId == server.Id && x.IsActive && x.Id != snapshot.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsActive, false), cancellationToken);
        }
        else
        {
            var active = await context.DnsInventorySnapshots
                .Where(x => x.DnsServerId == server.Id && x.IsActive && x.Id != snapshot.Id)
                .ToListAsync(cancellationToken);
            active.ForEach(x => x.IsActive = false);
        }
        var now = DateTime.UtcNow;
        snapshot.Status = DnsSyncStatus.Completed; snapshot.IsActive = true; snapshot.CompletedAt = now;
        snapshot.ZoneCount = zoneCount; snapshot.RecordCount = recordCount;
        snapshot.Message = $"Inventory synchronized: {zoneCount} zones, {recordCount} records.";
        job.Status = DnsSyncStatus.Completed; job.CompletedAt = now; job.LeaseExpiresAt = null;
        job.LeaseOwner = null; job.Message = snapshot.Message;
        server.LastSuccessfulSyncAt = now; server.LastSeenAt = now;
        server.LastSyncStatus = DnsSyncStatus.Completed.ToString(); server.LastSyncMessage = snapshot.Message;
        AddCompletionLog(job, server, "Succeeded", null, null, zoneCount, recordCount);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    private async Task MarkFailedAsync(DnsInventorySnapshot? snapshot, DnsSyncJob job, DnsServer server,
        string code, string message, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (snapshot is not null)
        {
            snapshot.Status = DnsSyncStatus.Failed; snapshot.IsActive = false;
            snapshot.CompletedAt = now; snapshot.ErrorCode = Limit(code, 64); snapshot.Message = Limit(message, 2000);
        }
        job.Status = DnsSyncStatus.Failed; job.CompletedAt = now; job.LeaseExpiresAt = null; job.LeaseOwner = null;
        job.ErrorCode = Limit(code, 64); job.Message = Limit(message, 2000);
        server.LastSyncStatus = DnsSyncStatus.Failed.ToString(); server.LastSyncMessage = Limit(message, 2000);
        AddCompletionLog(job, server, "Failed", code, message, snapshot?.ZoneCount ?? 0, snapshot?.RecordCount ?? 0);
        await context.SaveChangesAsync(cancellationToken);
    }

    private void AddCompletionLog(DnsSyncJob job, DnsServer server, string status,
        string? code, string? message, int zoneCount, int recordCount) => context.DnsOperationLogs.Add(new DnsOperationLog
        {
            DnsServerId = server.Id,
            ServerDisplayName = server.DisplayName,
            OperationType = "InventorySynchronization",
            Status = status,
            AfterSnapshotJson = status == "Succeeded" ? JsonSerializer.Serialize(new { zoneCount, recordCount }) : null,
            ErrorCode = Limit(code, 64),
            ErrorMessage = status == "Failed" ? Limit(message, 2000) : null,
            ActorUserId = job.RequestedByUserId,
            ActorUserName = job.RequestedByUserName,
            CorrelationId = job.CorrelationId,
            CreatedAt = DateTimeOffset.UtcNow,
        });

    private async Task RecoverExpiredLeasesAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expiredIds = await context.DnsSyncJobs.AsNoTracking()
            .Where(x => x.Status == DnsSyncStatus.Running && x.LeaseExpiresAt < now)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        foreach (var jobId in expiredIds)
        {
            if (context.Database.IsRelational())
            {
                await RecoverExpiredRelationalJobAsync(jobId, now, cancellationToken);
            }
            else
            {
                var job = await context.DnsSyncJobs.Include(x => x.DnsServer)
                    .SingleAsync(x => x.Id == jobId, cancellationToken);
                await RecoverClaimedExpiredJobAsync(job, now, cancellationToken);
            }
        }
    }

    private async Task RecoverExpiredRelationalJobAsync(
        Guid jobId, DateTime now, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var candidate = await context.DnsSyncJobs.AsNoTracking()
            .Where(x => x.Id == jobId && x.Status == DnsSyncStatus.Running && x.LeaseExpiresAt < now)
            .Select(x => new { x.AttemptCount })
            .SingleOrDefaultAsync(cancellationToken);
        if (candidate is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var retry = candidate.AttemptCount < 3;
        var claimed = await context.DnsSyncJobs
            .Where(x => x.Id == jobId && x.Status == DnsSyncStatus.Running && x.LeaseExpiresAt < now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, retry ? DnsSyncStatus.Pending : DnsSyncStatus.Failed)
                .SetProperty(x => x.CompletedAt, retry ? null : now)
                .SetProperty(x => x.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(x => x.LeaseOwner, (string?)null)
                .SetProperty(x => x.ErrorCode, retry ? null : "LeaseExpired")
                .SetProperty(x => x.Message, retry
                    ? "Interrupted synchronization queued for retry."
                    : "Synchronization stopped before completion."), cancellationToken);
        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        context.ChangeTracker.Clear();
        var job = await context.DnsSyncJobs.Include(x => x.DnsServer)
            .SingleAsync(x => x.Id == jobId, cancellationToken);
        await RecoverClaimedExpiredJobAsync(job, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        context.ChangeTracker.Clear();
    }

    private async Task RecoverClaimedExpiredJobAsync(
        DnsSyncJob job, DateTime now, CancellationToken cancellationToken)
    {
        var interruptedSnapshots = job.CorrelationId is null ? [] : await context.DnsInventorySnapshots
            .Where(x => x.CorrelationId == job.CorrelationId && x.Status == DnsSyncStatus.Running)
            .ToListAsync(cancellationToken);
        foreach (var snapshot in interruptedSnapshots)
        {
            snapshot.Status = DnsSyncStatus.Failed; snapshot.IsActive = false; snapshot.CompletedAt = now;
            snapshot.ErrorCode = "LeaseExpired"; snapshot.Message = "Synchronization stopped before completion.";
        }
        if (job.AttemptCount >= 3)
        {
            job.Status = DnsSyncStatus.Failed; job.CompletedAt = now;
            job.ErrorCode = "LeaseExpired"; job.Message = "Synchronization stopped before completion.";
            job.DnsServer.LastSyncStatus = DnsSyncStatus.Failed.ToString();
            job.DnsServer.LastSyncMessage = job.Message;
            AddCompletionLog(job, job.DnsServer, "Failed", job.ErrorCode, job.Message, 0, 0);
        }
        else
        {
            job.Status = DnsSyncStatus.Pending; job.CompletedAt = null; job.ErrorCode = null;
            job.Message = "Interrupted synchronization queued for retry.";
            job.DnsServer.LastSyncStatus = DnsSyncStatus.Pending.ToString();
            job.DnsServer.LastSyncMessage = job.Message;
        }
        job.LeaseExpiresAt = null; job.LeaseOwner = null;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static HostAgentRequest CreateBaseRequest(DnsServer server, int timeout, string password,
        string? correlationId) => new()
        {
            Operation = HostAgentOperation.ReadDnsServerInventoryPage,
            CorrelationId = correlationId,
            DnsHostName = server.HostName,
            DnsPort = server.Port,
            DnsAuthenticationMode = server.CredentialProfile.AuthenticationMode == DnsAuthenticationMode.BasicOverTls
            ? HostAgentDnsAuthenticationMode.BasicOverTls : HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = server.CredentialProfile.UserName,
            DnsPassword = password,
            DnsTlsCertificateThumbprint = server.TlsCertificateThumbprint,
            DnsTimeoutSeconds = timeout,
        };

    private static DnsRecordSnapshot MapRecord(
        DnsZoneSnapshot zone, HostAgentDnsRecordInventoryItem source, string? expectedScope)
    {
        var relativeName = Required(source.RelativeName, 253, "record name");
        var recordType = Required(source.RecordType, 32, "record type").ToUpperInvariant();
        var dataJson = CanonicalJson(source.RecordDataJson);
        var scope = Limit(expectedScope, 128);
        var instance = zone.VirtualizationInstance;
        var ttl = Math.Max(0, source.TimeToLiveSeconds);
        var fqdn = relativeName == "@" ? zone.Name.TrimEnd('.')
            : relativeName.EndsWith('.') ? relativeName.TrimEnd('.') : $"{relativeName}.{zone.Name.TrimEnd('.')}";
        fqdn = Required(fqdn, 512, "fully-qualified record name");
        var hashInput = string.Join('\n', zone.Name.ToLowerInvariant(), relativeName.ToLowerInvariant(),
            recordType, dataJson, ttl.ToString(System.Globalization.CultureInfo.InvariantCulture),
            scope?.ToLowerInvariant() ?? "", instance?.ToLowerInvariant() ?? "");
        return new DnsRecordSnapshot
        {
            DnsZoneSnapshotId = zone.Id,
            RelativeName = relativeName,
            FullyQualifiedName = fqdn,
            RecordType = recordType,
            CanonicalValue = dataJson,
            RecordDataJson = dataJson,
            TimeToLiveSeconds = ttl,
            Timestamp = source.Timestamp,
            ZoneScope = scope,
            VirtualizationInstance = instance,
            RecordHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))).ToLowerInvariant(),
        };
    }

    private static string CanonicalJson(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = 16 });
            return JsonSerializer.Serialize(document.RootElement);
        }
        catch (JsonException)
        {
            throw new DnsInventoryException("InvalidRecordData", "The DNS server returned invalid record data.");
        }
    }

    private static int CheckedNextOffset(int offset, int received)
    {
        if (received <= 0 || offset > 10_000_000 - received)
            throw new DnsInventoryException("InventoryLimitExceeded", "The DNS inventory exceeded the safety limit.");
        return offset + received;
    }
    private static DateTime? Latest(DateTime? left, DateTime? right) =>
        left is null ? right : right is null || left >= right ? left : right;
    private static string Required(string? value, int maxLength, string field)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > maxLength || normalized.Any(char.IsControl))
            throw new DnsInventoryException("InvalidInventoryData", $"The DNS server returned an invalid {field}.");
        return normalized;
    }
    private static string? Limit(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }
    private static DnsSyncJobModel Map(DnsSyncJob x, string serverName, bool alreadyQueued) =>
        new(x.Id, x.BatchId, x.DnsServerId, serverName, x.Scope, x.Trigger, x.Status,
            x.AttemptCount, x.RequestedAt, x.StartedAt, x.CompletedAt, x.ErrorCode, x.Message, alreadyQueued);

    private sealed record ZoneWorkItem(DnsZoneSnapshot Entity, IReadOnlyList<string> Scopes);
    private sealed class DnsInventoryException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }
}
