export const AD_GROUP_DETAIL_PATH_PREFIX = "/ad-management/groups";

export type AdGroupDetailSection = "members";

type BuildAdGroupDetailPathOptions = {
  section?: AdGroupDetailSection;
};

export function buildAdGroupDetailPath(
  groupId: string,
  options?: BuildAdGroupDetailPathOptions,
): string {
  const base = `${AD_GROUP_DETAIL_PATH_PREFIX}/${groupId}`;

  if (options?.section) {
    return `${base}?section=${options.section}`;
  }

  return base;
}

export function buildAdGroupMembersPath(groupId: string): string {
  return buildAdGroupDetailPath(groupId, { section: "members" });
}

export function buildAdGroupEditPath(groupId: string): string {
  return `${AD_GROUP_DETAIL_PATH_PREFIX}/${groupId}/edit`;
}

export const AD_GROUP_CREATE_PATH = "/ad-management/groups/create";
