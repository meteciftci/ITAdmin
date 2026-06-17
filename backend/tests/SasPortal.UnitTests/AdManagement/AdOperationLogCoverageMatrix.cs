namespace SasPortal.UnitTests.AdManagement;

public sealed record AdOperationLogCoverageRow(
    string OperationType,
    string LogSourceRelativePath,
    bool ExpectSuccessLog,
    bool ExpectFailureLog,
    string Notes);

public static class AdOperationLogCoverageMatrix
{
    public static IReadOnlyList<AdOperationLogCoverageRow> Rows { get; } =
    [
        new("SettingsUpdated", "backend/src/SasPortal.Persistence/Services/AdManagementSettingsService.cs", true, false, "Success-only operation log; failed saves do not write operation log."),
        new("SettingsValidated", "backend/src/SasPortal.Persistence/Services/AdManagementSettingsService.cs", true, true, "Validation summary and failure diagnostic."),
        new("AttributeMappingCreated", "backend/src/SasPortal.Persistence/Services/AdAttributeMappingService.cs", true, false, "Success-only operation log."),
        new("AttributeMappingUpdated", "backend/src/SasPortal.Persistence/Services/AdAttributeMappingService.cs", true, false, "Success-only operation log."),
        new("AttributeMappingDeleted", "backend/src/SasPortal.Persistence/Services/AdAttributeMappingService.cs", true, false, "Success-only operation log."),
        new("CreateUser", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.Create.cs", true, true, "Password excluded from request/after snapshots."),
        new("UserUpdate", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.Update.cs", true, true, "Before/after user snapshots."),
        new("UserEnable", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.AccountOperations.cs", true, true, "Account status snapshots."),
        new("UserDisable", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.AccountOperations.cs", true, true, "Account status snapshots."),
        new("UserUnlock", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.AccountOperations.cs", true, true, "Lock status snapshots."),
        new("UserGroupAdd", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.GroupMembership.cs", true, true, "Membership before/after snapshots."),
        new("UserGroupRemove", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.GroupMembership.cs", true, true, "Membership before/after snapshots."),
        new("UserOuMove", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.MoveOu.cs", true, true, "OU move before/after snapshots."),
        new("UserManagerUpdate", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.ManagerUpdate.cs", true, true, "Manager before/after snapshots."),
        new("UserAccountExpirationUpdate", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.AccountExpiration.cs", true, true, "Expiration before/after snapshots."),
        new("GroupCreate", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.GroupsCreate.cs", true, true, "Group after snapshot."),
        new("GroupUpdate", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.GroupsUpdate.cs", true, true, "Group before/after snapshots."),
        new("GroupDelete", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.GroupsDelete.cs", true, true, "Group before snapshot."),
        new("GroupMemberAdd", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.GroupsMembers.cs", true, true, "Member membership snapshots."),
        new("GroupMemberRemove", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.GroupsMembers.cs", true, true, "Member membership snapshots."),
        new("GroupMoveOu", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.GroupsMoveOu.cs", true, true, "Group OU move snapshots."),
        new("ComputerEnable", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.ComputerAccountOperations.cs", true, true, "Computer account status snapshots."),
        new("ComputerDisable", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.ComputerAccountOperations.cs", true, true, "Computer account status snapshots."),
        new("ComputerUpdate", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.ComputerUpdate.cs", true, true, "Computer before/after snapshots."),
        new("ComputerMoveOu", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.ComputerMoveOu.cs", true, true, "Computer OU move snapshots."),
        new("ComputerDelete", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.ComputerDelete.cs", true, true, "Computer delete before/after snapshots."),
        new("ComputerGroupAdd", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.ComputerGroupMembership.cs", true, true, "Computer group membership snapshots."),
        new("ComputerGroupRemove", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.ComputerGroupMembership.cs", true, true, "Computer group membership snapshots."),
        new("DeletedObjectRestore", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs", true, true, "Deleted/restored object snapshots; credentialMode diagnostic only."),
        new("OrganizationalUnitCreate", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.OrganizationalUnitsMutations.cs", true, true, "Organizational unit after snapshot."),
        new("OrganizationalUnitRename", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.OrganizationalUnitsMutations.cs", true, true, "Organizational unit before/after rename snapshots."),
        new("OrganizationalUnitMove", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.OrganizationalUnitsMutations.cs", true, true, "Organizational unit before/after move snapshots."),
        new("OrganizationalUnitDelete", "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.OrganizationalUnitsMutations.cs", true, true, "Organizational unit before delete snapshot."),
    ];
}
