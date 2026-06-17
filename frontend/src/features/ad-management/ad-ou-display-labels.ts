import { getParentDistinguishedName } from "./ad-ldap-dn.ts";

type OrganizationalUnitLabelSource = {
  displayLabel?: string | null;
  displayName?: string | null;
  ou?: string | null;
  name?: string | null;
  distinguishedName: string;
  canonicalName?: string | null;
  parentDistinguishedName?: string | null;
};

function parseOuRdnLabel(distinguishedName: string | null | undefined): string | null {
  if (!distinguishedName?.trim()) {
    return null;
  }

  const firstComponent = distinguishedName.trim().split(",")[0]?.trim();
  if (!firstComponent?.toUpperCase().startsWith("OU=")) {
    return null;
  }

  const value = firstComponent.slice(3).replace(/\\(.)/g, "$1").trim();
  return value || null;
}

export function getAdOrganizationalUnitPrimaryLabel(
  item: OrganizationalUnitLabelSource,
): string {
  if (item.displayLabel?.trim()) {
    return item.displayLabel.trim();
  }

  if (item.displayName?.trim()) {
    return item.displayName.trim();
  }

  if (item.ou?.trim()) {
    return item.ou.trim();
  }

  if (item.name?.trim()) {
    return item.name.trim();
  }

  const parsedRdn = parseOuRdnLabel(item.distinguishedName);
  if (parsedRdn) {
    return parsedRdn;
  }

  return item.distinguishedName.trim();
}

export function getAdOrganizationalUnitSecondaryLabel(
  item: OrganizationalUnitLabelSource,
  primaryLabel: string,
): string | null {
  const location =
    item.canonicalName?.trim()
    || item.parentDistinguishedName?.trim()
    || getParentDistinguishedName(item.distinguishedName);

  if (!location || location === primaryLabel) {
    return null;
  }

  return location;
}

export function formatAdOrganizationalUnitCount(value: number | null | undefined): string {
  return value == null ? "-" : String(value);
}

export function getAdOrganizationalUnitParentPath(canonicalName: string | null | undefined): string | null {
  if (!canonicalName?.trim()) {
    return null;
  }

  const parts = canonicalName.split("/").filter(Boolean);
  if (parts.length <= 1) {
    return null;
  }

  return parts.slice(0, -1).join("/");
}
