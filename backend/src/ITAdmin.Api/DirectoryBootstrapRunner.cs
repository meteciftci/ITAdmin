using System.Text.Json;
using System.Text.Json.Serialization;
using ITAdmin.Api.Configuration;
using ITAdmin.Api.Extensions;
using ITAdmin.Application;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models;
using ITAdmin.Infrastructure;
using ITAdmin.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ITAdmin.Api;

/// <summary>
/// Establishes the Primary Directory and the first administrator, then exits. Invoked as
/// <c>ITAdmin.Api.exe --bootstrap-directory --input &lt;file&gt;</c>.
///
/// <para>
/// ITAdmin authenticates every user through LDAP, so an installation with no working directory
/// configuration and no directory-backed administrator is not installed - it is a database with a
/// web server in front of it that nobody can log into. This step is therefore part of installation,
/// not post-install configuration.
/// </para>
///
/// <para>
/// It deliberately adds no authorization logic of its own. Role seeding, permission grants, the
/// portal-user representation of a directory identity, and the "setup is complete" marker all live
/// in <see cref="ISetupService"/>, which is covered by tests. There is no in-application setup
/// wizard; this runner is the only caller of that path. Reimplementing any of it in PowerShell
/// would create a second, unverified definition of what an ITAdmin administrator is. The
/// installer's job is to gather input, prove the directory works, and call this.
/// </para>
///
/// <para>
/// Input arrives as a file, not as arguments: the bind password and setup key would otherwise be
/// visible in the process command line to every user on the machine. The installer writes the file
/// with restrictive ACLs and deletes it afterwards; this runner never rewrites it and never echoes
/// its contents.
/// </para>
/// </summary>
public static class DirectoryBootstrapRunner
{
    public const string BootstrapArgument = "--bootstrap-directory";
    public const string InputArgument = "--input";

    public const int SuccessExitCode = 0;
    public const int FailureExitCode = 1;

    /// <summary>
    /// Distinct exit code for "the directory itself said no" - bad bind credential, unreachable
    /// controller, admin not found. The installer retries these interactively rather than failing
    /// the whole run, because they are input mistakes, not broken machines.
    /// </summary>
    public const int DirectoryRejectedExitCode = 3;

    public static bool IsRequested(string[] args) =>
        args.Any(argument => string.Equals(argument, BootstrapArgument, StringComparison.OrdinalIgnoreCase));

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            var inputPath = ResolveInputPath(args);
            if (inputPath is null)
            {
                error.WriteLine($"{BootstrapArgument} requires {InputArgument} <file>.");
                return FailureExitCode;
            }

            if (!File.Exists(inputPath))
            {
                error.WriteLine($"Bootstrap input file not found: {inputPath}");
                return FailureExitCode;
            }

            var request = DirectoryBootstrapRequest.FromJson(await File.ReadAllTextAsync(inputPath));
            if (request is null)
            {
                error.WriteLine("Bootstrap input file is not valid JSON.");
                return FailureExitCode;
            }

            var requestProblems = request.Validate();
            if (requestProblems.Count > 0)
            {
                foreach (var problem in requestProblems)
                {
                    error.WriteLine($"Bootstrap input is invalid: {problem}");
                }

                return FailureExitCode;
            }

            using var host = BuildBootstrapHost(args);
            await using var scope = host.Services.CreateAsyncScope();
            var setupService = scope.ServiceProvider.GetRequiredService<ISetupService>();

            return await ExecuteAsync(setupService, request, output, error);
        }
        catch (Exception exception)
        {
            // Message only. A directory or database exception can carry a bind DN, a connection
            // string, or the credential itself in its inner detail.
            error.WriteLine($"Directory bootstrap failed: {exception.Message}");
            return FailureExitCode;
        }
    }

    /// <summary>
    /// The bootstrap sequence itself, taking <see cref="ISetupService"/> directly so it can be
    /// tested against a fake directory without a host, a database, or a real domain.
    /// </summary>
    internal static async Task<int> ExecuteAsync(
        ISetupService setupService,
        DirectoryBootstrapRequest request,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        var ldap = request.ToLdapSettings();

        // Re-running after a partial failure must not create a second administrator or overwrite a
        // working directory configuration, so the already-complete case is a success, not an error.
        if (!await setupService.IsSetupRequiredAsync(cancellationToken))
        {
            WriteResult(output, new DirectoryBootstrapResult
            {
                Status = DirectoryBootstrapStatus.AlreadyBootstrapped,
                Message = "Directory configuration and the initial administrator already exist; nothing was changed.",
            });
            return SuccessExitCode;
        }

        var validation = await setupService.ValidateLdapAsync(
            new ValidateSetupLdapRequest(request.SetupKey, ldap),
            cancellationToken);

        if (!validation.IsValid)
        {
            error.WriteLine($"Directory validation failed: {validation.Message}");
            return DirectoryRejectedExitCode;
        }

        var search = await setupService.SearchAdminUsersAsync(
            new SearchSetupAdminUsersRequest(request.SetupKey, ldap, request.AdministratorIdentifier),
            cancellationToken);

        if (!search.IsSuccess)
        {
            error.WriteLine($"Administrator lookup failed: {search.ErrorMessage}");
            return DirectoryRejectedExitCode;
        }

        if (!TrySelectAdministrator(search.Users, request.AdministratorIdentifier, out var administrator, out var selectionError))
        {
            error.WriteLine(selectionError);
            return DirectoryRejectedExitCode;
        }

        var completion = await setupService.CompleteSetupAsync(
            new CompleteSetupRequest(
                request.SetupKey,
                ldap,
                [
                    new CompleteSetupAdminUser(
                        administrator.UserName,
                        administrator.DistinguishedName,
                        administrator.DirectoryObjectId),
                ]),
            cancellationToken);

        if (!completion.IsCompleted)
        {
            error.WriteLine($"Directory bootstrap was not completed: {completion.Message}");
            return DirectoryRejectedExitCode;
        }

        WriteResult(output, new DirectoryBootstrapResult
        {
            Status = DirectoryBootstrapStatus.Bootstrapped,
            // User name only. The directory entry's mail, DN, and object id are personal data with
            // no operational value in an installation log.
            AdministratorUserName = administrator.UserName,
            Message = "Primary Directory configured and the initial administrator was granted access.",
        });

        return SuccessExitCode;
    }

    /// <summary>
    /// Picks the single directory user the operator meant.
    ///
    /// <para>
    /// A substring search over a directory routinely returns several people, and granting
    /// administrator to the wrong one is not recoverable by re-running the installer. So an exact
    /// match on the identifier wins outright; otherwise a single result is accepted and anything
    /// ambiguous is refused with the candidates listed by user name.
    /// </para>
    /// </summary>
    internal static bool TrySelectAdministrator(
        IReadOnlyList<SetupAdminUserSearchResult> candidates,
        string identifier,
        out SetupAdminUserSearchResult administrator,
        out string errorMessage)
    {
        administrator = null!;
        errorMessage = string.Empty;

        if (candidates.Count == 0)
        {
            errorMessage =
                $"No directory user matched '{identifier}'. Check the identifier, the Base DN, and "
                + "that the bind account can read user objects.";
            return false;
        }

        var exact = candidates
            .Where(candidate => IsExactMatch(candidate, identifier))
            .ToList();

        if (exact.Count == 1)
        {
            administrator = exact[0];
            return true;
        }

        if (exact.Count > 1)
        {
            errorMessage =
                $"'{identifier}' matched {exact.Count} directory users exactly. Supply a unique "
                + "identifier (user principal name) instead.";
            return false;
        }

        if (candidates.Count == 1)
        {
            administrator = candidates[0];
            return true;
        }

        var names = string.Join(", ", candidates.Take(10).Select(candidate => candidate.UserName));
        errorMessage =
            $"'{identifier}' is ambiguous; it matched {candidates.Count} directory users ({names}). "
            + "Re-run with the exact user principal name or sAMAccountName.";
        return false;
    }

    private static bool IsExactMatch(SetupAdminUserSearchResult candidate, string identifier)
    {
        var trimmed = identifier.Trim();
        if (string.Equals(candidate.UserName, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // A UPN or an address is what an operator actually knows; the directory's sAMAccountName
        // is frequently neither, so an exact match must consider both forms.
        if (!string.IsNullOrWhiteSpace(candidate.Email)
            && string.Equals(candidate.Email, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var atIndex = trimmed.IndexOf('@');
        if (atIndex > 0
            && string.Equals(candidate.UserName, trimmed[..atIndex], StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var slashIndex = trimmed.LastIndexOf('\\');
        return slashIndex >= 0
            && slashIndex < trimmed.Length - 1
            && string.Equals(candidate.UserName, trimmed[(slashIndex + 1)..], StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteResult(TextWriter output, DirectoryBootstrapResult result) =>
        output.WriteLine(result.ToJson());

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

    private static IHost BuildBootstrapHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddITAdminMachineSecrets();
        builder.Configuration.AddITAdminPrefixedEnvironmentVariables();
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddPersistence(builder.Configuration);
        return builder.Build();
    }
}

/// <summary>
/// Everything the installer must hand over to bootstrap the directory. Mirrors the application's
/// own <see cref="CompleteSetupLdapSettings"/> rather than defining a parallel LDAP model.
/// </summary>
public sealed record DirectoryBootstrapRequest
{
    [JsonPropertyName("setupKey")]
    public string SetupKey { get; init; } = string.Empty;

    /// <summary>Display name for the directory configuration, e.g. the AD domain.</summary>
    [JsonPropertyName("directoryName")]
    public string DirectoryName { get; init; } = string.Empty;

    /// <summary>Directory host: a domain controller, or the domain name for DC locator failover.</summary>
    [JsonPropertyName("host")]
    public string Host { get; init; } = string.Empty;

    [JsonPropertyName("baseDn")]
    public string BaseDn { get; init; } = string.Empty;

    [JsonPropertyName("userSearchFilter")]
    public string UserSearchFilter { get; init; } = "(sAMAccountName={0})";

    [JsonPropertyName("bindUserName")]
    public string BindUserName { get; init; } = string.Empty;

    [JsonPropertyName("bindUserDomain")]
    public string? BindUserDomain { get; init; }

    [JsonPropertyName("bindPassword")]
    public string BindPassword { get; init; } = string.Empty;

    /// <summary>UPN, sAMAccountName, or mail of the person who becomes the first administrator.</summary>
    [JsonPropertyName("administratorIdentifier")]
    public string AdministratorIdentifier { get; init; } = string.Empty;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static DirectoryBootstrapRequest? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DirectoryBootstrapRequest>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(SetupKey))
        {
            problems.Add("setupKey is required.");
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            problems.Add("host is required.");
        }

        if (string.IsNullOrWhiteSpace(BaseDn))
        {
            problems.Add("baseDn is required.");
        }

        if (string.IsNullOrWhiteSpace(UserSearchFilter))
        {
            problems.Add("userSearchFilter is required.");
        }

        if (string.IsNullOrWhiteSpace(BindUserName))
        {
            problems.Add("bindUserName is required.");
        }

        if (string.IsNullOrWhiteSpace(BindPassword))
        {
            problems.Add("bindPassword is required.");
        }

        if (string.IsNullOrWhiteSpace(AdministratorIdentifier))
        {
            problems.Add("administratorIdentifier is required.");
        }

        return problems;
    }

    public CompleteSetupLdapSettings ToLdapSettings() => new(
        Name: string.IsNullOrWhiteSpace(DirectoryName) ? Host.Trim() : DirectoryName.Trim(),
        Host: Host.Trim(),
        BaseDn: BaseDn.Trim(),
        UserSearchFilter: UserSearchFilter.Trim(),
        BindUserName: BindUserName.Trim(),
        BindUserDomain: string.IsNullOrWhiteSpace(BindUserDomain) ? null : BindUserDomain.Trim(),
        BindPassword: BindPassword);
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DirectoryBootstrapStatus
{
    Bootstrapped,
    AlreadyBootstrapped,
}

/// <summary>
/// What the installer reads back. Carries no credential, no bind DN, and no directory attributes
/// beyond the administrator's user name, because the installer prints and logs this verbatim.
/// </summary>
public sealed record DirectoryBootstrapResult
{
    [JsonPropertyName("status")]
    public DirectoryBootstrapStatus Status { get; init; }

    [JsonPropertyName("administratorUserName")]
    public string? AdministratorUserName { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static DirectoryBootstrapResult? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DirectoryBootstrapResult>(json, SerializerOptions);
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
