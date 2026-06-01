export type BreadcrumbItem = {
  to: string;
  titleKey: string;
};

const breadcrumbItems: BreadcrumbItem[] = [
  { to: "/ad-management/users/create", titleKey: "items.adManagementUsersCreate" },
  { to: "/ad-management/users", titleKey: "items.adManagementUsers" },
  { to: "/settings/modules/ad-management", titleKey: "items.adManagementSettings" },
  { to: "/settings/application", titleKey: "items.applicationSettings" },
  { to: "/settings/modules", titleKey: "items.moduleSettings" },
  { to: "/users", titleKey: "items.users" },
  { to: "/roles", titleKey: "items.roles" },
  { to: "/permissions", titleKey: "items.permissions" },
  { to: "/audit-logs", titleKey: "items.auditLogs" },
  { to: "/security-logs", titleKey: "items.securityLogs" },
  {
    to: "/monitoring/module-logs/ad-operation-logs",
    titleKey: "items.adOperationLogs",
  },
  { to: "/settings", titleKey: "items.settings" },
];

export function getBreadcrumbKeyByPath(pathname: string): string | null {
  if (pathname === "/home") {
    return null;
  }
  if (pathname === "/error" || pathname.startsWith("/error/")) {
    return "items.error";
  }

  if (/^\/ad-management\/users\/[^/]+\/groups$/.test(pathname)) {
    return "items.adManagementUserGroups";
  }

  if (/^\/ad-management\/users\/[^/]+\/edit$/.test(pathname)) {
    return "items.adManagementUsersEdit";
  }

  const exactMatch = breadcrumbItems.find((item) => item.to === pathname);
  if (exactMatch) {
    return exactMatch.titleKey;
  }

  const prefixMatch = [...breadcrumbItems]
    .sort((a, b) => b.to.length - a.to.length)
    .find((item) => pathname.startsWith(`${item.to}/`));

  return prefixMatch?.titleKey ?? null;
}
