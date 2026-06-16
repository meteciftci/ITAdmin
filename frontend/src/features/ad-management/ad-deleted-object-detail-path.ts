import { AD_DELETED_OBJECTS_LIST_PATH } from "./ad-deleted-objects-list-query.ts";

export function buildAdDeletedObjectDetailPath(id: string): string {
  return `${AD_DELETED_OBJECTS_LIST_PATH}/${id}`;
}

export function buildAdDeletedObjectRestorePath(id: string): string {
  return `${AD_DELETED_OBJECTS_LIST_PATH}/${id}/restore`;
}
