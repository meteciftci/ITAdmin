type AdGroupLabelSource = {
  displayName?: string | null;
  samAccountName?: string | null;
  name: string;
};

export function formatAdGroupTableDisplayName(displayName: string | null | undefined): string {
  const trimmed = displayName?.trim();
  return trimmed || "-";
}

export function formatAdGroupSelectionPrimaryLabel(item: AdGroupLabelSource): string {
  const displayName = item.displayName?.trim();
  if (displayName) {
    return displayName;
  }

  return item.samAccountName?.trim() || item.name;
}

export function formatAdGroupSelectionSecondaryLabel(item: AdGroupLabelSource): string | null {
  const displayName = item.displayName?.trim();
  if (displayName) {
    const secondary = item.samAccountName?.trim() || item.name;
    return secondary || null;
  }

  if (item.samAccountName?.trim()) {
    return item.name;
  }

  return null;
}

export function filterAdUserGroupMemberships<
  T extends {
    displayName?: string | null;
    name: string;
    samAccountName?: string | null;
    description?: string | null;
    distinguishedName: string;
  },
>(groups: T[], query: string): T[] {
  const normalizedQuery = query.trim().toLowerCase();
  if (!normalizedQuery) {
    return groups;
  }

  return groups.filter((group) => {
    const searchableValues = [
      group.displayName,
      group.name,
      group.samAccountName,
      group.description,
      group.distinguishedName,
    ];

    return searchableValues.some((value) =>
      value?.toLowerCase().includes(normalizedQuery),
    );
  });
}
