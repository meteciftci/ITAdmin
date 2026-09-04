using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Npgsql;

namespace ITAdmin.Api;

/// <summary>
/// Creates the ITAdmin database and its least-privilege login role, then exits. Invoked as
/// <c>ITAdmin.Api.exe --provision-database --input &lt;file&gt;</c>.
///
/// <para>
/// <b>What changed, and why.</b> ITAdmin used to require the operator to pre-create both the
/// database and the role and to grant the role CONNECT + CREATE/USAGE by hand. That turned "install
/// ITAdmin" into "file a database ticket, wait, then install ITAdmin", and every site got the
/// grants slightly wrong. The installer now does it: the operator supplies a database that does not
/// have to exist yet, an application role name, and a <em>transient</em> PostgreSQL administrator
/// credential that is used only for this step and is never stored. The application still runs as
/// the least-privilege role - the admin credential does not travel into runtime configuration.
/// </para>
///
/// <para>
/// <b>Why it is a pre-migration step and not a startup check.</b> Discovering a privilege problem
/// while the installer is half-way through configuring IIS produces a machine in an unclear state.
/// Running this before any machine mutation means an unsatisfied contract costs the operator a
/// corrected credential and a re-run, not a cleanup.
/// </para>
///
/// <para>
/// Input arrives as a file, not as arguments: the admin password and the generated application
/// password would otherwise be visible in the process command line to every user on the machine.
/// The installer writes the file with restrictive ACLs and deletes it afterwards; this runner never
/// rewrites it and never echoes its contents.
/// </para>
/// </summary>
public static class DatabaseProvisionRunner
{
    public const string ProvisionDatabaseArgument = "--provision-database";
    public const string InputArgument = "--input";

    public const int SuccessExitCode = 0;
    public const int FailureExitCode = 1;

    /// <summary>Distinct code for "reachable, but the precondition is still not met" - an operator fix.</summary>
    public const int PreconditionNotMetExitCode = 4;

    /// <summary>
    /// PostgreSQL identifiers are at most 63 bytes and, unquoted, are lower-case alphanumerics and
    /// underscores. The installer only ever passes such names; anything else is rejected rather than
    /// quoted-and-hoped, because these values are concatenated into DDL that cannot take parameters.
    /// </summary>
    private static readonly Regex SafeIdentifier = new("^[A-Za-z_][A-Za-z0-9_]{0,62}$", RegexOptions.Compiled);

    /// <summary>
    /// The generated application password is CSPRNG alphanumeric by construction (see the installer's
    /// <c>New-DatabasePassword</c>); validating it here keeps the single-quoted literal in
    /// <c>CREATE ROLE ... PASSWORD</c> unambiguously safe.
    /// </summary>
    private static readonly Regex SafePassword = new("^[A-Za-z0-9]{16,128}$", RegexOptions.Compiled);

    public static bool IsRequested(string[] args) =>
        args.Any(argument => string.Equals(argument, ProvisionDatabaseArgument, StringComparison.OrdinalIgnoreCase));

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            var inputPath = ResolveInputPath(args);
            if (inputPath is null)
            {
                error.WriteLine($"{ProvisionDatabaseArgument} requires {InputArgument} <file>.");
                return FailureExitCode;
            }

            if (!File.Exists(inputPath))
            {
                error.WriteLine($"Provision input file not found: {inputPath}");
                return FailureExitCode;
            }

            var request = DatabaseProvisionRequest.FromJson(await File.ReadAllTextAsync(inputPath));
            if (request is null)
            {
                error.WriteLine("Provision input file is not valid JSON.");
                return FailureExitCode;
            }

            var problems = request.Validate();
            if (problems.Count > 0)
            {
                foreach (var problem in problems)
                {
                    error.WriteLine($"Provision input is invalid: {problem}");
                }

                return FailureExitCode;
            }

            return await ExecuteAsync(request, output, error);
        }
        catch (Exception exception)
        {
            // Message only. A connection failure legitimately names a host; the full exception can
            // carry the connection string including a password.
            error.WriteLine($"Database provisioning failed: {exception.Message}");
            return FailureExitCode;
        }
    }

    /// <summary>
    /// The provisioning sequence itself. Every step is idempotent, so re-running after a partial
    /// failure converges rather than erroring.
    /// </summary>
    internal static async Task<int> ExecuteAsync(
        DatabaseProvisionRequest request,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        var adminBuilder = new NpgsqlConnectionStringBuilder(request.AdminConnectionString);
        var role = request.AppRole.Trim();
        var database = request.TargetDatabase.Trim();
        var password = request.AppRolePassword;

        var result = new DatabaseProvisionResult
        {
            TargetDatabase = database,
            AppRole = role,
        };

        bool roleExists;
        await using (var admin = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await admin.OpenAsync(cancellationToken);

            roleExists = await ScalarBoolAsync(
                admin, "SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @n)", role, cancellationToken);

            if (roleExists)
            {
                await ExecuteNonQueryAsync(
                    admin, $"ALTER ROLE \"{role}\" WITH LOGIN PASSWORD '{password}'", cancellationToken);
                result = result with { RoleUpdated = true };
            }
            else
            {
                await ExecuteNonQueryAsync(
                    admin, $"CREATE ROLE \"{role}\" WITH LOGIN PASSWORD '{password}'", cancellationToken);
                result = result with { RoleCreated = true };
            }

            var databaseExists = await ScalarBoolAsync(
                admin, "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @n)", database, cancellationToken);

            if (!databaseExists)
            {
                // CREATE DATABASE cannot run inside a transaction block; a bare command on the
                // open connection is exactly right.
                await ExecuteNonQueryAsync(
                    admin, $"CREATE DATABASE \"{database}\" OWNER \"{role}\"", cancellationToken);
                result = result with { DatabaseCreated = true };
            }

            await ExecuteNonQueryAsync(
                admin, $"GRANT ALL ON DATABASE \"{database}\" TO \"{role}\"", cancellationToken);
        }

        // Schema-level grants must be issued while connected to the target database.
        var targetBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString) { Database = database };
        await using (var adminOnTarget = new NpgsqlConnection(targetBuilder.ConnectionString))
        {
            await adminOnTarget.OpenAsync(cancellationToken);

            await ExecuteNonQueryAsync(
                adminOnTarget, $"GRANT ALL ON SCHEMA public TO \"{role}\"", cancellationToken);

            // PostgreSQL 15+ no longer grants CREATE on public to everyone; making the application
            // role own the schema is the durable fix and matches CREATE DATABASE ... OWNER above.
            try
            {
                await ExecuteNonQueryAsync(
                    adminOnTarget, $"ALTER SCHEMA public OWNER TO \"{role}\"", cancellationToken);
            }
            catch (PostgresException)
            {
                // Admin is the database owner but not a superuser and does not own public. The
                // GRANT above is enough for migrations; ownership is a nicety, not a requirement.
            }
        }

        // The same question the migration will ask a moment later, asked now as the app role.
        var appBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString)
        {
            Database = database,
            Username = role,
            Password = password,
        };

        await using (var appConnection = new NpgsqlConnection(appBuilder.ConnectionString))
        {
            try
            {
                await appConnection.OpenAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                result = result with
                {
                    Satisfied = false,
                    Problems = [$"The application role '{role}' cannot connect to '{database}' after provisioning: {exception.Message}"],
                };
                output.WriteLine(result.ToJson());
                foreach (var problem in result.Problems)
                {
                    error.WriteLine(problem);
                }

                return PreconditionNotMetExitCode;
            }

            var canCreate = await ScalarAsync<bool>(
                appConnection,
                "SELECT has_schema_privilege(current_user, current_schema(), 'CREATE')",
                cancellationToken);
            var canUse = await ScalarAsync<bool>(
                appConnection,
                "SELECT has_schema_privilege(current_user, current_schema(), 'USAGE')",
                cancellationToken);

            var checkProblems = new List<string>();
            if (!canCreate)
            {
                checkProblems.Add($"Role '{role}' still cannot CREATE in schema 'public' of '{database}'.");
            }

            if (!canUse)
            {
                checkProblems.Add($"Role '{role}' still has no USAGE on schema 'public' of '{database}'.");
            }

            result = result with
            {
                Satisfied = checkProblems.Count == 0,
                Problems = checkProblems,
            };
        }

        output.WriteLine(result.ToJson());

        if (result.Satisfied)
        {
            return SuccessExitCode;
        }

        foreach (var problem in result.Problems)
        {
            error.WriteLine(problem);
        }

        return PreconditionNotMetExitCode;
    }

    internal static string? ResolveInputPath(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], InputArgument, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    internal static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && SafeIdentifier.IsMatch(value.Trim());

    internal static bool IsSafeGeneratedPassword(string? value) =>
        !string.IsNullOrEmpty(value) && SafePassword.IsMatch(value);

    private static async Task ExecuteNonQueryAsync(
        NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> ScalarBoolAsync(
        NpgsqlConnection connection, string sql, string name, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("n", name);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is true;
    }

    private static async Task<T?> ScalarAsync<T>(
        NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? default : (T)value;
    }
}

/// <summary>
/// Everything the installer must hand over to provision the database. The admin connection string
/// is transient: this runner uses it and forgets it, and it is never written to runtime config.
/// </summary>
public sealed record DatabaseProvisionRequest
{
    [JsonPropertyName("adminConnectionString")]
    public string AdminConnectionString { get; init; } = string.Empty;

    [JsonPropertyName("targetDatabase")]
    public string TargetDatabase { get; init; } = string.Empty;

    [JsonPropertyName("appRole")]
    public string AppRole { get; init; } = string.Empty;

    [JsonPropertyName("appRolePassword")]
    public string AppRolePassword { get; init; } = string.Empty;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static DatabaseProvisionRequest? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DatabaseProvisionRequest>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(AdminConnectionString))
        {
            problems.Add("adminConnectionString is required.");
        }
        else
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString);
                if (string.IsNullOrWhiteSpace(builder.Host) || string.IsNullOrWhiteSpace(builder.Username))
                {
                    problems.Add("adminConnectionString must carry at least Host and Username.");
                }
            }
            catch (ArgumentException)
            {
                problems.Add("adminConnectionString is not a valid Npgsql connection string.");
            }
        }

        if (!DatabaseProvisionRunner.IsSafeIdentifier(TargetDatabase))
        {
            problems.Add("targetDatabase must be a plain PostgreSQL identifier (letters, digits, underscore; <= 63 chars).");
        }

        if (!DatabaseProvisionRunner.IsSafeIdentifier(AppRole))
        {
            problems.Add("appRole must be a plain PostgreSQL identifier (letters, digits, underscore; <= 63 chars).");
        }

        if (!DatabaseProvisionRunner.IsSafeGeneratedPassword(AppRolePassword))
        {
            problems.Add("appRolePassword must be a generated alphanumeric secret of 16-128 characters.");
        }

        return problems;
    }
}

/// <summary>
/// What the installer reads back. Carries no credential: the database name and the role name are
/// operational coordinates the operator already supplied.
/// </summary>
public sealed record DatabaseProvisionResult
{
    [JsonPropertyName("targetDatabase")]
    public string TargetDatabase { get; init; } = string.Empty;

    [JsonPropertyName("appRole")]
    public string AppRole { get; init; } = string.Empty;

    [JsonPropertyName("roleCreated")]
    public bool RoleCreated { get; init; }

    [JsonPropertyName("roleUpdated")]
    public bool RoleUpdated { get; init; }

    [JsonPropertyName("databaseCreated")]
    public bool DatabaseCreated { get; init; }

    [JsonPropertyName("satisfied")]
    public bool Satisfied { get; init; } = true;

    [JsonPropertyName("problems")]
    public IReadOnlyList<string> Problems { get; init; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static DatabaseProvisionResult? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DatabaseProvisionResult>(json, SerializerOptions);
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
