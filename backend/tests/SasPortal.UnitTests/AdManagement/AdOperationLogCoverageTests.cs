using System.Reflection;
using SasPortal.Application.Common.Constants;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdOperationLogCoverageTests
{
  [Fact]
  public void AdManagementOperationTypes_PublicConstants_AreNonEmpty()
  {
    var operationTypes = typeof(AdManagementOperationTypes)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToList();

    Assert.NotEmpty(operationTypes);
    Assert.All(operationTypes, value => Assert.False(string.IsNullOrWhiteSpace(value)));
    Assert.Equal(operationTypes.Count, operationTypes.Distinct(StringComparer.Ordinal).Count());
  }

  [Fact]
  public void AdManagementOperationStatuses_UsesStandardSucceededFailedSkippedValues()
  {
    Assert.Equal("Succeeded", AdManagementOperationStatuses.Succeeded);
    Assert.Equal("Failed", AdManagementOperationStatuses.Failed);
    Assert.Equal("Skipped", AdManagementOperationStatuses.Skipped);
  }

  [Fact]
  public void CoverageMatrix_IncludesAllBackendOperationTypeConstants()
  {
    var constants = typeof(AdManagementOperationTypes)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    var matrixTypes = AdOperationLogCoverageMatrix.Rows
        .Select(row => row.OperationType)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    Assert.Equal(constants, matrixTypes);
    Assert.Equal(29, matrixTypes.Length);
  }

  [Theory]
  [InlineData("DeletedObjectRestore")]
  [InlineData("ComputerDelete")]
  [InlineData("ComputerMoveOu")]
  [InlineData("ComputerGroupAdd")]
  [InlineData("ComputerGroupRemove")]
  [InlineData("GroupMoveOu")]
  [InlineData("UserOuMove")]
  public void RecentlyAddedOperationTypes_ArePresentInCoverageMatrix(string operationType)
  {
    var row = AdOperationLogCoverageMatrix.Rows.SingleOrDefault(entry =>
        string.Equals(entry.OperationType, operationType, StringComparison.Ordinal));

    Assert.NotNull(row);
    Assert.False(string.IsNullOrWhiteSpace(row.LogSourceRelativePath));
  }

  [Fact]
  public void DeletedObjectRestore_LogProduction_UsesStandardOperationTypeAndStatuses()
  {
    var source = ReadRepositoryFile(AdOperationLogCoverageMatrix.Rows
        .Single(row => row.OperationType == AdManagementOperationTypes.DeletedObjectRestore)
        .LogSourceRelativePath);

    Assert.Contains($"AdManagementOperationTypes.{AdManagementOperationTypes.DeletedObjectRestore}", source, StringComparison.Ordinal);
    Assert.Contains("AdManagementOperationStatuses.Succeeded", source, StringComparison.Ordinal);
    Assert.Contains("AdManagementOperationStatuses.Failed", source, StringComparison.Ordinal);
    Assert.Contains("WriteDeletedObjectRestoreOperationLogAsync", source, StringComparison.Ordinal);
    Assert.Contains("BuildDeletedObjectRestoreBeforeSnapshot", source, StringComparison.Ordinal);
    Assert.Contains("BuildDeletedObjectRestoreAfterSnapshot", source, StringComparison.Ordinal);
  }

  [Fact]
  public void CoverageMatrix_LogSources_WriteOperationLogsWithExpectedStatuses()
  {
    foreach (var row in AdOperationLogCoverageMatrix.Rows)
    {
      var source = ReadRepositoryFile(row.LogSourceRelativePath);

      Assert.Contains($"AdManagementOperationTypes.{row.OperationType}", source, StringComparison.Ordinal);
      Assert.Contains("adOperationLogService.WriteAsync", source, StringComparison.Ordinal);

      if (row.ExpectSuccessLog)
      {
        Assert.Contains("AdManagementOperationStatuses.Succeeded", source, StringComparison.Ordinal);
      }

      if (row.ExpectFailureLog)
      {
        Assert.Contains("AdManagementOperationStatuses.Failed", source, StringComparison.Ordinal);
      }
    }
  }

  [Fact]
  public void CreateUserSnapshotBuilder_DoesNotReferenceInitialPasswordField()
  {
    var snapshotBuilderSource = ReadRepositoryFile(
        "backend/src/SasPortal.Application/Common/AdManagement/AdOperationLogSnapshotBuilder.cs");

    Assert.DoesNotContain("initialPassword", snapshotBuilderSource, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("hasServiceAccountPassword", snapshotBuilderSource, StringComparison.Ordinal);
  }

  [Fact]
  public void CoverageMatrix_InventorySummary_IsStableForPhase20B1()
  {
    var rows = AdOperationLogCoverageMatrix.Rows;

    Assert.Equal(29, rows.Count);
    Assert.Equal(29, rows.Count(row => row.ExpectSuccessLog));
    Assert.Equal(25, rows.Count(row => row.ExpectFailureLog));
    Assert.Contains(rows, row => row.OperationType == AdManagementOperationTypes.ComputerUpdate);
    Assert.Contains(rows, row => row.OperationType == AdManagementOperationTypes.DeletedObjectRestore);
  }

  private static string ReadRepositoryFile(string relativePath)
  {
    var fullPath = Path.Combine(FindRepositoryRoot(), relativePath);
    return File.ReadAllText(fullPath);
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
