import type { PermissionCode } from "@/lib/permission-codes";
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
  permissionCode: PermissionCode,
): boolean => {
  if (!user) return false;
  if (user.isSuperAdmin) return true;
  return user.permissions.includes(permissionCode);
};

export const canAccess = (
  user: CurrentUser | null,
  permissionCode: PermissionCode,
): boolean => {
  return hasPermission(user, permissionCode);
};

export const canAccessAny = (
  user: CurrentUser | null,
  permissionCodes: readonly PermissionCode[],
): boolean => {
  if (!user) return false;
  if (user.isSuperAdmin) return true;
  return permissionCodes.some((code) => user.permissions.includes(code));
};
