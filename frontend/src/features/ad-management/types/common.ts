export type AdManagementApiMessageParams = Record<string, string | number | boolean>;

export type AdManagementApiMessageFields = {
  messageKey: string;
  messageParams?: AdManagementApiMessageParams | null;
};

export type AdManagementNotificationRecipientSource = {
  type: string;
  value: string | null;
};

export type AdManagementNotificationRule = {
  id: string;
  eventKey: string;
  channel: string;
  isEnabled: boolean;
  recipientSource: AdManagementNotificationRecipientSource | null;
};

export type AdManagementNotificationSettings = {
  rules: AdManagementNotificationRule[];
};

export const AD_NOTIFICATION_EVENT_KEYS = {
  userCreated: "UserCreated",
  userEnabled: "UserEnabled",
  userDisabled: "UserDisabled",
  userUnlocked: "UserUnlocked",
} as const;

export const AD_NOTIFICATION_CHANNELS = {
  sms: "Sms",
  email: "Email",
} as const;

export const AD_NOTIFICATION_RECIPIENT_SOURCE_TYPES = {
  mappedAttribute: "MappedAttribute",
  adAttribute: "AdAttribute",
  userPrincipalName: "UserPrincipalName",
  mailAttribute: "MailAttribute",
} as const;

export type AdManagementSettings = {
  isConfigured: boolean;
  isEnabled: boolean;
  domainFqdn: string | null;
  defaultUserCreationUpnSuffix: string | null;
  defaultUserOu: string | null;
  defaultGroupOu: string | null;
  defaultComputerOu: string | null;
  netbiosDomainName: string | null;
  defaultNamingContext: string | null;
  baseDn: string | null;
  usersRootOu: string | null;
  disabledUsersOu: string | null;
  groupsSearchBase: string | null;
  computersSearchBase: string | null;
  preferredDomainControllers: string[];
  serviceAccountUserName: string | null;
  hasServiceAccountPassword: boolean;
  powerShellHealthEnabled: boolean;
  powerShellTimeoutSeconds: number;
  lastValidatedAt: string | null;
  lastValidationStatus: string | null;
  lastValidationMessage: string | null;
  notificationSettings: AdManagementNotificationSettings;
};

export type UpdateAdManagementSettingsRequest = {
  isEnabled: boolean;
  domainFqdn: string | null;
  defaultUserCreationUpnSuffix: string | null;
  defaultUserOu: string | null;
  defaultGroupOu: string | null;
  defaultComputerOu: string | null;
  netbiosDomainName: string | null;
  defaultNamingContext: string | null;
  baseDn: string | null;
  usersRootOu: string | null;
  disabledUsersOu: string | null;
  groupsSearchBase: string | null;
  computersSearchBase: string | null;
  preferredDomainControllers: string[];
  serviceAccountUserName: string | null;
  serviceAccountPassword?: string | null;
  clearServiceAccountPassword: boolean;
  powerShellHealthEnabled: boolean;
  powerShellTimeoutSeconds: number;
  notificationSettings: AdManagementNotificationSettings;
};

export type AdOrganizationalUnitListItem = {
  distinguishedName: string;
  name: string | null;
  displayName: string | null;
  ou: string | null;
  label: string;
};

export type AdOrganizationalUnitSearchResponse = {
  items: AdOrganizationalUnitListItem[];
  hasMore: boolean;
};

export type AdAttributeMapping = {
  id: string;
  logicalField: string;
  displayName: string;
  attributeName: string;
  isEnabled: boolean;
  isEditable: boolean;
  isSensitive: boolean;
  isSearchable: boolean;
  validationType: string;
  maskingStrategy: string;
  sortOrder: number;
};

export type CreateAdAttributeMappingRequest = {
  logicalField: string;
  displayName: string;
  attributeName: string;
  isEnabled: boolean;
  isEditable: boolean;
  isSensitive: boolean;
  isSearchable: boolean;
  validationType: string;
  maskingStrategy: string;
  sortOrder: number;
};

export type UpdateAdAttributeMappingRequest = {
  displayName: string;
  attributeName: string;
  isEnabled: boolean;
  isEditable: boolean;
  isSensitive: boolean;
  isSearchable: boolean;
  validationType: string;
  maskingStrategy: string;
  sortOrder: number;
};

export type AdManagementValidationDetail = AdManagementApiMessageFields & {
  key: string;
  status: string;
};

export type AdManagementValidationResult = AdManagementApiMessageFields & {
  isValid: boolean;
  checkedAt: string;
  details: AdManagementValidationDetail[];
  restoreReadiness?: AdDeletedObjectRestoreReadinessResult | null;
};

export type AdDeletedObjectRestoreReadinessStatus = "Ready" | "Warning" | "NotReady";

export type AdDeletedObjectRestoreReadinessCheckStatus =
  | "Success"
  | "Warning"
  | "Failed"
  | "NotChecked";

export type AdDeletedObjectRestoreReadinessTextParams = Record<
  string,
  string | number | boolean
>;

export type AdDeletedObjectRestoreReadinessCheck = {
  key: string;
  status: AdDeletedObjectRestoreReadinessCheckStatus;
  title: string;
  remediation: string | null;
  command: string | null;
  isBlocking: boolean;
  details: string | null;
  titleKey: string;
  titleParams?: AdDeletedObjectRestoreReadinessTextParams | null;
  messageKey: string | null;
  messageParams?: AdDeletedObjectRestoreReadinessTextParams | null;
  remediationKey?: string | null;
  remediationParams?: AdDeletedObjectRestoreReadinessTextParams | null;
};

export type AdDeletedObjectRestoreReadinessResult = {
  isReady: boolean;
  status: AdDeletedObjectRestoreReadinessStatus;
  summaryMessage: string;
  blockingReasons: AdDeletedObjectRestoreReadinessCheck[];
  warnings: AdDeletedObjectRestoreReadinessCheck[];
  checks: AdDeletedObjectRestoreReadinessCheck[];
  checkedAtUtc: string;
  domainController: string | null;
  summaryKey: string;
  summaryParams?: AdDeletedObjectRestoreReadinessTextParams | null;
};

export const AD_VALIDATION_TYPES = [
  "None",
  "NationalId",
  "Phone",
  "Email",
  "Text",
  "Number",
] as const;

export type AdValidationType = (typeof AD_VALIDATION_TYPES)[number];

export const AD_MASKING_STRATEGIES = [
  "None",
  "Last4",
  "Phone",
  "Email",
  "Hidden",
] as const;

export type AdMaskingStrategy = (typeof AD_MASKING_STRATEGIES)[number];

export const AD_LOGICAL_FIELD_REGEX = /^[a-z][a-zA-Z0-9]{1,63}$/;
export const AD_ATTRIBUTE_NAME_REGEX = /^[A-Za-z][A-Za-z0-9_-]{0,63}$/;
