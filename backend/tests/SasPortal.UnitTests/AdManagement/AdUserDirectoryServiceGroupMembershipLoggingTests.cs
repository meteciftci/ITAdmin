using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Infrastructure.Services;
using SasPortal.Persistence.Services;
using SasPortal.UnitTests.TestInfrastructure;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdUserDirectoryServiceGroupMembershipLoggingTests
{
  private static readonly Guid UserObjectGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
  private static readonly Guid ActorUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

  [Fact]
  public async Task CompleteGroupOperationAsync_AuditEntityId_UsesUserObjectGuid_NotDistinguishedName()
  {
    var recordingAuditWriter = new RecordingAuditLogWriter();
    var service = CreateService(recordingAuditWriter, new NoOpAdOperationLogService());

    var longDn = "CN=" + new string('u', 500) + ",OU=Users,DC=example,DC=com";
    var userContext = CreateUserContext(UserObjectGuid.ToString("D"), longDn);
    var groupInfo = CreateGroupInfo("CN=VPN Users,DC=example,DC=com", "VPN Users");
    var request = CreateChangeRequest();

    await InvokeCompleteGroupOperationAsync(
      service,
      request,
      userContext,
      groupInfo);

    Assert.NotNull(recordingAuditWriter.LastRequest);
    Assert.Equal(UserObjectGuid.ToString("D"), recordingAuditWriter.LastRequest!.EntityId);
    Assert.DoesNotContain("CN=", recordingAuditWriter.LastRequest.EntityId, StringComparison.Ordinal);
  }

  [Fact]
  public async Task CompleteGroupOperationAsync_WhenAuditLogWriteThrows_StillReturnsSuccess()
  {
    var service = CreateService(
      new ThrowingAuditLogWriter(),
      new NoOpAdOperationLogService());

    var userContext = CreateUserContext(UserObjectGuid.ToString("D"), "CN=user,DC=example,DC=com");
    var groupInfo = CreateGroupInfo("CN=VPN Users,DC=example,DC=com", "VPN Users");
    var request = CreateChangeRequest();

    var result = await InvokeCompleteGroupOperationAsync(
      service,
      request,
      userContext,
      groupInfo);

    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task CompleteGroupOperationAsync_WhenAdOperationLogWriteThrows_StillReturnsSuccess()
  {
    var service = CreateService(
      new RecordingAuditLogWriter(),
      new ThrowingAdOperationLogService());

    var userContext = CreateUserContext(UserObjectGuid.ToString("D"), "CN=user,DC=example,DC=com");
    var groupInfo = CreateGroupInfo("CN=VPN Users,DC=example,DC=com", "VPN Users");
    var request = CreateChangeRequest();

    var result = await InvokeCompleteGroupOperationAsync(
      service,
      request,
      userContext,
      groupInfo);

    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task FailGroupOperationAsync_WhenLoggingThrows_StillReturnsOriginalFailure()
  {
    var service = CreateService(
      new ThrowingAuditLogWriter(),
      new ThrowingAdOperationLogService());

    var userContext = CreateUserContext(UserObjectGuid.ToString("D"), "CN=user,DC=example,DC=com");
    var groupInfo = CreateGroupInfo("CN=VPN Users,DC=example,DC=com", "VPN Users");
    var request = CreateChangeRequest();

    var result = await InvokeFailGroupOperationAsync(
      service,
      request,
      userContext,
      groupInfo,
      "Group not found");

    Assert.False(result.IsSuccess);
    Assert.Equal("Group not found", result.MessageKey);
    Assert.Equal(AdDirectoryFailureKind.NotFound, result.FailureKind);
  }

  [Fact]
  public async Task FailGroupOperationAsync_AuditEntityId_UsesUserObjectGuid_NotDistinguishedName()
  {
    var recordingAuditWriter = new RecordingAuditLogWriter();
    var service = CreateService(recordingAuditWriter, new NoOpAdOperationLogService());

    var longDn = "CN=" + new string('u', 500) + ",OU=Users,DC=example,DC=com";
    var userContext = CreateUserContext(UserObjectGuid.ToString("D"), longDn);
    var groupInfo = CreateGroupInfo("CN=VPN Users,DC=example,DC=com", "VPN Users");
    var request = CreateChangeRequest();

    await InvokeFailGroupOperationAsync(
      service,
      request,
      userContext,
      groupInfo,
      "Group not found");

    Assert.Equal(UserObjectGuid.ToString("D"), recordingAuditWriter.LastRequest!.EntityId);
  }

  [Fact]
  public async Task FailGroupOperationAsync_WithoutUserContext_AuditEntityId_UsesRequestUserId()
  {
    var recordingAuditWriter = new RecordingAuditLogWriter();
    var service = CreateService(recordingAuditWriter, new NoOpAdOperationLogService());
    var request = CreateChangeRequest();

    await InvokeFailGroupOperationAsync(
      service,
      request,
      userContext: null,
      groupInfo: null,
      "Connection failed");

    Assert.Equal(UserObjectGuid.ToString("D"), recordingAuditWriter.LastRequest!.EntityId);
  }

  private static AdUserDirectoryService CreateService(
    IAuditLogWriter auditLogWriter,
    IAdOperationLogService adOperationLogService) =>
    new(
      new StubAdManagementSettingsService(),
      new StubAdAttributeMappingService(),
      adOperationLogService,
      auditLogWriter,
      new StubAdManagementNotificationEnqueueService(),
      new StubAdDeletedObjectRestoreCommandRunner(),
      NullLogger<AdUserDirectoryService>.Instance);

  private static async Task<AdUserGroupOperationResult> InvokeCompleteGroupOperationAsync(
    AdUserDirectoryService service,
    object request,
    object userContext,
    object groupInfo)
  {
    var method = typeof(AdUserDirectoryService).GetMethod(
      "CompleteGroupOperationAsync",
      BindingFlags.Instance | BindingFlags.NonPublic)
      ?? throw new InvalidOperationException("CompleteGroupOperationAsync not found.");

    var connection = new AdManagementConnectionParameters(
      "example.com",
      null,
      "DC=example,DC=com",
      "DC=example,DC=com",
      null,
      null,
      null,
      null,
      [],
      false,
      389,
      "svc",
      "secret");

    var task = (Task<AdUserGroupOperationResult>)method.Invoke(
      service,
      [
        request,
        AdManagementOperationTypes.UserGroupAdd,
        "Add",
        "AD user added to group. User: test.user. Group: VPN Users.",
        "Grup üyeliği eklendi.",
        connection,
        userContext,
        userContext,
        groupInfo,
        CancellationToken.None,
      ])!;

    return await task;
  }

  private static async Task<AdUserGroupOperationResult> InvokeFailGroupOperationAsync(
    AdUserDirectoryService service,
    object request,
    object? userContext,
    object? groupInfo,
    string message)
  {
    var method = typeof(AdUserDirectoryService).GetMethod(
      "FailGroupOperationAsync",
      BindingFlags.Instance | BindingFlags.NonPublic)
      ?? throw new InvalidOperationException("FailGroupOperationAsync not found.");

    var task = (Task<AdUserGroupOperationResult>)method.Invoke(
      service,
      [
        request,
        AdManagementOperationTypes.UserGroupAdd,
        "Add",
        "AD user added to group failed.",
        message,
        BuildFailureDiagnostic(),
        userContext,
        groupInfo,
        "CN=VPN Users,DC=example,DC=com",
        AdDirectoryFailureKind.NotFound,
        CancellationToken.None,
        null,
        null,
      ])!;

    return await task;
  }

  private static string BuildFailureDiagnostic() =>
    AdOperationErrorDiagnosticBuilder.BuildGroupMembershipFailureJson(
      AdManagementOperationTypes.UserGroupAdd,
      "LoadGroup",
      UserObjectGuid,
      "CN=user,DC=example,DC=com",
      englishMessageOverride: "The AD group could not be found.",
      normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject);

  private static object CreateChangeRequest()
  {
    var type = typeof(AdUserDirectoryService).GetNestedType(
      "GroupMembershipChangeRequest",
      BindingFlags.NonPublic)
      ?? throw new InvalidOperationException("GroupMembershipChangeRequest not found.");

    return Activator.CreateInstance(
      type,
      UserObjectGuid,
      "CN=VPN Users,DC=example,DC=com",
      ActorUserId,
      "actor.user",
      "127.0.0.1",
      "test-agent")!;
  }

  private static object CreateUserContext(string userId, string distinguishedName)
  {
    var type = typeof(AdUserDirectoryService).GetNestedType(
      "AdUserGroupContext",
      BindingFlags.NonPublic)
      ?? throw new InvalidOperationException("AdUserGroupContext not found.");

    return Activator.CreateInstance(
      type,
      userId,
      distinguishedName,
      "test.user",
      "test.user@example.com",
      "Test User",
      new HashSet<string>(StringComparer.OrdinalIgnoreCase))!;
  }

  private static object CreateGroupInfo(string distinguishedName, string name)
  {
    var type = typeof(AdUserDirectoryService).GetNestedType(
      "AdGroupDirectoryInfo",
      BindingFlags.NonPublic)
      ?? throw new InvalidOperationException("AdGroupDirectoryInfo not found.");

    return Activator.CreateInstance(
      type,
      distinguishedName,
      name,
      name,
      "vpn-users",
      "VPN access")!;
  }

  private sealed class RecordingAuditLogWriter : IAuditLogWriter
  {
    public AuditLogWriteRequest? LastRequest { get; private set; }

    public Task WriteAsync(AuditLogWriteRequest request, CancellationToken cancellationToken = default)
    {
      LastRequest = request;
      return Task.CompletedTask;
    }
  }

  private sealed class ThrowingAuditLogWriter : IAuditLogWriter
  {
    public Task WriteAsync(AuditLogWriteRequest request, CancellationToken cancellationToken = default) =>
      throw new InvalidOperationException("Audit log write failed.");
  }

  private sealed class NoOpAdOperationLogService : IAdOperationLogService
  {
    public Task WriteAsync(AdOperationLogEntry entry, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;

    public Task<PagedResult<AdOperationLogListItem>> GetLogsAsync(
      AdOperationLogListQuery query,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new PagedResult<AdOperationLogListItem>([], 1, 20, 0, 0));

    public Task<AdOperationLogDetail?> GetLogByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
      Task.FromResult<AdOperationLogDetail?>(null);
  }

  private sealed class ThrowingAdOperationLogService : IAdOperationLogService
  {
    public Task WriteAsync(AdOperationLogEntry entry, CancellationToken cancellationToken = default) =>
      throw new InvalidOperationException("AD operation log write failed.");

    public Task<PagedResult<AdOperationLogListItem>> GetLogsAsync(
      AdOperationLogListQuery query,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new PagedResult<AdOperationLogListItem>([], 1, 20, 0, 0));

    public Task<AdOperationLogDetail?> GetLogByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
      Task.FromResult<AdOperationLogDetail?>(null);
  }

  private sealed class StubAdManagementSettingsService : IAdManagementSettingsService
  {
    public Task<AdManagementSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<UpdateAdManagementSettingsResult> UpdateSettingsAsync(
      UpdateAdManagementSettingsRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<AdManagementConnectionParameters?> GetConnectionParametersAsync(
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task RecordValidationResultAsync(
      AdManagementValidationResult result,
      AdManagementValidationRequest request,
      string? primaryDomainController,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class StubAdAttributeMappingService : IAdAttributeMappingService
  {
    public Task<IReadOnlyList<AdAttributeMappingItem>> GetMappingsAsync(
      CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<AdAttributeMappingItem>>([]);

    public Task<AdAttributeMappingResult> CreateAsync(
      CreateAdAttributeMappingRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<AdAttributeMappingResult> UpdateAsync(
      UpdateAdAttributeMappingRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<AdAttributeMappingResult> DeleteAsync(
      DeleteAdAttributeMappingRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class StubAdManagementNotificationEnqueueService : IAdManagementNotificationEnqueueService
  {
    public Task<AdManagementNotificationSummary> EnqueueUserCreatedAsync(
      AdUserCreatedNotificationEnqueueRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<AdManagementNotificationSummary> EnqueueAccountOperationAsync(
      AdManagementAccountOperationNotificationRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class StubAdDeletedObjectRestoreCommandRunner : IAdDeletedObjectRestoreCommandRunner
  {
    public Task<AdDeletedObjectRestoreCommandResult> ExecuteRestoreAsync(
      AdDeletedObjectRestoreCommandRequest request,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new AdDeletedObjectRestoreCommandResult(
        true,
        "ProcessIdentity",
        0,
        0,
        null,
        null));
  }
}
