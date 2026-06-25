type AdUserCreateTargetOuSettings = {
  defaultUserOu?: string | null;
  usersRootOu?: string | null;
};

type AdGroupCreateTargetOuSettings = {
  defaultGroupOu?: string | null;
  groupsSearchBase?: string | null;
};

type AdComputerCreateTargetOuSettings = {
  defaultComputerOu?: string | null;
  computersSearchBase?: string | null;
};

export function resolveAdUserCreateTargetOu(
  selectedOuDistinguishedName: string | null | undefined,
  settings: AdUserCreateTargetOuSettings | null | undefined,
): string | null {
  const selected = selectedOuDistinguishedName?.trim();
  if (selected) {
    return selected;
  }

  const defaultUserOu = settings?.defaultUserOu?.trim();
  if (defaultUserOu) {
    return defaultUserOu;
  }

  const usersRootOu = settings?.usersRootOu?.trim();
  if (usersRootOu) {
    return usersRootOu;
  }

  return null;
}

export function resolveAdGroupCreateTargetOu(
  selectedOuDistinguishedName: string | null | undefined,
  settings: AdGroupCreateTargetOuSettings | null | undefined,
): string | null {
  const selected = selectedOuDistinguishedName?.trim();
  if (selected) {
    return selected;
  }

  const defaultGroupOu = settings?.defaultGroupOu?.trim();
  if (defaultGroupOu) {
    return defaultGroupOu;
  }

  const groupsSearchBase = settings?.groupsSearchBase?.trim();
  if (groupsSearchBase) {
    return groupsSearchBase;
  }

  return null;
}

export function resolveAdComputerCreateTargetOu(
  selectedOuDistinguishedName: string | null | undefined,
  settings: AdComputerCreateTargetOuSettings | null | undefined,
): string | null {
  const selected = selectedOuDistinguishedName?.trim();
  if (selected) {
    return selected;
  }

  const defaultComputerOu = settings?.defaultComputerOu?.trim();
  if (defaultComputerOu) {
    return defaultComputerOu;
  }

  const computersSearchBase = settings?.computersSearchBase?.trim();
  if (computersSearchBase) {
    return computersSearchBase;
  }

  return null;
}
