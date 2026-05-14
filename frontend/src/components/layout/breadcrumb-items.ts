export type BreadcrumbItem = {
  to: string;
  titleKey: string;
};

const breadcrumbItems: BreadcrumbItem[] = [
  { to: "/users", titleKey: "items.users" },
  { to: "/roles", titleKey: "items.roles" },
  { to: "/permissions", titleKey: "items.permissions" },
  { to: "/audit-logs", titleKey: "items.auditLogs" },
  { to: "/security-logs", titleKey: "items.securityLogs" },
  { to: "/settings", titleKey: "items.settings" },
];

export function getBreadcrumbKeyByPath(pathname: string): string | null {
  if (pathname === "/error" || pathname.startsWith("/error/")) {
    return "items.error";
  }
  const exactMatch = breadcrumbItems.find((item) => item.to === pathname);
  return exactMatch?.titleKey ?? null;
}
