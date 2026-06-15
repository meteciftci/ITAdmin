import type { TFunction } from "i18next";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import {
  ComparisonTable,
  InfoLine,
  KeyValueGrid,
  RawJsonDisclosure,
} from "@/features/ad-management/ad-operation-snapshot-ui";
import {
  buildAccountExpirationComparisonRows,
  buildAccountStatusComparisonRows,
  buildCoreFieldComparisonRows,
  buildGenericSnapshotSections,
  buildGroupComparisonRows,
  buildLockStatusComparisonRows,
  buildManagerComparisonRows,
  buildMappedAttributeComparisonRows,
  buildMembershipComparisonRows,
  buildOuMoveComparisonRows,
  formatSnapshotBoolean,
  getSnapshotRenderStrategy,
  hasNestedSnapshotContent,
  hasSnapshotContent,
  parseAdOperationSnapshot,
  parseNestedAdOperationSnapshot,
  parseRequestSummaryEntries,
  readSnapshotDeletedFlag,
  resolveSnapshotComputer,
  resolveSnapshotGroup,
  resolveSnapshotMember,
  resolveSnapshotUser,
  type GenericSnapshotEntry,
  type ParsedSnapshotComputer,
  type ParsedSnapshotDeletedObject,
  type ParsedSnapshotGroup,
  type ParsedSnapshotRestoredObject,
  type SnapshotCoreFieldKey,
  type SnapshotGroupComparisonFieldKey,
} from "@/features/ad-management/parse-ad-operation-snapshot";

type AdOperationLogSnapshotDetailProps = {
  operationType: string;
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  requestSummaryJson: string | null | undefined;
};

function getCoreFieldLabel(t: TFunction<"adOperationLogs">, fieldKey: string): string {
  const translationKey = `snapshotFields.${fieldKey}` as const;
  const translated = t(translationKey, { defaultValue: "" });
  return translated || fieldKey;
}

function getAccountFieldLabel(t: TFunction<"adOperationLogs">, fieldKey: string): string {
  const translationKey = `snapshotSections.fields.${fieldKey}` as const;
  const translated = t(translationKey, { defaultValue: "" });
  return translated || fieldKey;
}

function getGroupFieldLabel(t: TFunction<"adOperationLogs">, fieldKey: string): string {
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

function getGroupFieldEntries(
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

function getUserFieldEntries(
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

function getComputerFieldEntries(
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
      key: "distinguishedName",
      label: t("snapshotSections.fields.distinguishedName"),
      value: computer.distinguishedName,
      mono: true,
    },
  ];
}

function getComputerDeleteAccountFieldEntries(
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

function GenericSnapshotBlock({
  title,
  entries,
  noneLabel,
}: {
  title: string;
  entries: GenericSnapshotEntry[];
  noneLabel: string;
}) {
  if (entries.length === 0) {
    return (
      <div className="space-y-2">
        <h4 className="text-sm font-medium">{title}</h4>
        <span className="text-muted-foreground">{noneLabel}</span>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <h4 className="text-sm font-medium">{title}</h4>
      <div className="space-y-3 rounded-lg border bg-card p-3">
        {entries.map((entry) =>
          entry.nested && entry.nested.length > 0 ? (
            <div key={entry.key} className="space-y-2">
              <p className="text-xs font-medium text-muted-foreground">{entry.key}</p>
              <div className="space-y-2 border-l pl-3">
                {entry.nested.map((nestedEntry) => (
                  <div key={nestedEntry.key} className="space-y-1">
                    <p className="text-xs text-muted-foreground">{nestedEntry.key}</p>
                    <p className="break-all text-sm">{nestedEntry.displayValue}</p>
                  </div>
                ))}
              </div>
            </div>
          ) : (
            <div key={entry.key} className="space-y-1">
              <p className="text-xs text-muted-foreground">{entry.key}</p>
              <p className="break-all text-sm">{entry.displayValue}</p>
            </div>
          ),
        )}
      </div>
    </div>
  );
}

function UserUpdateSnapshotSections({
  beforeSnapshotJson,
  afterSnapshotJson,
  noneLabel,
  emptyDash,
  t,
}: {
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  noneLabel: string;
  emptyDash: string;
  t: TFunction<"adOperationLogs">;
}) {
  const beforeSnapshot = useMemo(
    () => parseAdOperationSnapshot(beforeSnapshotJson),
    [beforeSnapshotJson],
  );
  const afterSnapshot = useMemo(
    () => parseAdOperationSnapshot(afterSnapshotJson),
    [afterSnapshotJson],
  );
  const coreRows = useMemo(
    () => buildCoreFieldComparisonRows(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const mappedRows = useMemo(
    () => buildMappedAttributeComparisonRows(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );

  const hasAnySnapshot =
    hasSnapshotContent(beforeSnapshot) ||
    hasSnapshotContent(afterSnapshot) ||
    Boolean(beforeSnapshotJson?.trim()) ||
    Boolean(afterSnapshotJson?.trim());

  return (
    <>
      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("detail.sections.snapshotComparison")}</h3>
        {hasAnySnapshot ? (
          <ComparisonTable
            rows={coreRows}
            getFieldLabel={(key) => getCoreFieldLabel(t, key as SnapshotCoreFieldKey)}
            emptyLabel={emptyDash}
            noneLabel={noneLabel}
          />
        ) : (
          <span className="text-muted-foreground">{noneLabel}</span>
        )}
        {!afterSnapshotJson?.trim() && beforeSnapshotJson?.trim() ? (
          <p className="text-xs text-muted-foreground">{t("comparison.afterMissing")}</p>
        ) : null}
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("detail.sections.mappedAttributes")}</h3>
        <ComparisonTable
          rows={mappedRows}
          getFieldLabel={(key) => key}
          emptyLabel={emptyDash}
          noneLabel={noneLabel}
        />
      </section>
    </>
  );
}

function AccountStatusSnapshotSections({
  beforeSnapshotJson,
  afterSnapshotJson,
  noneLabel,
  emptyDash,
  t,
}: {
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  noneLabel: string;
  emptyDash: string;
  t: TFunction<"adOperationLogs">;
}) {
  const booleanLabels = useMemo(
    () => ({ yes: t("snapshotSections.boolean.yes"), no: t("snapshotSections.boolean.no") }),
    [t],
  );
  const formatBoolean = useMemo(
    () => (value: boolean | null | undefined) => formatSnapshotBoolean(value, booleanLabels),
    [booleanLabels],
  );

  const beforeSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(beforeSnapshotJson),
    [beforeSnapshotJson],
  );
  const afterSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(afterSnapshotJson),
    [afterSnapshotJson],
  );
  const user = useMemo(
    () => resolveSnapshotUser(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const accountRows = useMemo(
    () => buildAccountStatusComparisonRows(beforeSnapshot, afterSnapshot, formatBoolean),
    [beforeSnapshot, afterSnapshot, formatBoolean],
  );

  return (
    <>
      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.user")}</h3>
        <KeyValueGrid entries={getUserFieldEntries(t, user)} noneLabel={noneLabel} />
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.accountStatus")}</h3>
        <ComparisonTable
          rows={accountRows}
          getFieldLabel={(key) => getAccountFieldLabel(t, key)}
          emptyLabel={emptyDash}
          noneLabel={noneLabel}
        />
        {afterSnapshot?.notifications ? (
          <InfoLine label={t("snapshotSections.notifications")} value={afterSnapshot.notifications} />
        ) : null}
      </section>
    </>
  );
}

function LockStatusSnapshotSections({
  beforeSnapshotJson,
  afterSnapshotJson,
  noneLabel,
  emptyDash,
  t,
}: {
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  noneLabel: string;
  emptyDash: string;
  t: TFunction<"adOperationLogs">;
}) {
  const booleanLabels = useMemo(
    () => ({ yes: t("snapshotSections.boolean.yes"), no: t("snapshotSections.boolean.no") }),
    [t],
  );
  const formatBoolean = useMemo(
    () => (value: boolean | null | undefined) => formatSnapshotBoolean(value, booleanLabels),
    [booleanLabels],
  );

  const beforeSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(beforeSnapshotJson),
    [beforeSnapshotJson],
  );
  const afterSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(afterSnapshotJson),
    [afterSnapshotJson],
  );
  const user = useMemo(
    () => resolveSnapshotUser(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const lockRows = useMemo(
    () => buildLockStatusComparisonRows(beforeSnapshot, afterSnapshot, formatBoolean),
    [beforeSnapshot, afterSnapshot, formatBoolean],
  );

  return (
    <>
      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.user")}</h3>
        <KeyValueGrid entries={getUserFieldEntries(t, user)} noneLabel={noneLabel} />
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.lockStatus")}</h3>
        <ComparisonTable
          rows={lockRows}
          getFieldLabel={(key) => getAccountFieldLabel(t, key)}
          emptyLabel={emptyDash}
          noneLabel={noneLabel}
        />
      </section>
    </>
  );
}

function OuMoveSnapshotSections({
  operationType,
  beforeSnapshotJson,
  afterSnapshotJson,
  noneLabel,
  emptyDash,
  t,
}: {
  operationType: string;
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  noneLabel: string;
  emptyDash: string;
  t: TFunction<"adOperationLogs">;
}) {
  const beforeSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(beforeSnapshotJson),
    [beforeSnapshotJson],
  );
  const afterSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(afterSnapshotJson),
    [afterSnapshotJson],
  );
  const user = useMemo(
    () => resolveSnapshotUser(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const group = useMemo(
    () => resolveSnapshotGroup(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const ouMoveRows = useMemo(
    () => buildOuMoveComparisonRows(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const isGroupMove = operationType === "GroupMoveOu";
  const isComputerMove = operationType === "ComputerMoveOu";
  const booleanLabels = useMemo(
    () => ({ yes: t("snapshotSections.boolean.yes"), no: t("snapshotSections.boolean.no") }),
    [t],
  );
  const formatBoolean = useMemo(
    () => (value: boolean | null | undefined) => formatSnapshotBoolean(value, booleanLabels),
    [booleanLabels],
  );

  const getOuMoveFieldLabel = (key: string) => {
    if (key === "ou") {
      return t("snapshotSections.fields.ou");
    }
    if (key === "distinguishedName") {
      return t("snapshotSections.fields.distinguishedName");
    }
    return key;
  };

  return (
    <>
      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">
          {isGroupMove
            ? t("snapshotSections.group")
            : isComputerMove
              ? t("snapshotSections.computer")
              : t("snapshotSections.user")}
        </h3>
        <KeyValueGrid
          entries={
            isGroupMove
              ? getGroupFieldEntries(t, group, formatBoolean)
              : getUserFieldEntries(t, user)
          }
          noneLabel={noneLabel}
        />
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.ouMove")}</h3>
        <ComparisonTable
          rows={ouMoveRows}
          getFieldLabel={getOuMoveFieldLabel}
          emptyLabel={emptyDash}
          noneLabel={noneLabel}
        />
      </section>
    </>
  );
}

function UserManagerUpdateSnapshotSections({
  beforeSnapshotJson,
  afterSnapshotJson,
  noneLabel,
  emptyDash,
  t,
}: {
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  noneLabel: string;
  emptyDash: string;
  t: TFunction<"adOperationLogs">;
}) {
  const beforeSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(beforeSnapshotJson),
    [beforeSnapshotJson],
  );
  const afterSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(afterSnapshotJson),
    [afterSnapshotJson],
  );
  const user = useMemo(
    () => resolveSnapshotUser(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const managerRows = useMemo(
    () => buildManagerComparisonRows(beforeSnapshot, afterSnapshot),
    [afterSnapshot, beforeSnapshot],
  );

  const getManagerFieldLabel = (key: string) => {
    const labels: Record<string, string> = {
      displayName: t("snapshotSections.fields.displayName"),
      samAccountName: t("snapshotSections.fields.samAccountName"),
      userPrincipalName: t("snapshotSections.fields.userPrincipalName"),
      distinguishedName: t("snapshotSections.fields.distinguishedName"),
    };
    return labels[key] ?? key;
  };

  return (
    <>
      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.user")}</h3>
        <KeyValueGrid entries={getUserFieldEntries(t, user)} noneLabel={noneLabel} />
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.userManagerUpdate")}</h3>
        <ComparisonTable
          rows={managerRows}
          getFieldLabel={getManagerFieldLabel}
          emptyLabel={emptyDash}
          noneLabel={noneLabel}
        />
      </section>
    </>
  );
}

function UserAccountExpirationUpdateSnapshotSections({
  beforeSnapshotJson,
  afterSnapshotJson,
  noneLabel,
  emptyDash,
  t,
}: {
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  noneLabel: string;
  emptyDash: string;
  t: TFunction<"adOperationLogs">;
}) {
  const booleanLabels = useMemo(
    () => ({ yes: t("snapshotSections.boolean.yes"), no: t("snapshotSections.boolean.no") }),
    [t],
  );
  const formatBoolean = useMemo(
    () => (value: boolean | null | undefined) => formatSnapshotBoolean(value, booleanLabels),
    [booleanLabels],
  );
  const beforeSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(beforeSnapshotJson),
    [beforeSnapshotJson],
  );
  const afterSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(afterSnapshotJson),
    [afterSnapshotJson],
  );
  const user = useMemo(
    () => resolveSnapshotUser(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const expirationRows = useMemo(
    () => buildAccountExpirationComparisonRows(beforeSnapshot, afterSnapshot, formatBoolean),
    [afterSnapshot, beforeSnapshot, formatBoolean],
  );

  const getExpirationFieldLabel = (key: string) => {
    const labels: Record<string, string> = {
      neverExpires: t("snapshotSections.fields.neverExpires"),
      accountExpiresAt: t("snapshotSections.fields.accountExpiresAt"),
      accountExpiresDate: t("snapshotSections.fields.accountExpiresAt"),
    };
    return labels[key] ?? key;
  };

  return (
    <>
      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.user")}</h3>
        <KeyValueGrid entries={getUserFieldEntries(t, user)} noneLabel={noneLabel} />
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.userAccountExpirationUpdate")}</h3>
        <ComparisonTable
          rows={expirationRows}
          getFieldLabel={getExpirationFieldLabel}
          emptyLabel={emptyDash}
          noneLabel={noneLabel}
        />
      </section>
    </>
  );
}

function GroupMemberSnapshotSections({
  beforeSnapshotJson,
  afterSnapshotJson,
  noneLabel,
  emptyDash,
  t,
}: {
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  noneLabel: string;
  emptyDash: string;
  t: TFunction<"adOperationLogs">;
}) {
  const booleanLabels = useMemo(
    () => ({ yes: t("snapshotSections.boolean.yes"), no: t("snapshotSections.boolean.no") }),
    [t],
  );
  const formatBoolean = useMemo(
    () => (value: boolean | null | undefined) => formatSnapshotBoolean(value, booleanLabels),
    [booleanLabels],
  );

  const beforeSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(beforeSnapshotJson),
    [beforeSnapshotJson],
  );
  const afterSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(afterSnapshotJson),
    [afterSnapshotJson],
  );
  const group = useMemo(
    () => resolveSnapshotGroup(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const member = useMemo(
    () => resolveSnapshotMember(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const membershipRows = useMemo(
    () => buildMembershipComparisonRows(beforeSnapshot, afterSnapshot, formatBoolean),
    [beforeSnapshot, afterSnapshot, formatBoolean],
  );

  const groupEntries = [
    {
      key: "displayName",
      label: t("snapshotSections.fields.groupDisplayName"),
      value: group?.displayName ?? null,
    },
    {
      key: "name",
      label: t("snapshotSections.fields.groupName"),
      value: group?.name ?? null,
    },
    {
      key: "samAccountName",
      label: t("snapshotSections.fields.samAccountName"),
      value: group?.samAccountName ?? null,
    },
    {
      key: "distinguishedName",
      label: t("snapshotSections.fields.groupDistinguishedName"),
      value: group?.distinguishedName ?? null,
      mono: true,
    },
  ];

  const memberEntries = [
    {
      key: "type",
      label: t("snapshotSections.fields.memberType"),
      value: member?.type ?? null,
    },
    {
      key: "displayName",
      label: t("snapshotSections.fields.displayName"),
      value: member?.displayName ?? null,
    },
    {
      key: "name",
      label: t("snapshotSections.fields.groupName"),
      value: member?.name ?? null,
    },
    {
      key: "cn",
      label: t("snapshotSections.fields.cn"),
      value: member?.cn ?? null,
    },
    {
      key: "samAccountName",
      label: t("snapshotSections.fields.samAccountName"),
      value: member?.samAccountName ?? null,
    },
    {
      key: "userPrincipalName",
      label: t("snapshotSections.fields.userPrincipalName"),
      value: member?.userPrincipalName ?? null,
    },
    {
      key: "dNSHostName",
      label: t("snapshotSections.fields.dNSHostName"),
      value: member?.dNSHostName ?? null,
    },
    {
      key: "distinguishedName",
      label: t("snapshotSections.fields.distinguishedName"),
      value: member?.distinguishedName ?? null,
      mono: true,
    },
  ];

  return (
    <>
      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.group")}</h3>
        <KeyValueGrid entries={groupEntries} noneLabel={noneLabel} />
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.member")}</h3>
        <KeyValueGrid entries={memberEntries} noneLabel={noneLabel} />
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.groupMember")}</h3>
        <ComparisonTable
          rows={membershipRows}
          getFieldLabel={() => t("snapshotSections.fields.isDirectMember")}
          emptyLabel={emptyDash}
          noneLabel={noneLabel}
        />
      </section>
    </>
  );
}

function GroupMembershipSnapshotSections({
  beforeSnapshotJson,
  afterSnapshotJson,
  noneLabel,
  emptyDash,
  t,
}: {
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  noneLabel: string;
  emptyDash: string;
  t: TFunction<"adOperationLogs">;
}) {
  const booleanLabels = useMemo(
    () => ({ yes: t("snapshotSections.boolean.yes"), no: t("snapshotSections.boolean.no") }),
    [t],
  );
  const formatBoolean = useMemo(
    () => (value: boolean | null | undefined) => formatSnapshotBoolean(value, booleanLabels),
    [booleanLabels],
  );

  const beforeSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(beforeSnapshotJson),
    [beforeSnapshotJson],
  );
  const afterSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(afterSnapshotJson),
    [afterSnapshotJson],
  );
  const user = useMemo(
    () => resolveSnapshotUser(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const computer = useMemo(
    () => resolveSnapshotComputer(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const group = useMemo(
    () => resolveSnapshotGroup(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const membershipRows = useMemo(
    () => buildMembershipComparisonRows(beforeSnapshot, afterSnapshot, formatBoolean),
    [beforeSnapshot, afterSnapshot, formatBoolean],
  );
  const operationType = afterSnapshot?.operation ?? beforeSnapshot?.operation ?? null;
  const isComputerGroupMembership =
    operationType === "ComputerGroupAdd" || operationType === "ComputerGroupRemove";

  const groupEntries = [
    {
      key: "name",
      label: t("snapshotSections.fields.groupName"),
      value: group?.name ?? null,
    },
    {
      key: "distinguishedName",
      label: t("snapshotSections.fields.groupDistinguishedName"),
      value: group?.distinguishedName ?? null,
      mono: true,
    },
  ];

  return (
    <>
      {isComputerGroupMembership ? (
        <section className="space-y-3 border-t pt-4">
          <h3 className="text-sm font-medium">{t("snapshotSections.computer")}</h3>
          <KeyValueGrid entries={getComputerFieldEntries(t, computer)} noneLabel={noneLabel} />
        </section>
      ) : (
        <section className="space-y-3 border-t pt-4">
          <h3 className="text-sm font-medium">{t("snapshotSections.user")}</h3>
          <KeyValueGrid entries={getUserFieldEntries(t, user)} noneLabel={noneLabel} />
        </section>
      )}

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.group")}</h3>
        <KeyValueGrid entries={groupEntries} noneLabel={noneLabel} />
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.groupMembership")}</h3>
        <ComparisonTable
          rows={membershipRows}
          getFieldLabel={() => t("snapshotSections.fields.isDirectMember")}
          emptyLabel={emptyDash}
          noneLabel={noneLabel}
        />
      </section>
    </>
  );
}

function UserCreateSnapshotSections({
  afterSnapshotJson,
  noneLabel,
  emptyDash,
  t,
}: {
  afterSnapshotJson: string | null | undefined;
  noneLabel: string;
  emptyDash: string;
  t: TFunction<"adOperationLogs">;
}) {
  const booleanLabels = useMemo(
    () => ({ yes: t("snapshotSections.boolean.yes"), no: t("snapshotSections.boolean.no") }),
    [t],
  );
  const formatBoolean = useMemo(
    () => (value: boolean | null | undefined) => formatSnapshotBoolean(value, booleanLabels),
    [booleanLabels],
  );

  const afterSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(afterSnapshotJson),
    [afterSnapshotJson],
  );

  const accountEntries = [
    {
      key: "isEnabled",
      label: t("snapshotSections.fields.isEnabled"),
      value: formatBoolean(afterSnapshot?.account?.isEnabled),
    },
    {
      key: "isLocked",
      label: t("snapshotSections.fields.isLocked"),
      value: formatBoolean(afterSnapshot?.account?.isLocked),
    },
    {
      key: "userAccountControl",
      label: t("snapshotSections.fields.userAccountControl"),
      value:
        afterSnapshot?.account?.userAccountControl != null
          ? String(afterSnapshot.account.userAccountControl)
          : null,
    },
  ];

  return (
    <>
      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.createdUser")}</h3>
        <KeyValueGrid
          entries={getUserFieldEntries(t, afterSnapshot?.user ?? null)}
          noneLabel={noneLabel}
        />
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.accountStatus")}</h3>
        <KeyValueGrid entries={accountEntries} noneLabel={noneLabel} />
      </section>

      {afterSnapshot && afterSnapshot.mappedAttributes.length > 0 ? (
        <section className="space-y-3 border-t pt-4">
          <h3 className="text-sm font-medium">{t("detail.sections.mappedAttributes")}</h3>
          <div className="grid gap-3 rounded-lg border bg-card p-3 md:grid-cols-2">
            {afterSnapshot.mappedAttributes.map((attribute) => (
              <div key={attribute.logicalField} className="space-y-1">
                <p className="text-xs text-muted-foreground">{attribute.logicalField}</p>
                <p className="break-all text-sm">{attribute.displayValue ?? emptyDash}</p>
              </div>
            ))}
          </div>
        </section>
      ) : null}
    </>
  );
}

function GroupCreateSnapshotSections({
  afterSnapshotJson,
  noneLabel,
  t,
}: {
  afterSnapshotJson: string | null | undefined;
  noneLabel: string;
  t: TFunction<"adOperationLogs">;
}) {
  const booleanLabels = useMemo(
    () => ({ yes: t("snapshotSections.boolean.yes"), no: t("snapshotSections.boolean.no") }),
    [t],
  );
  const formatBoolean = useMemo(
    () => (value: boolean | null | undefined) => formatSnapshotBoolean(value, booleanLabels),
    [booleanLabels],
  );

  const afterSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(afterSnapshotJson),
    [afterSnapshotJson],
  );

  return (
    <section className="space-y-3 border-t pt-4">
      <h3 className="text-sm font-medium">{t("snapshotSections.createdGroup")}</h3>
      <KeyValueGrid
        entries={getGroupFieldEntries(t, afterSnapshot?.group ?? null, formatBoolean)}
        noneLabel={noneLabel}
      />
    </section>
  );
}

function getDeletedObjectFieldEntries(
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

function getRestoredObjectFieldEntries(
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

function DeletedObjectRestoreSnapshotSections({
  beforeSnapshotJson,
  afterSnapshotJson,
  requestSummaryJson,
  noneLabel,
  t,
}: {
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  requestSummaryJson: string | null | undefined;
  noneLabel: string;
  t: TFunction<"adOperationLogs">;
}) {
  const booleanLabels = useMemo(
    () => ({ yes: t("snapshotSections.boolean.yes"), no: t("snapshotSections.boolean.no") }),
    [t],
  );
  const formatBoolean = useMemo(
    () => (value: boolean | null | undefined) => formatSnapshotBoolean(value, booleanLabels),
    [booleanLabels],
  );

  const beforeSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(beforeSnapshotJson),
    [beforeSnapshotJson],
  );
  const afterSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(afterSnapshotJson),
    [afterSnapshotJson],
  );
  const restoreTargetEntries = useMemo(() => {
    const parsed = parseNestedAdOperationSnapshot(requestSummaryJson);
    const restoreTarget = parsed?.rawRecord.restoreTarget ?? parsed?.rawRecord.RestoreTarget;
    const record =
      restoreTarget && typeof restoreTarget === "object" && !Array.isArray(restoreTarget)
        ? (restoreTarget as Record<string, unknown>)
        : null;

    if (!record) {
      return [];
    }

    return [
      {
        key: "lastKnownParent",
        label: t("snapshotSections.fields.lastKnownParent"),
        value:
          typeof record.lastKnownParent === "string"
            ? record.lastKnownParent
            : typeof record.LastKnownParent === "string"
              ? record.LastKnownParent
              : null,
        mono: true,
      },
      {
        key: "lastKnownRdn",
        label: t("snapshotSections.fields.lastKnownRdn"),
        value:
          typeof record.lastKnownRdn === "string"
            ? record.lastKnownRdn
            : typeof record.LastKnownRdn === "string"
              ? record.LastKnownRdn
              : null,
        mono: true,
      },
      {
        key: "restoredDistinguishedName",
        label: t("snapshotSections.fields.restoredDistinguishedName"),
        value:
          typeof record.restoredDistinguishedName === "string"
            ? record.restoredDistinguishedName
            : typeof record.RestoredDistinguishedName === "string"
              ? record.RestoredDistinguishedName
              : null,
        mono: true,
      },
    ];
  }, [requestSummaryJson, t]);

  return (
    <>
      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.deletedObject")}</h3>
        <KeyValueGrid
          entries={getDeletedObjectFieldEntries(t, beforeSnapshot?.deletedObject ?? null)}
          noneLabel={noneLabel}
        />
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.restoreTarget")}</h3>
        <KeyValueGrid entries={restoreTargetEntries} noneLabel={noneLabel} />
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.restoredObject")}</h3>
        <KeyValueGrid
          entries={getRestoredObjectFieldEntries(
            t,
            afterSnapshot?.restoredObject ?? null,
            formatBoolean,
          )}
          noneLabel={noneLabel}
        />
      </section>
    </>
  );
}

function ComputerDeleteSnapshotSections({
  beforeSnapshotJson,
  afterSnapshotJson,
  noneLabel,
  t,
}: {
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  noneLabel: string;
  t: TFunction<"adOperationLogs">;
}) {
  const booleanLabels = useMemo(
    () => ({ yes: t("snapshotSections.boolean.yes"), no: t("snapshotSections.boolean.no") }),
    [t],
  );
  const formatBoolean = useMemo(
    () => (value: boolean | null | undefined) => formatSnapshotBoolean(value, booleanLabels),
    [booleanLabels],
  );

  const beforeSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(beforeSnapshotJson),
    [beforeSnapshotJson],
  );
  const afterSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(afterSnapshotJson),
    [afterSnapshotJson],
  );
  const computer = useMemo(
    () => resolveSnapshotComputer(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const deletedFlag = useMemo(
    () => readSnapshotDeletedFlag(afterSnapshot),
    [afterSnapshot],
  );

  return (
    <>
      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.deletedComputer")}</h3>
        <KeyValueGrid
          entries={getComputerFieldEntries(t, computer)}
          noneLabel={noneLabel}
        />
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.accountStatus")}</h3>
        <KeyValueGrid
          entries={getComputerDeleteAccountFieldEntries(
            t,
            beforeSnapshot?.account ?? null,
            formatBoolean,
          )}
          noneLabel={noneLabel}
        />
      </section>

      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.deleteResult")}</h3>
        <KeyValueGrid
          entries={[
            {
              key: "deleted",
              label: t("snapshotSections.fields.deleted"),
              value: formatBoolean(deletedFlag),
            },
          ]}
          noneLabel={noneLabel}
        />
      </section>
    </>
  );
}

function GroupDeleteSnapshotSections({
  beforeSnapshotJson,
  noneLabel,
  t,
}: {
  beforeSnapshotJson: string | null | undefined;
  noneLabel: string;
  t: TFunction<"adOperationLogs">;
}) {
  const booleanLabels = useMemo(
    () => ({ yes: t("snapshotSections.boolean.yes"), no: t("snapshotSections.boolean.no") }),
    [t],
  );
  const formatBoolean = useMemo(
    () => (value: boolean | null | undefined) => formatSnapshotBoolean(value, booleanLabels),
    [booleanLabels],
  );

  const beforeSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(beforeSnapshotJson),
    [beforeSnapshotJson],
  );

  return (
    <section className="space-y-3 border-t pt-4">
      <h3 className="text-sm font-medium">{t("snapshotSections.deletedGroup")}</h3>
      <KeyValueGrid
        entries={getGroupFieldEntries(t, beforeSnapshot?.group ?? null, formatBoolean)}
        noneLabel={noneLabel}
      />
    </section>
  );
}

function GroupUpdateSnapshotSections({
  beforeSnapshotJson,
  afterSnapshotJson,
  noneLabel,
  emptyDash,
  t,
}: {
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  noneLabel: string;
  emptyDash: string;
  t: TFunction<"adOperationLogs">;
}) {
  const booleanLabels = useMemo(
    () => ({ yes: t("snapshotSections.boolean.yes"), no: t("snapshotSections.boolean.no") }),
    [t],
  );
  const formatBoolean = useMemo(
    () => (value: boolean | null | undefined) => formatSnapshotBoolean(value, booleanLabels),
    [booleanLabels],
  );

  const beforeSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(beforeSnapshotJson),
    [beforeSnapshotJson],
  );
  const afterSnapshot = useMemo(
    () => parseNestedAdOperationSnapshot(afterSnapshotJson),
    [afterSnapshotJson],
  );
  const groupRows = useMemo(
    () => buildGroupComparisonRows(beforeSnapshot, afterSnapshot, formatBoolean),
    [beforeSnapshot, afterSnapshot, formatBoolean],
  );

  const hasAnySnapshot =
    hasNestedSnapshotContent(beforeSnapshot) ||
    hasNestedSnapshotContent(afterSnapshot) ||
    Boolean(beforeSnapshotJson?.trim()) ||
    Boolean(afterSnapshotJson?.trim());

  return (
    <section className="space-y-3 border-t pt-4">
      <h3 className="text-sm font-medium">{t("snapshotSections.groupUpdate")}</h3>
      {hasAnySnapshot ? (
        <ComparisonTable
          rows={groupRows}
          getFieldLabel={(key) => getGroupFieldLabel(t, key as SnapshotGroupComparisonFieldKey)}
          emptyLabel={emptyDash}
          noneLabel={noneLabel}
        />
      ) : (
        <span className="text-muted-foreground">{noneLabel}</span>
      )}
      {!afterSnapshotJson?.trim() && beforeSnapshotJson?.trim() ? (
        <p className="text-xs text-muted-foreground">{t("comparison.afterMissing")}</p>
      ) : null}
    </section>
  );
}

function GenericSnapshotSections({
  beforeSnapshotJson,
  afterSnapshotJson,
  noneLabel,
  t,
}: {
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  noneLabel: string;
  t: TFunction<"adOperationLogs">;
}) {
  const sections = useMemo(
    () => buildGenericSnapshotSections(beforeSnapshotJson, afterSnapshotJson),
    [beforeSnapshotJson, afterSnapshotJson],
  );

  const hasContent =
    sections.before.length > 0 ||
    sections.after.length > 0 ||
    Boolean(beforeSnapshotJson?.trim()) ||
    Boolean(afterSnapshotJson?.trim());

  if (!hasContent) {
    return (
      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("detail.sections.snapshotComparison")}</h3>
        <span className="text-muted-foreground">{noneLabel}</span>
      </section>
    );
  }

  return (
    <section className="space-y-4 border-t pt-4">
      <h3 className="text-sm font-medium">{t("detail.sections.snapshotComparison")}</h3>
      <GenericSnapshotBlock
        title={t("comparison.before")}
        entries={sections.before}
        noneLabel={noneLabel}
      />
      <GenericSnapshotBlock
        title={t("comparison.after")}
        entries={sections.after}
        noneLabel={noneLabel}
      />
      {!afterSnapshotJson?.trim() && beforeSnapshotJson?.trim() ? (
        <p className="text-xs text-muted-foreground">{t("comparison.afterMissing")}</p>
      ) : null}
    </section>
  );
}

export function AdOperationLogSnapshotDetail({
  operationType,
  beforeSnapshotJson,
  afterSnapshotJson,
  requestSummaryJson,
}: AdOperationLogSnapshotDetailProps) {
  const { t } = useTranslation("adOperationLogs");
  const noneLabel = t("detail.none");
  const emptyDash = "-";
  const strategy = getSnapshotRenderStrategy(operationType);

  const requestSummaryEntries = useMemo(
    () => parseRequestSummaryEntries(requestSummaryJson),
    [requestSummaryJson],
  );

  const hasRawJson =
    Boolean(beforeSnapshotJson?.trim()) ||
    Boolean(afterSnapshotJson?.trim()) ||
    Boolean(requestSummaryJson?.trim());

  const snapshotSections = (() => {
    switch (strategy) {
      case "userUpdate":
        return (
          <UserUpdateSnapshotSections
            beforeSnapshotJson={beforeSnapshotJson}
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            emptyDash={emptyDash}
            t={t}
          />
        );
      case "accountStatus":
        return (
          <AccountStatusSnapshotSections
            beforeSnapshotJson={beforeSnapshotJson}
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            emptyDash={emptyDash}
            t={t}
          />
        );
      case "lockStatus":
        return (
          <LockStatusSnapshotSections
            beforeSnapshotJson={beforeSnapshotJson}
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            emptyDash={emptyDash}
            t={t}
          />
        );
      case "groupMembership":
        return (
          <GroupMembershipSnapshotSections
            beforeSnapshotJson={beforeSnapshotJson}
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            emptyDash={emptyDash}
            t={t}
          />
        );
      case "groupMember":
        return (
          <GroupMemberSnapshotSections
            beforeSnapshotJson={beforeSnapshotJson}
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            emptyDash={emptyDash}
            t={t}
          />
        );
      case "ouMove":
        return (
          <OuMoveSnapshotSections
            operationType={operationType}
            beforeSnapshotJson={beforeSnapshotJson}
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            emptyDash={emptyDash}
            t={t}
          />
        );
      case "userManagerUpdate":
        return (
          <UserManagerUpdateSnapshotSections
            beforeSnapshotJson={beforeSnapshotJson}
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            emptyDash={emptyDash}
            t={t}
          />
        );
      case "userAccountExpirationUpdate":
        return (
          <UserAccountExpirationUpdateSnapshotSections
            beforeSnapshotJson={beforeSnapshotJson}
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            emptyDash={emptyDash}
            t={t}
          />
        );
      case "userCreate":
        return (
          <UserCreateSnapshotSections
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            emptyDash={emptyDash}
            t={t}
          />
        );
      case "groupCreate":
        return (
          <GroupCreateSnapshotSections
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            t={t}
          />
        );
      case "groupUpdate":
        return (
          <GroupUpdateSnapshotSections
            beforeSnapshotJson={beforeSnapshotJson}
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            emptyDash={emptyDash}
            t={t}
          />
        );
      case "groupDelete":
        return (
          <GroupDeleteSnapshotSections
            beforeSnapshotJson={beforeSnapshotJson}
            noneLabel={noneLabel}
            t={t}
          />
        );
      case "computerDelete":
        return (
          <ComputerDeleteSnapshotSections
            beforeSnapshotJson={beforeSnapshotJson}
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            t={t}
          />
        );
      case "deletedObjectRestore":
        return (
          <DeletedObjectRestoreSnapshotSections
            beforeSnapshotJson={beforeSnapshotJson}
            afterSnapshotJson={afterSnapshotJson}
            requestSummaryJson={requestSummaryJson}
            noneLabel={noneLabel}
            t={t}
          />
        );
      default:
        return (
          <GenericSnapshotSections
            beforeSnapshotJson={beforeSnapshotJson}
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            t={t}
          />
        );
    }
  })();

  return (
    <>
      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("detail.sections.requestSummary")}</h3>
        {requestSummaryEntries && requestSummaryEntries.length > 0 ? (
          <div className="grid gap-3 rounded-lg border bg-card p-3 md:grid-cols-2">
            {requestSummaryEntries.map((entry) => (
              <div key={entry.key} className="space-y-1">
                <p className="text-xs text-muted-foreground">{entry.key}</p>
                <p className="break-all text-sm">{entry.displayValue}</p>
              </div>
            ))}
          </div>
        ) : requestSummaryJson?.trim() ? (
          <p className="break-all rounded-md border bg-muted/30 p-3 text-sm whitespace-pre-wrap">
            {requestSummaryJson.trim()}
          </p>
        ) : (
          <span className="text-muted-foreground">{noneLabel}</span>
        )}
      </section>

      {snapshotSections}

      {hasRawJson ? (
        <section className="space-y-2 border-t pt-4">
          <h3 className="text-sm font-medium">{t("detail.sections.rawJson")}</h3>
          <div className="space-y-2">
            <RawJsonDisclosure
              title={t("detail.sections.rawBeforeJson")}
              value={beforeSnapshotJson}
              noneLabel={noneLabel}
            />
            <RawJsonDisclosure
              title={t("detail.sections.rawAfterJson")}
              value={afterSnapshotJson}
              noneLabel={noneLabel}
            />
            <RawJsonDisclosure
              title={t("detail.sections.rawRequestSummaryJson")}
              value={requestSummaryJson}
              noneLabel={noneLabel}
            />
          </div>
        </section>
      ) : null}
    </>
  );
}
