import type { ParsedMappedSnapshotAttribute, SnapshotComparisonRow } from "./snapshot-types.ts";

export function readTrimmedString(value: unknown): string | null {
  if (typeof value !== "string") {
    return null;
  }
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

export function normalizeComparisonValue(value: string | null | undefined): string | null {
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

export function readMappedAttributes(raw: unknown): ParsedMappedSnapshotAttribute[] {
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

export function readRecord(value: unknown): Record<string, unknown> | null {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return null;
  }
  return value as Record<string, unknown>;
}

export function readBoolean(value: unknown): boolean | null {
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

export function readNumber(value: unknown): number | null {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
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

export function buildComparisonRow(
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
