import type { PermissionListItem } from "@/features/permissions/types";

export type PermissionModuleGroup = {
  module: string;
  items: PermissionListItem[];
};

export function groupPermissionsByModule(
  permissions: PermissionListItem[],
): PermissionModuleGroup[] {
  const groups = new Map<string, PermissionListItem[]>();

  for (const permission of permissions) {
    const current = groups.get(permission.module);
    if (current) {
      current.push(permission);
    } else {
      groups.set(permission.module, [permission]);
    }
  }

  return Array.from(groups, ([module, items]) => ({ module, items }));
}
