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
  buildAccountStatusComparisonRows,
  buildCoreFieldComparisonRows,
  buildGenericSnapshotSections,
  buildLockStatusComparisonRows,
  buildMappedAttributeComparisonRows,
  buildMembershipComparisonRows,
  formatSnapshotBoolean,
  getSnapshotRenderStrategy,
  hasSnapshotContent,
  parseAdOperationSnapshot,
  parseNestedAdOperationSnapshot,
  parseRequestSummaryEntries,
  resolveSnapshotGroup,
  resolveSnapshotUser,
  type GenericSnapshotEntry,
  type SnapshotCoreFieldKey,
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
  const group = useMemo(
    () => resolveSnapshotGroup(beforeSnapshot, afterSnapshot),
    [beforeSnapshot, afterSnapshot],
  );
  const membershipRows = useMemo(
    () => buildMembershipComparisonRows(beforeSnapshot, afterSnapshot, formatBoolean),
    [beforeSnapshot, afterSnapshot, formatBoolean],
  );

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
      <section className="space-y-3 border-t pt-4">
        <h3 className="text-sm font-medium">{t("snapshotSections.user")}</h3>
        <KeyValueGrid entries={getUserFieldEntries(t, user)} noneLabel={noneLabel} />
      </section>

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
      case "userCreate":
        return (
          <UserCreateSnapshotSections
            afterSnapshotJson={afterSnapshotJson}
            noneLabel={noneLabel}
            emptyDash={emptyDash}
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
