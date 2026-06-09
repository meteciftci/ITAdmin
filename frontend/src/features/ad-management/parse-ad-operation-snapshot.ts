import { unwrapJsonLikeString } from "../../lib/parse-json-like-value.ts";

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

function readTrimmedString(value: unknown): string | null {
  if (typeof value !== "string") {
    return null;
  }
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

function normalizeComparisonValue(value: string | null | undefined): string | null {
  if (value === null || value === undefined) {
    return null;
  }
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

export function formatSnapshotValue(value: unknown): string | null {
  if (value === null || value === undefined) {
    return null;
  }

  if (typeof value === "string") {
    return normalizeComparisonValue(value);
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  if (Array.isArray(value)) {
    const parts = value
      .map((item) => formatSnapshotValue(item))
      .filter((item): item is string => item !== null);
    return parts.length > 0 ? parts.join(", ") : null;
  }

  if (typeof value === "object") {
    try {
      return JSON.stringify(value);
    } catch {
      return null;
    }
  }

  return null;
}

function readMappedAttributes(raw: unknown): ParsedMappedSnapshotAttribute[] {
  if (!Array.isArray(raw)) {
    return [];
  }

  const attributes: ParsedMappedSnapshotAttribute[] = [];

  for (const item of raw) {
    if (!item || typeof item !== "object" || Array.isArray(item)) {
      continue;
    }

    const record = item as Record<string, unknown>;
    const logicalField =
      readTrimmedString(record.logicalField) ?? readTrimmedString(record.LogicalField);
    if (!logicalField) {
      continue;
    }

    attributes.push({
      logicalField,
      displayValue: formatSnapshotValue(record.values ?? record.Values ?? record.value ?? record.Value),
    });
  }

  return attributes.sort((left, right) => left.logicalField.localeCompare(right.logicalField));
}

export function parseAdOperationSnapshot(value: unknown): ParsedAdOperationSnapshot | null {
  if (value === null || value === undefined) {
    return null;
  }

  let payload: unknown = value;
  if (typeof value === "string") {
    const trimmed = value.trim();
    if (!trimmed) {
      return null;
    }
    payload = unwrapJsonLikeString(trimmed);
  }

  if (!payload || typeof payload !== "object" || Array.isArray(payload)) {
    return null;
  }

  const record = payload as Record<string, unknown>;
  const core: Partial<Record<SnapshotCoreFieldKey, string | null>> = {};

  for (const fieldKey of SNAPSHOT_CORE_FIELD_KEYS) {
    const pascalKey = `${fieldKey[0]!.toUpperCase()}${fieldKey.slice(1)}`;
    core[fieldKey] = formatSnapshotValue(record[fieldKey] ?? record[pascalKey]);
  }

  return {
    core,
    mappedAttributes: readMappedAttributes(record.mappedAttributes ?? record.MappedAttributes),
  };
}

export function buildCoreFieldComparisonRows(
  before: ParsedAdOperationSnapshot | null,
  after: ParsedAdOperationSnapshot | null,
): SnapshotComparisonRow[] {
  return SNAPSHOT_CORE_FIELD_KEYS.map((fieldKey) => {
    const beforeValue = before?.core[fieldKey] ?? null;
    const afterValue = after?.core[fieldKey] ?? null;
    const normalizedBefore = normalizeComparisonValue(beforeValue);
    const normalizedAfter = normalizeComparisonValue(afterValue);

    return {
      key: fieldKey,
      before: normalizedBefore,
      after: normalizedAfter,
      changed: normalizedBefore !== normalizedAfter,
    };
  }).filter((row) => row.before !== null || row.after !== null);
}

export function buildMappedAttributeComparisonRows(
  before: ParsedAdOperationSnapshot | null,
  after: ParsedAdOperationSnapshot | null,
): SnapshotComparisonRow[] {
  const beforeMap = new Map(
    (before?.mappedAttributes ?? []).map((item) => [item.logicalField, item.displayValue]),
  );
  const afterMap = new Map(
    (after?.mappedAttributes ?? []).map((item) => [item.logicalField, item.displayValue]),
  );

  const keys = new Set([...beforeMap.keys(), ...afterMap.keys()]);

  return [...keys]
    .sort((left, right) => left.localeCompare(right))
    .map((logicalField) => {
      const normalizedBefore = normalizeComparisonValue(beforeMap.get(logicalField));
      const normalizedAfter = normalizeComparisonValue(afterMap.get(logicalField));

      return {
        key: logicalField,
        before: normalizedBefore,
        after: normalizedAfter,
        changed: normalizedBefore !== normalizedAfter,
      };
    });
}

export function hasSnapshotContent(snapshot: ParsedAdOperationSnapshot | null): boolean {
  if (!snapshot) {
    return false;
  }

  const hasCore = SNAPSHOT_CORE_FIELD_KEYS.some((fieldKey) => snapshot.core[fieldKey] != null);
  return hasCore || snapshot.mappedAttributes.length > 0;
}

export function parseRequestSummaryEntries(
  value: unknown,
): { key: string; displayValue: string }[] | null {
  if (value === null || value === undefined) {
    return null;
  }

  let payload: unknown = value;
  if (typeof value === "string") {
    const trimmed = value.trim();
    if (!trimmed) {
      return null;
    }
    payload = unwrapJsonLikeString(trimmed);
  }

  if (!payload || typeof payload !== "object" || Array.isArray(payload)) {
    return null;
  }

  return Object.entries(payload as Record<string, unknown>)
    .map(([key, entryValue]) => ({
      key,
      displayValue: formatSnapshotValue(entryValue) ?? "-",
    }))
    .sort((left, right) => left.key.localeCompare(right.key));
}

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
};

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

export type ParsedNestedAdOperationSnapshot = {
  operation: string | null;
  user: ParsedSnapshotUser | null;
  account: ParsedSnapshotAccount | null;
  group: ParsedSnapshotGroup | null;
  member: ParsedSnapshotMember | null;
  ou: ParsedSnapshotOu | null;
  membership: ParsedSnapshotMembership | null;
  manager: ParsedSnapshotManager | null;
  accountExpiration: ParsedSnapshotAccountExpiration | null;
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
  | "accountStatus"
  | "lockStatus"
  | "groupMembership"
  | "groupMember"
  | "ouMove"
  | "userManagerUpdate"
  | "userAccountExpirationUpdate"
  | "generic";

const ACCOUNT_STATUS_OPERATION_TYPES = new Set(["UserEnable", "UserDisable"]);
const LOCK_STATUS_OPERATION_TYPES = new Set(["UserUnlock"]);
const GROUP_MEMBERSHIP_OPERATION_TYPES = new Set(["UserGroupAdd", "UserGroupRemove"]);
const GROUP_MEMBER_OPERATION_TYPES = new Set(["GroupMemberAdd", "GroupMemberRemove"]);
const OU_MOVE_OPERATION_TYPES = new Set(["UserOuMove", "GroupMoveOu"]);
const USER_MANAGER_UPDATE_OPERATION_TYPES = new Set(["UserManagerUpdate"]);
const USER_ACCOUNT_EXPIRATION_UPDATE_OPERATION_TYPES = new Set(["UserAccountExpirationUpdate"]);

function readRecord(value: unknown): Record<string, unknown> | null {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return null;
  }
  return value as Record<string, unknown>;
}

function readBoolean(value: unknown): boolean | null {
  if (typeof value === "boolean") {
    return value;
  }
  if (typeof value === "string") {
    const normalized = value.trim().toLowerCase();
    if (normalized === "true") {
      return true;
    }
    if (normalized === "false") {
      return false;
    }
  }
  return null;
}

function readNumber(value: unknown): number | null {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
}

function parseSnapshotUser(value: unknown): ParsedSnapshotUser | null {
  const record = readRecord(value);
  if (!record) {
    return null;
  }

  const user: ParsedSnapshotUser = {
    id: formatSnapshotValue(record.id ?? record.Id),
    samAccountName: formatSnapshotValue(record.samAccountName ?? record.SamAccountName),
    userPrincipalName: formatSnapshotValue(record.userPrincipalName ?? record.UserPrincipalName),
    displayName: formatSnapshotValue(record.displayName ?? record.DisplayName),
    distinguishedName: formatSnapshotValue(record.distinguishedName ?? record.DistinguishedName),
  };

  return Object.values(user).some((entry) => entry !== null) ? user : null;
}

function parseSnapshotAccount(value: unknown): ParsedSnapshotAccount | null {
  const record = readRecord(value);
  if (!record) {
    return null;
  }

  const account: ParsedSnapshotAccount = {
    isEnabled: readBoolean(record.isEnabled ?? record.IsEnabled),
    isLocked: readBoolean(record.isLocked ?? record.IsLocked),
    userAccountControl: readNumber(record.userAccountControl ?? record.UserAccountControl),
    lockoutTime: formatSnapshotValue(record.lockoutTime ?? record.LockoutTime),
  };

  return Object.values(account).some((entry) => entry !== null) ? account : null;
}

function parseSnapshotOu(value: unknown): ParsedSnapshotOu | null {
  const record = readRecord(value);
  if (!record) {
    return null;
  }

  const ou: ParsedSnapshotOu = {
    distinguishedName: formatSnapshotValue(record.distinguishedName ?? record.DistinguishedName),
  };

  return ou.distinguishedName !== null ? ou : null;
}

function hasParsedSnapshotGroupContent(group: ParsedSnapshotGroup): boolean {
  return (
    group.id !== null ||
    group.displayName !== null ||
    group.name !== null ||
    group.cn !== null ||
    group.samAccountName !== null ||
    group.description !== null ||
    group.distinguishedName !== null ||
    group.groupScope !== null ||
    group.securityEnabled !== null ||
    group.groupType !== null ||
    group.memberCount !== null ||
    group.memberOfCount !== null
  );
}

function parseSnapshotGroup(value: unknown): ParsedSnapshotGroup | null {
  const record = readRecord(value);
  if (!record) {
    return null;
  }

  const group: ParsedSnapshotGroup = {
    id: formatSnapshotValue(record.id ?? record.Id),
    displayName: formatSnapshotValue(record.displayName ?? record.DisplayName),
    name: formatSnapshotValue(record.name ?? record.Name),
    cn: formatSnapshotValue(record.cn ?? record.Cn),
    samAccountName: formatSnapshotValue(record.samAccountName ?? record.SamAccountName),
    description: formatSnapshotValue(record.description ?? record.Description),
    distinguishedName: formatSnapshotValue(record.distinguishedName ?? record.DistinguishedName),
    groupScope: formatSnapshotValue(record.groupScope ?? record.GroupScope),
    securityEnabled: readBoolean(record.securityEnabled ?? record.SecurityEnabled),
    groupType: readNumber(record.groupType ?? record.GroupType),
    memberCount: readNumber(record.memberCount ?? record.MemberCount),
    memberOfCount: readNumber(record.memberOfCount ?? record.MemberOfCount),
  };

  return hasParsedSnapshotGroupContent(group) ? group : null;
}

function tryParseFlatGroupSnapshot(record: Record<string, unknown>): ParsedSnapshotGroup | null {
  const operation = formatSnapshotValue(record.operation ?? record.Operation);
  if (operation !== "GroupCreate" && operation !== "GroupUpdate" && operation !== "GroupDelete") {
    return null;
  }

  return parseSnapshotGroup(record);
}

function parseSnapshotMember(value: unknown): ParsedSnapshotMember | null {
  const record = readRecord(value);
  if (!record) {
    return null;
  }

  const member: ParsedSnapshotMember = {
    id: formatSnapshotValue(record.id ?? record.Id),
    type: formatSnapshotValue(record.type ?? record.Type),
    displayName: formatSnapshotValue(record.displayName ?? record.DisplayName),
    name: formatSnapshotValue(record.name ?? record.Name),
    cn: formatSnapshotValue(record.cn ?? record.Cn),
    samAccountName: formatSnapshotValue(record.samAccountName ?? record.SamAccountName),
    userPrincipalName: formatSnapshotValue(record.userPrincipalName ?? record.UserPrincipalName),
    dNSHostName: formatSnapshotValue(record.dNSHostName ?? record.DNSHostName),
    description: formatSnapshotValue(record.description ?? record.Description),
    distinguishedName: formatSnapshotValue(record.distinguishedName ?? record.DistinguishedName),
  };

  return Object.values(member).some((entry) => entry !== null) ? member : null;
}

function parseSnapshotMembership(value: unknown): ParsedSnapshotMembership | null {
  const record = readRecord(value);
  if (!record) {
    return null;
  }

  const isDirectMember = readBoolean(record.isDirectMember ?? record.IsDirectMember);
  return isDirectMember === null ? null : { isDirectMember };
}

function parseSnapshotManager(value: unknown): ParsedSnapshotManager | null {
  if (value === null) {
    return {
      id: null,
      displayName: null,
      samAccountName: null,
      userPrincipalName: null,
      distinguishedName: null,
    };
  }

  const record = readRecord(value);
  if (!record) {
    return null;
  }

  const manager: ParsedSnapshotManager = {
    id: formatSnapshotValue(record.id ?? record.Id),
    displayName: formatSnapshotValue(record.displayName ?? record.DisplayName),
    samAccountName: formatSnapshotValue(record.samAccountName ?? record.SamAccountName),
    userPrincipalName: formatSnapshotValue(record.userPrincipalName ?? record.UserPrincipalName),
    distinguishedName: formatSnapshotValue(record.distinguishedName ?? record.DistinguishedName),
  };

  return Object.values(manager).some((entry) => entry !== null) ? manager : null;
}

function parseSnapshotAccountExpiration(value: unknown): ParsedSnapshotAccountExpiration | null {
  const record = readRecord(value);
  if (!record) {
    return null;
  }

  const accountExpiration: ParsedSnapshotAccountExpiration = {
    neverExpires: readBoolean(record.neverExpires ?? record.NeverExpires),
    accountExpiresDate: formatSnapshotValue(
      record.accountExpiresDate
        ?? record.AccountExpiresDate
        ?? record.accountExpiresAt
        ?? record.AccountExpiresAt,
    ),
  };

  return accountExpiration.neverExpires !== null || accountExpiration.accountExpiresDate !== null
    ? accountExpiration
    : null;
}

export function parseNestedAdOperationSnapshot(value: unknown): ParsedNestedAdOperationSnapshot | null {
  if (value === null || value === undefined) {
    return null;
  }

  let payload: unknown = value;
  if (typeof value === "string") {
    const trimmed = value.trim();
    if (!trimmed) {
      return null;
    }
    payload = unwrapJsonLikeString(trimmed);
  }

  const record = readRecord(payload);
  if (!record) {
    return null;
  }

  const operation = formatSnapshotValue(record.operation ?? record.Operation);
  const nestedGroup = parseSnapshotGroup(record.group ?? record.Group);
  const group = nestedGroup ?? tryParseFlatGroupSnapshot(record);

  return {
    operation,
    user: parseSnapshotUser(record.user ?? record.User),
    account: parseSnapshotAccount(record.account ?? record.Account),
    group,
    member: parseSnapshotMember(record.member ?? record.Member),
    ou: parseSnapshotOu(record.ou ?? record.Ou),
    membership: parseSnapshotMembership(record.membership ?? record.Membership),
    manager: parseSnapshotManager(record.manager ?? record.Manager),
    accountExpiration: parseSnapshotAccountExpiration(
      record.accountExpiration ?? record.AccountExpiration,
    ),
    mappedAttributes: readMappedAttributes(record.mappedAttributes ?? record.MappedAttributes),
    notifications: formatSnapshotValue(record.notifications ?? record.Notifications),
    rawRecord: record,
  };
}

export function resolveSnapshotUser(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
): ParsedSnapshotUser | null {
  return after?.user ?? before?.user ?? null;
}

export function resolveSnapshotMember(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
): ParsedSnapshotMember | null {
  return after?.member ?? before?.member ?? null;
}

export function resolveSnapshotGroup(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
): ParsedSnapshotGroup | null {
  return after?.group ?? before?.group ?? null;
}

export function resolveSnapshotOu(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
): ParsedSnapshotOu | null {
  return after?.ou ?? before?.ou ?? null;
}

export function formatSnapshotBoolean(
  value: boolean | null | undefined,
  labels: { yes: string; no: string },
): string | null {
  if (value === null || value === undefined) {
    return null;
  }
  return value ? labels.yes : labels.no;
}

function buildComparisonRow(
  key: string,
  beforeValue: string | null,
  afterValue: string | null,
  monoBefore = false,
  monoAfter = false,
): SnapshotComparisonRow {
  const normalizedBefore = normalizeComparisonValue(beforeValue);
  const normalizedAfter = normalizeComparisonValue(afterValue);

  return {
    key,
    before: normalizedBefore,
    after: normalizedAfter,
    changed: normalizedBefore !== normalizedAfter,
    monoBefore,
    monoAfter,
  };
}

export function buildAccountStatusComparisonRows(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
  formatBoolean: (value: boolean | null | undefined) => string | null,
): SnapshotComparisonRow[] {
  const rows = [
    buildComparisonRow(
      "isEnabled",
      formatBoolean(before?.account?.isEnabled),
      formatBoolean(after?.account?.isEnabled),
    ),
    buildComparisonRow(
      "isLocked",
      formatBoolean(before?.account?.isLocked),
      formatBoolean(after?.account?.isLocked),
    ),
    buildComparisonRow(
      "userAccountControl",
      before?.account?.userAccountControl != null
        ? String(before.account.userAccountControl)
        : null,
      after?.account?.userAccountControl != null ? String(after.account.userAccountControl) : null,
    ),
  ];

  return rows.filter((row) => row.before !== null || row.after !== null);
}

export function buildLockStatusComparisonRows(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
  formatBoolean: (value: boolean | null | undefined) => string | null,
): SnapshotComparisonRow[] {
  const rows = [
    buildComparisonRow(
      "isLocked",
      formatBoolean(before?.account?.isLocked),
      formatBoolean(after?.account?.isLocked),
    ),
    buildComparisonRow(
      "lockoutTime",
      before?.account?.lockoutTime ?? null,
      after?.account?.lockoutTime ?? null,
    ),
    buildComparisonRow(
      "userAccountControl",
      before?.account?.userAccountControl != null
        ? String(before.account.userAccountControl)
        : null,
      after?.account?.userAccountControl != null ? String(after.account.userAccountControl) : null,
    ),
  ];

  return rows.filter((row) => row.before !== null || row.after !== null);
}

export function buildOuMoveComparisonRows(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
): SnapshotComparisonRow[] {
  const beforeIdentityDn =
    before?.user?.distinguishedName ?? before?.group?.distinguishedName ?? null;
  const afterIdentityDn =
    after?.user?.distinguishedName ?? after?.group?.distinguishedName ?? null;

  const rows = [
    buildComparisonRow(
      "ou",
      before?.ou?.distinguishedName ?? null,
      after?.ou?.distinguishedName ?? null,
      true,
      true,
    ),
    buildComparisonRow(
      "distinguishedName",
      beforeIdentityDn,
      afterIdentityDn,
      true,
      true,
    ),
  ];

  return rows.filter((row) => row.before !== null || row.after !== null);
}

export function buildManagerComparisonRows(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
): SnapshotComparisonRow[] {
  const rows = [
    buildComparisonRow(
      "displayName",
      before?.manager?.displayName ?? null,
      after?.manager?.displayName ?? null,
    ),
    buildComparisonRow(
      "samAccountName",
      before?.manager?.samAccountName ?? null,
      after?.manager?.samAccountName ?? null,
    ),
    buildComparisonRow(
      "userPrincipalName",
      before?.manager?.userPrincipalName ?? null,
      after?.manager?.userPrincipalName ?? null,
    ),
    buildComparisonRow(
      "distinguishedName",
      before?.manager?.distinguishedName ?? null,
      after?.manager?.distinguishedName ?? null,
      true,
      true,
    ),
  ];

  return rows.filter((row) => row.before !== null || row.after !== null);
}

export function buildAccountExpirationComparisonRows(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
  formatBoolean: (value: boolean | null | undefined) => string | null,
): SnapshotComparisonRow[] {
  const rows = [
    buildComparisonRow(
      "neverExpires",
      formatBoolean(before?.accountExpiration?.neverExpires),
      formatBoolean(after?.accountExpiration?.neverExpires),
    ),
    buildComparisonRow(
      "accountExpiresDate",
      before?.accountExpiration?.accountExpiresDate ?? null,
      after?.accountExpiration?.accountExpiresDate ?? null,
    ),
  ];

  return rows.filter((row) => row.before !== null || row.after !== null);
}

export function buildGroupComparisonRows(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
  formatBoolean: (value: boolean | null | undefined) => string | null,
): SnapshotComparisonRow[] {
  const beforeGroup = before?.group ?? null;
  const afterGroup = after?.group ?? null;

  const rows = [
    buildComparisonRow("displayName", beforeGroup?.displayName ?? null, afterGroup?.displayName ?? null),
    buildComparisonRow("name", beforeGroup?.name ?? null, afterGroup?.name ?? null),
    buildComparisonRow("cn", beforeGroup?.cn ?? null, afterGroup?.cn ?? null),
    buildComparisonRow(
      "samAccountName",
      beforeGroup?.samAccountName ?? null,
      afterGroup?.samAccountName ?? null,
    ),
    buildComparisonRow(
      "description",
      beforeGroup?.description ?? null,
      afterGroup?.description ?? null,
    ),
    buildComparisonRow(
      "distinguishedName",
      beforeGroup?.distinguishedName ?? null,
      afterGroup?.distinguishedName ?? null,
      true,
      true,
    ),
    buildComparisonRow("groupScope", beforeGroup?.groupScope ?? null, afterGroup?.groupScope ?? null),
    buildComparisonRow(
      "securityEnabled",
      formatBoolean(beforeGroup?.securityEnabled),
      formatBoolean(afterGroup?.securityEnabled),
    ),
    buildComparisonRow(
      "groupType",
      beforeGroup?.groupType != null ? String(beforeGroup.groupType) : null,
      afterGroup?.groupType != null ? String(afterGroup.groupType) : null,
    ),
  ];

  return rows.filter((row) => row.before !== null || row.after !== null);
}

export function buildMembershipComparisonRows(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
  formatBoolean: (value: boolean | null | undefined) => string | null,
): SnapshotComparisonRow[] {
  const rows = [
    buildComparisonRow(
      "isDirectMember",
      formatBoolean(before?.membership?.isDirectMember),
      formatBoolean(after?.membership?.isDirectMember),
    ),
  ];

  return rows.filter((row) => row.before !== null || row.after !== null);
}

export function hasNestedSnapshotContent(snapshot: ParsedNestedAdOperationSnapshot | null): boolean {
  if (!snapshot) {
    return false;
  }

  return Boolean(
    snapshot.user ||
      snapshot.account ||
      snapshot.group ||
      snapshot.member ||
      snapshot.ou ||
      snapshot.membership ||
      snapshot.manager ||
      snapshot.accountExpiration ||
      snapshot.mappedAttributes.length > 0 ||
      snapshot.notifications ||
      Object.keys(snapshot.rawRecord).length > 0,
  );
}

export function getSnapshotRenderStrategy(operationType: string): SnapshotRenderStrategy {
  if (operationType === "UserUpdate") {
    return "userUpdate";
  }
  if (operationType === "CreateUser") {
    return "userCreate";
  }
  if (operationType === "GroupCreate") {
    return "groupCreate";
  }
  if (operationType === "GroupUpdate") {
    return "groupUpdate";
  }
  if (operationType === "GroupDelete") {
    return "groupDelete";
  }
  if (ACCOUNT_STATUS_OPERATION_TYPES.has(operationType)) {
    return "accountStatus";
  }
  if (LOCK_STATUS_OPERATION_TYPES.has(operationType)) {
    return "lockStatus";
  }
  if (GROUP_MEMBERSHIP_OPERATION_TYPES.has(operationType)) {
    return "groupMembership";
  }
  if (GROUP_MEMBER_OPERATION_TYPES.has(operationType)) {
    return "groupMember";
  }
  if (OU_MOVE_OPERATION_TYPES.has(operationType)) {
    return "ouMove";
  }
  if (USER_MANAGER_UPDATE_OPERATION_TYPES.has(operationType)) {
    return "userManagerUpdate";
  }
  if (USER_ACCOUNT_EXPIRATION_UPDATE_OPERATION_TYPES.has(operationType)) {
    return "userAccountExpirationUpdate";
  }
  return "generic";
}

export function buildGenericSnapshotEntries(
  value: unknown,
  prefix = "",
): GenericSnapshotEntry[] {
  if (value === null || value === undefined) {
    return [];
  }

  if (Array.isArray(value)) {
    const displayValue = formatSnapshotValue(value);
    return displayValue ? [{ key: prefix || "value", displayValue }] : [];
  }

  if (typeof value !== "object") {
    const displayValue = formatSnapshotValue(value);
    return displayValue ? [{ key: prefix || "value", displayValue }] : [];
  }

  const record = value as Record<string, unknown>;
  const entries: GenericSnapshotEntry[] = [];

  for (const [key, entryValue] of Object.entries(record).sort(([left], [right]) =>
    left.localeCompare(right),
  )) {
    const fullKey = prefix ? `${prefix}.${key}` : key;

    if (entryValue && typeof entryValue === "object" && !Array.isArray(entryValue)) {
      const nested = buildGenericSnapshotEntries(entryValue, fullKey);
      if (nested.length > 0) {
        entries.push({ key: fullKey, displayValue: "", nested });
      }
      continue;
    }

    const displayValue = formatSnapshotValue(entryValue);
    if (displayValue) {
      entries.push({ key: fullKey, displayValue });
    }
  }

  return entries;
}

export function buildGenericSnapshotSections(
  beforeSnapshotJson: string | null | undefined,
  afterSnapshotJson: string | null | undefined,
): { before: GenericSnapshotEntry[]; after: GenericSnapshotEntry[] } {
  const beforeParsed = parseNestedAdOperationSnapshot(beforeSnapshotJson);
  const afterParsed = parseNestedAdOperationSnapshot(afterSnapshotJson);

  return {
    before: beforeParsed ? buildGenericSnapshotEntries(beforeParsed.rawRecord) : [],
    after: afterParsed ? buildGenericSnapshotEntries(afterParsed.rawRecord) : [],
  };
}
