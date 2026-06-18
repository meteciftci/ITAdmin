using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdUserUpdateChangePlanBuilderTests
{
    [Fact]
    public void Build_WhenNoChanges_ReturnsEmptyPlan()
    {
        var userId = Guid.NewGuid();
        var request = CreateRequest(userId);
        var currentScalars = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["givenName"] = request.GivenName,
            ["sn"] = request.Surname,
            ["displayName"] = request.DisplayName,
            ["sAMAccountName"] = request.SamAccountName,
            ["userPrincipalName"] = request.UserPrincipalName,
        };

        var plan = AdUserUpdateChangePlanBuilder.Build(
            request,
            currentScalars,
            new Dictionary<string, IReadOnlyList<string>>(),
            "CN=Ali Veli,OU=Users,DC=corp,DC=local",
            []);

        Assert.False(plan.HasChanges);
        Assert.Empty(plan.ScalarChanges);
        Assert.Empty(plan.MappedChanges);
        Assert.False(plan.RequiresRename);
    }

    [Fact]
    public void Build_OrdersUniqueScalarsBeforeOtherScalars()
    {
        var userId = Guid.NewGuid();
        var request = CreateRequest(userId) with
        {
            GivenName = "NewGiven",
            SamAccountName = "new.sam",
            UserPrincipalName = "new.sam@corp.local",
        };

        var currentScalars = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["givenName"] = "OldGiven",
            ["sn"] = request.Surname,
            ["displayName"] = request.DisplayName,
            ["sAMAccountName"] = "old.sam",
            ["userPrincipalName"] = "old.sam@corp.local",
        };

        var plan = AdUserUpdateChangePlanBuilder.Build(
            request,
            currentScalars,
            new Dictionary<string, IReadOnlyList<string>>(),
            "CN=Ali Veli,OU=Users,DC=corp,DC=local",
            []);

        var orderedAttributes = plan.GetOrderedScalarChanges()
            .Select(static change => change.AttributeName)
            .ToList();

        Assert.Equal(
            ["sAMAccountName", "userPrincipalName", "givenName"],
            orderedAttributes.Take(3));
    }

    [Fact]
    public void Build_WhenDisplayNameChanges_SchedulesRenameLast()
    {
        var userId = Guid.NewGuid();
        var request = CreateRequest(userId) with { DisplayName = "Yeni Ad Soyad" };

        var plan = AdUserUpdateChangePlanBuilder.Build(
            request,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["givenName"] = request.GivenName,
                ["sn"] = request.Surname,
                ["displayName"] = "Eski Ad Soyad",
                ["sAMAccountName"] = request.SamAccountName,
                ["userPrincipalName"] = request.UserPrincipalName,
            },
            new Dictionary<string, IReadOnlyList<string>>(),
            "CN=Eski Ad Soyad,OU=Users,DC=corp,DC=local",
            []);

        Assert.True(plan.RequiresRename);
        Assert.NotNull(plan.RenameChange);
        Assert.Equal("Yeni Ad Soyad", plan.RenameChange!.RequestedCommonName);
    }

    [Fact]
    public void Build_MappedDelete_StoresOldValuesForRollback()
    {
        var userId = Guid.NewGuid();
        var mappings = new[]
        {
            new AdAttributeMappingItem(
                Guid.NewGuid(),
                "employeeId",
                "Employee ID",
                "employeeID",
                IsEnabled: true,
                IsEditable: true,
                IsSensitive: false,
                IsSearchable: false,
                ValidationType: "None",
                MaskingStrategy: "None",
                SortOrder: 1),
        };

        var request = CreateRequest(userId) with
        {
            MappedAttributes =
            [
                new UpdateAdUserMappedAttributeRequest("employeeId", null),
            ],
        };

        var plan = AdUserUpdateChangePlanBuilder.Build(
            request,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["givenName"] = request.GivenName,
                ["sn"] = request.Surname,
                ["displayName"] = request.DisplayName,
                ["sAMAccountName"] = request.SamAccountName,
                ["userPrincipalName"] = request.UserPrincipalName,
            },
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["employeeID"] = ["E123"],
            },
            "CN=Ali Veli,OU=Users,DC=corp,DC=local",
            mappings);

        var mappedChange = Assert.Single(plan.MappedChanges);
        Assert.Equal(AdUserUpdateScalarChangeKind.Delete, mappedChange.ChangeKind);
        Assert.Equal(["E123"], mappedChange.OldValues);
    }

    private static UpdateAdUserRequest CreateRequest(Guid userId) =>
        new(
            userId,
            "Ali",
            "Veli",
            "Ali Veli",
            "ali.veli",
            "ali.veli@corp.local",
            null,
            null,
            [],
            null,
            null,
            null,
            null);
}
