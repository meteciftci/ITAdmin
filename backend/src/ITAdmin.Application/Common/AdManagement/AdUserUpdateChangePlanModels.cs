namespace ITAdmin.Application.Common.AdManagement;

public enum AdUserUpdateScalarChangeKind
{
    Replace,
    Delete,
}

public sealed record AdUserUpdateScalarChange(
    string AttributeName,
    string UpdateStep,
    AdUserUpdateScalarChangeKind ChangeKind,
    string[] NewValues,
    string[] OldValues);

public sealed record AdUserUpdateMappedChange(
    string LogicalField,
    string AttributeName,
    string UpdateStep,
    AdUserUpdateScalarChangeKind ChangeKind,
    string[] NewValues,
    string[] OldValues);

public sealed record AdUserUpdateRenameChange(
    string CurrentCommonName,
    string RequestedCommonName,
    string ParentDistinguishedName,
    string CurrentDistinguishedName);

public sealed class AdUserUpdateChangePlan
{
    public required Guid UserObjectGuid { get; init; }
    public required string CurrentDistinguishedName { get; init; }
    public required string CurrentCommonName { get; init; }
    public required string RequestedCommonName { get; init; }
    public required bool RequiresRename { get; init; }
    public string? ParentDistinguishedName { get; init; }
    public required IReadOnlyList<AdUserUpdateScalarChange> ScalarChanges { get; init; }
    public required IReadOnlyList<AdUserUpdateMappedChange> MappedChanges { get; init; }
    public AdUserUpdateRenameChange? RenameChange { get; init; }

    public bool HasChanges =>
        ScalarChanges.Count > 0 || MappedChanges.Count > 0 || RequiresRename;

    public IEnumerable<AdUserUpdateScalarChange> GetOrderedScalarChanges()
    {
        static int Order(string attributeName) => attributeName switch
        {
            "sAMAccountName" => 0,
            "userPrincipalName" => 1,
            "givenName" => 2,
            "sn" => 3,
            "displayName" => 4,
            "mail" => 5,
            "department" => 6,
            _ => 99,
        };

        return ScalarChanges.OrderBy(static change => Order(change.AttributeName));
    }
}

public sealed class AdUserUpdateAppliedChange
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

public sealed class AdUserUpdateRollbackResult
{
    public required string Status { get; init; }
    public required IReadOnlyList<string> RolledBackChanges { get; init; }
    public required IReadOnlyList<AdUserUpdateRollbackError> Errors { get; init; }

    public static AdUserUpdateRollbackResult NotRequired() =>
        new()
        {
            Status = AdUserUpdateRollbackStatus.NotRequired,
            RolledBackChanges = [],
            Errors = [],
        };
}
