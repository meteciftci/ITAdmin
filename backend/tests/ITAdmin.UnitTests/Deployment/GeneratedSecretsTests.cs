using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Common.Security;
using ITAdmin.Deployment;

namespace ITAdmin.UnitTests.Deployment;

/// <summary>
/// The operator is never asked for the JWT signing key or the setup key, which means nothing
/// downstream will notice if they stop being strong. These tests are that noticing.
/// </summary>
public sealed class GeneratedSecretsTests
{
    [Fact]
    public void Create_ProducesDistinctValuesEveryTime()
    {
        var secrets = Enumerable.Range(0, 100).Select(_ => GeneratedSecrets.Create()).ToList();

        Assert.Equal(secrets.Count, secrets.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Create_CarriesTheDeclaredEntropy()
    {
        var secret = GeneratedSecrets.Create();

        // 48 bytes of base64 with padding stripped is 64 characters.
        Assert.Equal(64, secret.Length);
        Assert.True(GeneratedSecrets.LooksSufficientlyRandom(secret));
    }

    [Fact]
    public void Create_IsUrlSafe() =>
        Assert.DoesNotContain(
            GeneratedSecrets.Create(),
            character => character is '+' or '/' or '=');

    [Fact]
    public void Create_BelowTheMinimumEntropy_IsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneratedSecrets.Create(16));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("password")]
    [InlineData("changeme")]
    [InlineData("ThisIsAReasonablyLongButHumanChosenPassword!")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("your-key-goes-here-your-key-goes-here-your-key-goes-here")]
    public void LooksSufficientlyRandom_RejectsWhatAHumanWouldHaveTyped(string? value) =>
        Assert.False(GeneratedSecrets.LooksSufficientlyRandom(value));

    [Fact]
    public void HashSetupKey_MatchesTheApplicationsOwnValidator()
    {
        // The installer computes this hash without loading application assemblies. If the two
        // implementations diverged, first-run setup would reject every key the installer generated
        // and the failure would only appear on a real server.
        var setupKey = GeneratedSecrets.Create();

        Assert.Equal(SetupKeyHashValidator.ComputeConfiguredHash(setupKey), GeneratedSecrets.HashSetupKey(setupKey));
    }

    [Fact]
    public void HashSetupKey_IsAcceptedByTheApplicationsValidator()
    {
        var setupKey = GeneratedSecrets.Create();
        var validator = new SetupKeyHashValidator();

        Assert.Equal(
            SetupKeyValidationOutcome.Valid,
            validator.Validate(GeneratedSecrets.HashSetupKey(setupKey), setupKey));

        Assert.Equal(
            SetupKeyValidationOutcome.InvalidKey,
            validator.Validate(GeneratedSecrets.HashSetupKey(setupKey), GeneratedSecrets.Create()));
    }

    [Fact]
    public void HashSetupKey_DoesNotRevealTheKey()
    {
        var setupKey = GeneratedSecrets.Create();

        Assert.DoesNotContain(setupKey, GeneratedSecrets.HashSetupKey(setupKey), StringComparison.Ordinal);
    }

    [Fact]
    public void MachineSecrets_GeneratedSet_IsValid()
    {
        var setupKey = GeneratedSecrets.Create();
        var secrets = new MachineSecrets
        {
            ConnectionString = "Host=db.example.com;Database=itadmin;Username=itadmin_app;Password=x",
            JwtKey = GeneratedSecrets.Create(),
            SetupKey = setupKey,
            SetupKeyHash = GeneratedSecrets.HashSetupKey(setupKey),
        };

        var result = secrets.Validate();

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void MachineSecrets_WeakJwtKey_IsRejected()
    {
        var setupKey = GeneratedSecrets.Create();
        var secrets = new MachineSecrets
        {
            ConnectionString = "Host=db;Database=itadmin;Username=app;Password=x",
            JwtKey = "changeme",
            SetupKey = setupKey,
            SetupKeyHash = GeneratedSecrets.HashSetupKey(setupKey),
        };

        Assert.Contains(secrets.Validate().Errors, error => error.Contains("jwtKey", StringComparison.Ordinal));
    }

    [Fact]
    public void MachineSecrets_HashThatDoesNotMatchTheKey_IsRejected()
    {
        // Catches the update path where one of the pair is regenerated and the other is not, which
        // would leave a machine whose setup key silently no longer works.
        var secrets = new MachineSecrets
        {
            ConnectionString = "Host=db;Database=itadmin;Username=app;Password=x",
            JwtKey = GeneratedSecrets.Create(),
            SetupKey = GeneratedSecrets.Create(),
            SetupKeyHash = GeneratedSecrets.HashSetupKey(GeneratedSecrets.Create()),
        };

        Assert.Contains(
            secrets.Validate().Errors,
            error => error.Contains("setupKeyHash", StringComparison.Ordinal));
    }

    [Fact]
    public void MachineSecrets_RoundTripsThroughItsStoredForm()
    {
        var setupKey = GeneratedSecrets.Create();
        var secrets = new MachineSecrets
        {
            ConnectionString = "Host=db;Database=itadmin;Username=app;Password=x",
            JwtKey = GeneratedSecrets.Create(),
            SetupKey = setupKey,
            SetupKeyHash = GeneratedSecrets.HashSetupKey(setupKey),
        };

        var restored = MachineSecrets.FromJson(secrets.ToJson());

        Assert.NotNull(restored);
        Assert.Equal(secrets.JwtKey, restored!.JwtKey);
        Assert.Equal(secrets.SetupKey, restored.SetupKey);
        Assert.Equal(secrets.SetupKeyHash, restored.SetupKeyHash);
    }

    [Fact]
    public void InstallationState_NeverCarriesGeneratedSecrets()
    {
        // The state file is meant to be readable by an operator diagnosing a rollout, so it must
        // stay free of anything from the secret store.
        var state = new InstallationState
        {
            Phase = InstallationPhase.Installed,
            ActiveVersion = "2.0.0",
            LastMigrationApplied = "20240101000000_Initial",
            LastError = new InstallationError { Step = "Activate", Message = "example" },
        };

        var json = state.ToJson();

        foreach (var term in new[] { "jwtKey", "setupKey", "connectionString", "password", "bindPassword" })
        {
            Assert.DoesNotContain(term, json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
