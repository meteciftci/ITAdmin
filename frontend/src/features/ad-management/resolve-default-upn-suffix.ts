import type { AdUpnSuffixItem } from "@/features/ad-management/types";

function normalizeSuffix(value: string): string {
  return value.trim().replace(/^@+/, "").toLowerCase();
}

export function resolveDefaultUpnSuffix(
  savedDefault: string | null | undefined,
  items: AdUpnSuffixItem[],
): string {
  if (items.length === 0) {
    return "";
  }

  const normalizedSaved = savedDefault ? normalizeSuffix(savedDefault) : "";
  if (normalizedSaved) {
    const savedMatch = items.find(
      (item) => normalizeSuffix(item.value) === normalizedSaved,
    );
    if (savedMatch) {
      return savedMatch.value;
    }
  }

  const forestMatch = items.find((item) => item.source === "Forest");
  if (forestMatch) {
    return forestMatch.value;
  }

  const domainMatch = items.find((item) => item.source === "Domain");
  if (domainMatch) {
    return domainMatch.value;
  }

  return items[0]?.value ?? "";
}

export function isSavedDefaultMissingFromAdList(
  savedDefault: string | null | undefined,
  items: AdUpnSuffixItem[],
): boolean {
  if (!savedDefault?.trim() || items.length === 0) {
    return false;
  }

  const normalizedSaved = normalizeSuffix(savedDefault);
  return !items.some((item) => normalizeSuffix(item.value) === normalizedSaved);
}
