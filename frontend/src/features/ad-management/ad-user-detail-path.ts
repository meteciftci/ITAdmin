export const AD_USER_DETAIL_PATH_PREFIX = "/ad-management/users";

export function buildAdUserDetailPath(userId: string): string {
  return `${AD_USER_DETAIL_PATH_PREFIX}/${userId}`;
}
