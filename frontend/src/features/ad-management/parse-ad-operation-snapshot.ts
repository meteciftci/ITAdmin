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
