import { AD_DELETED_OBJECTS_LIST_PATH } from "./ad-deleted-objects-list-query.ts";

export type AdDeletedObjectReturnState = {
  returnPath?: string;
};

export function buildAdDeletedObjectsListReturnState(): AdDeletedObjectReturnState {
  return { returnPath: AD_DELETED_OBJECTS_LIST_PATH };
}

export function buildAdDeletedObjectDetailReturnState(id: string): AdDeletedObjectReturnState {
  return { returnPath: `${AD_DELETED_OBJECTS_LIST_PATH}/${id}` };
}

export function resolveAdDeletedObjectReturnPath(
  state: unknown,
  fallbackPath: string = AD_DELETED_OBJECTS_LIST_PATH,
): string {
  if (!state || typeof state !== "object") {
    return fallbackPath;
  }

  const returnPath = (state as AdDeletedObjectReturnState).returnPath;
  return typeof returnPath === "string" && returnPath.startsWith("/")
    ? returnPath
    : fallbackPath;
}
