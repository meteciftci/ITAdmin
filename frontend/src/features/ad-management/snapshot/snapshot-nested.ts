import { unwrapJsonLikeString } from "../../../lib/parse-json-like-value.ts";

import {
  formatSnapshotValue,
  readBoolean,
  readMappedAttributes,
  readNumber,
  readRecord,
} from "./snapshot-primitives.ts";
import type {
  ParsedNestedAdOperationSnapshot,
  ParsedSnapshotAccount,
  ParsedSnapshotAccountExpiration,
  ParsedSnapshotComputer,
  ParsedSnapshotDeletedObject,
  ParsedSnapshotGroup,
  ParsedSnapshotManager,
  ParsedSnapshotMember,
  ParsedSnapshotMembership,
  ParsedSnapshotOrganizationalUnit,
  ParsedSnapshotOu,
  ParsedSnapshotRestoredObject,
  ParsedSnapshotUser,
} from "./snapshot-types.ts";

function parseSnapshotUser(value: unknown): ParsedSnapshotUser | null {
  const record = readRecord(value);
  if (!record) {
    return null;
  }

  const user: ParsedSnapshotUser = {
    id: formatSnapshotValue(record.id ?? record.Id),
    samAccountName: formatSnapshotValue(record.samAccountName ?? record.SamAccountName),
    userPrincipalName: formatSnapshotValue(record.userPrincipalName ?? record.UserPrincipalName),
    displayName: formatSnapshotValue(
      record.displayName ?? record.DisplayName ?? record.name ?? record.Name),
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
    primaryGroupId: readNumber(record.primaryGroupId ?? record.PrimaryGroupId),
  };

  return Object.values(account).some((entry) => entry !== null) ? account : null;
}

function parseSnapshotComputer(value: unknown): ParsedSnapshotComputer | null {
  const record = readRecord(value);
  if (!record) {
    return null;
  }

  const computer: ParsedSnapshotComputer = {
    id: formatSnapshotValue(record.id ?? record.Id),
    samAccountName: formatSnapshotValue(record.samAccountName ?? record.SamAccountName),
    name: formatSnapshotValue(record.name ?? record.Name),
    displayName: formatSnapshotValue(record.displayName ?? record.DisplayName),
    dNSHostName: formatSnapshotValue(record.dNSHostName ?? record.DNSHostName),
    distinguishedName: formatSnapshotValue(record.distinguishedName ?? record.DistinguishedName),
    description: formatSnapshotValue(record.description ?? record.Description),
    operatingSystem: formatSnapshotValue(record.operatingSystem ?? record.OperatingSystem),
    operatingSystemVersion: formatSnapshotValue(
      record.operatingSystemVersion ?? record.OperatingSystemVersion,
    ),
    lastLogonTimestamp: formatSnapshotValue(
      record.lastLogonTimestamp ?? record.LastLogonTimestamp,
    ),
    whenChanged: formatSnapshotValue(record.whenChanged ?? record.WhenChanged),
    modifiedAt: formatSnapshotValue(record.modifiedAt ?? record.ModifiedAt),
  };

  return Object.values(computer).some((entry) => entry !== null && entry !== undefined)
    ? computer
    : null;
}

function parseSnapshotOrganizationalUnit(value: unknown): ParsedSnapshotOrganizationalUnit | null {
  const record = readRecord(value);
  if (!record) {
    return null;
  }

  const contentSummary = readRecord(record.contentSummary ?? record.ContentSummary);
  const organizationalUnit: ParsedSnapshotOrganizationalUnit = {
    id: formatSnapshotValue(record.id ?? record.Id),
    name: formatSnapshotValue(record.name ?? record.Name),
    ou: formatSnapshotValue(record.ou ?? record.Ou),
    displayName: formatSnapshotValue(record.displayName ?? record.DisplayName),
    distinguishedName: formatSnapshotValue(record.distinguishedName ?? record.DistinguishedName),
    parentDistinguishedName: formatSnapshotValue(
      record.parentDistinguishedName ?? record.ParentDistinguishedName,
    ),
    canonicalName: formatSnapshotValue(record.canonicalName ?? record.CanonicalName),
    childOuCount: readNumber(contentSummary?.childOuCount ?? record.childOuCount),
    userCount: readNumber(contentSummary?.userCount ?? record.userCount),
    groupCount: readNumber(contentSummary?.groupCount ?? record.groupCount),
    computerCount: readNumber(contentSummary?.computerCount ?? record.computerCount),
  };

  return Object.values(organizationalUnit).some((entry) => entry !== null && entry !== undefined)
    ? organizationalUnit
    : null;
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

function parseSnapshotDeletedObject(value: unknown): ParsedSnapshotDeletedObject | null {
  const record = readRecord(value);
  if (!record) {
    return null;
  }

  const deletedObject: ParsedSnapshotDeletedObject = {
    objectId: formatSnapshotValue(record.objectId ?? record.ObjectId),
    objectType: formatSnapshotValue(record.objectType ?? record.ObjectType),
    name: formatSnapshotValue(record.name ?? record.Name),
    displayName: formatSnapshotValue(record.displayName ?? record.DisplayName),
    samAccountName: formatSnapshotValue(record.samAccountName ?? record.SamAccountName),
    userPrincipalName: formatSnapshotValue(record.userPrincipalName ?? record.UserPrincipalName),
    distinguishedName: formatSnapshotValue(record.distinguishedName ?? record.DistinguishedName),
    lastKnownParent: formatSnapshotValue(record.lastKnownParent ?? record.LastKnownParent),
    lastKnownRdn: formatSnapshotValue(record.lastKnownRdn ?? record.LastKnownRdn),
    objectClass: formatSnapshotValue(record.objectClass ?? record.ObjectClass),
    whenChanged: formatSnapshotValue(record.whenChanged ?? record.WhenChanged),
    deletedAt: formatSnapshotValue(record.deletedAt ?? record.DeletedAt),
  };

  return Object.values(deletedObject).some((entry) => entry !== null) ? deletedObject : null;
}

function parseSnapshotRestoredObject(value: unknown): ParsedSnapshotRestoredObject | null {
  const record = readRecord(value);
  if (!record) {
    return null;
  }

  const restoredObject: ParsedSnapshotRestoredObject = {
    objectId: formatSnapshotValue(record.objectId ?? record.ObjectId),
    objectType: formatSnapshotValue(record.objectType ?? record.ObjectType),
    name: formatSnapshotValue(record.name ?? record.Name),
    samAccountName: formatSnapshotValue(record.samAccountName ?? record.SamAccountName),
    distinguishedName: formatSnapshotValue(record.distinguishedName ?? record.DistinguishedName),
    restored: readBoolean(record.restored ?? record.Restored),
    restoredParent: formatSnapshotValue(record.restoredParent ?? record.RestoredParent),
    restoredRdn: formatSnapshotValue(record.restoredRdn ?? record.RestoredRdn),
  };

  return Object.values(restoredObject).some((entry) => entry !== null) ? restoredObject : null;
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
  const computerSource = record.computer ?? record.Computer;

  return {
    operation,
    user: parseSnapshotUser(record.user ?? record.User ?? computerSource),
    computer: parseSnapshotComputer(computerSource),
    account: parseSnapshotAccount(record.account ?? record.Account),
    group,
    member: parseSnapshotMember(record.member ?? record.Member),
    ou: parseSnapshotOu(record.ou ?? record.Ou),
    organizationalUnit: parseSnapshotOrganizationalUnit(
      record.organizationalUnit ?? record.OrganizationalUnit,
    ),
    membership: parseSnapshotMembership(record.membership ?? record.Membership),
    manager: parseSnapshotManager(record.manager ?? record.Manager),
    accountExpiration: parseSnapshotAccountExpiration(
      record.accountExpiration ?? record.AccountExpiration,
    ),
    deletedObject: parseSnapshotDeletedObject(record.deletedObject ?? record.DeletedObject),
    restoredObject: parseSnapshotRestoredObject(record.restoredObject ?? record.RestoredObject),
    mappedAttributes: readMappedAttributes(record.mappedAttributes ?? record.MappedAttributes),
    notifications: formatSnapshotValue(record.notifications ?? record.Notifications),
    rawRecord: record,
  };
}
