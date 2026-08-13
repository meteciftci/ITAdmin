using System.Text.Json;
using System.Text.Json.Serialization;
using ITAdmin.Api.Configuration;
using ITAdmin.Api.Extensions;
using ITAdmin.Persistence;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ITAdmin.Api;

/// <summary>
/// Checks that the database precondition is satisfied, then exits. Invoked as
/// <c>ITAdmin.Api.exe --check-database</c>.
///
/// <para>
/// <b>The contract this makes explicit.</b> ITAdmin does not create its own database or its own
/// role, and it does not ask for a superuser credential in order to do so. The operator supplies a
/// database that exists and a role that can connect to it, and that role needs exactly enough
/// privilege to run EF Core migrations against the schema it owns: CONNECT on the database, plus
/// CREATE and USAGE on the target schema. Nothing wider. A runtime account with CREATEDB or
/// SUPERUSER would mean a compromise of the web application is a compromise of the whole cluster.
/// </para>
///
/// <para>
/// <b>Why it is a preflight and not a startup check.</b> Discovering a privilege problem while the
/// installer is half-way through configuring IIS produces a machine in an unclear state. Running
/// this before any machine mutation means an unsatisfied contract costs the operator a corrected
/// GRANT and a re-run, not a cleanup.
/// </para>
///
/// <para>
/// Privileges are read through PostgreSQL's own <c>has_*_privilege</c> functions rather than
/// inferred from role membership: the effective answer is what matters, and it is the same question
/// the migration will ask a moment later.
/// </para>
/// </summary>
public static class DatabasePreflightRunner
{
    public const string CheckDatabaseArgument = "--check-database";

    public const int SuccessExitCode = 0;
    public const int FailureExitCode = 1;

    /// <summary>Distinct code for "reachable, but the precondition is not met" - an operator fix.</summary>
    public const int PreconditionNotMetExitCode = 4;

    public static bool IsRequested(string[] args) =>
        args.Any(argument => string.Equals(argument, CheckDatabaseArgument, StringComparison.OrdinalIgnoreCase));

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            using var host = BuildHost(args);
            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var result = await InspectAsync(context, CancellationToken.None);
            output.WriteLine(result.ToJson());

            if (result.IsSatisfied)
            {
                return SuccessExitCode;
            }

            foreach (var problem in result.Problems)
            {
                error.WriteLine(problem);
            }

            return result.CanConnect ? PreconditionNotMetExitCode : FailureExitCode;
        }
        catch (Exception exception)
        {
            // Message only. A connection failure legitimately names a host; the full exception can
            // carry the connection string including the password.
            error.WriteLine($"Database preflight failed: {exception.Message}");
            return FailureExitCode;
        }
    }

    /// <summary>
    /// The inspection itself, taking a context so it can be exercised against a real database in
    /// integration tests without going through the process entry point.
    /// </summary>
    internal static async Task<DatabasePreflightResult> InspectAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var problems = new List<string>();

        var connection = context.Database.GetDbConnection();
        var builder = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        var databaseName = builder.Database ?? string.Empty;
        var userName = builder.Username ?? string.Empty;

        try
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Every one of these has a different fix, and PostgreSQL's own message is the most
            // accurate description of which one it is - so it is surfaced rather than replaced.
            problems.Add(
                $"Could not connect to database '{databaseName}' as '{userName}': {exception.Message}");
            problems.Add(
                "ITAdmin does not create its database or its role. Create both, grant the role CONNECT "
                + "on the database and CREATE + USAGE on its schema, then re-run.");

            return new DatabasePreflightResult
            {
                DatabaseName = databaseName,
                UserName = userName,
                CanConnect = false,
                Problems = problems,
            };
        }

        try
        {
            var schemaName = await ScalarAsync<string>(context, "SELECT current_schema()", cancellationToken)
                ?? "public";

            var canCreateInSchema = await ScalarAsync<bool>(
                context,
                "SELECT has_schema_privilege(current_user, current_schema(), 'CREATE')",
                cancellationToken);

            var canUseSchema = await ScalarAsync<bool>(
                context,
                "SELECT has_schema_privilege(current_user, current_schema(), 'USAGE')",
                cancellationToken);

            var isSuperuser = await ScalarAsync<bool>(
                context,
                "SELECT COALESCE((SELECT rolsuper FROM pg_roles WHERE rolname = current_user), false)",
                cancellationToken);

            var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

            if (!canCreateInSchema)
            {
                problems.Add(
                    $"Role '{userName}' cannot CREATE in schema '{schemaName}' of database "
                    + $"'{databaseName}', so migrations cannot run. Grant it with: "
                    + $"GRANT CREATE, USAGE ON SCHEMA {schemaName} TO \"{userName}\";");
            }

            if (!canUseSchema)
            {
                problems.Add(
                    $"Role '{userName}' has no USAGE on schema '{schemaName}'. Grant it with: "
                    + $"GRANT USAGE ON SCHEMA {schemaName} TO \"{userName}\";");
            }

            return new DatabasePreflightResult
            {
                DatabaseName = databaseName,
                UserName = userName,
                SchemaName = schemaName,
                CanConnect = true,
                CanCreateInSchema = canCreateInSchema,
                CanUseSchema = canUseSchema,
                IsSuperuser = isSuperuser,
                AppliedMigrationCount = appliedMigrations.Count,
                PendingMigrationCount = pendingMigrations.Count,
                CurrentMigration = appliedMigrations.LastOrDefault(),
                Problems = problems,
            };
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<T?> ScalarAsync<T>(
        AppDbContext context,
        string sql,
        CancellationToken cancellationToken)
    {
        // Constant SQL only - none of these statements interpolate anything.
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? default : (T)value;
    }

    private static IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddITAdminMachineSecrets();
        builder.Configuration.AddITAdminPrefixedEnvironmentVariables();
        builder.Services.AddPersistence(builder.Configuration);
        return builder.Build();
    }
}

/// <summary>
/// What the preflight found. Carries no credential: the database name, the role name, and the
/// schema name are operational coordinates an operator already knows.
/// </summary>
public sealed record DatabasePreflightResult
{
    [JsonPropertyName("databaseName")]
    public string DatabaseName { get; init; } = string.Empty;

    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;

    [JsonPropertyName("schemaName")]
    public string SchemaName { get; init; } = string.Empty;

    [JsonPropertyName("canConnect")]
    public bool CanConnect { get; init; }

    [JsonPropertyName("canCreateInSchema")]
    public bool CanCreateInSchema { get; init; }

    [JsonPropertyName("canUseSchema")]
    public bool CanUseSchema { get; init; }

    /// <summary>
    /// Reported, not required. A superuser runtime account satisfies the privilege checks while
    /// being a considerably worse idea than the minimum the product actually needs, so it is
    /// surfaced as a warning rather than silently accepted as "fine".
    /// </summary>
    [JsonPropertyName("isSuperuser")]
    public bool IsSuperuser { get; init; }

    [JsonPropertyName("appliedMigrationCount")]
    public int AppliedMigrationCount { get; init; }

    [JsonPropertyName("pendingMigrationCount")]
    public int PendingMigrationCount { get; init; }

    [JsonPropertyName("currentMigration")]
    public string? CurrentMigration { get; init; }

    [JsonPropertyName("problems")]
    public IReadOnlyList<string> Problems { get; init; } = [];

    [JsonIgnore]
    public bool IsSatisfied => CanConnect && CanCreateInSchema && CanUseSchema && Problems.Count == 0;

    /// <summary>
    /// Advisory notes that do not block installation. Kept separate from <see cref="Problems"/> so
    /// "this works but is broader than it needs to be" never blocks an install, and never gets lost
    /// either.
    /// </summary>
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings =>
        IsSuperuser
            ?
            [
                $"Role '{UserName}' is a PostgreSQL superuser. ITAdmin only needs CONNECT on the "
                + $"database and CREATE + USAGE on schema '{SchemaName}'; consider a least-privilege role.",
            ]
            : [];

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static DatabasePreflightResult? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DatabasePreflightResult>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };
}
