using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Common.Models;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;

namespace SasPortal.UnitTests.Services;

public sealed class AdAttributeMappingServiceTests
{
    [Fact]
    public async Task GetMappingsAsync_WhenEmpty_ReturnsEmptyList()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.GetMappingsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_PersistsAndLogs()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var request = CreateRequest("mobilePhone", "Cep Telefonu", "telephoneNumber");
        var result = await service.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Mapping);
        Assert.Equal("mobilePhone", result.Mapping!.LogicalField);

        var stored = await dbContext.AdAttributeMappings.SingleAsync();
        Assert.Equal("mobilePhone", stored.LogicalField);

        var audit = Assert.Single(dbContext.AuditLogs.Where(x => x.EntityName == "AdAttributeMapping"));
        Assert.Equal("Create", audit.Action);

        var op = Assert.Single(dbContext.AdOperationLogs);
        Assert.Equal("AttributeMappingCreated", op.OperationType);
        Assert.Equal("Succeeded", op.Status);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateLogicalField_Rejected()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var first = await service.CreateAsync(CreateRequest("mobilePhone", "Cep Telefonu", "mobile"));
        Assert.True(first.IsSuccess);

        var duplicate = await service.CreateAsync(CreateRequest("mobilePhone", "Tel No 2", "telephoneNumber"));
        Assert.False(duplicate.IsSuccess);
        Assert.Contains("already exists", duplicate.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(dbContext.AdAttributeMappings);
    }

    [Theory]
    [InlineData("InvalidStartsUpper")]
    [InlineData("0digit")]
    [InlineData("with space")]
    [InlineData("a")]
    public async Task CreateAsync_WithInvalidLogicalField_Rejected(string logicalField)
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.CreateAsync(CreateRequest(logicalField, "DN", "telephoneNumber"));

        Assert.False(result.IsSuccess);
        Assert.Empty(dbContext.AdAttributeMappings);
    }

    [Theory]
    [InlineData("1startsWithDigit")]
    [InlineData("with space")]
    [InlineData("a$bad$char")]
    public async Task CreateAsync_WithInvalidAttributeName_Rejected(string attributeName)
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.CreateAsync(CreateRequest("mobilePhone", "Cep", attributeName));

        Assert.False(result.IsSuccess);
        Assert.Empty(dbContext.AdAttributeMappings);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotChangeLogicalField_AndLogs()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var created = await service.CreateAsync(CreateRequest("mobilePhone", "Old", "mobile"));
        Assert.True(created.IsSuccess);

        var id = created.Mapping!.Id;

        var update = new UpdateAdAttributeMappingRequest(
            Id: id,
            DisplayName: "Yeni Ad",
            AttributeName: "telephoneNumber",
            IsEnabled: false,
            IsEditable: false,
            IsSensitive: true,
            ValidationType: "Phone",
            MaskingStrategy: "Phone",
            SortOrder: 5,
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "127.0.0.1",
            ActorUserAgent: "xunit");

        var result = await service.UpdateAsync(update);
        Assert.True(result.IsSuccess);

        var entity = await dbContext.AdAttributeMappings.SingleAsync(x => x.Id == id);
        Assert.Equal("mobilePhone", entity.LogicalField);
        Assert.Equal("Yeni Ad", entity.DisplayName);
        Assert.Equal("telephoneNumber", entity.AttributeName);
        Assert.False(entity.IsEnabled);
        Assert.True(entity.IsSensitive);
        Assert.Equal("Phone", entity.ValidationType);
        Assert.Equal("Phone", entity.MaskingStrategy);
        Assert.Equal(5, entity.SortOrder);

        var auditCount = dbContext.AuditLogs.Count(x => x.EntityName == "AdAttributeMapping");
        Assert.Equal(2, auditCount);

        var opCount = dbContext.AdOperationLogs.Count();
        Assert.Equal(2, opCount);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntityAndLogs()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var created = await service.CreateAsync(CreateRequest("mobilePhone", "Cep", "mobile"));
        Assert.True(created.IsSuccess);

        var id = created.Mapping!.Id;
        var deleteRequest = new DeleteAdAttributeMappingRequest(
            Id: id,
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "127.0.0.1",
            ActorUserAgent: "xunit");

        var result = await service.DeleteAsync(deleteRequest);
        Assert.True(result.IsSuccess);

        Assert.Empty(dbContext.AdAttributeMappings);

        var auditCount = dbContext.AuditLogs.Count(x => x.EntityName == "AdAttributeMapping");
        Assert.Equal(2, auditCount);

        var ops = dbContext.AdOperationLogs.ToList();
        Assert.Equal(2, ops.Count);
        Assert.Contains(ops, o => o.OperationType == "AttributeMappingDeleted");
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ReturnsFailure()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.DeleteAsync(new DeleteAdAttributeMappingRequest(
            Id: Guid.NewGuid(),
            ActorUserId: null,
            ActorUserName: null,
            ActorIpAddress: null,
            ActorUserAgent: null));

        Assert.False(result.IsSuccess);
        Assert.Empty(dbContext.AdOperationLogs);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private static AdAttributeMappingService CreateService(AppDbContext context) =>
        new(context, new AdOperationLogService(context));

    private static CreateAdAttributeMappingRequest CreateRequest(
        string logicalField,
        string displayName,
        string attributeName) =>
        new(
            LogicalField: logicalField,
            DisplayName: displayName,
            AttributeName: attributeName,
            IsEnabled: true,
            IsEditable: true,
            IsSensitive: false,
            ValidationType: "None",
            MaskingStrategy: "None",
            SortOrder: 0,
            ActorUserId: Guid.NewGuid(),
            ActorUserName: "tester",
            ActorIpAddress: "127.0.0.1",
            ActorUserAgent: "xunit");
}
