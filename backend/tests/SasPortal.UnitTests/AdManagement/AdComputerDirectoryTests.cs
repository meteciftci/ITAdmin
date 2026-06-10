using System.Reflection;
using SasPortal.Api.Authorization;
using SasPortal.Api.Controllers;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Persistence.Services;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdComputerDirectoryTests
{
    [Fact]
    public void ComputersViewPermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.Computers.View", AdManagementPermissions.ComputersView);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsComputersView()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.ComputersView, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ListComputersEndpoint_RequiresComputersViewPermission()
    {
        var method = typeof(AdManagementController).GetMethod(nameof(AdManagementController.ListComputers));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.ComputersView,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void GetComputerByIdEndpoint_RequiresComputersViewPermission()
    {
        var method = typeof(AdManagementController).GetMethod(nameof(AdManagementController.GetComputerById));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.ComputersView,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void SearchComputerOrganizationalUnitsEndpoint_RequiresComputersViewPermission()
    {
        var method = typeof(AdManagementController)
            .GetMethod(nameof(AdManagementController.SearchComputerOrganizationalUnits));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.ComputersView,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void ResolveRequiredComputersSearchBase_ReturnsNullWhenEmpty()
    {
        var connection = new AdManagementConnectionParameters(
            "corp.local",
            null,
            "DC=corp,DC=local",
            "DC=corp,DC=local",
            null,
            null,
            null,
            " ",
            Array.Empty<string>(),
            false,
            389,
            null,
            null);

        Assert.Null(AdLdapComputerSearchBases.ResolveRequiredComputersSearchBase(connection));
    }

    [Fact]
    public void ResolveRequiredComputersSearchBase_ReturnsTrimmedValue()
    {
        var connection = new AdManagementConnectionParameters(
            "corp.local",
            null,
            "DC=corp,DC=local",
            "DC=corp,DC=local",
            null,
            null,
            null,
            "  OU=Computers,DC=corp,DC=local  ",
            Array.Empty<string>(),
            false,
            389,
            null,
            null);

        Assert.Equal("OU=Computers,DC=corp,DC=local", AdLdapComputerSearchBases.ResolveRequiredComputersSearchBase(connection));
    }

    [Theory]
    [InlineData("cn")]
    [InlineData("name")]
    [InlineData("sAMAccountName")]
    [InlineData("dNSHostName")]
    [InlineData("operatingSystem")]
    public void BuildComputerDirectorySearchFilter_IncludesSearchField(string fieldName)
    {
        var filter = AdLdapComputerFilterHelper.BuildComputerDirectorySearchFilter(
            "srv",
            AdUserStatusFilter.All);

        Assert.Contains($"{fieldName}=*srv*", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildComputerDirectorySearchFilter_EscapesSpecialCharacters()
    {
        var filter = AdLdapComputerFilterHelper.BuildComputerDirectorySearchFilter(
            "(pc*)",
            AdUserStatusFilter.All);

        Assert.Contains("\\28pc\\2a\\29", filter, StringComparison.Ordinal);
        Assert.DoesNotContain("(pc*)", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildComputerDirectorySearchFilter_IncludesComputerObjectClasses()
    {
        var filter = AdLdapComputerFilterHelper.BuildComputerDirectorySearchFilter(
            "pc",
            AdUserStatusFilter.All);

        Assert.Contains("(objectCategory=computer)", filter, StringComparison.Ordinal);
        Assert.Contains("(objectClass=computer)", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildComputerDirectorySearchFilter_ActiveStatus_ExcludesDisabledAccounts()
    {
        var filter = AdLdapComputerFilterHelper.BuildComputerDirectorySearchFilter(
            "pc",
            AdUserStatusFilter.Active);

        Assert.Contains("(!(userAccountControl:1.2.840.113556.1.4.803:=2))", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildComputerDirectorySearchFilter_DisabledStatus_IncludesDisabledBit()
    {
        var filter = AdLdapComputerFilterHelper.BuildComputerDirectorySearchFilter(
            "pc",
            AdUserStatusFilter.Disabled);

        Assert.Contains("(userAccountControl:1.2.840.113556.1.4.803:=2)", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildComputerObjectGuidFilter_IncludesComputerObjectClasses()
    {
        var objectGuid = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var filter = AdLdapComputerFilterHelper.BuildComputerObjectGuidFilter(objectGuid);

        Assert.Contains("(objectCategory=computer)", filter, StringComparison.Ordinal);
        Assert.Contains("(objectClass=computer)", filter, StringComparison.Ordinal);
        Assert.Contains("objectGUID=", filter, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0x00000200, true)]
    [InlineData(0x00000202, false)]
    [InlineData(null, true)]
    public void IsAccountEnabled_InterpretsAccountDisableBit(int? userAccountControl, bool expectedEnabled)
    {
        Assert.Equal(expectedEnabled, AdLdapValueConverter.IsAccountEnabled(userAccountControl));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void FromAdFileTime_ReturnsNullForMissingOrZeroTimestamp(long? fileTime)
    {
        Assert.Null(AdLdapValueConverter.FromAdFileTime(fileTime));
    }

    [Fact]
    public void FromAdFileTime_ReturnsUtcDateTimeOffsetForValidTimestamp()
    {
        var fileTime = DateTimeOffset.Parse("2024-06-01T10:15:00Z").ToFileTime();
        var parsed = AdLdapValueConverter.FromAdFileTime(fileTime);

        Assert.NotNull(parsed);
        Assert.Equal(DateTimeOffset.Parse("2024-06-01T10:15:00Z").UtcDateTime, parsed!.Value.UtcDateTime);
    }

    [Fact]
    public void BuildComputerDirectorySearchFilter_PreservesTrailingDollarInSamAccountNameSearch()
    {
        var filter = AdLdapComputerFilterHelper.BuildComputerDirectorySearchFilter(
            "PC01$",
            AdUserStatusFilter.All);

        Assert.Contains("sAMAccountName=*PC01$*", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void MemberOfDisplayLimit_IsUsedForComputerMemberOfPreview()
    {
        Assert.True(AdGroupDirectoryLimits.MemberOfDisplayLimit > 0);
    }

    [Fact]
    public void IsSearchTermValid_RejectsShortSearchTerms()
    {
        Assert.False(AdLdapAttributeCatalog.IsSearchTermValid("a"));
        Assert.False(AdLdapAttributeCatalog.IsSearchTermValid(null));
        Assert.True(AdLdapAttributeCatalog.IsSearchTermValid("pc"));
    }
}
