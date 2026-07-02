export const SNAPSHOT_CORE_FIELD_KEYS = [
  "givenName",
  "surname",
  "displayName",
  "samAccountName",
  "userPrincipalName",
  "mail",
  "department",
  "distinguishedName",
] as const;

export type SnapshotCoreFieldKey = (typeof SNAPSHOT_CORE_FIELD_KEYS)[number];

export type ParsedMappedSnapshotAttribute = {
  logicalField: string;
  displayValue: string | null;
};

export type ParsedAdOperationSnapshot = {
  core: Partial<Record<SnapshotCoreFieldKey, string | null>>;
  mappedAttributes: ParsedMappedSnapshotAttribute[];
};

export type SnapshotComparisonRow = {
  key: string;
  before: string | null;
  after: string | null;
  changed: boolean;
  monoBefore?: boolean;
  monoAfter?: boolean;
};

export type ParsedSnapshotUser = {
  id: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  displayName: string | null;
  distinguishedName: string | null;
};

export type ParsedSnapshotAccount = {
  isEnabled: boolean | null;
  isLocked: boolean | null;
  userAccountControl: number | null;
  lockoutTime: string | null;
  primaryGroupId: number | null;
};

export type ParsedSnapshotComputer = {
  id: string | null;
  samAccountName: string | null;
  name: string | null;
  displayName?: string | null;
  dNSHostName?: string | null;
  distinguishedName: string | null;
  description?: string | null;
  operatingSystem?: string | null;
  operatingSystemVersion?: string | null;
  lastLogonTimestamp?: string | null;
  whenChanged?: string | null;
  modifiedAt?: string | null;
};

export const SNAPSHOT_COMPUTER_COMPARISON_FIELD_KEYS = [
  "name",
  "samAccountName",
  "distinguishedName",
  "dNSHostName",
  "description",
  "operatingSystem",
  "operatingSystemVersion",
  "isEnabled",
  "isLocked",
  "lastLogonTimestamp",
  "whenChanged",
  "modifiedAt",
] as const;

export type SnapshotComputerComparisonFieldKey =
  (typeof SNAPSHOT_COMPUTER_COMPARISON_FIELD_KEYS)[number];

export type ParsedSnapshotGroup = {
  id: string | null;
  displayName: string | null;
  name: string | null;
  cn: string | null;
  samAccountName: string | null;
  description: string | null;
  distinguishedName: string | null;
  groupScope: string | null;
  securityEnabled: boolean | null;
  groupType: number | null;
  memberCount: number | null;
  memberOfCount: number | null;
};

export const SNAPSHOT_GROUP_COMPARISON_FIELD_KEYS = [
  "displayName",
  "name",
  "cn",
  "samAccountName",
  "description",
  "distinguishedName",
  "groupScope",
  "securityEnabled",
  "groupType",
] as const;

export type SnapshotGroupComparisonFieldKey = (typeof SNAPSHOT_GROUP_COMPARISON_FIELD_KEYS)[number];

export type ParsedSnapshotOu = {
  distinguishedName: string | null;
};

export type ParsedSnapshotOrganizationalUnit = {
  id: string | null;
  name: string | null;
  ou: string | null;
  displayName: string | null;
  distinguishedName: string | null;
  parentDistinguishedName: string | null;
  canonicalName: string | null;
  childOuCount: number | null;
  userCount: number | null;
  groupCount: number | null;
  computerCount: number | null;
};

export type ParsedSnapshotMembership = {
  isDirectMember: boolean | null;
};

export type ParsedSnapshotMember = {
  id: string | null;
  type: string | null;
  displayName: string | null;
  name: string | null;
  cn: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  dNSHostName: string | null;
  description: string | null;
  distinguishedName: string | null;
};

export type ParsedSnapshotManager = {
  id: string | null;
  displayName: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  distinguishedName: string | null;
};

export type ParsedSnapshotAccountExpiration = {
  neverExpires: boolean | null;
  accountExpiresDate: string | null;
};

export type ParsedSnapshotDeletedObject = {
  objectId: string | null;
  objectType: string | null;
  name: string | null;
  displayName: string | null;
  samAccountName: string | null;
  userPrincipalName: string | null;
  distinguishedName: string | null;
  lastKnownParent: string | null;
  lastKnownRdn: string | null;
  objectClass: string | null;
  whenChanged: string | null;
  deletedAt: string | null;
};

export type ParsedSnapshotRestoredObject = {
  objectId: string | null;
  objectType: string | null;
  name: string | null;
  samAccountName: string | null;
  distinguishedName: string | null;
  restored: boolean | null;
  restoredParent: string | null;
  restoredRdn: string | null;
};

export type ParsedNestedAdOperationSnapshot = {
  operation: string | null;
  user: ParsedSnapshotUser | null;
  computer: ParsedSnapshotComputer | null;
  account: ParsedSnapshotAccount | null;
  group: ParsedSnapshotGroup | null;
  member: ParsedSnapshotMember | null;
  ou: ParsedSnapshotOu | null;
  organizationalUnit: ParsedSnapshotOrganizationalUnit | null;
  membership: ParsedSnapshotMembership | null;
  manager: ParsedSnapshotManager | null;
  accountExpiration: ParsedSnapshotAccountExpiration | null;
  deletedObject: ParsedSnapshotDeletedObject | null;
  restoredObject: ParsedSnapshotRestoredObject | null;
  mappedAttributes: ParsedMappedSnapshotAttribute[];
  notifications: string | null;
  rawRecord: Record<string, unknown>;
};

export type GenericSnapshotEntry = {
  key: string;
  displayValue: string;
  nested?: GenericSnapshotEntry[];
};

export type SnapshotRenderStrategy =
  | "userUpdate"
  | "userCreate"
  | "groupCreate"
  | "groupUpdate"
  | "groupDelete"
  | "computerDelete"
  | "computerUpdate"
  | "deletedObjectRestore"
  | "accountStatus"
  | "lockStatus"
  | "groupMembership"
  | "groupMember"
  | "ouMove"
  | "organizationalUnit"
  | "userManagerUpdate"
  | "userAccountExpirationUpdate"
  | "generic";
