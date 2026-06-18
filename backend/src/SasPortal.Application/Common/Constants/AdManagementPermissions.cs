using SasPortal.Application.Common.Security;

namespace SasPortal.Application.Common.Constants;

public static class AdManagementPermissions
{
    public const string SettingsView = PermissionCodes.AdManagement.Settings.View;
    public const string SettingsUpdate = PermissionCodes.AdManagement.Settings.Update;
    public const string UsersView = PermissionCodes.AdManagement.Users.View;
    public const string UsersCreate = PermissionCodes.AdManagement.Users.Create;
    public const string UsersUpdate = PermissionCodes.AdManagement.Users.Update;
    public const string UsersEnable = PermissionCodes.AdManagement.Users.Enable;
    public const string UsersDisable = PermissionCodes.AdManagement.Users.Disable;
    public const string UsersUnlock = PermissionCodes.AdManagement.Users.Unlock;
    public const string UsersGroupsView = PermissionCodes.AdManagement.Users.Groups.View;
    public const string UsersGroupsAdd = PermissionCodes.AdManagement.Users.Groups.Add;
    public const string UsersGroupsRemove = PermissionCodes.AdManagement.Users.Groups.Remove;
    public const string GroupsView = PermissionCodes.AdManagement.Groups.View;
    public const string GroupsCreate = PermissionCodes.AdManagement.Groups.Create;
    public const string GroupsUpdate = PermissionCodes.AdManagement.Groups.Update;
    public const string GroupsDelete = PermissionCodes.AdManagement.Groups.Delete;
    public const string GroupsManageMembers = PermissionCodes.AdManagement.Groups.ManageMembers;
    public const string GroupsMoveOu = PermissionCodes.AdManagement.Groups.MoveOu;
    public const string ComputersView = PermissionCodes.AdManagement.Computers.View;
    public const string ComputersUpdate = PermissionCodes.AdManagement.Computers.Update;
    public const string ComputersMoveOu = PermissionCodes.AdManagement.Computers.MoveOu;
    public const string ComputersEnable = PermissionCodes.AdManagement.Computers.Enable;
    public const string ComputersDisable = PermissionCodes.AdManagement.Computers.Disable;
    public const string ComputersDelete = PermissionCodes.AdManagement.Computers.Delete;
    public const string ComputersGroupsView = PermissionCodes.AdManagement.Computers.Groups.View;
    public const string ComputersGroupsAdd = PermissionCodes.AdManagement.Computers.Groups.Add;
    public const string ComputersGroupsRemove = PermissionCodes.AdManagement.Computers.Groups.Remove;
    public const string UsersMoveOu = PermissionCodes.AdManagement.Users.MoveOu;
    public const string DeletedObjectsView = PermissionCodes.AdManagement.DeletedObjects.View;
    public const string DeletedObjectsRestore = PermissionCodes.AdManagement.DeletedObjects.Restore;
    public const string OrganizationalUnitsView = PermissionCodes.AdManagement.OrganizationalUnits.View;
    public const string OrganizationalUnitsCreate = PermissionCodes.AdManagement.OrganizationalUnits.Create;
    public const string OrganizationalUnitsUpdate = PermissionCodes.AdManagement.OrganizationalUnits.Update;
    public const string OrganizationalUnitsMove = PermissionCodes.AdManagement.OrganizationalUnits.Move;
    public const string OrganizationalUnitsDelete = PermissionCodes.AdManagement.OrganizationalUnits.Delete;
    public const string OperationLogsView = PermissionCodes.AdOperationLogs.View;
}
