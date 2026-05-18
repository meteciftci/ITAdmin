export type AdManagementSettings = {
  isEnabled: boolean;
  domainFqdn: string | null;
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
};

export type UpdateAdManagementSettingsRequest = {
  isEnabled: boolean;
  domainFqdn: string | null;
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

export type AdManagementValidationDetail = {
  key: string;
  status: string;
  message: string | null;
};

export type AdManagementValidationResult = {
  isValid: boolean;
  message: string;
  checkedAt: string;
  details: AdManagementValidationDetail[];
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
  groups: AdUserGroupMembership[];
  mappedAttributes: MappedAdUserAttribute[];
};

export type GetAdUsersParams = {
  search?: string;
  status?: AdUserStatusFilter;
  pageNumber?: number;
  pageSize?: number;
};
