using System.Text.RegularExpressions;

namespace ITAdmin.Deployment;

/// <summary>
/// The first-hop Git/SSH contract: how a Windows server proves its identity to the ITAdmin
/// repository, and how it keeps doing so after the administrator who set it up has logged off.
///
/// <para>
/// <b>The problem this solves.</b> Generating a key at a non-default path
/// (<c>~/.ssh/itadmin_deploy</c>) is good practice, but it is not sufficient on its own: a plain
/// <c>git clone git@github.com:owner/repo.git</c> does not know that key exists. OpenSSH tries its
/// default identity names and whatever an agent happens to be offering, which means the documented
/// clone command either fails, or - worse - succeeds using some other credential that the operator
/// did not intend and that the server will not have later. So the preparation must include an
/// explicit SSH configuration entry that binds the deploy key with <c>IdentitiesOnly</c> set.
/// </para>
///
/// <para>
/// <b>Why an alias rather than <c>Host github.com</c>.</b> Writing the entry against the real host
/// name would capture <em>every</em> GitHub SSH operation that administrator ever performs from
/// that profile - their own repositories included - and force all of it through a read-only deploy
/// key scoped to one repository. On a dedicated server that is merely untidy; on a jump box or an
/// administrator's workstation it silently breaks unrelated work in a way that is genuinely
/// annoying to diagnose. So the entry is written against an ITAdmin-specific alias
/// (<see cref="SshHostAlias"/>) whose <c>HostName</c> is the real host. Cloning through the alias
/// deterministically uses the deploy key; <c>git@github.com:...</c> continues to behave exactly as
/// it did before ITAdmin was installed.
/// </para>
///
/// <para>
/// <b>Host identity.</b> The first connection to a Git host is the one moment where "just accept
/// it" is genuinely dangerous, and also the only moment a human can meaningfully verify. So the
/// preparation records the host key deliberately - after the operator compares its fingerprint
/// against the value the Git host publishes - rather than through
/// <c>StrictHostKeyChecking=accept-new</c>.
/// </para>
///
/// <para>
/// <b>Life after bootstrap.</b> Everything above lives in an administrator's user profile, which
/// the Host Agent (running as LocalSystem) cannot and should not read. The bootstrap therefore
/// copies the key and the verified host-key entries into a machine-owned directory under
/// ProgramData with an ACL restricted to SYSTEM and Administrators, and the agent uses those. A
/// server whose administrator account is later deleted keeps working.
/// </para>
/// </summary>
public static partial class RepositoryAccessContract
{
    /// <summary>Machine-owned directory holding the deploy key and known-hosts material.</summary>
    public const string KeysDirectoryName = "keys";

    public const string DeployKeyFileName = "deploy_key";

    public const string KnownHostsFileName = "known_hosts";

    /// <summary>Conventional file name for the operator-generated key, used in the documented steps.</summary>
    public const string RecommendedOperatorKeyFileName = "itadmin_deploy";

    /// <summary>
    /// ITAdmin-specific SSH host alias used for the first-hop clone.
    ///
    /// <para>
    /// The alias exists so the deploy key applies to ITAdmin's clone and nothing else. It is only
    /// ever used interactively, by the operator, for the bootstrap clone: the machine-persisted
    /// configuration the Host Agent uses names the real host directly and supplies the key through
    /// <c>GIT_SSH_COMMAND</c>, so it depends on no user-profile SSH configuration at all.
    /// </para>
    /// </summary>
    public const string SshHostAlias = "github-itadmin";

    /// <summary>
    /// The clone URL the documentation tells an operator to use, routed through the alias.
    /// </summary>
    public static string BuildAliasCloneUrl(string owner, string repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        return $"git@{SshHostAlias}:{owner}/{repository}.git";
    }

    /// <summary>
    /// Rewrites an alias-form remote back to its real host.
    ///
    /// <para>
    /// The bootstrap clone leaves <c>origin</c> pointing at the alias, which only resolves inside
    /// the profile that has the SSH config entry. The machine configuration the Host Agent reads
    /// must name the real host, or repository access would break the moment that profile is gone.
    /// </para>
    /// </summary>
    public static string ResolveAliasToRealHost(string repositoryUrl, string realHost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(realHost);

        if (!TryGetSshHost(repositoryUrl, out var host, out _)
            || !string.Equals(host, SshHostAlias, StringComparison.OrdinalIgnoreCase))
        {
            return repositoryUrl;
        }

        // Replace only the host segment; the user, path, and any ssh:// scheme are preserved.
        var index = repositoryUrl.IndexOf(SshHostAlias, StringComparison.OrdinalIgnoreCase);
        return repositoryUrl[..index] + realHost + repositoryUrl[(index + SshHostAlias.Length)..];
    }

    /// <summary>
    /// Extracts the host from a Git SSH remote. Handles both forms Git accepts:
    /// <c>git@host:owner/repo.git</c> (scp-like) and <c>ssh://git@host[:port]/owner/repo.git</c>.
    /// </summary>
    public static bool TryGetSshHost(string? repositoryUrl, out string host, out int port)
    {
        host = string.Empty;
        port = 22;

        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return false;
        }

        var url = repositoryUrl.Trim();

        if (url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            {
                return false;
            }

            host = uri.Host;
            port = uri.Port > 0 ? uri.Port : 22;
            return true;
        }

        // scp-like: [user@]host:path - the colon separates host from path, not host from port.
        var match = ScpLikeRemote().Match(url);
        if (!match.Success)
        {
            return false;
        }

        host = match.Groups["host"].Value;
        return !string.IsNullOrWhiteSpace(host);
    }

    /// <summary>
    /// The SSH config stanza the operator adds during preparation. Emitted from code so the
    /// documentation, the bootstrap's diagnostics, and the tests cannot describe three different
    /// things.
    /// </summary>
    /// <param name="realHost">The Git host the alias resolves to, e.g. <c>github.com</c>.</param>
    /// <param name="identityFilePath">Path to the private deploy key.</param>
    /// <param name="alias">
    /// The alias to define. Defaults to <see cref="SshHostAlias"/>; the alias is what keeps the
    /// deploy key from capturing every other SSH operation against the real host.
    /// </param>
    public static string BuildSshConfigEntry(
        string realHost,
        string identityFilePath,
        string? alias = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(realHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(identityFilePath);

        return string.Join(
            Environment.NewLine,
            $"Host {alias ?? SshHostAlias}",
            $"    HostName {realHost}",
            "    User git",
            $"    IdentityFile {identityFilePath}",
            // Without this, OpenSSH still offers default identities and agent keys first, and the
            // clone may succeed on a credential the server will not have afterwards.
            "    IdentitiesOnly yes");
    }

    /// <summary>
    /// SSH options for ITAdmin's own automated Git operations.
    ///
    /// <para>
    /// Every one of these is load-bearing. <c>IdentitiesOnly</c> stops SSH substituting another
    /// credential. <c>BatchMode</c> stops any prompt hanging a service that has no console.
    /// <c>StrictHostKeyChecking=yes</c> plus an explicit <c>UserKnownHostsFile</c> means the machine
    /// trusts exactly the host keys the operator verified, independent of any user profile - and
    /// <c>GlobalKnownHostsFile=/dev/null</c> ensures a system-wide file cannot silently widen that.
    /// </para>
    /// </summary>
    public static string BuildSshCommand(string deployKeyPath, string? knownHostsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deployKeyPath);

        var options = new List<string>
        {
            $"ssh -i \"{deployKeyPath}\"",
            "-o IdentitiesOnly=yes",
            "-o BatchMode=yes",
            "-o StrictHostKeyChecking=yes",
        };

        if (!string.IsNullOrWhiteSpace(knownHostsPath))
        {
            options.Add($"-o UserKnownHostsFile=\"{knownHostsPath}\"");
            options.Add("-o GlobalKnownHostsFile=/dev/null");
        }

        return string.Join(' ', options);
    }

    /// <summary>
    /// Whether a known_hosts file carries at least one usable entry for a host. Used to refuse a
    /// bootstrap that would otherwise persist an empty trust store and fail later, inside a service,
    /// with a far less obvious message.
    /// </summary>
    public static bool ContainsHostEntry(string? knownHostsContent, string host)
    {
        if (string.IsNullOrWhiteSpace(knownHostsContent) || string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        foreach (var line in knownHostsContent.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            // Hashed entries (|1|...) cannot be matched by name, but their presence still means the
            // operator recorded something; ssh-keygen -F is what actually resolves them.
            if (trimmed.StartsWith("|1|", StringComparison.Ordinal))
            {
                return true;
            }

            var fields = trimmed.Split(' ', 2);
            if (fields.Length < 2)
            {
                continue;
            }

            foreach (var pattern in fields[0].Split(','))
            {
                var candidate = pattern.Trim();
                // A bracketed non-default port form: [host]:2222
                if (candidate.StartsWith('[') && candidate.Contains("]:", StringComparison.Ordinal))
                {
                    candidate = candidate[1..candidate.IndexOf("]:", StringComparison.Ordinal)];
                }

                if (string.Equals(candidate, host, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    [GeneratedRegex(@"^(?:(?<user>[^@/]+)@)?(?<host>[A-Za-z0-9._-]+):(?!//)", RegexOptions.ExplicitCapture)]
    private static partial Regex ScpLikeRemote();
}
