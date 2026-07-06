namespace ITAdmin.Application.Common.Security;

public static class PermissionCodes
{
    public static class Dashboard
    {
        public const string View = "Dashboard.View";
    }

    public static class Users
    {
        public const string View = "Users.View";
        public const string Create = "Users.Create";
        public const string Update = "Users.Update";
        public const string Delete = "Users.Delete";
        public const string AssignRoles = "Users.AssignRoles";
    }

    public static class Roles
    {
        public const string View = "Roles.View";
        public const string Create = "Roles.Create";
        public const string Update = "Roles.Update";
        public const string Delete = "Roles.Delete";
        public const string AssignPermissions = "Roles.AssignPermissions";
    }

    public static class Permissions
    {
        public const string View = "Permissions.View";
    }

    public static class AuditLogs
    {
        public const string View = "AuditLogs.View";
    }

    public static class SecurityLogs
    {
        public const string View = "SecurityLogs.View";
    }

    public static class Settings
    {
        public const string View = "Settings.View";
        public const string Update = "Settings.Update";
    }

    public static class Setup
    {
        public const string Manage = "Setup.Manage";
    }

    public static class NotificationProviders
    {
        public const string View = "NotificationProviders.View";
        public const string Update = "NotificationProviders.Update";
        public const string Test = "NotificationProviders.Test";
    }

    public static class NotificationOutbox
    {
        public const string View = "NotificationOutbox.View";
        public const string Retry = "NotificationOutbox.Retry";
        public const string Cancel = "NotificationOutbox.Cancel";
    }

    public static class NotificationTemplates
    {
        public const string View = "NotificationTemplates.View";
        public const string Update = "NotificationTemplates.Update";
    }

    public static class AdOperationLogs
    {
        public const string View = "AdOperationLogs.View";
    }

    public static class AdManagement
    {
        public static class Settings
        {
            public const string View = "AdManagement.Settings.View";
            public const string Update = "AdManagement.Settings.Update";
        }

        public static class Users
        {
            public const string View = "AdManagement.Users.View";
            public const string Create = "AdManagement.Users.Create";
            public const string Update = "AdManagement.Users.Update";
            public const string Enable = "AdManagement.Users.Enable";
            public const string Disable = "AdManagement.Users.Disable";
            public const string Unlock = "AdManagement.Users.Unlock";
            public const string MoveOu = "AdManagement.Users.MoveOu";

            public static class Groups
            {
                public const string View = "AdManagement.Users.Groups.View";
                public const string Add = "AdManagement.Users.Groups.Add";
                public const string Remove = "AdManagement.Users.Groups.Remove";
            }
        }

        public static class Groups
        {
            public const string View = "AdManagement.Groups.View";
            public const string Create = "AdManagement.Groups.Create";
            public const string Update = "AdManagement.Groups.Update";
            public const string Delete = "AdManagement.Groups.Delete";
            public const string ManageMembers = "AdManagement.Groups.ManageMembers";
            public const string MoveOu = "AdManagement.Groups.MoveOu";
        }

        public static class Computers
        {
            public const string View = "AdManagement.Computers.View";
            public const string Update = "AdManagement.Computers.Update";
            public const string MoveOu = "AdManagement.Computers.MoveOu";
            public const string Enable = "AdManagement.Computers.Enable";
            public const string Disable = "AdManagement.Computers.Disable";
            public const string Delete = "AdManagement.Computers.Delete";

            public static class Groups
            {
                public const string View = "AdManagement.Computers.Groups.View";
                public const string Add = "AdManagement.Computers.Groups.Add";
                public const string Remove = "AdManagement.Computers.Groups.Remove";
            }
        }

        public static class DeletedObjects
        {
            public const string View = "AdManagement.DeletedObjects.View";
            public const string Restore = "AdManagement.DeletedObjects.Restore";
        }

        public static class OrganizationalUnits
        {
            public const string View = "AdManagement.OrganizationalUnits.View";
            public const string Create = "AdManagement.OrganizationalUnits.Create";
            public const string Update = "AdManagement.OrganizationalUnits.Update";
            public const string Move = "AdManagement.OrganizationalUnits.Move";
            public const string Delete = "AdManagement.OrganizationalUnits.Delete";
        }
    }

    public static class LicenseManagement
    {
        public const string View = "LicenseManagement.View";
        public const string ManageCatalog = "LicenseManagement.ManageCatalog";
        public const string ManagePurchases = "LicenseManagement.ManagePurchases";
        public const string ManageRequests = "LicenseManagement.ManageRequests";
        public const string FulfillRequests = "LicenseManagement.FulfillRequests";
        public const string ViewReports = "LicenseManagement.ViewReports";
        public const string ManageSettings = "LicenseManagement.ManageSettings";
    }

    public static class Directory
    {
        public static class Users
        {
            public const string Lookup = "Directory.Users.Lookup";
        }

        public static class OrganizationalUnits
        {
            public const string Lookup = "Directory.OrganizationalUnits.Lookup";
        }
    }
}
