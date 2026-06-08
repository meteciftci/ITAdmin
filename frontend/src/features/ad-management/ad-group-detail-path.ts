export const AD_GROUP_DETAIL_PATH_PREFIX = "/ad-management/groups";

export function buildAdGroupDetailPath(groupId: string): string {
  return `${AD_GROUP_DETAIL_PATH_PREFIX}/${groupId}`;
}

export function buildAdGroupEditPath(groupId: string): string {
  return `${AD_GROUP_DETAIL_PATH_PREFIX}/${groupId}/edit`;
}

export const AD_GROUP_CREATE_PATH = "/ad-management/groups/create";
