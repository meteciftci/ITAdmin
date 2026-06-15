import type { AdDeletedObjectListItem } from "@/features/ad-management/types";

export function getAdDeletedObjectPrimaryLabel(item: Pick<AdDeletedObjectListItem, "displayName" | "name">): string {
  return item.displayName?.trim() || item.name?.trim() || "-";
}

export function getAdDeletedObjectSecondaryLabel(
  item: Pick<AdDeletedObjectListItem, "samAccountName" | "userPrincipalName">,
  primaryLabel: string,
): string | null {
  const samAccountName = item.samAccountName?.trim();
  if (samAccountName && samAccountName !== primaryLabel) {
    return samAccountName;
  }

  const userPrincipalName = item.userPrincipalName?.trim();
  if (userPrincipalName && userPrincipalName !== primaryLabel) {
    return userPrincipalName;
  }

  return null;
}
