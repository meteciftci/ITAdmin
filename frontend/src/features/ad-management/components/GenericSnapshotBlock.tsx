import type { GenericSnapshotEntry } from "@/features/ad-management/parse-ad-operation-snapshot";

/**
 * Presentational block rendering a titled list of generic snapshot entries (with optional
 * one-level nesting). Extracted from AdOperationLogSnapshotDetail; purely presentational.
 */
export function GenericSnapshotBlock({
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
