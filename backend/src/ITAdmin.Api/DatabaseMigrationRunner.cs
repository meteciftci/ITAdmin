using ITAdmin.Api.Configuration;
using ITAdmin.Api.Extensions;
using ITAdmin.Persistence;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ITAdmin.Api;

/// <summary>
/// Applies pending EF Core migrations, then exits. Invoked as <c>ITAdmin.Api.exe --migrate</c>.
///
/// <para>
/// This exists so a production host needs no build or database toolchain. The previous model
/// required either <c>psql.exe</c> on the web server to run a generated SQL file, or a human
/// applying migrations by hand. Both are avoidable: the published application already carries EF
/// Core, Npgsql, and the compiled migrations, so it can migrate its own schema using exactly the
/// connection configuration it will run with. No .NET SDK, no <c>dotnet ef</c>, no PostgreSQL
/// client tools on the target.
/// </para>
///
/// <para>
/// Migration stays an explicit installer step rather than something the web host does at startup:
/// several IIS worker processes can start concurrently, and schema changes must happen once, under
/// operator control, at a known point in the install sequence.
/// </para>
/// </summary>
public static class DatabaseMigrationRunner
{
    /// <summary>Command-line switch that selects migration mode instead of serving.</summary>
    public const string MigrateArgument = "--migrate";

    /// <summary>Reports the applied/pending migrations as JSON without changing anything.</summary>
    public const string MigrationStatusArgument = "--migration-status";

    public const int SuccessExitCode = 0;
    public const int FailureExitCode = 1;

    public static bool IsMigrateRequested(string[] args) =>
        args.Any(argument => string.Equals(argument, MigrateArgument, StringComparison.OrdinalIgnoreCase));

    public static bool IsStatusRequested(string[] args) =>
        args.Any(argument => string.Equals(argument, MigrationStatusArgument, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Applies pending migrations. Returns a process exit code; never throws, so the installer
    /// always gets a deterministic result rather than an unhandled exception trace.
    /// </summary>
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            using var host = BuildMigrationHost(args);
            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count == 0)
            {
                var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
                output.WriteLine("No pending migrations.");
                output.WriteLine($"currentMigration={applied.LastOrDefault() ?? "(none)"}");
                return SuccessExitCode;
            }

            output.WriteLine($"Applying {pending.Count} pending migration(s):");
            foreach (var migration in pending)
            {
                output.WriteLine($"  {migration}");
            }

            await context.Database.MigrateAsync();

            var current = (await context.Database.GetAppliedMigrationsAsync()).LastOrDefault();
            output.WriteLine("Migration completed.");
            output.WriteLine($"currentMigration={current ?? "(none)"}");
            return SuccessExitCode;
        }
        catch (Exception exception)
        {
            // Message only: a connection failure message can legitimately name a host, but the
            // full exception could carry the connection string including the password.
            error.WriteLine($"Migration failed: {exception.Message}");
            return FailureExitCode;
        }
    }

    /// <summary>Prints applied and pending migrations without modifying the database.</summary>
    public static async Task<int> RunStatusAsync(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            using var host = BuildMigrationHost(args);
            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
            var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();

            output.WriteLine($"appliedCount={applied.Count}");
            output.WriteLine($"pendingCount={pending.Count}");
            output.WriteLine($"currentMigration={applied.LastOrDefault() ?? "(none)"}");
            return SuccessExitCode;
        }
        catch (Exception exception)
        {
            error.WriteLine($"Migration status check failed: {exception.Message}");
            return FailureExitCode;
        }
    }

    /// <summary>
    /// A minimal host that reuses the application's own configuration pipeline — machine secrets
    /// plus ITADMIN_-prefixed environment variables — so migration can never target a different
    /// database than the one the application will use.
    /// </summary>
    private static IHost BuildMigrationHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddITAdminMachineSecrets();
        builder.Configuration.AddITAdminPrefixedEnvironmentVariables();
        builder.Services.AddPersistence(builder.Configuration);
        return builder.Build();
    }
}
