import type { AdDeletedObjectDetail, AdDeletedObjectListItem, AdDeletedObjectType } from "@/features/ad-management/types";

const RESTORABLE_DELETED_OBJECT_TYPES = new Set<AdDeletedObjectType>([
  "User",
  "Group",
  "Computer",
]);

export function isRestorableDeletedObjectType(objectType: AdDeletedObjectType): boolean {
  return RESTORABLE_DELETED_OBJECT_TYPES.has(objectType);
}

export function canRestoreDeletedObject(
  item: Pick<AdDeletedObjectListItem, "objectType" | "lastKnownParent"> &
    Partial<Pick<AdDeletedObjectDetail, "lastKnownRdn">>,
): boolean {
  if (!isRestorableDeletedObjectType(item.objectType)) {
    return false;
  }

  if (!item.lastKnownParent?.trim()) {
    return false;
  }

  if ("lastKnownRdn" in item && item.lastKnownRdn !== undefined && !item.lastKnownRdn?.trim()) {
    return false;
  }

  return true;
}
