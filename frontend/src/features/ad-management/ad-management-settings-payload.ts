import type {
  AdManagementNotificationSettings,
  AdManagementSettings,
  UpdateAdManagementSettingsRequest,
} from "@/features/ad-management/types";

export const defaultAdManagementNotificationSettings = (): AdManagementNotificationSettings => ({
  rules: [],
});

export function resolveNullableOverride(
  override: string | null | undefined,
  current: string | null,
): string | null {
  return override === undefined ? current : override;
}

export const NULLABLE_OU_SETTINGS_FIELDS = [
  "defaultUserCreationUpnSuffix",
  "defaultUserOu",
  "defaultGroupOu",
  "defaultComputerOu",
  "usersRootOu",
  "disabledUsersOu",
  "groupsSearchBase",
  "computersSearchBase",
] as const satisfies ReadonlyArray<keyof UpdateAdManagementSettingsRequest>;

export function buildUpdateAdManagementSettingsPayload(
  settings: AdManagementSettings,
  overrides: Partial<UpdateAdManagementSettingsRequest> = {},
): UpdateAdManagementSettingsRequest {
  return {
    isEnabled: overrides.isEnabled ?? settings.isEnabled,
    domainFqdn: overrides.domainFqdn ?? settings.domainFqdn,
    defaultUserCreationUpnSuffix: resolveNullableOverride(
      overrides.defaultUserCreationUpnSuffix,
      settings.defaultUserCreationUpnSuffix,
    ),
    defaultUserOu: resolveNullableOverride(
      overrides.defaultUserOu,
      settings.defaultUserOu,
    ),
    defaultGroupOu: resolveNullableOverride(
      overrides.defaultGroupOu,
      settings.defaultGroupOu,
    ),
    defaultComputerOu: resolveNullableOverride(
      overrides.defaultComputerOu,
      settings.defaultComputerOu,
    ),
    netbiosDomainName: overrides.netbiosDomainName ?? settings.netbiosDomainName,
    defaultNamingContext: overrides.defaultNamingContext ?? settings.defaultNamingContext,
    baseDn: overrides.baseDn ?? settings.baseDn,
    usersRootOu: resolveNullableOverride(
      overrides.usersRootOu,
      settings.usersRootOu,
    ),
    disabledUsersOu: resolveNullableOverride(
      overrides.disabledUsersOu,
      settings.disabledUsersOu,
    ),
    groupsSearchBase: resolveNullableOverride(
      overrides.groupsSearchBase,
      settings.groupsSearchBase,
    ),
    computersSearchBase: resolveNullableOverride(
      overrides.computersSearchBase,
      settings.computersSearchBase,
    ),
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
