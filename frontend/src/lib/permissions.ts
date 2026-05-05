import type { CurrentUser } from "@/features/auth/types";

export const hasRole = (
  user: CurrentUser | null,
  roleCode: string,
): boolean => {
  if (!user) return false;
  return user.roles.includes(roleCode);
};

export const isSuperAdmin = (user: CurrentUser | null): boolean => {
  if (!user) return false;
  return user.isSuperAdmin;
};

export const hasPermission = (
  user: CurrentUser | null,
  permissionCode: string,
): boolean => {
  if (!user) return false;
  if (user.isSuperAdmin) return true;
  return user.permissions.includes(permissionCode);
};

export const canAccess = (
  user: CurrentUser | null,
  permissionCode: string,
): boolean => {
  return hasPermission(user, permissionCode);
};
