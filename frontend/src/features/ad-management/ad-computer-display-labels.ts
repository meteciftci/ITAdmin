type ComputerLabelSource = {
  name: string;
  samAccountName?: string | null;
  distinguishedName: string;
};

export function getAdComputerPrimaryLabel(computer: ComputerLabelSource): string {
  return computer.name?.trim() || computer.samAccountName?.trim() || computer.distinguishedName;
}

export function getAdComputerSecondaryLabel(
  computer: ComputerLabelSource,
  primaryLabel: string,
): string | null {
  const candidates = [
    computer.samAccountName?.trim(),
    computer.distinguishedName?.trim(),
  ].filter((value): value is string => Boolean(value));

  for (const candidate of candidates) {
    if (candidate !== primaryLabel) {
      return candidate;
    }
  }

  return null;
}
