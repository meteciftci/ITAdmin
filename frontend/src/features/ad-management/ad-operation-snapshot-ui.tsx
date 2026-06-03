import { useTranslation } from "react-i18next";

import { PrettyJsonBlock } from "@/components/common/PrettyJsonBlock";
import { cn } from "@/lib/utils";
import type { SnapshotComparisonRow } from "@/features/ad-management/parse-ad-operation-snapshot";

export function ComparisonCell({
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

export function ComparisonTable({
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
                  mono={row.monoBefore}
                  emptyLabel={emptyLabel}
                />
              </td>
              <td className="px-3 py-2">
                <ComparisonCell
                  value={row.after}
                  mono={row.monoAfter}
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

export function KeyValueGrid({
  entries,
  noneLabel,
}: {
  entries: { key: string; label: string; value: string | null; mono?: boolean }[];
  noneLabel: string;
}) {
  const visibleEntries = entries.filter((entry) => entry.value);

  if (visibleEntries.length === 0) {
    return <span className="text-muted-foreground">{noneLabel}</span>;
  }

  return (
    <div className="grid gap-3 rounded-lg border bg-card p-3 md:grid-cols-2">
      {visibleEntries.map((entry) => (
        <div key={entry.key} className="space-y-1">
          <p className="text-xs text-muted-foreground">{entry.label}</p>
          <p
            className={cn("break-all text-sm", entry.mono && "font-mono text-xs text-muted-foreground")}
            title={entry.value ?? undefined}
          >
            {entry.value}
          </p>
        </div>
      ))}
    </div>
  );
}

export function InfoLine({ label, value }: { label: string; value: string }) {
  return (
    <p className="text-sm text-muted-foreground">
      <span className="font-medium text-foreground">{label}: </span>
      <span className="break-all">{value}</span>
    </p>
  );
}

export function RawJsonDisclosure({
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
