export const AD_GROUP_DETAIL_PATH_PREFIX = "/ad-management/groups";

export function buildAdGroupDetailPath(groupId: string): string {
  return `${AD_GROUP_DETAIL_PATH_PREFIX}/${groupId}`;
}
