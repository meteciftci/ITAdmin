import type {
  AdManagementNotificationSettings,
  AdManagementSettings,
  UpdateAdManagementSettingsRequest,
} from "@/features/ad-management/types";

export const defaultAdManagementNotificationSettings = (): AdManagementNotificationSettings => ({
  rules: [],
});

export function buildUpdateAdManagementSettingsPayload(
  settings: AdManagementSettings,
  overrides: Partial<UpdateAdManagementSettingsRequest> = {},
): UpdateAdManagementSettingsRequest {
  return {
    isEnabled: overrides.isEnabled ?? settings.isEnabled,
    domainFqdn: overrides.domainFqdn ?? settings.domainFqdn,
    defaultUserCreationUpnSuffix:
      overrides.defaultUserCreationUpnSuffix ?? settings.defaultUserCreationUpnSuffix ?? null,
    defaultUserOu: overrides.defaultUserOu ?? settings.defaultUserOu ?? null,
    defaultGroupOu: overrides.defaultGroupOu ?? settings.defaultGroupOu ?? null,
    defaultComputerOu: overrides.defaultComputerOu ?? settings.defaultComputerOu ?? null,
    netbiosDomainName: overrides.netbiosDomainName ?? settings.netbiosDomainName,
    defaultNamingContext: overrides.defaultNamingContext ?? settings.defaultNamingContext,
    baseDn: overrides.baseDn ?? settings.baseDn,
    usersRootOu: overrides.usersRootOu ?? settings.usersRootOu,
    disabledUsersOu: overrides.disabledUsersOu ?? settings.disabledUsersOu,
    groupsSearchBase: overrides.groupsSearchBase ?? settings.groupsSearchBase,
    computersSearchBase: overrides.computersSearchBase ?? settings.computersSearchBase,
    preferredDomainControllers:
      overrides.preferredDomainControllers ?? settings.preferredDomainControllers,
    serviceAccountUserName:
      overrides.serviceAccountUserName ?? settings.serviceAccountUserName,
    serviceAccountPassword: overrides.serviceAccountPassword ?? null,
    clearServiceAccountPassword: overrides.clearServiceAccountPassword ?? false,
    powerShellHealthEnabled:
      overrides.powerShellHealthEnabled ?? settings.powerShellHealthEnabled,
    powerShellTimeoutSeconds:
      overrides.powerShellTimeoutSeconds ?? settings.powerShellTimeoutSeconds,
    notificationSettings:
      overrides.notificationSettings
      ?? settings.notificationSettings
      ?? defaultAdManagementNotificationSettings(),
  };
}
