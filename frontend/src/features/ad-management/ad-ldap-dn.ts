function splitDnComponents(distinguishedName: string): string[] {
  const parts: string[] = [];
  let current = "";

  for (let index = 0; index < distinguishedName.length; index += 1) {
    const character = distinguishedName[index];
    if (character === "\\" && index + 1 < distinguishedName.length) {
      current += distinguishedName[index + 1];
      index += 1;
      continue;
    }

    if (character === ",") {
      if (current.length > 0) {
        parts.push(current);
        current = "";
      }
      continue;
    }

    current += character;
  }

  if (current.length > 0) {
    parts.push(current);
  }

  return parts;
}

export function getParentDistinguishedName(distinguishedName: string | null | undefined): string | null {
  if (!distinguishedName?.trim()) {
    return null;
  }

  const components = splitDnComponents(distinguishedName.trim());
  if (components.length <= 1) {
    return null;
  }

  return components.slice(1).map((component) => component.trim()).join(",");
}

function normalizeDn(distinguishedName: string): string {
  return splitDnComponents(distinguishedName.trim())
    .map((component) => component.trim())
    .join(",");
}

export function isEqualOrDescendantOf(
  childDistinguishedName: string | null | undefined,
  ancestorDistinguishedName: string | null | undefined,
): boolean {
  if (!childDistinguishedName?.trim() || !ancestorDistinguishedName?.trim()) {
    return false;
  }

  const child = normalizeDn(childDistinguishedName);
  const ancestor = normalizeDn(ancestorDistinguishedName);
  return child.toLowerCase() === ancestor.toLowerCase()
    || child.toLowerCase().endsWith(`,${ancestor.toLowerCase()}`);
}

export function isInvalidOrganizationalUnitMoveTarget(
  sourceDistinguishedName: string,
  targetParentDistinguishedName: string,
): boolean {
  if (sourceDistinguishedName.trim().toLowerCase() === targetParentDistinguishedName.trim().toLowerCase()) {
    return true;
  }

  return isEqualOrDescendantOf(targetParentDistinguishedName, sourceDistinguishedName);
}
