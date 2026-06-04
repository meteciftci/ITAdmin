export type AdGroupDisplayFields = {
  displayName: string | null;
  name: string;
  samAccountName: string | null;
  description?: string | null;
  distinguishedName: string;
};

function normalizeLabel(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

function labelsEqual(left: string, right: string): boolean {
  return left.localeCompare(right, undefined, { sensitivity: "accent" }) === 0;
}

export function getAdGroupPrimaryLabel(group: AdGroupDisplayFields): string {
  return (
    normalizeLabel(group.displayName)
    ?? normalizeLabel(group.name)
    ?? normalizeLabel(group.samAccountName)
    ?? group.distinguishedName
  );
}

export function getAdGroupSecondaryLabel(
  group: AdGroupDisplayFields,
  primaryLabel: string,
): string | null {
  for (const candidate of [
    normalizeLabel(group.name),
    normalizeLabel(group.samAccountName),
    normalizeLabel(group.description),
  ]) {
    if (candidate && !labelsEqual(candidate, primaryLabel)) {
      return candidate;
    }
  }

  return null;
}

export function getAdGroupPathNodeLabel(
  node: AdGroupDisplayFields,
  fallbackUserLabel?: string,
): string {
  if (fallbackUserLabel) {
    return (
      normalizeLabel(node.displayName)
      ?? normalizeLabel(node.name)
      ?? normalizeLabel(node.samAccountName)
      ?? fallbackUserLabel
    );
  }

  return (
    normalizeLabel(node.displayName)
    ?? normalizeLabel(node.name)
    ?? normalizeLabel(node.samAccountName)
    ?? node.distinguishedName
  );
}
