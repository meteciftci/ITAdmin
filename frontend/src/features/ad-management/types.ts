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
  netbiosDomainName: string | null;
  defaultNamingContext: string | null;
  baseDn: string | null;
  usersRootOu: string | null;
  disabledUsersOu: string | null;
  groupsSearchBase: string | null;
  computersSearchBase: string | null;
  preferredDomainControllers: string[];
  useSsl: boolean;
  ldapPort: number;
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
  netbiosDomainName: string | null;
  defaultNamingContext: string | null;
  baseDn: string | null;
  usersRootOu: string | null;
  disabledUsersOu: string | null;
  groupsSearchBase: string | null;
  computersSearchBase: string | null;
  preferredDomainControllers: string[];
  useSsl: boolean;
  ldapPort: number;
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

export type CreateAdUserMappedAttributeRequest = {
  logicalField: string;
  value: string | string[] | null;
};

export type CreateAdUserRequest = {
  givenName: string;
  surname: string;
  department?: string | null;
  samAccountName?: string | null;
  upnSuffix: string;
  targetOuDistinguishedName: string;
  initialPassword: string;
  isEnabled: boolean;
  mustChangePasswordAtNextLogon: boolean;
  mappedAttributes: CreateAdUserMappedAttributeRequest[];
};

export type AdUpnSuffixItem = {
  value: string;
  source: string;
};

export type AdUpnSuffixesResponse = {
  items: AdUpnSuffixItem[];
  warning?: string | null;
};

export type AdUserCreatedNotificationSummary = {
  queuedCount: number;
  skippedCount: number;
  messages: string[];
};

export type CreateAdUserResponse = AdManagementApiMessageFields & {
  id: string;
  distinguishedName: string;
  cn: string;
  samAccountName: string;
  userPrincipalName: string;
  displayName: string;
  isEnabled: boolean;
  namingCollisionResolved: boolean;
  generatedSuffix: number | null;
  notificationSummary: AdUserCreatedNotificationSummary | null;
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

export type AdUserStatusFilter = "active" | "disabled" | "all";

export type AdUserListItem = {
  id: string;
  distinguishedName: string;
  samAccountName: string | null;
  userPrincipalName: string | null;
  displayName: string | null;
  mail: string | null;
  department: string | null;
  isEnabled: boolean;
  isLockedOut: boolean;
  whenCreated: string | null;
  whenChanged: string | null;
  lastLogonAt: string | null;
};

export type AdUserSearchResponse = {
  items: AdUserListItem[];
  pageNumber: number;
  pageSize: number;
  hasNextPage: boolean;
};

export type AdGroupScope = "Global" | "DomainLocal" | "Universal" | "Unknown";

export type AdGroupMemberType = "User" | "Group" | "Computer" | "Unknown";

export type AdGroupListItem = {
  id: string;
  distinguishedName: string;
  displayName: string | null;
  name: string;
  cn: string | null;
  samAccountName: string | null;
  description: string | null;
  groupScope: AdGroupScope;
  securityEnabled: boolean;
  groupType: number | null;
};

export type AdGroupListResponse = {
  items: AdGroupListItem[];
  pageNumber: number;
  pageSize: number;
  hasNextPage: boolean;
};

export type AdGroupMemberItem = {
  type: AdGroupMemberType;
  displayName: string | null;
  name: string | null;
  samAccountName: string | null;
  distinguishedName: string;
  description: string | null;
};

export type AdGroupDetail = {
  id: string;
  distinguishedName: string;
  displayName: string | null;
  name: string;
  cn: string | null;
  samAccountName: string | null;
  description: string | null;
  groupScope: AdGroupScope;
  securityEnabled: boolean;
  groupType: number | null;
  whenCreated: string | null;
  whenChanged: string | null;
  managedByDistinguishedName: string | null;
  managedByDisplayName: string | null;
  memberCount: number;
  memberOfCount: number;
  members: AdGroupMemberItem[];
  memberOf: AdGroupMemberItem[];
  membersTruncated: boolean;
  memberOfTruncated: boolean;
};

export type AdGroupMemberListTypeFilter = "all" | "user" | "group" | "computer";

export type AdGroupMemberListItem = {
  id: string | null;
  type: AdGroupMemberType;
  displayName: string | null;
  name: string | null;
  cn: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  dNSHostName: string | null;
  description: string | null;
  distinguishedName: string;
  isDirectMember: boolean;
};

export type AdGroupMembersListResponse = {
  items: AdGroupMemberListItem[];
  pageNumber: number;
  pageSize: number;
  memberCount: number;
  hasNextPage: boolean;
};

export type GetAdGroupMembersParams = {
  search?: string;
  type?: AdGroupMemberListTypeFilter;
  pageNumber?: number;
  pageSize?: number;
};

export type AdGroupMemberCandidateType = "user" | "group" | "computer";

export type AdGroupMemberCandidateItem = {
  id: string | null;
  type: AdGroupMemberType;
  displayName: string | null;
  name: string | null;
  cn: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  dNSHostName: string | null;
  description: string | null;
  distinguishedName: string;
  isAlreadyDirectMember: boolean;
  isEnabled: boolean | null;
};

export type AdGroupMemberCandidatesResponse = {
  items: AdGroupMemberCandidateItem[];
};

export type GetAdGroupMemberCandidatesParams = {
  search: string;
  types?: AdGroupMemberCandidateType[];
  pageSize?: number;
};

export type AddAdGroupMemberRequest = {
  memberDistinguishedName: string;
  memberType?: AdGroupMemberCandidateType;
};

export type RemoveAdGroupMemberRequest = {
  memberDistinguishedName: string;
};

export type AdGroupMemberOperationResponse = AdManagementApiMessageFields & {
  success: boolean;
  groupId: string | null;
  groupDistinguishedName: string | null;
  groupName: string | null;
  memberDistinguishedName: string | null;
  memberName: string | null;
};

export type GetAdGroupsParams = {
  search?: string;
  page?: number;
  pageSize?: number;
};

export type AdComputerStatusFilter = "active" | "disabled" | "all";

export type AdComputerListItem = {
  id: string;
  name: string;
  samAccountName: string | null;
  dnsHostName: string | null;
  operatingSystem: string | null;
  distinguishedName: string;
  isEnabled: boolean;
  whenChanged: string | null;
};

export type AdComputerListResponse = {
  items: AdComputerListItem[];
  pageNumber: number;
  pageSize: number;
  hasNextPage: boolean;
};

export type AdComputerOperatingSystemOptionsResponse = {
  items: string[];
};

export type AdComputerMemberOfItem = {
  distinguishedName: string;
  name: string | null;
  samAccountName: string | null;
};

export type AdComputerDetail = {
  id: string;
  name: string;
  cn: string | null;
  samAccountName: string | null;
  dnsHostName: string | null;
  distinguishedName: string;
  parentOuDistinguishedName: string | null;
  description: string | null;
  operatingSystem: string | null;
  operatingSystemVersion: string | null;
  operatingSystemServicePack: string | null;
  managedByDistinguishedName: string | null;
  managedByDisplayName: string | null;
  lastLogonAt: string | null;
  whenCreated: string | null;
  whenChanged: string | null;
  userAccountControl: number | null;
  isEnabled: boolean;
  primaryGroupId: number | null;
  memberOfCount: number;
  memberOf: AdComputerMemberOfItem[];
  memberOfTruncated: boolean;
};

export type GetAdComputersParams = {
  search?: string;
  status?: AdComputerStatusFilter;
  operatingSystem?: string;
  pageNumber?: number;
  pageSize?: number;
};

export type AdComputerAccountConfirmAction = "enable" | "disable";

export type AdComputerAccountOperationResponse = AdManagementApiMessageFields & {
  success: boolean;
  computer: AdComputerDetail | null;
};

export type DeleteAdComputerResponse = AdManagementApiMessageFields & {
  success: boolean;
  deletedComputerId: string | null;
  deletedComputerName: string | null;
  deletedDistinguishedName: string | null;
};

export type UpdateAdComputerRequest = {
  description: string | null;
};

export type MoveAdComputerOuRequest = {
  targetOuDistinguishedName: string;
};

export type CreateAdGroupRequest = {
  displayName: string;
  name: string;
  samAccountName: string;
  description?: string | null;
  groupScope: AdGroupScope;
  targetOuDistinguishedName: string;
};

export type UpdateAdGroupRequest = {
  displayName: string;
  name: string;
  samAccountName: string;
  description?: string | null;
};

export type DeleteAdGroupResponse = AdManagementApiMessageFields & {
  success: boolean;
  deletedGroupId: string | null;
};

export type AdUserGroupMembership = {
  name: string;
  distinguishedName: string;
};

export type MappedAdUserAttribute = {
  logicalField: string;
  displayName: string;
  adAttribute: string;
  value: string[] | null;
  isSensitive: boolean;
  maskingStrategy: string | null;
  isEditable: boolean;
  isSearchable: boolean;
  sortOrder: number;
};

export type AdUserDetail = {
  id: string;
  distinguishedName: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  displayName: string | null;
  mail: string | null;
  givenName: string | null;
  surname: string | null;
  department: string | null;
  isEnabled: boolean;
  isLockedOut: boolean;
  passwordLastSetAt: string | null;
  lastLogonAt: string | null;
  whenCreated: string | null;
  whenChanged: string | null;
  userAccountControl: number | null;
  accountExpiresAt: string | null;
  accountExpiresDate: string | null;
  lockoutTimeAt: string | null;
  badPwdCount: number | null;
  badPasswordTimeAt: string | null;
  lastLogonTimestampAt: string | null;
  groups: AdUserGroupMembership[];
  mappedAttributes: MappedAdUserAttribute[];
  managerDistinguishedName: string | null;
  managerId: string | null;
  managerSamAccountName: string | null;
  managerUserPrincipalName: string | null;
  managerDisplayName: string | null;
};

export type GetAdUsersParams = {
  search?: string;
  status?: AdUserStatusFilter;
  pageNumber?: number;
  pageSize?: number;
};

export type UpdateAdUserMappedAttributeRequest = {
  logicalField: string;
  value: string | string[] | null;
};

export type UpdateAdUserRequest = {
  givenName: string;
  surname: string;
  displayName: string;
  samAccountName: string;
  userPrincipalName: string;
  mail?: string | null;
  department?: string | null;
  mappedAttributes: UpdateAdUserMappedAttributeRequest[];
};

export type AdUserAccountOperationResponse = AdManagementApiMessageFields & {
  success: boolean;
  userId: string;
  samAccountName: string | null;
  userPrincipalName: string | null;
  distinguishedName: string | null;
  isEnabled: boolean | null;
  isLockedOut: boolean | null;
};

export type AdUserAccountConfirmAction = "enable" | "disable" | "unlock";

export type MoveAdUserOuRequest = {
  targetOuDistinguishedName: string;
};

export type MoveAdUserOuResponse = AdManagementApiMessageFields & {
  success: boolean;
  userId: string;
  samAccountName: string | null;
  userPrincipalName: string | null;
  distinguishedName: string | null;
  previousDistinguishedName: string | null;
  targetOuDistinguishedName: string | null;
};

export type MoveAdGroupOuRequest = {
  targetOuDistinguishedName: string;
};

export type MoveAdGroupOuResponse = AdManagementApiMessageFields & {
  success: boolean;
  groupId: string;
  displayName: string | null;
  name: string | null;
  samAccountName: string | null;
  distinguishedName: string | null;
  previousDistinguishedName: string | null;
  targetOuDistinguishedName: string | null;
};

export type AdUserGroupMembershipItem = {
  distinguishedName: string;
  displayName: string | null;
  name: string;
  samAccountName: string | null;
  description: string | null;
  isDirect: boolean;
};

export type AdUserGroupMembershipResponse = {
  userId: string;
  displayName: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  distinguishedName: string | null;
  groups: AdUserGroupMembershipItem[];
};

export type AdMembershipPathNode = {
  type: string;
  name: string;
  displayName: string | null;
  samAccountName: string | null;
  distinguishedName: string;
};

export type AdEffectiveGroupSummaryItem = {
  name: string;
  distinguishedName: string;
  samAccountName: string | null;
  description: string | null;
  displayName: string | null;
};

export type AdEffectiveGroupNestedItem = {
  name: string;
  distinguishedName: string;
  samAccountName: string | null;
  description: string | null;
  displayName: string | null;
  depth: number;
  isDirect: boolean;
  path: AdMembershipPathNode[];
};

export type AdUserEffectiveGroupsResponse = {
  userId: string;
  displayName: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  distinguishedName: string | null;
  directGroups: AdEffectiveGroupSummaryItem[];
  effectiveGroups: AdEffectiveGroupNestedItem[];
  maxDepth: number;
  truncated: boolean;
  truncatedReason: string | null;
};

export type AdGroupSearchItem = {
  distinguishedName: string;
  displayName: string | null;
  name: string;
  samAccountName: string | null;
  description: string | null;
};

export type AdGroupSearchResponse = {
  items: AdGroupSearchItem[];
};

export type AdUserGroupOperationResponse = AdManagementApiMessageFields & {
  success: boolean;
  userId: string;
  groupDistinguishedName: string;
  groupName: string | null;
};

export type AdUserGroupMutationRequest = {
  groupDistinguishedName: string;
};

export type AdComputerGroupMembershipItem = {
  id: string;
  distinguishedName: string;
  displayName: string | null;
  name: string;
  samAccountName: string | null;
  description: string | null;
  isDirect: boolean;
};

export type AdComputerGroupMembershipResponse = {
  computerId: string;
  name: string | null;
  samAccountName: string | null;
  dnsHostName: string | null;
  distinguishedName: string | null;
  groups: AdComputerGroupMembershipItem[];
};

export type AdComputerGroupCandidateItem = {
  distinguishedName: string;
  displayName: string | null;
  name: string;
  samAccountName: string | null;
  description: string | null;
};

export type AdComputerGroupCandidateSearchResponse = {
  items: AdComputerGroupCandidateItem[];
};

export type AdComputerGroupMutationRequest = {
  groupDistinguishedName: string;
};

export type AdComputerGroupOperationResponse = AdManagementApiMessageFields & {
  success: boolean;
  computerId: string | null;
  computerName: string | null;
  computerSamAccountName: string | null;
  groupDistinguishedName: string | null;
  groupName: string | null;
  groupDisplayName: string | null;
  groupSamAccountName: string | null;
};

export type UpdateAdUserManagerRequest = {
  managerUserId: string | null;
  clearManager: boolean;
};

export type UpdateAdUserManagerResponse = AdManagementApiMessageFields & {
  success: boolean;
  userId: string;
  samAccountName: string | null;
  managerDistinguishedName: string | null;
  managerDisplayName: string | null;
};

export type UpdateAdUserAccountExpirationRequest = {
  neverExpires: boolean;
  expiresAt: string | null;
};

export type UpdateAdUserAccountExpirationResponse = AdManagementApiMessageFields & {
  success: boolean;
  userId: string;
  samAccountName: string | null;
  accountExpiresDate: string | null;
  neverExpires: boolean;
};

export type AdDeletedObjectType = "User" | "Group" | "Computer" | "Unknown";

export type AdDeletedObjectTypeFilter = "all" | "user" | "group" | "computer";

export type AdDeletedObjectListItem = {
  id: string;
  objectType: AdDeletedObjectType;
  name: string | null;
  displayName: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  distinguishedName: string;
  lastKnownParent: string | null;
  whenChanged: string | null;
  deletedAt: string | null;
};

export type AdDeletedObjectListResponse = {
  items: AdDeletedObjectListItem[];
  pageNumber: number;
  pageSize: number;
  hasNextPage: boolean;
};

export type AdDeletedObjectDetail = {
  id: string;
  objectType: AdDeletedObjectType;
  name: string | null;
  displayName: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  description: string | null;
  distinguishedName: string;
  lastKnownParent: string | null;
  lastKnownRdn: string | null;
  objectClass: string[];
  objectSid: string | null;
  whenCreated: string | null;
  whenChanged: string | null;
  deletedAt: string | null;
  mail: string | null;
  department: string | null;
  dnsHostName: string | null;
  operatingSystem: string | null;
  memberOfCount: number;
  memberOf: string[];
  memberOfTruncated: boolean;
  additionalAttributes: Record<string, string>;
};

export type GetAdDeletedObjectsParams = {
  search?: string;
  type?: AdDeletedObjectTypeFilter;
  pageNumber?: number;
  pageSize?: number;
  includeAll?: boolean;
};

export type AdDeletedObjectRestoreResponse = AdManagementApiMessageFields & {
  success: boolean;
  restoredObjectId: string | null;
  restoredObjectType: AdDeletedObjectType | null;
  restoredName: string | null;
  restoredSamAccountName: string | null;
  restoredDistinguishedName: string | null;
  restoredLastKnownParent: string | null;
};

export type AdOrganizationalUnitManageListItem = {
  objectGuid: string;
  name: string | null;
  ou: string | null;
  displayName: string | null;
  displayLabel: string;
  distinguishedName: string;
  parentDistinguishedName: string | null;
  canonicalName: string;
  childOuCount: number | null;
  userCount: number | null;
  groupCount: number | null;
  computerCount: number | null;
};

export type AdOrganizationalUnitManageListResponse = {
  items: AdOrganizationalUnitManageListItem[];
  pageNumber: number;
  pageSize: number;
  hasNextPage: boolean;
};

export type GetAdOrganizationalUnitsParams = {
  search?: string;
  pageNumber?: number;
  pageSize?: number;
};

export type AdOrganizationalUnitContentSummary = {
  childOuCount: number | null;
  userCount: number | null;
  groupCount: number | null;
  computerCount: number | null;
};

export type AdOrganizationalUnitChildListItem = {
  objectGuid: string;
  name: string | null;
  ou: string | null;
  displayName: string | null;
  displayLabel: string;
  distinguishedName: string;
  canonicalName: string;
};

export type AdOrganizationalUnitDetail = {
  objectGuid: string;
  name: string | null;
  ou: string | null;
  displayName: string | null;
  displayLabel?: string | null;
  distinguishedName: string;
  parentDistinguishedName: string | null;
  canonicalName: string;
  contentSummary: AdOrganizationalUnitContentSummary;
  childOrganizationalUnits: AdOrganizationalUnitChildListItem[];
};

export type CreateAdOrganizationalUnitRequest = {
  name: string;
  parentDistinguishedName: string;
};

export type RenameAdOrganizationalUnitRequest = {
  name: string;
};

export type MoveAdOrganizationalUnitRequest = {
  targetParentDistinguishedName: string;
};

export type CreateAdOrganizationalUnitResponse = AdManagementApiMessageFields & {
  success: boolean;
  organizationalUnit: AdOrganizationalUnitDetail | null;
};

export type RenameAdOrganizationalUnitResponse = AdManagementApiMessageFields & {
  success: boolean;
  organizationalUnit: AdOrganizationalUnitDetail | null;
  previousDistinguishedName: string | null;
};

export type MoveAdOrganizationalUnitResponse = AdManagementApiMessageFields & {
  success: boolean;
  organizationalUnit: AdOrganizationalUnitDetail | null;
  previousDistinguishedName: string | null;
  targetParentDistinguishedName: string | null;
};

export type DeleteAdOrganizationalUnitResponse = AdManagementApiMessageFields & {
  success: boolean;
  deletedOrganizationalUnitId: string | null;
};
