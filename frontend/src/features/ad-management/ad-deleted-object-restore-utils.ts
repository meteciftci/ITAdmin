const DELETED_OBJECT_RESTORE_RDN_MARKER_PATTERN = /ADEL:|\\0ADEL|\0/iu;

export function containsDeletedObjectRestoreNameMarker(value: string): boolean {
  return DELETED_OBJECT_RESTORE_RDN_MARKER_PATTERN.test(value);
}

export function getAdDeletedObjectRestoreConfirmationValue(item: {
  id: string;
  samAccountName?: string | null;
  name?: string | null;
  displayName?: string | null;
}): { value: string; usesSamAccountName: boolean } {
  const samAccountName = item.samAccountName?.trim();
  if (samAccountName) {
    return { value: samAccountName, usesSamAccountName: true };
  }

  const name = item.name?.trim();
  if (name && !containsDeletedObjectRestoreNameMarker(name)) {
    return { value: name, usesSamAccountName: false };
  }

  const displayName = item.displayName?.trim();
  if (displayName && !containsDeletedObjectRestoreNameMarker(displayName)) {
    return { value: displayName, usesSamAccountName: false };
  }

  return { value: item.id.trim(), usesSamAccountName: false };
}

export function normalizeDeletedObjectRestoreRdn(lastKnownRdn: string | null | undefined): string | null {
  if (!lastKnownRdn?.trim()) {
    return null;
  }

  const trimmed = lastKnownRdn.trim();
  if (DELETED_OBJECT_RESTORE_RDN_MARKER_PATTERN.test(trimmed)) {
    return null;
  }

  if (trimmed.includes("=")) {
    return trimmed;
  }

  const escaped = trimmed.replace(/[,+"\\<>;]/g, (character) => `\\${character}`);
  return `CN=${escaped}`;
}

export function buildExpectedRestoredDistinguishedName(
  restoreRdn: string | null,
  parentDn: string | null | undefined,
): string | null {
  if (!restoreRdn?.trim() || !parentDn?.trim()) {
    return null;
  }

  return `${restoreRdn.trim()},${parentDn.trim()}`;
}

export function resolveDeletedObjectOuSearchContext(
  objectType: string,
): "users" | "groups" | "computers" {
  if (objectType === "Group") {
    return "groups";
  }

  if (objectType === "Computer") {
    return "computers";
  }

  return "users";
}
