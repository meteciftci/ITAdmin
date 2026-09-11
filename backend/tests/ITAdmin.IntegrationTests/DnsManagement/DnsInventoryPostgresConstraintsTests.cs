using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.IntegrationTests.LicenseManagement;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ITAdmin.IntegrationTests.DnsManagement;

public sealed class DnsInventoryPostgresConstraintsTests
{
    [PostgresFact]
    public async Task Database_prevents_multiple_active_snapshots_and_active_duplicate_jobs()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable(
            LicenseSeatConcurrencyPostgresTests.ConnectionVariable);
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
                Assert.Fail($"{LicenseSeatConcurrencyPostgresTests.ConnectionVariable} must be configured in CI.");
            return;
        }

        var databaseName = $"itadmin_dns_inventory_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = "postgres" };
        var testBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = databaseName };
        await using var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(testBuilder.ConnectionString).Options;
            Guid serverId;
            await using (var setup = new AppDbContext(options))
            {
                await setup.Database.MigrateAsync();
                var credential = new DnsCredentialProfile
                {
                    Name = "DNS",
                    UserName = "dns-user",
                    EncryptedPassword = "protected",
                    IsEnabled = true,
                };
                var server = new DnsServer
                {
                    DisplayName = "DNS",
                    HostName = "dns.example.test",
                    Port = 5986,
                    CredentialProfile = credential,
                    IsEnabled = true,
                };
                setup.DnsServers.Add(server);
                await setup.SaveChangesAsync();
                serverId = server.Id;
            }

            await using (var snapshots = new AppDbContext(options))
            {
                snapshots.DnsInventorySnapshots.AddRange(
                    CompletedActiveSnapshot(serverId), CompletedActiveSnapshot(serverId));
                await Assert.ThrowsAsync<DbUpdateException>(() => snapshots.SaveChangesAsync());
            }

            await using (var jobs = new AppDbContext(options))
            {
                jobs.DnsSyncJobs.AddRange(ActiveJob(serverId), ActiveJob(serverId));
                await Assert.ThrowsAsync<DbUpdateException>(() => jobs.SaveChangesAsync());
            }
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static DnsInventorySnapshot CompletedActiveSnapshot(Guid serverId) => new()
    {
        DnsServerId = serverId,
        Scope = DnsSyncScope.FullInventory,
        Trigger = DnsSyncTrigger.Manual,
        Status = DnsSyncStatus.Completed,
        IsActive = true,
        StartedAt = DateTime.UtcNow,
        CompletedAt = DateTime.UtcNow,
    };

    private static DnsSyncJob ActiveJob(Guid serverId) => new()
    {
        BatchId = Guid.NewGuid(),
        DnsServerId = serverId,
        Scope = DnsSyncScope.FullInventory,
        Trigger = DnsSyncTrigger.Manual,
        Status = DnsSyncStatus.Pending,
        DedupeKey = $"{serverId:N}:inventory:*",
        RequestedAt = DateTime.UtcNow,
    };
}
