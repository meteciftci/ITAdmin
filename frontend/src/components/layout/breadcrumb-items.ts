export type BreadcrumbItem = {
  to: string;
  titleKey: string;
};

const breadcrumbItems: BreadcrumbItem[] = [
  { to: "/settings/modules/ad-management", titleKey: "items.adManagementSettings" },
  { to: "/settings/application", titleKey: "items.applicationSettings" },
  { to: "/settings/modules", titleKey: "items.moduleSettings" },
  { to: "/users", titleKey: "items.users" },
  { to: "/roles", titleKey: "items.roles" },
  { to: "/permissions", titleKey: "items.permissions" },
  { to: "/audit-logs", titleKey: "items.auditLogs" },
  { to: "/security-logs", titleKey: "items.securityLogs" },
  { to: "/settings", titleKey: "items.settings" },
];

export function getBreadcrumbKeyByPath(pathname: string): string | null {
  if (pathname === "/home") {
    return null;
  }
  if (pathname === "/error" || pathname.startsWith("/error/")) {
    return "items.error";
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
