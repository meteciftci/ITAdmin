using System.Reflection;
using SasPortal.Api.Authorization;
using SasPortal.Api.Controllers;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Persistence.Services;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdDeletedObjectDirectoryTests
{
    [Fact]
    public void DeletedObjectsViewPermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.DeletedObjects.View", AdManagementPermissions.DeletedObjectsView);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsDeletedObjectsView()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.DeletedObjectsView, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void SeedDeletedObjectsViewMigration_ContainsPermissionAndAdministratorGrant()
    {
        var migrationPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "SasPortal.Persistence",
            "Migrations",
            "20260615120000_SeedAdManagementDeletedObjectsViewPermission.cs"));
        var migrationSource = File.ReadAllText(migrationPath);

        Assert.Contains(AdManagementPermissions.DeletedObjectsView, migrationSource, StringComparison.Ordinal);
        Assert.Contains("portal_permissions", migrationSource, StringComparison.Ordinal);
        Assert.Contains("portal_role_permissions", migrationSource, StringComparison.Ordinal);
        Assert.Contains("WHERE NOT EXISTS", migrationSource, StringComparison.Ordinal);
        Assert.Contains("Administrator", migrationSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ListDeletedObjectsEndpoint_RequiresDeletedObjectsViewPermission()
    {
        var method = typeof(AdManagementController).GetMethod(nameof(AdManagementController.ListDeletedObjects));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.DeletedObjectsView,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void GetDeletedObjectByIdEndpoint_RequiresDeletedObjectsViewPermission()
    {
        var method = typeof(AdManagementController).GetMethod(nameof(AdManagementController.GetDeletedObjectById));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.DeletedObjectsView,
            permissionAttribute?.Policy);
    }

    [Theory]
    [InlineData("all", AdDeletedObjectTypeFilter.All)]
    [InlineData("user", AdDeletedObjectTypeFilter.User)]
    [InlineData("group", AdDeletedObjectTypeFilter.Group)]
    [InlineData("computer", AdDeletedObjectTypeFilter.Computer)]
    public void ParseDeletedObjectTypeFilter_SupportsKnownValues(string rawType, AdDeletedObjectTypeFilter expected)
    {
        var method = typeof(AdManagementController).GetMethod(
            "ParseDeletedObjectTypeFilter",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var parsed = method.Invoke(null, [rawType]);
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void BuildDeletedObjectSearchFilter_IncludesIsDeletedAndShowDeletedTypeFilters()
    {
        var allFilter = AdLdapDeletedObjectFilterHelper.BuildDeletedObjectSearchFilter(
            "john",
            AdDeletedObjectTypeFilter.All);
        Assert.Contains("(isDeleted=TRUE)", allFilter, StringComparison.Ordinal);
        Assert.Contains("(objectClass=group)", allFilter, StringComparison.Ordinal);
        Assert.Contains("(objectClass=computer)", allFilter, StringComparison.Ordinal);
        Assert.Contains("(!(objectClass=computer))", allFilter, StringComparison.Ordinal);

        var userFilter = AdLdapDeletedObjectFilterHelper.BuildDeletedObjectSearchFilter(
            null,
            AdDeletedObjectTypeFilter.User);
        Assert.Contains("(objectClass=user)", userFilter, StringComparison.Ordinal);
        Assert.Contains("(!(objectClass=computer))", userFilter, StringComparison.Ordinal);

        var groupFilter = AdLdapDeletedObjectFilterHelper.BuildDeletedObjectSearchFilter(
            null,
            AdDeletedObjectTypeFilter.Group);
        Assert.Contains("(objectClass=group)", groupFilter, StringComparison.Ordinal);

        var computerFilter = AdLdapDeletedObjectFilterHelper.BuildDeletedObjectSearchFilter(
            null,
            AdDeletedObjectTypeFilter.Computer);
        Assert.Contains("(objectClass=computer)", computerFilter, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDeletedObjectSearchFilter_EscapesSearchText()
    {
        var filter = AdLdapDeletedObjectFilterHelper.BuildDeletedObjectSearchFilter(
            "(user*)",
            AdDeletedObjectTypeFilter.All);

        Assert.Contains("\\28user\\2a\\29", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDeletedObjectGuidFilter_UsesObjectGuidAndIsDeleted()
    {
        var objectGuid = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var filter = AdLdapDeletedObjectFilterHelper.BuildDeletedObjectGuidFilter(objectGuid);

        Assert.Contains("(isDeleted=TRUE)", filter, StringComparison.Ordinal);
        Assert.Contains("objectGUID=", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveDeletedObjectsSearchBase_UsesDeletedObjectsContainer()
    {
        var searchBase = AdLdapDeletedObjectFilterHelper.ResolveDeletedObjectsSearchBase("DC=corp,DC=local");
        Assert.Equal("CN=Deleted Objects,DC=corp,DC=local", searchBase);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void NormalizePageNumber_EnforcesMinimumOne(int pageNumber, int expected)
    {
        Assert.Equal(expected, AdLdapValueConverter.NormalizePageNumber(pageNumber));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    [InlineData(150, 100)]
    public void ClampPageSize_NormalizesDeletedObjectPagination(int pageSize, int expected)
    {
        Assert.Equal(expected, AdLdapValueConverter.ClampPageSize(pageSize, min: 1));
    }

    [Fact]
    public void IsQueryEnabled_AllowsTypeFilterWithoutSearch()
    {
        Assert.False(AdLdapDeletedObjectFilterHelper.IsQueryEnabled(null, AdDeletedObjectTypeFilter.All));
        Assert.True(AdLdapDeletedObjectFilterHelper.IsQueryEnabled(null, AdDeletedObjectTypeFilter.User));
        Assert.True(AdLdapDeletedObjectFilterHelper.IsQueryEnabled("ab", AdDeletedObjectTypeFilter.All));
    }

    [Fact]
    public void DeletedObjectsDirectorySource_UsesShowDeletedControlAndDeletedObjectsBase()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "SasPortal.Infrastructure",
            "Services",
            "AdUserDirectoryService.DeletedObjectsDirectory.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("ShowDeletedControlOid", source, StringComparison.Ordinal);
        Assert.Contains("ResolveDeletedObjectsSearchBase", source, StringComparison.Ordinal);
        Assert.Contains("AdLdapDeletedObjectFilterHelper", source, StringComparison.Ordinal);
        Assert.Contains("SearchScope.Subtree", source, StringComparison.Ordinal);
        Assert.Contains("BuildDeletedObjectGuidFilter", source, StringComparison.Ordinal);
        Assert.Contains("AdDirectoryFailureKind.NotFound", source, StringComparison.Ordinal);
        Assert.Contains("AdDirectoryFailureKind.ConnectionFailed", source, StringComparison.Ordinal);
    }
}
