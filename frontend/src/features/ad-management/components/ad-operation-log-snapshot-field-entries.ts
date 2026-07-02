import type { TFunction } from "i18next";

import type {
  ParsedSnapshotComputer,
  ParsedSnapshotDeletedObject,
  ParsedSnapshotGroup,
  ParsedSnapshotRestoredObject,
} from "@/features/ad-management/parse-ad-operation-snapshot";

/**
 * Pure field-entry / field-label builders extracted from AdOperationLogSnapshotDetail.
 * These translate parsed snapshot records into the `{ key, label, value }` shape consumed
 * by KeyValueGrid, and resolve i18n labels for comparison tables. No component state.
 */

export function getCoreFieldLabel(t: TFunction<"adOperationLogs">, fieldKey: string): string {
  const translationKey = `snapshotFields.${fieldKey}` as const;
  const translated = t(translationKey, { defaultValue: "" });
  return translated || fieldKey;
}

export function getAccountFieldLabel(t: TFunction<"adOperationLogs">, fieldKey: string): string {
  const translationKey = `snapshotSections.fields.${fieldKey}` as const;
  const translated = t(translationKey, { defaultValue: "" });
  return translated || fieldKey;
}

export function getGroupFieldLabel(t: TFunction<"adOperationLogs">, fieldKey: string): string {
  const labelKey =
    fieldKey === "displayName"
      ? "groupDisplayName"
      : fieldKey === "name"
        ? "groupName"
        : fieldKey;
  const translationKey = `snapshotSections.fields.${labelKey}` as const;
  const translated = t(translationKey, { defaultValue: "" });
  return translated || fieldKey;
}

export function getOrganizationalUnitFieldLabel(
  t: TFunction<"adOperationLogs">,
  fieldKey: string,
): string {
  const translationKey = `snapshotSections.fields.${fieldKey}` as const;
  const translated = t(translationKey, { defaultValue: "" });
  return translated || fieldKey;
}

export function getGroupFieldEntries(
  t: TFunction<"adOperationLogs">,
  group: ParsedSnapshotGroup | null,
  formatBoolean: (value: boolean | null | undefined) => string | null,
) {
  if (!group) {
    return [];
  }

  return [
    {
      key: "groupDisplayName",
      label: t("snapshotSections.fields.groupDisplayName"),
      value: group.displayName,
    },
    {
      key: "groupName",
      label: t("snapshotSections.fields.groupName"),
      value: group.name,
    },
    { key: "cn", label: t("snapshotSections.fields.cn"), value: group.cn },
    {
      key: "samAccountName",
      label: t("snapshotSections.fields.samAccountName"),
      value: group.samAccountName,
    },
    {
      key: "description",
      label: t("snapshotSections.fields.description"),
      value: group.description,
    },
    {
      key: "distinguishedName",
      label: t("snapshotSections.fields.distinguishedName"),
      value: group.distinguishedName,
      mono: true,
    },
    {
      key: "groupScope",
      label: t("snapshotSections.fields.groupScope"),
      value: group.groupScope,
    },
    {
      key: "securityEnabled",
      label: t("snapshotSections.fields.securityEnabled"),
      value: formatBoolean(group.securityEnabled),
    },
    {
      key: "groupType",
      label: t("snapshotSections.fields.groupType"),
      value: group.groupType != null ? String(group.groupType) : null,
    },
    {
      key: "memberCount",
      label: t("snapshotSections.fields.memberCount"),
      value: group.memberCount != null ? String(group.memberCount) : null,
    },
    {
      key: "memberOfCount",
      label: t("snapshotSections.fields.memberOfCount"),
      value: group.memberOfCount != null ? String(group.memberOfCount) : null,
    },
  ];
}

export function getUserFieldEntries(
  t: TFunction<"adOperationLogs">,
  user: {
    id: string | null;
    samAccountName: string | null;
    userPrincipalName: string | null;
    displayName?: string | null;
    distinguishedName: string | null;
  } | null,
) {
  if (!user) {
    return [];
  }

  return [
    { key: "id", label: t("snapshotSections.fields.userId"), value: user.id },
    {
      key: "samAccountName",
      label: t("snapshotSections.fields.samAccountName"),
      value: user.samAccountName,
    },
    {
      key: "userPrincipalName",
      label: t("snapshotSections.fields.userPrincipalName"),
      value: user.userPrincipalName,
    },
    {
      key: "displayName",
      label: t("snapshotSections.fields.displayName"),
      value: user.displayName ?? null,
    },
    {
      key: "distinguishedName",
      label: t("snapshotSections.fields.distinguishedName"),
      value: user.distinguishedName,
      mono: true,
    },
  ];
}

export function getComputerFieldEntries(
  t: TFunction<"adOperationLogs">,
  computer: ParsedSnapshotComputer | null,
) {
  if (!computer) {
    return [];
  }

  return [
    {
      key: "computerId",
      label: t("snapshotSections.fields.computerId"),
      value: computer.id,
    },
    {
      key: "name",
      label: t("snapshotSections.fields.name"),
      value: computer.name,
    },
    {
      key: "samAccountName",
      label: t("snapshotSections.fields.samAccountName"),
      value: computer.samAccountName,
    },
    {
      key: "dNSHostName",
      label: t("snapshotSections.fields.dNSHostName"),
      value: computer.dNSHostName ?? null,
    },
    {
      key: "distinguishedName",
      label: t("snapshotSections.fields.distinguishedName"),
      value: computer.distinguishedName,
      mono: true,
    },
  ];
}

export function getComputerDeleteAccountFieldEntries(
  t: TFunction<"adOperationLogs">,
  account: {
    isEnabled: boolean | null;
    userAccountControl: number | null;
    primaryGroupId: number | null;
  } | null,
  formatBoolean: (value: boolean | null | undefined) => string | null,
) {
  if (!account) {
    return [];
  }

  return [
    {
      key: "isEnabled",
      label: t("snapshotSections.fields.isEnabled"),
      value: formatBoolean(account.isEnabled),
    },
    {
      key: "userAccountControl",
      label: t("snapshotSections.fields.userAccountControl"),
      value: account.userAccountControl != null ? String(account.userAccountControl) : null,
    },
    {
      key: "primaryGroupId",
      label: t("snapshotSections.fields.primaryGroupId"),
      value: account.primaryGroupId != null ? String(account.primaryGroupId) : null,
    },
  ];
}

export function getDeletedObjectFieldEntries(
  t: TFunction<"adOperationLogs">,
  deletedObject: ParsedSnapshotDeletedObject | null,
) {
  if (!deletedObject) {
    return [];
  }

  return [
    { key: "objectId", label: t("snapshotSections.fields.objectId"), value: deletedObject.objectId, mono: true },
    { key: "objectType", label: t("snapshotSections.fields.objectType"), value: deletedObject.objectType },
    { key: "name", label: t("snapshotSections.fields.name"), value: deletedObject.name },
    {
      key: "displayName",
      label: t("snapshotSections.fields.displayName"),
      value: deletedObject.displayName,
    },
    {
      key: "samAccountName",
      label: t("snapshotSections.fields.samAccountName"),
      value: deletedObject.samAccountName,
    },
    {
      key: "userPrincipalName",
      label: t("snapshotSections.fields.userPrincipalName"),
      value: deletedObject.userPrincipalName,
    },
    {
      key: "distinguishedName",
      label: t("snapshotSections.fields.distinguishedName"),
      value: deletedObject.distinguishedName,
      mono: true,
    },
    {
      key: "lastKnownParent",
      label: t("snapshotSections.fields.lastKnownParent"),
      value: deletedObject.lastKnownParent,
      mono: true,
    },
    {
      key: "lastKnownRdn",
      label: t("snapshotSections.fields.lastKnownRdn"),
      value: deletedObject.lastKnownRdn,
      mono: true,
    },
    {
      key: "objectClass",
      label: t("snapshotSections.fields.objectClass"),
      value: deletedObject.objectClass,
      mono: true,
    },
    {
      key: "whenChanged",
      label: t("snapshotSections.fields.whenChanged"),
      value: deletedObject.whenChanged,
    },
    {
      key: "deletedAt",
      label: t("snapshotSections.fields.deletedAt"),
      value: deletedObject.deletedAt,
    },
  ];
}

export function getRestoredObjectFieldEntries(
  t: TFunction<"adOperationLogs">,
  restoredObject: ParsedSnapshotRestoredObject | null,
  formatBoolean: (value: boolean | null | undefined) => string | null,
) {
  if (!restoredObject) {
    return [];
  }

  return [
    { key: "objectId", label: t("snapshotSections.fields.objectId"), value: restoredObject.objectId, mono: true },
    { key: "objectType", label: t("snapshotSections.fields.objectType"), value: restoredObject.objectType },
    { key: "name", label: t("snapshotSections.fields.name"), value: restoredObject.name },
    {
      key: "samAccountName",
      label: t("snapshotSections.fields.samAccountName"),
      value: restoredObject.samAccountName,
    },
    {
      key: "distinguishedName",
      label: t("snapshotSections.fields.distinguishedName"),
      value: restoredObject.distinguishedName,
      mono: true,
    },
    {
      key: "restored",
      label: t("snapshotSections.fields.restored"),
      value: formatBoolean(restoredObject.restored),
    },
    {
      key: "restoredParent",
      label: t("snapshotSections.fields.restoredParent"),
      value: restoredObject.restoredParent,
      mono: true,
    },
    {
      key: "restoredRdn",
      label: t("snapshotSections.fields.restoredRdn"),
      value: restoredObject.restoredRdn,
      mono: true,
    },
  ];
}
