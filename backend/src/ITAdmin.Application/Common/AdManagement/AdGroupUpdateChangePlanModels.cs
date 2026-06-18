namespace ITAdmin.Application.Common.AdManagement;

public sealed record AdGroupUpdateScalarChange(
    string AttributeName,
    string UpdateStep,
    AdUserUpdateScalarChangeKind ChangeKind,
    string[] NewValues,
    string[] OldValues);

public sealed record AdGroupUpdateRenameChange(
    string CurrentCommonName,
    string RequestedCommonName,
    string ParentDistinguishedName,
    string CurrentDistinguishedName);

public sealed class AdGroupUpdateChangePlan
{
    public required Guid GroupObjectGuid { get; init; }
    public required string CurrentDistinguishedName { get; init; }
    public required string CurrentCommonName { get; init; }
    public required string RequestedCommonName { get; init; }
    public required bool RequiresRename { get; init; }
    public string? ParentDistinguishedName { get; init; }
    public required IReadOnlyList<AdGroupUpdateScalarChange> ScalarChanges { get; init; }
    public AdGroupUpdateRenameChange? RenameChange { get; init; }

    public bool HasChanges => ScalarChanges.Count > 0 || RequiresRename;

    public IEnumerable<AdGroupUpdateScalarChange> GetOrderedScalarChanges()
    {
        static int Order(string attributeName) => attributeName switch
        {
            "sAMAccountName" => 0,
            "displayName" => 1,
            "description" => 2,
            _ => 99,
        };

        return ScalarChanges.OrderBy(static change => Order(change.AttributeName));
    }
}

public sealed class AdGroupUpdateAppliedChange
{
    public required string LogAttributeName { get; init; }
    public required string UpdateStep { get; init; }
    public required AdUserUpdateScalarChangeKind ChangeKind { get; init; }
    public required bool IsRename { get; init; }
    public string? AttributeName { get; init; }
    public string[]? OldValues { get; init; }
    public string[]? NewValues { get; init; }
    public string? PreviousDistinguishedName { get; init; }
    public string? PreviousCommonName { get; init; }
    public string? ParentDistinguishedName { get; init; }
    public string? NewCommonName { get; init; }
}

public sealed class AdGroupUpdateRollbackResult
{
    public required string Status { get; init; }
    public required IReadOnlyList<string> RolledBackChanges { get; init; }
    public required IReadOnlyList<AdUserUpdateRollbackError> Errors { get; init; }

    public static AdGroupUpdateRollbackResult NotRequired() =>
        new()
        {
            Status = AdUserUpdateRollbackStatus.NotRequired,
            RolledBackChanges = [],
            Errors = [],
        };
}

public sealed record AdGroupUpdateFailureContext(
    string Step,
    string? AttributeName = null,
    int? LdapResultCode = null,
    int? LdapExceptionErrorCode = null,
    string? LdapDiagnosticMessage = null,
    Guid? TargetObjectGuid = null,
    string? TargetDistinguishedName = null,
    string? DiagnosticCode = null,
    string? NormalizedReasonOverride = null,
    string? EnglishMessageOverride = null,
    bool? PartialUpdate = null,
    string? RollbackStatus = null,
    IReadOnlyList<string>? AppliedChanges = null,
    IReadOnlyList<string>? RolledBackChanges = null,
    IReadOnlyList<AdUserUpdateRollbackError>? RollbackErrors = null,
    bool? AfterReloadFailed = null);
