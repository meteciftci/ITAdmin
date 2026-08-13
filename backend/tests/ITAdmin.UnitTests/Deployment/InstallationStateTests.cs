using ITAdmin.Deployment;

namespace ITAdmin.UnitTests.Deployment;

public sealed class InstallationStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    private static InstallationState Installed(string activeVersion) => new()
    {
        Phase = InstallationPhase.Installed,
        ActiveVersion = activeVersion,
        UpdatedAtUtc = Now,
    };

    [Fact]
    public void Fresh_MachineWithNothingInstalled_ClassifiesAsFreshInstall()
    {
        var state = InstallationState.Fresh(Now);

        Assert.Equal(InstallationPhase.NotInstalled, state.Phase);
        Assert.Equal(InstallationIntent.FreshInstall, state.ClassifyIntent(ReleaseVersion.Parse("2.0.0")));
    }

    [Fact]
    public void ClassifyIntent_SameVersionAsActive_IsRepairNotUpgrade() =>
        Assert.Equal(
            InstallationIntent.SameVersionRepair,
            Installed("2.0.0").ClassifyIntent(ReleaseVersion.Parse("2.0.0")));

    [Fact]
    public void ClassifyIntent_NewerVersion_IsUpgrade() =>
        Assert.Equal(
            InstallationIntent.Upgrade,
            Installed("2.0.0").ClassifyIntent(ReleaseVersion.Parse("2.1.0")));

    [Fact]
    public void ClassifyIntent_OlderVersion_IsDowngrade() =>
        // Surfaced distinctly so the installer can require explicit intent rather than silently
        // rolling a machine backwards past migrations it has already applied.
        Assert.Equal(
            InstallationIntent.Downgrade,
            Installed("2.1.0").ClassifyIntent(ReleaseVersion.Parse("2.0.0")));

    [Theory]
    [InlineData(InstallationPhase.Failed)]
    [InlineData(InstallationPhase.Staging)]
    [InlineData(InstallationPhase.Configuring)]
    [InlineData(InstallationPhase.Activating)]
    [InlineData(InstallationPhase.ProvisioningPrerequisites)]
    public void ClassifyIntent_InterruptedPhases_RequireResume(InstallationPhase phase)
    {
        var state = Installed("2.0.0") with { Phase = phase };

        Assert.Equal(
            InstallationIntent.ResumeFailedInstall,
            state.ClassifyIntent(ReleaseVersion.Parse("2.0.0")));
    }

    [Fact]
    public void ClassifyIntent_AwaitingReboot_IsDistinctFromOrdinaryResume()
    {
        var state = Installed("2.0.0") with { Phase = InstallationPhase.AwaitingReboot };

        Assert.Equal(
            InstallationIntent.ResumeAfterReboot,
            state.ClassifyIntent(ReleaseVersion.Parse("2.0.0")));
    }

    [Fact]
    public void ClassifyIntent_MigrationInFlight_TakesPriorityOverEverythingElse()
    {
        // A machine that died mid-migration may have a partially migrated database. That must be
        // surfaced, never treated as an ordinary rerun.
        var state = Installed("2.0.0") with { MigrationInFlight = true };

        Assert.Equal(
            InstallationIntent.RecoverInterruptedMigration,
            state.ClassifyIntent(ReleaseVersion.Parse("2.0.0")));
    }

    [Fact]
    public void ClassifyIntent_InstalledPhaseButNoActiveVersion_IsTreatedAsFreshInstall()
    {
        var state = new InstallationState { Phase = InstallationPhase.Installed, UpdatedAtUtc = Now };

        Assert.Equal(InstallationIntent.FreshInstall, state.ClassifyIntent(ReleaseVersion.Parse("2.0.0")));
    }

    [Fact]
    public void ClassifyIntent_CorruptActiveVersion_RequiresResumeRatherThanGuessing()
    {
        var state = Installed("not-a-version");

        Assert.Equal(
            InstallationIntent.ResumeFailedInstall,
            state.ClassifyIntent(ReleaseVersion.Parse("2.0.0")));
    }

    [Fact]
    public void Json_RoundTripsIncludingPhaseAndError()
    {
        var state = Installed("2.0.0") with
        {
            StagedVersion = "2.1.0",
            PreviousVersion = "1.9.0",
            LastMigrationApplied = "20260101000000_Baseline",
            LastError = new InstallationError
            {
                Step = "Activate",
                Message = "Health check failed.",
                OccurredAtUtc = Now,
            },
        };

        var restored = InstallationState.FromJson(state.ToJson());

        Assert.NotNull(restored);
        Assert.Equal(InstallationPhase.Installed, restored!.Phase);
        Assert.Equal("2.0.0", restored.ActiveVersion);
        Assert.Equal("2.1.0", restored.StagedVersion);
        Assert.Equal("1.9.0", restored.PreviousVersion);
        Assert.Equal("20260101000000_Baseline", restored.LastMigrationApplied);
        Assert.Equal("Activate", restored.LastError!.Step);
    }

    [Fact]
    public void Json_SerialisesPhaseAsAReadableName()
    {
        // An operator diagnosing a failed rollout reads this file directly.
        Assert.Contains("\"Installed\"", Installed("2.0.0").ToJson(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ corrupt")]
    public void FromJson_UnreadableState_ReturnsNullSoTheInstallerDoesNotActOnAGuess(string? json) =>
        Assert.Null(InstallationState.FromJson(json));

    [Fact]
    public void State_CarriesNoSecretMaterial()
    {
        var state = Installed("2.0.0") with
        {
            LastError = new InstallationError { Step = "Migrate", Message = "connection refused", OccurredAtUtc = Now },
        };

        var json = state.ToJson();

        foreach (var term in new[] { "password", "secret", "connectionstring", "jwt", "setupkey" })
        {
            Assert.DoesNotContain(term, json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
