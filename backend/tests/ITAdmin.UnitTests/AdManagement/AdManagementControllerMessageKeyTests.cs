using ITAdmin.Application.Common.Constants;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdManagementControllerMessageKeyTests
{
    private static readonly string ControllerSource = ReadControllerSource();

    private static string ReadControllerSource()
    {
        var controllerDirectory = Path.Combine(
            FindRepositoryRoot(),
            "backend/src/ITAdmin.Api/Controllers/AdManagement");

        // AD management endpoints are split across domain controllers; scan them all.
        return string.Concat(
            Directory.EnumerateFiles(controllerDirectory, "*.cs")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    [Fact]
    public void Controller_DoesNotReferenceLegacyMessages()
    {
        var source = ControllerSource;
        Assert.DoesNotContain("AdManagementApiMessages.Legacy", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Controller_DoesNotReturnUserFacingMessageField()
    {
        var source = ControllerSource;
        Assert.DoesNotContain("message =", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateComputer_InvalidIdBranch_SetsInvalidComputerIdMessageKey()
    {
        var source = ControllerSource;

        Assert.Contains(
            "AdManagementApiMessageKeys.Computers.InvalidComputerId,",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "public async Task<ActionResult<AdComputerAccountOperationResponse>> UpdateComputer(",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MoveComputerOu_InvalidIdBranch_SetsInvalidComputerIdMessageKey()
    {
        var source = ControllerSource;
        var moveComputerOuStart = source.IndexOf(
            "public async Task<ActionResult<AdComputerAccountOperationResponse>> MoveComputerOu(",
            StringComparison.Ordinal);
        Assert.True(moveComputerOuStart >= 0);

        var moveComputerOuMethod = source[moveComputerOuStart..];
        Assert.Contains(
            "AdManagementApiMessageKeys.Computers.InvalidComputerId",
            moveComputerOuMethod,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MoveComputerOu_MissingTargetOuBranch_UsesComputersTargetOuRequiredMessageKey()
    {
        var source = ControllerSource;
        var moveComputerOuStart = source.IndexOf(
            "public async Task<ActionResult<AdComputerAccountOperationResponse>> MoveComputerOu(",
            StringComparison.Ordinal);
        var moveComputerOuEnd = source.IndexOf(
            "[HttpGet(\"computers/{id}/groups\")]",
            moveComputerOuStart,
            StringComparison.Ordinal);
        Assert.True(moveComputerOuStart >= 0);
        Assert.True(moveComputerOuEnd > moveComputerOuStart);

        var moveComputerOuMethod = source[moveComputerOuStart..moveComputerOuEnd];
        Assert.Contains(
            "AdManagementApiMessageKeys.Computers.TargetOuRequired",
            moveComputerOuMethod,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AdManagementApiMessageKeys.Users.TargetOuRequired",
            moveComputerOuMethod,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MoveGroupOu_MissingTargetOuBranch_UsesGroupsTargetOuRequiredMessageKey()
    {
        var source = ControllerSource;
        var moveGroupOuStart = source.IndexOf(
            "public async Task<ActionResult<MoveAdGroupOuResponse>> MoveGroupOu(",
            StringComparison.Ordinal);
        var moveGroupOuEnd = source.IndexOf(
            "[HttpGet(\"groups/{id}/members\")]",
            moveGroupOuStart,
            StringComparison.Ordinal);
        Assert.True(moveGroupOuStart >= 0);
        Assert.True(moveGroupOuEnd > moveGroupOuStart);

        var moveGroupOuMethod = source[moveGroupOuStart..moveGroupOuEnd];
        Assert.Contains(
            "AdManagementApiMessageKeys.Groups.TargetOuRequired",
            moveGroupOuMethod,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AdManagementApiMessageKeys.Users.TargetOuRequired",
            moveGroupOuMethod,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AdManagementApiMessageKeys.Computers.InvalidComputerId)]
    [InlineData(AdManagementApiMessageKeys.Computers.TargetOuRequired)]
    [InlineData(AdManagementApiMessageKeys.Groups.GroupDnRequired)]
    [InlineData(AdManagementApiMessageKeys.Users.InvalidUserId)]
    [InlineData(AdManagementApiMessageKeys.Groups.InvalidGroupId)]
    [InlineData(AdManagementApiMessageKeys.DeletedObjects.NotFound)]
    public void ControllerValidationKeys_AreDefinedInMessageKeys(string messageKey)
    {
        Assert.StartsWith("apiMessages.", messageKey, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(messageKey));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "backend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
