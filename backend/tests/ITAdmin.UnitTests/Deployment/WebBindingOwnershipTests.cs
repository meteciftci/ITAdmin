using ITAdmin.Deployment;

namespace ITAdmin.UnitTests.Deployment;

/// <summary>
/// Who gets port 80.
///
/// <para>
/// A clean IIS installation creates a Default Web Site owning the wildcard <c>*:80:</c> binding, so
/// ITAdmin's preferred binding is contested on exactly the machine type it most wants to work on.
/// The safe response depends entirely on whether <em>this installer</em> turned IIS on: if it did,
/// the Default Web Site is a pristine artifact; if it did not, every site is somebody's. These tests
/// pin that the branch is chosen from recorded history rather than from a site name.
/// </para>
/// </summary>
public sealed class WebBindingOwnershipTests
{
    private const string ITAdminSite = "ITAdmin";

    private static WebBindingSpecification Http(int port = 80, string? host = null) =>
        new("http", port, host);

    private static ExistingWebSite PristineDefaultSite() =>
        new(WebBindingOwnership.DefaultWebSiteName, [Http()]);

    private static BindingOwnershipDecision Decide(
        IReadOnlyList<ExistingWebSite> sites,
        bool provisionedByUs,
        WebBindingSpecification? requested = null) =>
        WebBindingOwnership.Decide(requested ?? Http(), sites, ITAdminSite, provisionedByUs);

    // ==========================================================================================
    // Case A — this installer provisioned IIS
    // ==========================================================================================

    [Fact]
    public void FreshIis_PristineDefaultSite_IsStoodDown()
    {
        var decision = Decide([PristineDefaultSite()], provisionedByUs: true);

        Assert.Equal(BindingOwnershipAction.StandDownPristineDefaultSite, decision.Action);
        Assert.True(decision.CanProceed);
        Assert.Contains("as-created state", decision.Message, StringComparison.Ordinal);
        // Stopped, not deleted — the change stays trivially reversible.
        Assert.Contains("not deleted", decision.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FreshIis_DefaultSiteWithAnExtraBinding_IsNotTouched()
    {
        // An adopted Default Web Site is what an administrator ends up with when they did not want
        // to create a new site. Standing it down would take out a real workload.
        var adopted = new ExistingWebSite(
            WebBindingOwnership.DefaultWebSiteName,
            [Http(), Http(8080, "intranet.example.com")]);

        var decision = Decide([adopted], provisionedByUs: true);

        Assert.Equal(BindingOwnershipAction.FailConflict, decision.Action);
        Assert.False(decision.CanProceed);
    }

    [Fact]
    public void FreshIis_DefaultSiteWithAnApplication_IsNotTouched()
    {
        var deployedTo = new ExistingWebSite(
            WebBindingOwnership.DefaultWebSiteName,
            [Http()],
            HasApplicationsBeyondRoot: true);

        Assert.Equal(BindingOwnershipAction.FailConflict, Decide([deployedTo], provisionedByUs: true).Action);
    }

    [Fact]
    public void FreshIis_ADifferentSiteOnPort80_IsNotTouchedEvenThoughWeProvisionedIis()
    {
        // Provisioning IIS licenses standing down the site IIS created for itself — nothing else.
        var other = new ExistingWebSite("Contoso Intranet", [Http()]);

        var decision = Decide([other], provisionedByUs: true);

        Assert.Equal(BindingOwnershipAction.FailConflict, decision.Action);
        Assert.Contains("Contoso Intranet", decision.Message, StringComparison.Ordinal);
    }

    // ==========================================================================================
    // Case B — IIS pre-existed
    // ==========================================================================================

    [Fact]
    public void PreExistingIis_UnrelatedPort80Site_FailsClosed()
    {
        var decision = Decide([new ExistingWebSite("Contoso Intranet", [Http()])], provisionedByUs: false);

        Assert.Equal(BindingOwnershipAction.FailConflict, decision.Action);
        Assert.False(decision.CanProceed);
    }

    [Fact]
    public void PreExistingIis_DefaultWebSite_IsTreatedAsOperatorOwned()
    {
        // The name proves nothing. A Default Web Site on an IIS instance we did not create may be
        // quietly serving something important.
        var decision = Decide([PristineDefaultSite()], provisionedByUs: false);

        Assert.Equal(BindingOwnershipAction.FailConflict, decision.Action);
        Assert.Contains("already installed before ITAdmin", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConflictDiagnosis_NamesEverythingAnOperatorNeeds()
    {
        var decision = Decide([new ExistingWebSite("Contoso Intranet", [Http()])], provisionedByUs: false);

        // The conflicting site, its binding, ITAdmin's requested binding, and the available choices.
        Assert.Contains("Contoso Intranet", decision.Message, StringComparison.Ordinal);
        Assert.Contains("*:80:", decision.Message, StringComparison.Ordinal);
        Assert.Contains("Requested by ITAdmin", decision.Message, StringComparison.Ordinal);
        Assert.Contains("-HttpPort 8080", decision.Message, StringComparison.Ordinal);
        Assert.Contains("-HttpHostHeader", decision.Message, StringComparison.Ordinal);
        Assert.Contains("will not stop, rebind, or remove", decision.Message, StringComparison.Ordinal);

        var conflict = Assert.Single(decision.Conflicts);
        Assert.Equal("Contoso Intranet", conflict.SiteName);
    }

    [Fact]
    public void ExplicitAlternatePort_Succeeds()
    {
        // The documented escape hatch from a conflict.
        var decision = Decide(
            [new ExistingWebSite("Contoso Intranet", [Http()])],
            provisionedByUs: false,
            requested: Http(8080));

        Assert.Equal(BindingOwnershipAction.Claim, decision.Action);
        Assert.True(decision.CanProceed);
    }

    [Fact]
    public void ExplicitHostHeaderOnTheSamePort_Succeeds()
    {
        // IIS keys a binding on host header too, so two sites can share port 80 with distinct names
        // — but only when the existing site is not itself a wildcard.
        var decision = Decide(
            [new ExistingWebSite("Contoso Intranet", [Http(80, "intranet.example.com")])],
            provisionedByUs: false,
            requested: Http(80, "itadmin.example.com"));

        Assert.Equal(BindingOwnershipAction.Claim, decision.Action);
    }

    [Fact]
    public void WildcardExistingBinding_ConflictsWithAnyHostHeaderOnThatPort()
    {
        // This is exactly why the Default Web Site's "*:80:" is in the way.
        var decision = Decide(
            [new ExistingWebSite("Contoso Intranet", [Http()])],
            provisionedByUs: false,
            requested: Http(80, "itadmin.example.com"));

        Assert.Equal(BindingOwnershipAction.FailConflict, decision.Action);
    }

    [Fact]
    public void NeverSilentlyPicksAnotherPort()
    {
        // On a production server, quietly moving to a random port is worse than failing.
        var decision = Decide([new ExistingWebSite("Contoso Intranet", [Http()])], provisionedByUs: false);

        Assert.Equal(80, decision.Requested.Port);
        Assert.Equal(BindingOwnershipAction.FailConflict, decision.Action);
    }

    // ==========================================================================================
    // Case C — rerun / repair
    // ==========================================================================================

    [Fact]
    public void Repair_ITAdminAlreadyOwnsTheBinding_IsIdempotent()
    {
        var decision = Decide([new ExistingWebSite(ITAdminSite, [Http()])], provisionedByUs: true);

        Assert.Equal(BindingOwnershipAction.AlreadyOwned, decision.Action);
        Assert.Empty(decision.Conflicts);
        Assert.Contains("Nothing to change", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Repair_ITAdminsOwnSiteIsNeverMistakenForAnExternalConflict()
    {
        var decision = Decide(
            [
                new ExistingWebSite(ITAdminSite, [Http()]),
                new ExistingWebSite("Contoso Intranet", [Http(8080)]),
            ],
            provisionedByUs: false);

        Assert.Equal(BindingOwnershipAction.AlreadyOwned, decision.Action);
        Assert.Empty(decision.Conflicts);
    }

    [Fact]
    public void Repair_SiteNameMatchingIsCaseInsensitive() =>
        Assert.Equal(
            BindingOwnershipAction.AlreadyOwned,
            Decide([new ExistingWebSite("itadmin", [Http()])], provisionedByUs: false).Action);

    [Fact]
    public void Repair_ITAdminSiteExistsButWithADifferentBinding_ClaimsTheRequestedOne()
    {
        // Changing -HttpPort on a rerun. No conflict, so the new binding is simply added.
        var decision = Decide(
            [new ExistingWebSite(ITAdminSite, [Http(8080)])],
            provisionedByUs: false);

        Assert.Equal(BindingOwnershipAction.Claim, decision.Action);
    }

    // ==========================================================================================
    // General
    // ==========================================================================================

    [Fact]
    public void NoSitesAtAll_ClaimsTheBinding() =>
        Assert.Equal(BindingOwnershipAction.Claim, Decide([], provisionedByUs: true).Action);

    [Fact]
    public void MultipleUnrelatedSitesOnOtherPorts_AreNeverModified()
    {
        var sites = new List<ExistingWebSite>
        {
            new("Site A", [Http(8080)]),
            new("Site B", [Http(8081, "b.example.com")]),
            new("Site C", [new WebBindingSpecification("https", 443, "c.example.com")]),
        };

        var decision = Decide(sites, provisionedByUs: false);

        Assert.Equal(BindingOwnershipAction.Claim, decision.Action);
        Assert.Empty(decision.Conflicts);
    }

    [Fact]
    public void HttpsBindingsDoNotConflictWithTheHttpRequest() =>
        // Different protocol, same port number is still a different binding.
        Assert.Equal(
            BindingOwnershipAction.Claim,
            Decide([new ExistingWebSite("Secure", [new WebBindingSpecification("https", 80, null)])], false).Action);

    [Fact]
    public void MultipleConflicts_AreAllReported()
    {
        var sites = new List<ExistingWebSite>
        {
            new("Site A", [Http()]),
            new("Site B", [Http(80, "b.example.com")]),
        };

        var decision = Decide(sites, provisionedByUs: false);

        Assert.Equal(BindingOwnershipAction.FailConflict, decision.Action);
        Assert.Equal(2, decision.Conflicts.Count);
    }

    [Fact]
    public void BindingSpecification_FormatsAsIisBindingInformation()
    {
        Assert.Equal("http  *:80:", Http().ToString());
        Assert.Equal("http  *:8080:itadmin.example.com", Http(8080, "itadmin.example.com").ToString());
    }

    [Fact]
    public void BindingSpecification_EqualityIgnoresHostHeaderCaseAndNullVersusEmpty()
    {
        Assert.Equal(Http(80, null), Http(80, ""));
        Assert.Equal(Http(80, "ITAdmin.Example.com"), Http(80, "itadmin.example.com"));
        Assert.NotEqual(Http(80, "a.example.com"), Http(80, "b.example.com"));
    }

    [Fact]
    public void HttpOnlyDecision_NeverConsultsACertificate()
    {
        // The whole ownership decision is protocol/port/host-header only — no thumbprint, no store.
        foreach (var property in typeof(WebBindingSpecification).GetProperties())
        {
            foreach (var forbidden in new[] { "certificate", "thumbprint", "ssl", "tls" })
            {
                Assert.False(
                    property.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"WebBindingSpecification.{property.Name} would drag TLS into the HTTP-only path.");
            }
        }
    }

    [Fact]
    public void InstallationState_RecordsWhetherWeProvisionedIis()
    {
        // The branch above must come from recorded history, not from a site name — so the history
        // has to survive a restart and a re-run.
        var state = InstallationState.Fresh(DateTimeOffset.UnixEpoch) with { IisProvisionedByInstaller = true };

        var restored = InstallationState.FromJson(state.ToJson());

        Assert.NotNull(restored);
        Assert.True(restored!.IisProvisionedByInstaller);
        Assert.False(InstallationState.Fresh(DateTimeOffset.UnixEpoch).IisProvisionedByInstaller);
    }
}
