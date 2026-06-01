import type { TFunction } from "i18next";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { PrettyJsonBlock } from "@/components/common/PrettyJsonBlock";
import { cn } from "@/lib/utils";
import {
  buildCoreFieldComparisonRows,
  buildMappedAttributeComparisonRows,
  hasSnapshotContent,
  parseAdOperationSnapshot,
  parseRequestSummaryEntries,
  type SnapshotComparisonRow,
  type SnapshotCoreFieldKey,
} from "@/features/ad-management/parse-ad-operation-snapshot";

type AdOperationLogSnapshotDetailProps = {
  beforeSnapshotJson: string | null | undefined;
  afterSnapshotJson: string | null | undefined;
  requestSummaryJson: string | null | undefined;
};

function ComparisonCell({
  value,
  mono = false,
  emptyLabel,
}: {
  value: string | null;
  mono?: boolean;
  emptyLabel: string;
}) {
  if (!value) {
    return <span className="text-muted-foreground">{emptyLabel}</span>;
  }

  return (
    <span
      className={cn("break-all", mono && "font-mono text-xs text-muted-foreground")}
      title={value}
    >
      {value}
    </span>
  );
}

function ComparisonTable({
  rows,
  getFieldLabel,
  emptyLabel,
  noneLabel,
}: {
  rows: SnapshotComparisonRow[];
  getFieldLabel: (key: string) => string;
  emptyLabel: string;
  noneLabel: string;
}) {
  const { t } = useTranslation("adOperationLogs");

  if (rows.length === 0) {
    return <span className="text-muted-foreground">{noneLabel}</span>;
  }

  return (
    <div className="overflow-x-auto rounded-lg border bg-card">
      <table className="min-w-full text-sm">
        <thead className="bg-muted/50 text-left">
          <tr>
            <th className="px-3 py-2 font-medium whitespace-nowrap">
              {t("comparison.field")}
            </th>
            <th className="px-3 py-2 font-medium whitespace-nowrap">
              {t("comparison.before")}
            </th>
            <th className="px-3 py-2 font-medium whitespace-nowrap">
              {t("comparison.after")}
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr
              key={row.key}
              className={cn(
                "border-t align-top",
                row.changed && "bg-amber-500/10 dark:bg-amber-500/10",
              )}
            >
              <td className="px-3 py-2 font-medium whitespace-nowrap">
                <span className="inline-flex items-center gap-2">
                  {getFieldLabel(row.key)}
                  {row.changed ? (
                    <span className="rounded bg-amber-500/15 px-1.5 py-0.5 text-[10px] font-medium text-amber-800 dark:text-amber-200">
                      {t("comparison.changed")}
                    </span>
                  ) : null}
                </span>
              </td>
              <td className="px-3 py-2">
                <ComparisonCell
                  value={row.before}
                  mono={row.key === "distinguishedName"}
                  emptyLabel={emptyLabel}
                />
              </td>
              <td className="px-3 py-2">
                <ComparisonCell
                  value={row.after}
                  mono={row.key === "distinguishedName"}
                  emptyLabel={emptyLabel}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function RawJsonDisclosure({
  title,
  value,
  noneLabel,
}: {
  title: string;
  value: string | null | undefined;
  noneLabel: string;
}) {
  if (!value?.trim()) {
    return null;
  }

  return (
    <details className="rounded-md border bg-card">
      <summary className="cursor-pointer px-3 py-2 text-sm font-medium">{title}</summary>
      <div className="border-t p-3">
        <PrettyJsonBlock value={value} emptyLabel={noneLabel} />
      </div>
    </details>
  );
}

function getCoreFieldLabel(t: TFunction<"adOperationLogs">, fieldKey: string): string {
  const translationKey = `snapshotFields.${fieldKey}` as const;
  const translated = t(translationKey, { defaultValue: "" });
  return translated || fieldKey;
}

export function AdOperationLogSnapshotDetail({
  beforeSnapshotJson,
  afterSnapshotJson,
  requestSummaryJson,
}: AdOperationLogSnapshotDetailProps) {
  const { t } = useTranslation("adOperationLogs");
  const noneLabel = t("detail.none");
  const emptyDash = "-";

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
  const requestSummaryEntries = useMemo(
    () => parseRequestSummaryEntries(requestSummaryJson),
    [requestSummaryJson],
  );

  const hasAnySnapshot =
    hasSnapshotContent(beforeSnapshot) ||
    hasSnapshotContent(afterSnapshot) ||
    Boolean(beforeSnapshotJson?.trim()) ||
    Boolean(afterSnapshotJson?.trim());

  const hasRawJson =
    Boolean(beforeSnapshotJson?.trim()) ||
    Boolean(afterSnapshotJson?.trim()) ||
    Boolean(requestSummaryJson?.trim());

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
