export const PermissionCodes = {
  Dashboard: {
    View: "Dashboard.View",
  },
  Users: {
    View: "Users.View",
    Create: "Users.Create",
    Update: "Users.Update",
    Delete: "Users.Delete",
    AssignRoles: "Users.AssignRoles",
  },
  Roles: {
    View: "Roles.View",
    Create: "Roles.Create",
    Update: "Roles.Update",
    Delete: "Roles.Delete",
    AssignPermissions: "Roles.AssignPermissions",
  },
  Permissions: {
    View: "Permissions.View",
  },
  AuditLogs: {
    View: "AuditLogs.View",
  },
  SecurityLogs: {
    View: "SecurityLogs.View",
  },
  Settings: {
    View: "Settings.View",
    Update: "Settings.Update",
  },
  SystemUpdates: {
    View: "System.Updates.View",
    Manage: "System.Updates.Manage",
  },
  SystemHttps: {
    View: "System.Https.View",
    Manage: "System.Https.Manage",
  },
  Setup: {
    Manage: "Setup.Manage",
  },
  NotificationProviders: {
    View: "NotificationProviders.View",
    Update: "NotificationProviders.Update",
    Test: "NotificationProviders.Test",
  },
  NotificationOutbox: {
    View: "NotificationOutbox.View",
    Retry: "NotificationOutbox.Retry",
    Cancel: "NotificationOutbox.Cancel",
  },
  NotificationTemplates: {
    View: "NotificationTemplates.View",
    Update: "NotificationTemplates.Update",
  },
  AdOperationLogs: {
    View: "AdOperationLogs.View",
  },
  AdManagement: {
    Settings: {
      View: "AdManagement.Settings.View",
      Update: "AdManagement.Settings.Update",
    },
    Users: {
      View: "AdManagement.Users.View",
      Create: "AdManagement.Users.Create",
      Update: "AdManagement.Users.Update",
      Enable: "AdManagement.Users.Enable",
      Disable: "AdManagement.Users.Disable",
      Unlock: "AdManagement.Users.Unlock",
      MoveOu: "AdManagement.Users.MoveOu",
      Groups: {
        View: "AdManagement.Users.Groups.View",
        Add: "AdManagement.Users.Groups.Add",
        Remove: "AdManagement.Users.Groups.Remove",
      },
    },
    Groups: {
      View: "AdManagement.Groups.View",
      Create: "AdManagement.Groups.Create",
      Update: "AdManagement.Groups.Update",
      Delete: "AdManagement.Groups.Delete",
      ManageMembers: "AdManagement.Groups.ManageMembers",
      MoveOu: "AdManagement.Groups.MoveOu",
    },
    Computers: {
      View: "AdManagement.Computers.View",
      Update: "AdManagement.Computers.Update",
      MoveOu: "AdManagement.Computers.MoveOu",
      Enable: "AdManagement.Computers.Enable",
      Disable: "AdManagement.Computers.Disable",
      Delete: "AdManagement.Computers.Delete",
      Groups: {
        View: "AdManagement.Computers.Groups.View",
        Add: "AdManagement.Computers.Groups.Add",
        Remove: "AdManagement.Computers.Groups.Remove",
      },
    },
    DeletedObjects: {
      View: "AdManagement.DeletedObjects.View",
      Restore: "AdManagement.DeletedObjects.Restore",
    },
    OrganizationalUnits: {
      View: "AdManagement.OrganizationalUnits.View",
      Create: "AdManagement.OrganizationalUnits.Create",
      Update: "AdManagement.OrganizationalUnits.Update",
      Move: "AdManagement.OrganizationalUnits.Move",
      Delete: "AdManagement.OrganizationalUnits.Delete",
    },
  },
  LicenseManagement: {
    View: "LicenseManagement.View",
    ViewSensitiveData: "LicenseManagement.ViewSensitiveData",
    ManageCatalog: "LicenseManagement.ManageCatalog",
    ManagePurchases: "LicenseManagement.ManagePurchases",
    ManageRequests: "LicenseManagement.ManageRequests",
    FulfillRequests: "LicenseManagement.FulfillRequests",
    ViewReports: "LicenseManagement.ViewReports",
    ManageSettings: "LicenseManagement.ManageSettings",
  },
  DnsManagement: {
    View: "DnsManagement.View",
    ManageSettings: "DnsManagement.ManageSettings",
    Servers: { View: "DnsManagement.Servers.View", Manage: "DnsManagement.Servers.Manage", TestConnection: "DnsManagement.Servers.TestConnection" },
    Zones: { View: "DnsManagement.Zones.View", Create: "DnsManagement.Zones.Create", Update: "DnsManagement.Zones.Update", Delete: "DnsManagement.Zones.Delete" },
    Records: { View: "DnsManagement.Records.View", Create: "DnsManagement.Records.Create", Update: "DnsManagement.Records.Update", Delete: "DnsManagement.Records.Delete" },
    Compare: "DnsManagement.Compare", Export: "DnsManagement.Export", Synchronize: "DnsManagement.Synchronize",
    ManageServerSettings: "DnsManagement.ServerSettings.Manage", ManageDnssec: "DnsManagement.Dnssec.Manage", ManagePolicies: "DnsManagement.Policies.Manage", ClearCache: "DnsManagement.Cache.Clear", ManageScavenging: "DnsManagement.Scavenging.Manage", ManageNetworkConfiguration: "DnsManagement.NetworkConfiguration.Manage", ViewOperationLogs: "DnsManagement.OperationLogs.View",
  },
  Directory: {
    Users: {
      Lookup: "Directory.Users.Lookup",
    },
    OrganizationalUnits: {
      Lookup: "Directory.OrganizationalUnits.Lookup",
    },
  },
} as const;

type DeepPermissionValue<T> = T extends string
  ? T
  : T extends Record<string, unknown>
    ? DeepPermissionValue<T[keyof T]>
    : never;

export type PermissionCode = DeepPermissionValue<typeof PermissionCodes>;
