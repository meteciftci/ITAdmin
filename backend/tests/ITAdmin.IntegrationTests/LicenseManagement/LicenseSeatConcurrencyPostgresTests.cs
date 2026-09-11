using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services.LicenseManagement;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ITAdmin.IntegrationTests.LicenseManagement;

public sealed class LicenseSeatConcurrencyPostgresTests
{
    internal const string ConnectionVariable = "ITADMIN_TEST_POSTGRES_CONNECTION";

    [PostgresFact]
    public async Task Parallel_assignments_cannot_exceed_package_capacity()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail($"{ConnectionVariable} must be configured in CI.");
            }

            return;
        }

        var databaseName = $"itadmin_concurrency_{Guid.NewGuid():N}";
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
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(testBuilder.ConnectionString)
                .Options;
            Guid packageId;
            await using (var setup = new AppDbContext(options))
            {
                await setup.Database.MigrateAsync();
                packageId = await SeedPackageAsync(setup);
            }

            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = AssignAfterGateAsync(options, packageId, "ad-user-1", "First User", gate.Task);
            var second = AssignAfterGateAsync(options, packageId, "ad-user-2", "Second User", gate.Task);
            gate.SetResult();

            var results = await Task.WhenAll(first, second);

            Assert.Single(results, x => x.IsSuccess);
            Assert.Single(results, x => !x.IsSuccess);
            await using var verifier = new AppDbContext(options);
            Assert.Equal(
                1,
                await verifier.LicenseSeatAssignments.CountAsync(
                    x => x.PackageId == packageId && x.Status == LicenseSeatAssignmentStatus.Active));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<LicenseSeatAssignmentOperationResult> AssignAfterGateAsync(
        DbContextOptions<AppDbContext> options,
        Guid packageId,
        string adObjectId,
        string displayName,
        Task gate)
    {
        await gate;
        await using var context = new AppDbContext(options);
        var service = new LicenseSeatAssignmentService(context);
        return await service.AssignAsync(
            new AssignLicenseSeatRequest(
                packageId,
                new LicenseSeatPersonInput(adObjectId, displayName, null, null, null, null, null, null),
                null,
                null,
                null,
                new LicenseSeatActorContext(null, "integration-test", null, null)));
    }

    private static async Task<Guid> SeedPackageAsync(AppDbContext context)
    {
        var now = DateTime.UtcNow;
        var category = new LicenseProductCategory
        {
            Name = $"Concurrency {Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "integration-test",
        };
        var product = new LicensedProduct
        {
            Name = "Concurrent seat test product",
            Category = category,
            CategoryId = category.Id,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "integration-test",
        };
        var purchase = new LicensePurchase
        {
            PurchaseType = LicensePurchaseType.DirectPurchase,
            Title = "Concurrent seat test purchase",
            Status = LicensePurchaseStatus.Active,
            CreatedAt = now,
            CreatedBy = "integration-test",
        };
        var package = new LicensePackage
        {
            Product = product,
            ProductId = product.Id,
            Purchase = purchase,
            PurchaseId = purchase.Id,
            LicenseType = LicenseType.NamedUser,
            Quantity = 1,
            IsActive = true,
            Status = LicensePackageStatus.Active,
            CreatedAt = now,
            CreatedBy = "integration-test",
        };
        context.LicensePackages.Add(package);
        await context.SaveChangesAsync();
        return package.Id;
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        var hasConnection = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(LicenseSeatConcurrencyPostgresTests.ConnectionVariable));
        var isCi = string.Equals(
            Environment.GetEnvironmentVariable("CI"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (!hasConnection && !isCi)
        {
            Skip = $"Set {LicenseSeatConcurrencyPostgresTests.ConnectionVariable} to run this PostgreSQL test locally.";
        }
    }
}
