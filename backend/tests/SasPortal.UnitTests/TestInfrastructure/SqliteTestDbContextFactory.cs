using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Context;

namespace SasPortal.UnitTests.TestInfrastructure;

public static class SqliteTestDbContextFactory
{
    public static async Task<(SqliteConnection Connection, AppDbContext Context)> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new SqliteLogsAppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return (connection, context);
    }
}

file sealed class SqliteLogsAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuditLog>()
            .Property(x => x.CreatedAt)
            .HasConversion(
                x => x.UtcDateTime,
                x => new DateTimeOffset(DateTime.SpecifyKind(x, DateTimeKind.Utc)));

        modelBuilder.Entity<SecurityLog>()
            .Property(x => x.CreatedAt)
            .HasConversion(
                x => x.UtcDateTime,
                x => new DateTimeOffset(DateTime.SpecifyKind(x, DateTimeKind.Utc)));
    }
}
