import type { AdManagementApiMessageFields } from "./common.ts";

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

export type AdUserGroupOperationResponse = AdManagementApiMessageFields & {
  success: boolean;
  userId: string;
  groupDistinguishedName: string;
  groupName: string | null;
};

export type AdUserGroupMutationRequest = {
  groupDistinguishedName: string;
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
