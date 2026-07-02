import {
  buildComparisonRow,
  formatSnapshotValue,
  readBoolean,
} from "./snapshot-primitives.ts";
import { parseNestedAdOperationSnapshot } from "./snapshot-nested.ts";
import type {
  GenericSnapshotEntry,
  ParsedNestedAdOperationSnapshot,
  ParsedSnapshotComputer,
  ParsedSnapshotGroup,
  ParsedSnapshotMember,
  ParsedSnapshotOu,
  ParsedSnapshotUser,
  SnapshotComparisonRow,
} from "./snapshot-types.ts";

export function resolveSnapshotUser(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
): ParsedSnapshotUser | null {
  return after?.user ?? before?.user ?? null;
}

export function resolveSnapshotComputer(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
): ParsedSnapshotComputer | null {
  return after?.computer ?? before?.computer ?? null;
}

export function readSnapshotRootDescription(
  snapshot: ParsedNestedAdOperationSnapshot | null,
): string | null {
  if (!snapshot) {
    return null;
  }

  return formatSnapshotValue(snapshot.rawRecord.description ?? snapshot.rawRecord.Description);
}

export function readSnapshotDeletedFlag(
  snapshot: ParsedNestedAdOperationSnapshot | null,
): boolean | null {
  if (!snapshot) {
    return null;
  }

  return readBoolean(snapshot.rawRecord.deleted ?? snapshot.rawRecord.Deleted);
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
    before?.user?.distinguishedName
    ?? before?.group?.distinguishedName
    ?? before?.computer?.distinguishedName
    ?? null;
  const afterIdentityDn =
    after?.user?.distinguishedName
    ?? after?.group?.distinguishedName
    ?? after?.computer?.distinguishedName
    ?? null;

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

export function buildOrganizationalUnitComparisonRows(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
): SnapshotComparisonRow[] {
  const beforeOu = before?.organizationalUnit ?? null;
  const afterOu = after?.organizationalUnit ?? null;

  const rows = [
    buildComparisonRow("name", beforeOu?.name ?? null, afterOu?.name ?? null),
    buildComparisonRow("ou", beforeOu?.ou ?? null, afterOu?.ou ?? null),
    buildComparisonRow("displayName", beforeOu?.displayName ?? null, afterOu?.displayName ?? null),
    buildComparisonRow(
      "distinguishedName",
      beforeOu?.distinguishedName ?? null,
      afterOu?.distinguishedName ?? null,
      true,
      true,
    ),
    buildComparisonRow(
      "parentDistinguishedName",
      beforeOu?.parentDistinguishedName ?? null,
      afterOu?.parentDistinguishedName ?? null,
      true,
      true,
    ),
    buildComparisonRow(
      "canonicalName",
      beforeOu?.canonicalName ?? null,
      afterOu?.canonicalName ?? null,
    ),
    buildComparisonRow(
      "childOuCount",
      beforeOu?.childOuCount != null ? String(beforeOu.childOuCount) : null,
      afterOu?.childOuCount != null ? String(afterOu.childOuCount) : null,
    ),
    buildComparisonRow(
      "userCount",
      beforeOu?.userCount != null ? String(beforeOu.userCount) : null,
      afterOu?.userCount != null ? String(afterOu.userCount) : null,
    ),
    buildComparisonRow(
      "groupCount",
      beforeOu?.groupCount != null ? String(beforeOu.groupCount) : null,
      afterOu?.groupCount != null ? String(afterOu.groupCount) : null,
    ),
    buildComparisonRow(
      "computerCount",
      beforeOu?.computerCount != null ? String(beforeOu.computerCount) : null,
      afterOu?.computerCount != null ? String(afterOu.computerCount) : null,
    ),
  ];

  return rows.filter((row) => row.before !== null || row.after !== null);
}

export function buildComputerComparisonRows(
  before: ParsedNestedAdOperationSnapshot | null,
  after: ParsedNestedAdOperationSnapshot | null,
  formatBoolean: (value: boolean | null | undefined) => string | null,
): SnapshotComparisonRow[] {
  const beforeComputer = before?.computer ?? null;
  const afterComputer = after?.computer ?? null;
  const beforeDescription =
    readSnapshotRootDescription(before) ?? beforeComputer?.description ?? null;
  const afterDescription = readSnapshotRootDescription(after) ?? afterComputer?.description ?? null;

  const rows = [
    buildComparisonRow("name", beforeComputer?.name ?? null, afterComputer?.name ?? null),
    buildComparisonRow(
      "samAccountName",
      beforeComputer?.samAccountName ?? null,
      afterComputer?.samAccountName ?? null,
    ),
    buildComparisonRow(
      "distinguishedName",
      beforeComputer?.distinguishedName ?? null,
      afterComputer?.distinguishedName ?? null,
      true,
      true,
    ),
    buildComparisonRow(
      "dNSHostName",
      beforeComputer?.dNSHostName ?? null,
      afterComputer?.dNSHostName ?? null,
    ),
    buildComparisonRow("description", beforeDescription, afterDescription),
    buildComparisonRow(
      "operatingSystem",
      beforeComputer?.operatingSystem ?? null,
      afterComputer?.operatingSystem ?? null,
    ),
    buildComparisonRow(
      "operatingSystemVersion",
      beforeComputer?.operatingSystemVersion ?? null,
      afterComputer?.operatingSystemVersion ?? null,
    ),
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
      "lastLogonTimestamp",
      beforeComputer?.lastLogonTimestamp ?? null,
      afterComputer?.lastLogonTimestamp ?? null,
    ),
    buildComparisonRow(
      "whenChanged",
      beforeComputer?.whenChanged ?? null,
      afterComputer?.whenChanged ?? null,
    ),
    buildComparisonRow(
      "modifiedAt",
      beforeComputer?.modifiedAt ?? null,
      afterComputer?.modifiedAt ?? null,
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
      snapshot.computer ||
      snapshot.account ||
      snapshot.group ||
      snapshot.member ||
      snapshot.ou ||
      snapshot.organizationalUnit ||
    snapshot.membership ||
      snapshot.manager ||
      snapshot.accountExpiration ||
      snapshot.mappedAttributes.length > 0 ||
      snapshot.notifications ||
      Object.keys(snapshot.rawRecord).length > 0,
  );
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
