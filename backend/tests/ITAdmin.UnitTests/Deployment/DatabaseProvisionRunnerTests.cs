using System.Text.Json;
using ITAdmin.Api;

namespace ITAdmin.UnitTests.Deployment;

/// <summary>
/// The installer's database-provisioning step. The DDL sequence itself needs a real PostgreSQL and
/// is covered by an integration test; this fixes the contract the installer depends on - which
/// switch triggers it, which inputs it refuses, and that nothing it echoes carries a credential.
/// </summary>
public sealed class DatabaseProvisionRunnerTests
{
    private const string AdminConnectionString =
        "Host=db.corp.example.com;Port=5432;Database=postgres;Username=postgres;Password=not-a-real-admin-password";

    private static DatabaseProvisionRequest ValidRequest() => new()
    {
        AdminConnectionString = AdminConnectionString,
        TargetDatabase = "itadmin",
        AppRole = "itadmin_app",
        AppRolePassword = "Abc123Def456Ghi789Jkl012Mno345Pq",
    };

    [Fact]
    public void IsRequested_OnlyForTheProvisionSwitch()
    {
        Assert.True(DatabaseProvisionRunner.IsRequested(["--provision-database"]));
        Assert.False(DatabaseProvisionRunner.IsRequested(["--migrate"]));
        Assert.False(DatabaseProvisionRunner.IsRequested([]));
    }

    [Fact]
    public void ResolveInputPath_ReadsTheFileArgument()
    {
        Assert.Equal(
            @"C:\ProgramData\ITAdmin\state\provision.json",
            DatabaseProvisionRunner.ResolveInputPath(
            [
                "--provision-database", "--input", @"C:\ProgramData\ITAdmin\state\provision.json",
            ]));

        Assert.Null(DatabaseProvisionRunner.ResolveInputPath(["--provision-database"]));
    }

    [Fact]
    public void Request_Valid_HasNoProblems()
    {
        Assert.Empty(ValidRequest().Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a connection string at all")]
    [InlineData("Port=5432;Database=postgres")]
    public void Request_BadAdminConnectionString_IsReported(string connectionString)
    {
        var request = ValidRequest() with { AdminConnectionString = connectionString };
        Assert.Contains(request.Validate(), problem => problem.Contains("adminConnectionString", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("it admin")]
    [InlineData("itadmin; DROP DATABASE postgres")]
    [InlineData("\"itadmin\"")]
    public void Request_UnsafeDatabaseOrRoleName_IsReported(string name)
    {
        Assert.Contains(
            (ValidRequest() with { TargetDatabase = name }).Validate(),
            problem => problem.Contains("targetDatabase", StringComparison.Ordinal));
        Assert.Contains(
            (ValidRequest() with { AppRole = name }).Validate(),
            problem => problem.Contains("appRole", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("has-a-dash-in-it-but-long-enough-to-pass")]
    [InlineData("has spaces in it but is otherwise long enough")]
    [InlineData("")]
    public void Request_NonGeneratedPassword_IsReported(string password)
    {
        var request = ValidRequest() with { AppRolePassword = password };
        Assert.Contains(request.Validate(), problem => problem.Contains("appRolePassword", StringComparison.Ordinal));
    }

    [Fact]
    public void Request_RoundTripsThroughItsFileForm()
    {
        var restored = DatabaseProvisionRequest.FromJson(JsonSerializer.Serialize(ValidRequest()));

        Assert.NotNull(restored);
        Assert.Empty(restored!.Validate());
        Assert.Equal("itadmin", restored.TargetDatabase);
        Assert.Equal("itadmin_app", restored.AppRole);
    }

    [Fact]
    public void Result_CarriesNoCredential()
    {
        var json = new DatabaseProvisionResult
        {
            TargetDatabase = "itadmin",
            AppRole = "itadmin_app",
            RoleCreated = true,
            DatabaseCreated = true,
            Satisfied = true,
        }.ToJson();

        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", json, StringComparison.Ordinal);
        Assert.Contains("\"satisfied\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void IsSafeGeneratedPassword_AcceptsOnlyLongAlphanumerics()
    {
        Assert.True(DatabaseProvisionRunner.IsSafeGeneratedPassword("Abc123Def456Ghi789Jkl012"));
        Assert.False(DatabaseProvisionRunner.IsSafeGeneratedPassword("tooshort"));
        Assert.False(DatabaseProvisionRunner.IsSafeGeneratedPassword("has-symbols-!@#-in-it-here"));
    }
}
