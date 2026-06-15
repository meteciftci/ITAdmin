import type { AdDeletedObjectTypeFilter } from "@/features/ad-management/types";

export const AD_DELETED_OBJECTS_LIST_PATH = "/ad-management/deleted-objects";

export const AD_DELETED_OBJECTS_LIST_STORAGE_KEY = "ad-management:deleted-objects:list-state";

export const AD_DELETED_OBJECTS_LIST_DEFAULTS = {
  search: "",
  type: "all" as AdDeletedObjectTypeFilter,
  pageNumber: 1,
  pageSize: 20,
  includeAll: false,
};

export type AdDeletedObjectsListState = {
  search: string;
  type: AdDeletedObjectTypeFilter;
  pageNumber: number;
  pageSize: number;
  includeAll: boolean;
};

export function normalizeAdDeletedObjectsListState(
  value: Partial<AdDeletedObjectsListState>,
): AdDeletedObjectsListState {
  const type = value.type ?? AD_DELETED_OBJECTS_LIST_DEFAULTS.type;
  const normalizedType: AdDeletedObjectTypeFilter =
    type === "user" || type === "group" || type === "computer" ? type : "all";

  return {
    search: typeof value.search === "string" ? value.search : AD_DELETED_OBJECTS_LIST_DEFAULTS.search,
    type: normalizedType,
    pageNumber:
      typeof value.pageNumber === "number" && value.pageNumber > 0
        ? value.pageNumber
        : AD_DELETED_OBJECTS_LIST_DEFAULTS.pageNumber,
    pageSize:
      typeof value.pageSize === "number" && value.pageSize > 0
        ? value.pageSize
        : AD_DELETED_OBJECTS_LIST_DEFAULTS.pageSize,
    includeAll: value.includeAll === true,
  };
}

export function parseAdDeletedObjectsListStateFromSession(
  raw: string | null,
): AdDeletedObjectsListState {
  if (!raw) {
    return { ...AD_DELETED_OBJECTS_LIST_DEFAULTS };
  }

  try {
    const parsed = JSON.parse(raw) as Partial<AdDeletedObjectsListState>;
    return normalizeAdDeletedObjectsListState(parsed);
  } catch {
    return { ...AD_DELETED_OBJECTS_LIST_DEFAULTS };
  }
}
