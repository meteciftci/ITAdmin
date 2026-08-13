namespace ITAdmin.Deployment;

/// <summary>
/// Decides whether ITAdmin may take the HTTP binding it wants, and what - if anything - it is
/// allowed to do about a conflict.
///
/// <para>
/// <b>The problem.</b> A clean IIS installation creates a <c>Default Web Site</c> that owns the
/// wildcard <c>*:80:</c> binding. ITAdmin wants exactly that binding, because requiring
/// <c>:8080</c> on a dedicated server is a permanent papercut and choosing a random port silently is
/// worse. But "something already owns port 80" covers two completely different situations, and the
/// safe action in one is destructive in the other.
/// </para>
///
/// <para>
/// <b>The distinction that matters.</b> If <em>this installer</em> just turned IIS on, the Default
/// Web Site is a pristine artifact of that provisioning - nobody has ever deployed to it, and
/// standing it down is reasonable. If IIS already existed, every site on it is somebody's, including
/// one that happens to be called "Default Web Site" and is quietly serving something important. So
/// the decision is driven by <em>recorded provisioning history</em>, not by a site name: a name is a
/// guess, and this is not a decision worth guessing at.
/// </para>
///
/// <para>
/// Pure and input-only, so every branch is unit-testable without IIS. The installer supplies what it
/// observed; this returns what to do.
/// </para>
/// </summary>
public static class WebBindingOwnership
{
    /// <summary>The site IIS creates for itself on a fresh install.</summary>
    public const string DefaultWebSiteName = "Default Web Site";

    /// <summary>
    /// Decides the action for a requested ITAdmin binding.
    /// </summary>
    /// <param name="requested">The binding ITAdmin wants.</param>
    /// <param name="existingSites">Every site currently on this IIS instance.</param>
    /// <param name="itAdminSiteName">The site name ITAdmin uses.</param>
    /// <param name="iisProvisionedByInstaller">
    /// Whether IIS was turned on by an ITAdmin installer run, as recorded in installation state.
    /// Never inferred from a site name.
    /// </param>
    public static BindingOwnershipDecision Decide(
        WebBindingSpecification requested,
        IReadOnlyList<ExistingWebSite> existingSites,
        string itAdminSiteName,
        bool iisProvisionedByInstaller)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(existingSites);
        ArgumentException.ThrowIfNullOrWhiteSpace(itAdminSiteName);

        var conflicts = existingSites
            .Where(site => !site.NameEquals(itAdminSiteName))
            .SelectMany(site => site.Bindings
                .Where(binding => binding.Conflicts(requested))
                .Select(binding => new BindingConflict(site.Name, binding)))
            .ToList();

        // --- Case C: ITAdmin already owns it -------------------------------------------------
        // Checked first so a rerun never mistakes ITAdmin's own site for an external conflict, and
        // never adds a duplicate binding.
        var itAdminSite = existingSites.FirstOrDefault(site => site.NameEquals(itAdminSiteName));
        var itAdminAlreadyHasBinding = itAdminSite?.Bindings.Any(binding => binding.Equals(requested)) == true;

        if (itAdminAlreadyHasBinding && conflicts.Count == 0)
        {
            return new BindingOwnershipDecision(
                BindingOwnershipAction.AlreadyOwned,
                requested,
                conflicts,
                $"ITAdmin already owns {requested}. Nothing to change.");
        }

        if (conflicts.Count == 0)
        {
            return new BindingOwnershipDecision(
                BindingOwnershipAction.Claim,
                requested,
                conflicts,
                $"{requested} is unused; ITAdmin will bind it.");
        }

        // --- Case A: this installer provisioned IIS ------------------------------------------
        // Only a pristine Default Web Site qualifies, and only when we recorded provisioning it.
        // "Pristine" means: it is the site IIS creates, it holds only the wildcard binding IIS gives
        // it, and it has no application beyond the root - i.e. nobody has deployed to it.
        if (iisProvisionedByInstaller
            && conflicts.All(conflict => IsPristineDefaultWebSite(conflict.SiteName, existingSites)))
        {
            return new BindingOwnershipDecision(
                BindingOwnershipAction.StandDownPristineDefaultSite,
                requested,
                conflicts,
                $"IIS was provisioned by this installation and '{DefaultWebSiteName}' is still in its "
                + $"as-created state, so it will be stopped and disabled to free {requested}. It is not "
                + "deleted: leaving it present, stopped, keeps the change trivially reversible.");
        }

        // --- Case B: somebody else's site ------------------------------------------------------
        return new BindingOwnershipDecision(
            BindingOwnershipAction.FailConflict,
            requested,
            conflicts,
            DescribeConflict(requested, conflicts, iisProvisionedByInstaller));
    }

    /// <summary>
    /// Whether a conflicting site is the untouched site IIS creates for itself.
    ///
    /// <para>
    /// Deliberately strict. A Default Web Site that has gained an extra binding, or an application
    /// below its root, is one somebody has adopted - and adopting it is exactly what an
    /// administrator does when they do not want to create a new site. Standing that down would take
    /// out a real workload.
    /// </para>
    /// </summary>
    private static bool IsPristineDefaultWebSite(string siteName, IReadOnlyList<ExistingWebSite> sites)
    {
        if (!string.Equals(siteName, DefaultWebSiteName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var site = sites.FirstOrDefault(candidate => candidate.NameEquals(siteName));
        if (site is null)
        {
            return false;
        }

        return site.Bindings.Count == 1
            && site.Bindings[0].Protocol is "http"
            && site.Bindings[0].Port == 80
            && string.IsNullOrEmpty(site.Bindings[0].HostHeader)
            && !site.HasApplicationsBeyondRoot;
    }

    private static string DescribeConflict(
        WebBindingSpecification requested,
        IReadOnlyList<BindingConflict> conflicts,
        bool iisProvisionedByInstaller)
    {
        var lines = new List<string>
        {
            $"The HTTP binding ITAdmin requested ({requested}) is already owned by another site on this server.",
            string.Empty,
            "  Requested by ITAdmin:",
            $"    {requested}",
            string.Empty,
            "  Already bound:",
        };

        foreach (var conflict in conflicts)
        {
            lines.Add($"    site '{conflict.SiteName}'  ->  {conflict.Binding}");
        }

        lines.Add(string.Empty);
        lines.Add("  ITAdmin will not stop, rebind, or remove a site it did not create. Choose one:");
        lines.Add(string.Empty);
        lines.Add("    1. Free the port deliberately - stop or rebind the site above, then re-run.");
        lines.Add("    2. Give ITAdmin a different port explicitly, e.g. -HttpPort 8080.");
        lines.Add("    3. Give ITAdmin its own host name on the same port, e.g. -HttpHostHeader itadmin.example.com");
        lines.Add("       (requires DNS pointing that name at this server).");

        if (!iisProvisionedByInstaller)
        {
            lines.Add(string.Empty);
            lines.Add(
                "  IIS was already installed before ITAdmin, so every site on it is assumed to be "
                + "operator-owned - including one named 'Default Web Site'.");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>What the installer should do about the requested binding.</summary>
public enum BindingOwnershipAction
{
    /// <summary>The binding is free; take it.</summary>
    Claim = 0,

    /// <summary>ITAdmin already has exactly this binding. Idempotent no-op.</summary>
    AlreadyOwned = 1,

    /// <summary>
    /// A pristine, installer-provisioned Default Web Site holds it. Stop and disable that site.
    /// </summary>
    StandDownPristineDefaultSite = 2,

    /// <summary>Somebody else's site holds it. Fail preflight with a diagnosis.</summary>
    FailConflict = 3,
}

/// <summary>One HTTP/HTTPS binding.</summary>
public sealed record WebBindingSpecification(string Protocol, int Port, string? HostHeader)
{
    public string NormalisedHostHeader => HostHeader?.Trim() ?? string.Empty;

    /// <summary>
    /// Whether two bindings collide.
    ///
    /// <para>
    /// IIS keys a binding on protocol + address + port + host header, so two sites can share a port
    /// when their host headers differ. A wildcard (empty) host header collides with everything on
    /// that port, which is precisely why the Default Web Site's <c>*:80:</c> is in the way.
    /// </para>
    /// </summary>
    public bool Conflicts(WebBindingSpecification other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!string.Equals(Protocol, other.Protocol, StringComparison.OrdinalIgnoreCase) || Port != other.Port)
        {
            return false;
        }

        if (NormalisedHostHeader.Length == 0 || other.NormalisedHostHeader.Length == 0)
        {
            return true;
        }

        return string.Equals(NormalisedHostHeader, other.NormalisedHostHeader, StringComparison.OrdinalIgnoreCase);
    }

    public bool Equals(WebBindingSpecification? other) =>
        other is not null
        && string.Equals(Protocol, other.Protocol, StringComparison.OrdinalIgnoreCase)
        && Port == other.Port
        && string.Equals(NormalisedHostHeader, other.NormalisedHostHeader, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        HashCode.Combine(Protocol.ToLowerInvariant(), Port, NormalisedHostHeader.ToLowerInvariant());

    /// <summary>IIS binding-information form: <c>*:80:</c> or <c>*:80:host</c>.</summary>
    public override string ToString() => $"{Protocol}  *:{Port}:{NormalisedHostHeader}";
}

/// <summary>A site as observed on this IIS instance.</summary>
public sealed record ExistingWebSite(
    string Name,
    IReadOnlyList<WebBindingSpecification> Bindings,
    bool HasApplicationsBeyondRoot = false)
{
    public bool NameEquals(string other) => string.Equals(Name, other, StringComparison.OrdinalIgnoreCase);
}

public sealed record BindingConflict(string SiteName, WebBindingSpecification Binding);

public sealed record BindingOwnershipDecision(
    BindingOwnershipAction Action,
    WebBindingSpecification Requested,
    IReadOnlyList<BindingConflict> Conflicts,
    string Message)
{
    /// <summary>True when the installer may proceed without operator intervention.</summary>
    public bool CanProceed => Action is not BindingOwnershipAction.FailConflict;
}
